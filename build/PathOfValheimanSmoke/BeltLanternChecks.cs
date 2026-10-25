using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static bool lanternPublishThrows;
    private static bool LanternPublish(ItemData it)
    { if (it != null && lanternPublishThrows) throw new InvalidOperationException("simulated lantern publication failure"); return false; }
    private static bool VanillaLanternEquip(Humanoid player, ItemData item)
    { AccessTools.Field(typeof(Humanoid), "m_leftItem").SetValue(player, item); item.m_equipped = true; return true; }
    private static void VanillaLanternUnequip(Humanoid player, ItemData item)
    {
        foreach (string name in new[] { "m_leftItem", "m_rightItem", "m_hiddenLeftItem", "m_hiddenRightItem" })
        {
            FieldInfo field = AccessTools.Field(typeof(Humanoid), name);
            if (ReferenceEquals(field.GetValue(player), item)) field.SetValue(player, null);
        }
        item.m_equipped = false;
    }
    private static IEnumerable<CodeInstruction> VanillaLanternBody(MethodBase __originalMethod)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); yield return new CodeInstruction(OpCodes.Ldarg_1);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Program),
            __originalMethod.Name == "EquipItem" ? nameof(VanillaLanternEquip) : nameof(VanillaLanternUnequip)));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    private static bool LanternIdentity(ItemData it, ref bool __result)
    { __result = it.m_shared.m_name == "Lantern"; return false; }

    private static void CheckBeltLantern(Assembly mod, Assembly pov, Type plugin, Type bridgeType, object bridge,
        Player player, Inventory bag, ConfigEntry<bool> enabled)
    {
        const string role = "belt.lantern";
        object? Call(string name, params object?[] values) => plugin.GetMethod(name, Statics)!.Invoke(null, values);
        Type belt = pov.GetType("PassiveTree.BeltSlot", true)!;
        Type delver = pov.GetType("PassiveTree.CraftDelver", true)!;
        Type state = pov.GetType("PassiveTree.Gear.State", true)!;
        Type progress = pov.GetType("PassiveTree.Progress", true)!;
        Type stats = pov.GetType("PassiveTree.StatSet", true)!;
        Type stat = pov.GetType("PassiveTree.Stat", true)!;
        Type api = pov.GetType("Valheiman.Api.Equipment", true)!;
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        var items = bag.GetAllItems();
        var custom = (Dictionary<string, string>)AccessTools.Field(typeof(Player), "m_customData").GetValue(player);
        var known = (HashSet<string>)AccessTools.Field(typeof(Player), "m_knownMaterial").GetValue(player);
        var predicates = (IList)belt.GetField("fits", Statics)!.GetValue(null)!;
        object[] oldPredicates = predicates.Cast<object>().ToArray();
        object? oldProgress = deathProgress;
        ItemData Item(string name = "Lantern") => new ItemData { m_gridPos = new Vector2i(items.Count % 8, items.Count / 8),
            m_shared = new ItemData.SharedData { m_name = name, m_itemType = (ItemData.ItemType)15, m_maxStackSize = 1 } };
        void Node(bool on)
        {
            deathProgress = FormatterServices.GetUninitializedObject(progress);
            object values = Activator.CreateInstance(stats)!;
            if (on) stats.GetMethod("Add", new[] { stat, typeof(float) })!.Invoke(values,
                new[] { Enum.Parse(stat, "BeltLantern"), (object)1f });
            progress.GetField("Stats", Instances)!.SetValue(deathProgress, values);
            Call("InvalidateSlotDefinitionCaches");
        }
        ItemData? Actual() => (ItemData?)bridgeType.GetMethod("NativeItem", Instances)!.Invoke(bridge, new object[] { player, role });
        bool Put(ItemData item) => (bool)Call("TryPutPathOfValheimanItem", player, role, item)!;
        var hooks = new Harmony("smoke.pov.belt-lantern");
        // Keep original mastery, API, BeltSlot ownership and IS transaction bodies.
        // Substitute Unity prefab identity, model/ZDO publication and action timing.
        hooks.Patch(AccessTools.PropertyGetter(progress, "Current"), postfix: new HarmonyMethod(typeof(Program), nameof(DeathProgress)));
        hooks.Patch(AccessTools.Method(state, "Prefab"), prefix: new HarmonyMethod(typeof(Program), nameof(DeathPrefab)));
        hooks.Patch(AccessTools.Method(delver, "IsLantern"), prefix: new HarmonyMethod(typeof(Program), nameof(LanternIdentity)));
        hooks.Patch(AccessTools.Method(belt, "Publish"), prefix: new HarmonyMethod(typeof(Program), nameof(LanternPublish)));
        hooks.Patch(AccessTools.Method(typeof(Player), "IsEquipActionQueued"), prefix: new HarmonyMethod(typeof(Program), nameof(QueuedTrinket)));
        hooks.Patch(AccessTools.Method(typeof(Humanoid), "UseItem"), prefix: new HarmonyMethod(typeof(Program), nameof(TrinketUse)));
        foreach (string name in new[] { "EquipItem", "UnequipItem" })
            hooks.Patch(AccessTools.Method(typeof(Humanoid), name), transpiler: new HarmonyMethod(typeof(Program), nameof(VanillaLanternBody)));
        foreach (string name in new[] { "Hum_Equip", "Hum_Unequip", "Hum_IsEquiped" })
            hooks.CreateClassProcessor(pov.GetType("PassiveTree.BeltSlot+" + name, true)!).Patch();
        foreach (string name in new[] { "HumanoidUseItemRouteToDedicatedSlotPatch", "HumanoidEquipItemRouteToDedicatedSlotPatch", "HumanoidUnequipItemPatch" })
            hooks.CreateClassProcessor(mod.GetType("InventorySlots." + name, true)!).Patch();
        try
        {
            predicates.Clear(); delver.GetMethod("RegisterBelt", Statics)!.Invoke(null, null);
            definitions.Clear(); mapped.Clear(); items.Clear(); known.Clear(); enabled.Value = true;
            Call("AppendPathOfValheimanSlots");
            object slot = mapped[role]!;
            Check(slot != null && slot.GetType().GetProperty("Kind")!.GetValue(slot)!.ToString() == "BuiltIn" &&
                !(bool)Call("IsPathOfValheimanRingSlot", slot)!, "lantern is a normal equipment-panel cell, not storage, a ring or a quick slot");
            Vector2i Pos() { object?[] args = { bag, slot, null }; Call("TryGetSlotGridPos", args); return (Vector2i)args[2]!; }
            bool Open() => (bool)Call("IsSpecialSlotUnlocked", player, bag, slot)!;
            bool Visible() => ((IList)Call("GetCustomPanelSlots", player, bag)!).Contains(slot);
            Node(false);
            Check(!Open() && !Visible(), "unallocated Belt Lantern mastery hides the empty cell");
            Node(true);
            Check(Open() && Visible() && known.Count == 0 && items.Count == 0, "mastery alone reveals the lantern cell without discovery or possession");
            var lantern = Item(); items.Add(lantern);
            Check((bool)api.GetMethod("Accepts")!.Invoke(null, new object[] { role, lantern })! &&
                !(bool)api.GetMethod("Accepts")!.Invoke(null, new object[] { role, Item("HelmetDverger") })!, "original PoV acceptance keeps Lantern separate from Circlet");
            ItemData ordinaryBelt = Item("BeltStrength"), weapon = Item("SwordIron");
            AccessTools.Field(typeof(Humanoid), "m_utilityItem").SetValue(player, ordinaryBelt);
            AccessTools.Field(typeof(Humanoid), "m_rightItem").SetValue(player, weapon);
            Check(Put(lantern) && Actual() == lantern && lantern.m_gridPos == Pos() && lantern.m_equipped && lantern.m_customData["pt.belt"] == "1",
                "provider put commits original BeltSlot equipment before moving the exact item into its cell");
            Check(ReferenceEquals(AccessTools.Field(typeof(Humanoid), "m_utilityItem").GetValue(player), ordinaryBelt) &&
                ReferenceEquals(AccessTools.Field(typeof(Humanoid), "m_rightItem").GetValue(player), weapon) && !(bool)Call("HasInventorySlotsSlot", lantern)!,
                "lantern retains native ownership and leaves normal belt and hand equipment unchanged");
            for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++)
                items.Add(new ItemData { m_gridPos = new Vector2i(x, y), m_shared = new ItemData.SharedData { m_name = "filler" } });
            void Withdraw() { object?[] args = { player, bag, lantern, true }; Call("TryHandleDedicatedSlotUse", args); }
            Withdraw();
            Check(Actual() == lantern && lantern.m_gridPos == Pos() && items.Count == 33, "full backpack blocks lantern withdrawal without losing equipment or item");
            Node(false);
            Check(!Open() && Visible() && !Put(Item()), "refund blocks insertion while occupied cell remains visible for recovery");
            // Execute PoV's actual periodic eligibility check, which removes the
            // native mark/effects after refund without destroying the item.
            belt.GetField("nextCheck", Statics)!.SetValue(null, 0f);
            pov.GetType("PassiveTree.BeltSlot+Player_Check", true)!.GetMethod("Postfix", Statics)!.Invoke(null, new object[] { player });
            Check(Actual() == null && !lantern.m_equipped && !lantern.m_customData.ContainsKey("pt.belt") && items.Contains(lantern),
                "original PoV refund check removes the lantern effect and preserves its item");
            Check(!(bool)Call("RestoreSlotEquipmentState", player, bag, lantern, slot)! && Actual() == null,
                "closed occupied lantern cell cannot auto-equip after refund");
            items.RemoveAt(1); Withdraw();
            Check(lantern.m_gridPos == new Vector2i(0, 0) && items.Count == 32 && items.Distinct().Count() == 32,
                "closed cell withdrawal returns the same unequipped lantern to available backpack space");
            items.Clear(); items.Add(lantern); Node(true);
            queueTrinket = true;
            Vector2i original = lantern.m_gridPos;
            Check(Put(lantern) && Actual() == null && lantern.m_gridPos == original && queuedTrinket == lantern,
                "queued native equip does not move the item before the action completes");
            queueTrinket = false; queuedTrinket = null;
            player.EquipItem(lantern, true);
            Check(Put(lantern) && Actual() == lantern && lantern.m_gridPos == Pos(), "completed native equip projects into the provider cell");
            enabled.Value = false;
            Check(!Open() && Actual() == lantern, "Equipment Off closes insertion but preserves access to native worn state");
            Withdraw();
            Check(Actual() == null && lantern.m_gridPos.y < 4 && !lantern.m_equipped && items.Count == 1,
                "Equipment Off withdrawal clears native ownership and conserves the exact item");
            enabled.Value = true; Node(true);
            // The native API skips equipping already hand-held items. The
            // adapter must convert that ownership transactionally.
            AccessTools.Field(typeof(Humanoid), "m_leftItem").SetValue(player, lantern); lantern.m_equipped = true;
            original = lantern.m_gridPos; lanternPublishThrows = true;
            Check(!Put(lantern) && Actual() == null && lantern.m_gridPos == original && lantern.m_equipped &&
                ReferenceEquals(AccessTools.Field(typeof(Humanoid), "m_leftItem").GetValue(player), lantern) &&
                !lantern.m_customData.ContainsKey("pt.belt") && items.Single() == lantern,
                "partial native publication failure restores original held lantern, metadata, position and item identity");
            lanternPublishThrows = false;
            Check(Put(lantern) && Actual() == lantern && lantern.m_gridPos == Pos() &&
                AccessTools.Field(typeof(Humanoid), "m_leftItem").GetValue(player) == null,
                "a previously hand-held lantern converts into native belt equipment without duplicate ownership");
            Call("UnequipInventorySlotsItem", player, lantern);
            Check(Actual() == null && !lantern.m_equipped, "external transfer synchronously removes native lantern state");
            lantern.m_gridPos = new Vector2i(4, 11); lantern.m_equipped = true; lantern.m_customData["pt.belt"] = "1";
            custom["valheiman.equipment.v1.rowStart"] = "4";
            Call("CompletePathOfValheimanLoad", player);
            Check(lantern.m_gridPos == Pos() && lantern.m_customData["pt.belt"] == "1" && items.Single() == lantern,
                "native saved ordinal60 maps to the hosted cell without cloning or dropping the PoV mark");
            Call("SavePathOfValheimanLayout", player);
            string savedLayout = custom["InventorySlots.PovLayout.v1"];
            Call("CompletePathOfValheimanLoad", player);
            Check(custom["InventorySlots.PovLayout.v1"] == savedLayout && items.Single() == lantern && lantern.m_gridPos == Pos(),
                "provider layout round trip preserves lantern identity and coordinates");
            Check((bool)Call("RestoreSlotEquipmentState", player, bag, lantern, slot)! && Actual() == lantern,
                "restoring a saved lantern uses original PoV equipment rather than inventing an equipped flag");
        }
        finally
        {
            belt.GetMethod("Take", Statics)!.Invoke(null, new object?[] { player, Actual() ?? new ItemData() });
            hooks.UnpatchSelf(); predicates.Clear(); foreach (object predicate in oldPredicates) predicates.Add(predicate);
            deathProgress = oldProgress; queueTrinket = false; queuedTrinket = null; enabled.Value = true; lanternPublishThrows = false;
            AccessTools.Field(typeof(Humanoid), "m_utilityItem").SetValue(player, null);
            AccessTools.Field(typeof(Humanoid), "m_rightItem").SetValue(player, null);
            AccessTools.Field(typeof(Humanoid), "m_leftItem").SetValue(player, null);
        }
    }
}
