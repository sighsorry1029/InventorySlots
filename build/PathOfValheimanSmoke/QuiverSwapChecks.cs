using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static ItemData? failQuiverPublish;
    private static ItemData? rejectQuiverEquip;
    private static bool RejectQuiverEquip(ItemData __1, ref bool __2, ref bool __result)
    { if (!ReferenceEquals(__1, rejectQuiverEquip)) return true; __2 = __result = false; return false; }
    private static bool NativeQuiverIdentity(ItemData item, ref bool __result)
    { __result = item?.m_shared?.m_name.StartsWith("NativeQuiver", StringComparison.Ordinal) == true; return false; }
    private static bool NativeQuiverPrefab(ItemData item, ref string __result)
    { if (item?.m_shared?.m_name.StartsWith("NativeQuiver", StringComparison.Ordinal) != true) return true; __result = item.m_shared.m_name; return false; }
    private static bool NativeQuiverPublish(ItemData it)
    { if (ReferenceEquals(it, failQuiverPublish) && it != null) { failQuiverPublish = null; throw new InvalidOperationException("fixture publish failure"); } return false; }

    private static void CheckNativeQuiverSwap(Assembly mod, Assembly pov, Type plugin, Player player, Inventory bag)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        Type native = pov.GetType("PassiveTree.QuiverSlot", true)!, quivers = pov.GetType("PassiveTree.Quivers", true)!, api = pov.GetType("PassiveTree.Gear.EquipmentApi", true)!;
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        object[] savedDefs = definitions.Cast<object>().ToArray();
        var savedMap = mapped.Keys.Cast<object>().Select(k => new DictionaryEntry(k, mapped[k])).ToArray();
        var items = bag.GetAllItems(); ItemData[] savedItems = items.ToArray();
        var external = (IDictionary)api.GetField("Quivers", Statics)!.GetValue(null)!;
        var hooks = new Harmony("smoke.pov.native-quiver");
        var wrapper = new Harmony("smoke.pov.native-quiver-wrapper");
        hooks.Patch(AccessTools.Method(quivers, "Is"), prefix: new HarmonyMethod(typeof(Program), nameof(NativeQuiverIdentity)));
        hooks.Patch(AccessTools.Method(api, "Prefab"), prefix: new HarmonyMethod(typeof(Program), nameof(NativeQuiverPrefab)));
        hooks.Patch(AccessTools.Method(native, "Publish"), prefix: new HarmonyMethod(typeof(Program), nameof(NativeQuiverPublish)));
        hooks.Patch(AccessTools.Method(native, "Changed"), prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
        hooks.Patch(AccessTools.Method(typeof(Player), "IsEquipActionQueued"), prefix: new HarmonyMethod(typeof(Program), nameof(QueuedTrinket)));
        hooks.Patch(AccessTools.Method(typeof(Humanoid), "UseItem"), prefix: new HarmonyMethod(typeof(Program), nameof(TrinketUse)));
        foreach (string name in new[] { "EquipItem", "UnequipItem" })
            hooks.Patch(AccessTools.Method(typeof(Humanoid), name), transpiler: new HarmonyMethod(typeof(Program), nameof(VanillaTrinketBody)));
        foreach (string name in new[] { "Equip", "Unequip", "IsEquiped" }) hooks.CreateClassProcessor(native.GetNestedType(name, BindingFlags.NonPublic)!).Patch();
        try
        {
            Call("InstallPathOfValheimanQuiverSwap", wrapper);
            definitions.Clear(); mapped.Clear(); items.Clear();
            Type definition = mod.GetType("InventorySlots.SlotDefinition", true)!;
            Type kind = mod.GetType("InventorySlots.SlotKind", true)!;
            var accepts = (Func<string, ItemData, bool>)pov.GetType("Valheiman.Api.Equipment", true)!.GetMethod("Accepts")!.CreateDelegate(typeof(Func<string, ItemData, bool>));
            object slot = Activator.CreateInstance(definition, new object?[] { "pov.quiver", "Quiver", Enum.Parse(kind, "BuiltIn"), new Func<ItemData?, bool>(it => it != null && accepts("quiver", it)), -1, "quiver" })!;
            definitions.Add(slot); mapped.Add("quiver", slot);
            object?[] posArgs = { bag, slot, null }; Call("TryGetSlotGridPos", posArgs); Vector2i dest = (Vector2i)posArgs[2]!;
            ItemData Item(string name, Vector2i pos, int capacity = 3) => new ItemData { m_gridPos = pos,
                m_shared = new ItemData.SharedData { m_name = name, m_itemType = ItemData.ItemType.Utility, m_maxStackSize = 1 },
                m_customData = new Dictionary<string,string> { ["valheiman.equipment.arrowCapacity"] = capacity.ToString() } };
            ItemData a = Item("NativeQuiverA", dest), b = Item("NativeQuiverB", new Vector2i(1, 0), 1);
            items.Add(a); items.Add(b); queueTrinket = false; queuedTrinket = null;
            player.EquipItem(a, true);
            ItemData? Current() => (ItemData?)native.GetMethod("Get")!.Invoke(null, new object[] { player });
            bool Put(ItemData item) => (bool)Call("TryPutPathOfValheimanItem", player, "quiver", item)!;
            Vector2i source = b.m_gridPos;
            Check(ReferenceEquals(Current(), a), "Original PoV native quiver equipped before replacement");
            Check(Put(b) && ReferenceEquals(Current(), b) && a.m_gridPos == source && b.m_gridPos == dest,
                "Drop/API replaces native quiver and swaps exact cells at native commit");
            Check(!a.m_equipped && !a.m_customData.ContainsKey("pt.quiver") && b.m_equipped && items.Count == 2,
                "Native replacement clears old worn marker without cloning/removing inventory items");
            // Model a full backpack and preserved ammo from a larger quiver.
            for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++)
                if (new Vector2i(x, y) != source) items.Add(Item("filler", new Vector2i(x,y)));
            ItemData ammo = Item("ammo", new Vector2i(3, dest.y + 1)); ammo.m_stack = 80; items.Add(ammo);
            int count = items.Count;
            Check(player.EquipItem(a, true) && ReferenceEquals(Current(), a) && a.m_gridPos == dest && b.m_gridPos == source && items.Count == count,
                "Right-click completion swaps a full backpack using the incoming item's vacated cell");
            queueTrinket = true;
            Check(Put(b) && ReferenceEquals(queuedTrinket, b) && ReferenceEquals(Current(), a) && b.m_gridPos == source && a.m_gridPos == dest,
                "Queued drag keeps both cells unchanged until native equipment completion");
            queuedTrinket = null; queueTrinket = false; player.EquipItem(b, true);
            Check(ReferenceEquals(Current(), b) && b.m_gridPos == dest && a.m_gridPos == source && items.Contains(ammo) && ammo.m_stack == 80,
                "Queued completion commits swap and preserves ammo after 3-to-1 capacity change");
            Check(Put(b) && ReferenceEquals(Current(), b) && a.m_gridPos == source, "Subsequent slot reconciliation retains the new quiver");
            failQuiverPublish = a;
            Check(!Put(a) && ReferenceEquals(Current(), b) && b.m_gridPos == dest && a.m_gridPos == source && b.m_equipped && !a.m_equipped && items.Count == count,
                "Failure inside original native Put rolls back ownership, cells, metadata and item count");
            Check((int)plugin.GetField("_povMutationDepth", Statics)!.GetValue(null)! == 0, "Native failure balances mutation scope");
            failQuiverPublish = a;
            Check(!player.EquipItem(a, true) && ReferenceEquals(Current(), b) && a.m_gridPos == source && b.m_gridPos == dest,
                "Queued/right-click completion failure returns false after recovery without escaping into the game loop");
            external.Add("NativeQuiverA", Activator.CreateInstance(external.GetType().GetGenericArguments()[1]));
            Check(!(bool)Call("IsNativePathOfValheimanQuiver", a)! && !Put(a) && ReferenceEquals(Current(), b), "Late external quiver registration is excluded from native swap");
            external.Remove("NativeQuiverA");
            a.m_customData.Remove("valheiman.equipment.arrowCapacity");
            Check(!player.EquipItem(a, true) && ReferenceEquals(Current(), b) && a.m_gridPos == source, "Direct native equip cannot bypass current slot acceptance");
            a.m_customData["valheiman.equipment.arrowCapacity"] = "3";
            a.m_gridPos = new Vector2i(6, dest.y + 2);
            Check(!player.EquipItem(a, true) && ReferenceEquals(Current(), b), "Queued incoming moved outside regular cells is refused at completion");
            a.m_gridPos = source;
            hooks.Patch(AccessTools.Method(native.GetNestedType("Equip", BindingFlags.NonPublic), "Prefix"),
                prefix: new HarmonyMethod(typeof(Program), nameof(RejectQuiverEquip)) { priority = Priority.First });
            rejectQuiverEquip = b; failQuiverPublish = a;
            Check(!Put(a) && Current() == null && !a.m_equipped && !b.m_equipped &&
                !a.m_customData.ContainsKey("pt.quiver") && !b.m_customData.ContainsKey("pt.quiver") &&
                a.m_gridPos == source && b.m_gridPos == dest && items.Count == count &&
                (bool)plugin.GetField("_povQuiverSwapFailed", Statics)!.GetValue(null)!,
                "Rejected recovery preserves both items, reports truthful worn metadata and disables only replacement");
            Check((int)plugin.GetField("_povMutationDepth", Statics)!.GetValue(null)! == 0,
                "Rejected recovery also balances the mutation scope");
        }
        finally
        {
            failQuiverPublish = rejectQuiverEquip = null; queueTrinket = false; queuedTrinket = null;
            external.Remove("NativeQuiverA"); wrapper.UnpatchSelf(); hooks.UnpatchSelf();
            foreach (string name in new[] { "_povNativeQuiver", "_povNativeQuiverItem", "_povQuiverPrefab", "_povExternalQuivers" }) plugin.GetField(name, Statics)!.SetValue(null, null);
            plugin.GetField("_povQuiverSwapFailed", Statics)!.SetValue(null, false);
            definitions.Clear(); foreach (object d in savedDefs) definitions.Add(d);
            mapped.Clear(); foreach (DictionaryEntry e in savedMap) mapped[e.Key] = e.Value;
            items.Clear(); items.AddRange(savedItems);
        }
    }
}
