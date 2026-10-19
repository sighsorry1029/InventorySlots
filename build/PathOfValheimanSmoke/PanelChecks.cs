using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static void CheckPanelLayout(Assembly mod, Assembly pov, Type plugin, Player player, Inventory bag, ConfigFile config)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        Type definition = mod.GetType("InventorySlots.SlotDefinition", true)!;
        Type kind = mod.GetType("InventorySlots.SlotKind", true)!;
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        object[] oldDefinitions = definitions.Cast<object>().ToArray();
        var oldMapped = mapped.Keys.Cast<object>().Select(key => new DictionaryEntry(key, mapped[key])).ToArray();
        ItemData[] oldItems = items.ToArray();
        FieldInfo beltField = AccessTools.Field(typeof(Humanoid), "m_utilityItem");
        object? oldBelt = beltField.GetValue(player);
        FieldInfo quickRows = plugin.GetField("_quickSlotRows", Statics)!;
        object? oldQuickRows = quickRows.GetValue(null);
        FieldInfo purseGate = pov.GetType("PassiveTree.Gear.Wallet", true)!.GetField("Gate", Statics)!;
        object? oldGate = purseGate.GetValue(null);
        string Id(object slot) => (string)definition.GetProperty("Id")!.GetValue(slot)!;
        string? Role(object slot) => (string?)definition.GetProperty("PovCell")!.GetValue(slot);
        Vector2i Position(object slot)
        {
            object?[] args = { bag, slot, null };
            Check((bool)Call("TryGetSlotGridPos", args)!, "slot has a physical coordinate: " + Id(slot));
            return (Vector2i)args[2]!;
        }
        void Layout(int quick = 3) => Call("UpdatePathOfValheimanPanelSlots", player, bag, quick);
        void Size(int columns, int rows, string scenario) => Check(
            (int)plugin.GetField("_povQuickColumns", Statics)!.GetValue(null)! == columns &&
            (int)plugin.GetField("_povQuickContentRows", Statics)!.GetValue(null)! == rows, scenario);
        IList View(string field) => (IList)plugin.GetField(field, Statics)!.GetValue(null)!;
        object Make(string id, string slotKind, int index = -1, string? cell = null, Func<ItemData?, bool>? accepts = null) =>
            Activator.CreateInstance(definition, new object?[] { id, id, Enum.Parse(kind, slotKind), accepts ?? (_ => false), index, cell })!;
        try
        {
            definitions.Clear(); mapped.Clear(); items.Clear();
            foreach (string id in new[] { "helmet", "chest", "legs", "cape", "utility", "trinket", "wishbone", "demister" })
                definitions.Add(Make(id, "BuiltIn"));
            for (int i = 0; i < 3; i++) definitions.Add(Make("quick." + i, "Quick", i));
            Call("AppendPathOfValheimanSlots");
            Call("InvalidateSlotDefinitionCaches");
            quickRows.SetValue(null, config.Bind("test", "panel-quick-rows", 1, ""));
            Check(!definitions.Cast<object>().Any(s => Role(s)?.StartsWith("tool.") == true || Role(s)?.StartsWith("key.") == true || Role(s)?.StartsWith("purse.") == true),
                "Tool/Key/Purse roles create no InventorySlots definitions");
            Check(!mapped.Contains("utility") && !definitions.Cast<object>().Any(s => Id(s) == "pov.utility"),
                "PoV does not add a separate generic Utility cell");
            Check(Id(mapped["belt"]!) == "utility", "configured Utility cell still provides the PoV Belt role");
            Type state = pov.GetType("PassiveTree.Gear.State", true)!;
            Type nativeSlot = pov.GetType("PassiveTree.Gear.Slot", true)!;
            MethodInfo isOpen = state.GetMethod("Enabled", Statics)!;
            foreach (string role in new[] { "Utility", "Tool0", "Tool7", "Key0", "Key7", "Wallet0", "Wallet7" })
                Check(!(bool)isOpen.Invoke(null, new[] { (object)player, Enum.Parse(nativeSlot, role) })!, "IS provider closes unsupported native role " + role);
            foreach (string role in new[] { "utility.wisp", "utility.wishbone" })
                Check((bool)Call("IsEquipmentPanelSlot", mapped[role])! && definition.GetProperty("Kind")!.GetValue(mapped[role])!.ToString() == "Quick",
                    role + " uses equipment visuals without changing storage/effect ownership");
            var extraUtility = new ItemData { m_gridPos = new Vector2i(0, 0), m_stack = 2,
                m_shared = new ItemData.SharedData { m_name = "smoke-utility", m_itemType = (ItemData.ItemType)18 } };
            var beltItem = new ItemData { m_gridPos = new Vector2i(1, 0), m_equipped = true,
                m_shared = new ItemData.SharedData { m_name = "smoke-belt", m_itemType = (ItemData.ItemType)18 } };
            items.Add(extraUtility); items.Add(beltItem); beltField.SetValue(player, beltItem);
            MethodInfo nativeEquip = pov.GetType("PassiveTree.Gear.UtilitySlots+Equip", true)!.GetMethod("Prefix", Statics)!;
            object[] equipArgs = { player, extraUtility, false };
            Check(!(bool)nativeEquip.Invoke(null, equipArgs)! && !(bool)equipArgs[2],
                "native utility equip rejects the removed cell without falling through to vanilla equipment");
            Check(!(bool)pov.GetType("Valheiman.Api.Equipment", true)!.GetMethod("Put")!.Invoke(null, new object[] { player, "utility", extraUtility })! &&
                extraUtility.m_gridPos == new Vector2i(0, 0) && extraUtility.m_stack == 2 && !extraUtility.m_equipped &&
                items.Count == 2 && ReferenceEquals(items[0], extraUtility) && ReferenceEquals(beltField.GetValue(player), beltItem) && beltItem.m_equipped,
                "native/API Utility rejection preserves item identity, stack, coordinates and the worn belt");
            equipArgs = new object[] { player, beltItem, false };
            Check((bool)nativeEquip.Invoke(null, equipArgs)!, "belt still follows its original equipment path");
            items.Clear(); beltField.SetValue(player, oldBelt);
            var originalCoordinates = definitions.Cast<object>().ToDictionary(Id, Position);
            Layout();
            Check(View("PovVisibleActions").Count == 1 && Role(View("PovVisibleActions")[0]!) == "arrow.0", "baseline action bank contains only unlocked arrow cell");
            Size(4, 1, "three quick slots plus one open PoV slot reserve exactly four columns");
            Check(((IList)Call("GetQuickPanelSlots", player)!).Cast<object>().All(s => Role(s) == null) && ((IList)Call("GetQuickPanelSlots", player)!).Count == 3,
                "generic quick HUD list remains exactly the original three hotkey cells");
            Check((Vector3)Call("GetPathOfValheimanActionPosition", 0, 70f)! == new Vector3(210, 0, 0), "PoV action bank starts beside the original quick bank");
            // A closed occupied cell remains visible for withdrawal; its empty
            // locked peers must not reserve unused columns or another row.
            items.Add(new ItemData { m_shared = new ItemData.SharedData(), m_gridPos = originalCoordinates["pov.potion.0"] });
            Layout(); Size(5, 1, "one additional occupied cell grows only one column");
            items.Add(new ItemData { m_shared = new ItemData.SharedData(), m_gridPos = originalCoordinates["pov.potion.1"] });
            Layout(); Size(6, 1, "three visible PoV cells complete the first bank row");
            items.Add(new ItemData { m_shared = new ItemData.SharedData(), m_gridPos = originalCoordinates["pov.potion.2"] });
            Layout(); Size(6, 2, "four visible PoV cells wrap without reserving another column bank");
            Check((Vector3)Call("GetPathOfValheimanActionPosition", 3, 70f)! == new Vector3(210, -70, 0), "fourth PoV cell retains three-column wrapping");
            Layout(2); Size(5, 2, "partial generic bank does not reserve an empty third column");
            Check((Vector3)Call("GetPathOfValheimanActionPosition", 0, 70f)! == new Vector3(140, 0, 0), "PoV starts directly after the visible generic bank");
            items.Clear(); Layout(9); Size(4, 3, "multiple generic rows do not widen the single PoV column");
            Layout(); Size(4, 1, "removing closed-cell contents shrinks the panel again");
            purseGate.SetValue(null, new Func<Player, bool>(_ => true));
            Layout();
            Check(!definitions.Cast<object>().Any(s => Role(s)?.StartsWith("purse.") == true), "Magic Purse never adds backpack capacity or display cells");
            items.Clear(); Layout(0);
            Size(1, 1, "PoV-only single slot does not reserve three columns");
            Check((Vector3)Call("GetPathOfValheimanActionPosition", 0, 70f)! == Vector3.zero, "no generic quick bank leaves no blank column bank");
            object arrow = mapped["arrow.0"]!;
            int arrowIndex = definitions.IndexOf(arrow);
            definitions.RemoveAt(arrowIndex);
            Layout(); Size(3, 1, "no PoV slots leaves only the generic bank width");
            Layout(0); Size(1, 0, "empty hidden panel keeps a safe animation divisor with zero content rows");
            definitions.Insert(arrowIndex, arrow);
            Check(definitions.Cast<object>().All(s => Position(s) == originalCoordinates[Id(s)]), "all UI layouts preserve every physical slot coordinate");

            var carried = new ItemData { m_shared = new ItemData.SharedData() };
            object quiver = Make("pov.quiver", "BuiltIn", cell: "quiver", accepts: item => ReferenceEquals(item, carried));
            // Swap only the predicate for the view test; real registered quiver
            // callbacks/equipment commits are exercised by the backend checks.
            int qi = definitions.IndexOf(mapped["quiver"]);
            definitions[qi] = quiver;
            plugin.GetField("_povQuiverAvailable", Statics)!.SetValue(null, false);
            Check(!(bool)Call("IsPathOfValheimanQuiverVisible", player, bag, quiver)!, "empty quiver with no supported item is hidden");
            items.Add(carried);
            Check((bool)Call("IsPathOfValheimanQuiverVisible", player, bag, quiver)!, "carried supported quiver reveals the equipment cell");
            items.Clear();
            plugin.GetField("_povQuiverAvailable", Statics)!.SetValue(null, true);
            Check(!(bool)Call("IsPathOfValheimanQuiverVisible", player, bag, quiver)!, "catalog availability alone does not discover a quiver");
            CheckHud(mod, pov, plugin, player, bag, config);
        }
        finally
        {
            definitions.Clear(); foreach (object value in oldDefinitions) definitions.Add(value);
            mapped.Clear(); foreach (DictionaryEntry pair in oldMapped) mapped.Add(pair.Key, pair.Value);
            items.Clear(); items.AddRange(oldItems);
            beltField.SetValue(player, oldBelt);
            purseGate.SetValue(null, oldGate); quickRows.SetValue(null, oldQuickRows);
            Call("ClearPathOfValheimanStoragePanels");
            Call("InvalidateSlotDefinitionCaches");
        }
    }
}
