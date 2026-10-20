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
    private static int grantedPovRows;
    private static int equipmentDiscoveryReads;
    private static void CountEquipmentDiscovery() => equipmentDiscoveryReads++;
    private static bool GrantedPovRows(ref int __result) { __result = grantedPovRows; return false; }

    private static ConfigEntryBase BindProgressionToggle(Type plugin, ConfigFile config, string field)
    {
        Type toggle = plugin.GetNestedType("Toggle", BindingFlags.Public)!;
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
            m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[3].ParameterType == typeof(string));
        var entry = (ConfigEntryBase)bind.MakeGenericMethod(toggle).Invoke(config,
            new object[] { "test", field, Enum.Parse(toggle, "On"), "" })!;
        plugin.GetField(field, Statics)!.SetValue(null, entry);
        return entry;
    }

    private static void InitializeProgressionConfig(Type plugin, ConfigFile config)
    {
        BindProgressionToggle(plugin, config, "_progressiveRowsEnabled");
        BindProgressionToggle(plugin, config, "_equipmentSlotProgressionEnabled");
        plugin.GetField("_maxExtraRows", Statics)!.SetValue(null, config.Bind("test", "max-rows", 3, ""));
        plugin.GetField("_rowUnlockItems", Statics)!.SetValue(null, new[] {
            config.Bind("test", "row-1", "HardAntler", ""), config.Bind("test", "row-2", "CryptKey", ""),
            config.Bind("test", "row-3", "Wishbone", "") });
    }

    private static void CheckProgression(Assembly mod, Assembly pov, Type plugin, Player player, Inventory bag,
        ConfigEntry<bool> enabled)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        var known = (HashSet<string>)AccessTools.Field(typeof(Player), "m_knownMaterial").GetValue(player);
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        object[] oldDefinitions = definitions.Cast<object>().ToArray();
        var oldMapped = mapped.Keys.Cast<object>().Select(k => new DictionaryEntry(k, mapped[k])).ToArray();
        ItemData[] oldItems = items.ToArray();
        Type definition = mod.GetType("InventorySlots.SlotDefinition", true)!;
        Type kind = mod.GetType("InventorySlots.SlotKind", true)!;
        ConfigEntryBase discovery = (ConfigEntryBase)plugin.GetField("_equipmentSlotProgressionEnabled", Statics)!.GetValue(null)!;
        ConfigEntryBase rowsOption = (ConfigEntryBase)plugin.GetField("_progressiveRowsEnabled", Statics)!.GetValue(null)!;
        void Toggle(ConfigEntryBase entry, bool on) => entry.BoxedValue = Enum.Parse(entry.SettingType, on ? "On" : "Off");
        bool Open(object slot) => (bool)Call("IsSpecialSlotUnlocked", player, bag, slot)!;
        bool Visible(object slot) => ((IList)Call("GetCustomPanelSlots", player, bag)!).Contains(slot);
        Vector2i Pos(object slot)
        {
            object?[] args = { bag, slot, null }; Call("TryGetSlotGridPos", args); return (Vector2i)args[2]!;
        }
        var hooks = new Harmony("smoke.pov.progression");
        // Only simulate the node grant. The actual bridge, discovery and row
        // policy execute from the merged mod; no live remote tree is loaded.
        hooks.Patch(AccessTools.PropertyGetter(pov.GetType("Valheiman.Api.Rows", true), "Extra"),
            prefix: new HarmonyMethod(typeof(Program), nameof(GrantedPovRows)));
        hooks.Patch(AccessTools.Method(plugin, "PlayerKnowsAcceptedSlotItem"),
            prefix: new HarmonyMethod(typeof(Program), nameof(CountEquipmentDiscovery)));
        try
        {
            items.Clear(); known.Clear(); grantedPovRows = 0;
            var aliases = (IDictionary)plugin.GetProperty("ItemNameTokens", Statics)!.GetValue(null)!;
            aliases["HardAntler"] = "$item_hardantler";
            Check((int)Call("GetUsableRegularRows", player)! == 4, "PoV with no row node starts at four backpack rows");
            var antler = new ItemData { m_shared = new ItemData.SharedData { m_name = "$item_hardantler" } };
            int before = (int)Call("CaptureRegularRowsBeforeKnownItem", player, antler)!;
            known.Add("$item_hardantler");
            Call("RevealRegularRowsAfterKnownItem", player, before);
            Check(before == 4 && (int)Call("GetUsableRegularRows", player)! == 5 &&
                (int)Call("GetInventoryViewportRows", 5)! == 5, "HardAntler discovery opens and reveals the fifth row with PoV active");
            Check((int)Call("CaptureRegularRowsBeforeKnownItem", player, antler)! == -1, "already-known antler does not repeat the discovery reveal");
            grantedPovRows = 2;
            Check((int)Call("GetUsableRegularRows", player)! == 6, "one IS row and two PoV rows grant two extra rows, not three");
            grantedPovRows = 0;
            Check((int)Call("GetUsableRegularRows", player)! == 5, "removing PoV row grant retains the IS discovery row");
            enabled.Value = false;
            Check((int)Call("GetUsableRegularRows", player)! == 5, "Equipment Off does not discard discovered backpack rows");
            enabled.Value = true;
            known.Clear(); Toggle(rowsOption, false);
            Check((int)Call("GetUsableRegularRows", player)! == 7, "row progression Off grants configured three rows even with PoV");
            Toggle(rowsOption, true);
            known.UnionWith(new[] { "$item_hardantler", "CryptKey", "Wishbone" });
            var cache = plugin.GetField("NativeRowCache", Statics)!.GetValue(null)!;
            object?[] rowArgs = { player, null };
            cache.GetType().GetMethod("TryGetValue")!.Invoke(cache, rowArgs);
            object native = rowArgs[1]!;
            native.GetType().GetField("Rows")!.SetValue(native, 6);
            Check((int)Call("GetUsableRegularRows", player)! == 9 && (int)Call("GetFixedRegularRows")! == 9,
                "two purchased rows plus three discovered rows keep the nine-row storage boundary");
            Call("RefreshNativeInventoryRows", player);
            known.Clear();

            definitions.Clear(); mapped.Clear();
            foreach (string id in new[] { "helmet", "chest", "legs", "cape", "utility", "trinket", "wishbone", "demister" })
                definitions.Add(Activator.CreateInstance(definition, new object?[] { id, id, Enum.Parse(kind, "BuiltIn"), new Func<ItemData, bool>(_ => false), -1, null }));
            Call("AppendPathOfValheimanSlots"); Call("InvalidateSlotDefinitionCaches");
            var coordinates = mapped.Keys.Cast<string>().ToDictionary(k => k, k => Pos(mapped[k]!));
            foreach (string role in new[] { "helmet", "chest", "legs", "cape", "belt", "trinket", "utility.wisp", "utility.wishbone" })
                Check(!Open(mapped[role]!), "undiscovered equipment cell stays closed: " + role);
            equipmentDiscoveryReads = 0;
            bool equipmentStayedClosed = true;
            for (int i = 0; i < 100; i++)
            {
                Call("NotifyPathOfValheimanStorageChanged");
                equipmentStayedClosed &= !Open(mapped["cape"]!) && !Open(mapped["helmet"]!);
            }
            Check(equipmentStayedClosed && equipmentDiscoveryReads == 0,
                "100 inventory changes reuse fixed equipment discovery without catalog searches");
            Check(!Open(mapped["belt"]!) && equipmentDiscoveryReads == 1,
                "inventory changes still reevaluate roles affected by late external quiver registration");
            known.Add("$smoke_new_material");
            Check(!Open(mapped["cape"]!) && equipmentDiscoveryReads == 2, "new knowledge reevaluates equipment discovery");
            Call("InvalidateSlotDefinitionCaches");
            Check(!Open(mapped["cape"]!) && equipmentDiscoveryReads == 3, "real slot definition change reevaluates equipment discovery");
            known.Clear();
            Check(Open(mapped["arrow.0"]!) && !Open(mapped["potion.0"]!), "action cells retain PoV capacity rules, independent of discovery");
            var cape = new ItemData { m_gridPos = new Vector2i(0, 0), m_shared = new ItemData.SharedData { m_name = "$smoke_cape", m_itemType = (ItemData.ItemType)17 } };
            items.Add(cape);
            Check(Open(mapped["cape"]!) && !Open(mapped["belt"]!), "carried cape opens only its accepted equipment role before discovery notification");
            items.Clear();
            Check(!Open(mapped["cape"]!), "possession alone does not permanently cache equipment discovery");
            Check(!Visible(mapped["cape"]!), "unknown empty cape is absent from the cached equipment panel");
            Toggle(discovery, false);
            Check(Open(mapped["cape"]!) && Open(mapped["utility.wisp"]!) && Visible(mapped["cape"]!),
                "discovery Off immediately reveals base gear without explicit cache invalidation");
            Toggle(discovery, true);
            Check(!Visible(mapped["cape"]!), "discovery On immediately hides unknown gear without waiting for a catalog poll");
            Toggle(discovery, false);
            if (mapped.Contains("trinket.1")) Check(!Open(mapped["trinket.1"]!), "discovery Off cannot bypass extra trinket node requirements");
            enabled.Value = false;
            Check(!Open(mapped["utility.wisp"]!) && !Open(mapped["quiver"]!), "Equipment Off still closes PoV storage and quiver insertion");
            var held = new ItemData { m_gridPos = Pos(mapped["utility.wisp"]!), m_shared = new ItemData.SharedData() };
            items.Add(held); Call("InvalidateSlotDefinitionCaches");
            Check(((IList)Call("GetCustomPanelSlots", player, bag)!).Contains(mapped["utility.wisp"]), "occupied closed utility remains visible for withdrawal");
            Check((bool)Call("TakeOutPathOfValheimanItem", player, "utility.wisp")! && held.m_gridPos.y < 4 && items.Count == 1,
                "closed utility withdrawal preserves the same item in ordinary inventory");
            Check(coordinates.All(pair => Pos(mapped[pair.Key]!) == pair.Value), "progression and option changes never move physical special-slot coordinates");
        }
        finally
        {
            hooks.UnpatchSelf(); grantedPovRows = 0; enabled.Value = true;
            Toggle(rowsOption, true); Toggle(discovery, true); known.Clear();
            Call("RefreshNativeInventoryRows", player);
            definitions.Clear(); foreach (object slot in oldDefinitions) definitions.Add(slot);
            mapped.Clear(); foreach (DictionaryEntry pair in oldMapped) mapped.Add(pair.Key, pair.Value);
            items.Clear(); items.AddRange(oldItems);
            Call("InvalidateSlotDefinitionCaches");
        }
    }
}
