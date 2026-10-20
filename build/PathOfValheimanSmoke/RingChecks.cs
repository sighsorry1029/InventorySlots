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
    private static int ringCapacity;
    private static bool RingCapacity(ref int __result) { __result = ringCapacity; return false; }
    private static bool RingUnequip(Humanoid __instance, ItemData item)
    {
        foreach (string name in new[] { "m_utilityItem", "m_trinketItem" })
        {
            FieldInfo field = AccessTools.Field(typeof(Humanoid), name);
            if (ReferenceEquals(field.GetValue(__instance), item)) field.SetValue(__instance, null);
        }
        item.m_equipped = false;
        return false; // Only the absent Unity equipment/visual boundary is replaced.
    }

    private static void CheckRings(Assembly pov, Type plugin, Player player, Inventory bag)
    {
        Type api = pov.GetType("Valheiman.Api.Equipment", true)!;
        if (api.GetMethod("IsRing") == null) return;
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        ItemData[] savedItems = items.ToArray();
        var data = (Dictionary<string, string>)AccessTools.Field(typeof(Player), "m_customData").GetValue(player);
        var savedData = new Dictionary<string, string>(data);
        Type state = pov.GetType("PassiveTree.Gear.State", true)!;
        Type utility = pov.GetType("PassiveTree.Gear.UtilitySlots", true)!;
        var pending = (HashSet<ItemData>)state.GetField("Pending", Statics)!.GetValue(null)!;
        ItemData[] savedPending = pending.ToArray();
        var enabled = (ConfigEntry<bool>)pov.GetType("PassiveTree.Gear.Module", true)!.GetField("Enabled", Statics)!.GetValue(null)!;
        bool oldEnabled = enabled.Value;
        FieldInfo belt = AccessTools.Field(typeof(Humanoid), "m_utilityItem");
        object? oldBelt = belt.GetValue(player);
        object bridge = plugin.GetField("_pov", Statics)!.GetValue(null)!;
        object provider = bridge.GetType().GetField("_provider", Instances)!.GetValue(bridge)!;
        Type contract = pov.GetType("Valheiman.Api.IStorageProvider", true)!;
        bool Put(ItemData item, string cell) => (bool)contract.GetMethod("Put")!.Invoke(provider, new object[] { player, cell, item })!;
        bool Take(string cell) => (bool)contract.GetMethod("TakeOut")!.Invoke(provider, new object[] { player, cell })!;
        bool Open(int index) => (bool)api.GetMethod("IsOpen")!.Invoke(null, new object[] { player, "ring." + index })!;
        ItemData[] Effects() => ((IEnumerable<ItemData>)utility.GetMethod("Rings", Statics)!.Invoke(null, new object[] { player })!).ToArray();
        void Layout() { Call("InvalidateSlotDefinitionCaches"); Call("UpdatePathOfValheimanPanelSlots", player, bag, 3,
            ((IList)Call("GetCustomPanelSlots", player, bag)!).Count); }
        IList Visible() => (IList)plugin.GetField("PovVisibleRings", Statics)!.GetValue(null)!;
        Vector2i Pos(int index)
        {
            object?[] args = { bag, mapped["ring." + index], null };
            Call("TryGetSlotGridPos", args);
            return (Vector2i)args[2]!;
        }
        ItemData Ring(int x, int type = 18) => new ItemData {
            m_stack = 1, m_gridPos = new Vector2i(x, 0),
            m_shared = new ItemData.SharedData { m_name = "smoke-ring", m_itemType = (ItemData.ItemType)type },
            m_customData = new Dictionary<string, string> { { "valheiman.ring", "1" }, { "smoke.effect", "distinct" } }
        };
        var hooks = new Harmony("smoke.pov.rings");
        try
        {
            items.Clear(); pending.Clear(); enabled.Value = true;
            Check((int)state.GetMethod("RingCount", Statics)!.Invoke(null, new object[] { player })! == 2 &&
                Open(0) && Open(1) && !Open(2), "original PoV gives two base rings without a node");
            Layout();
            Check(Visible().Count == 2 && !((IList)Call("GetCustomPanelSlots", player, bag)!).Contains(mapped["ring.0"]),
                "base rings bypass discovery and do not widen ordinary equipment columns");
            Check(Enumerable.Range(0, 10).All(i => mapped.Contains("ring." + i)),
                "all ten API ring identities retain stable storage definitions");
            var first = Ring(0);
            var second = Ring(1, 24);
            var plain = Ring(2); plain.m_customData.Clear();
            foreach (ItemData ring in new[] { first, second })
                Check((bool)api.GetMethod("IsRing")!.Invoke(null, new object[] { ring })! &&
                    (bool)api.GetMethod("Accepts")!.Invoke(null, new object[] { "ring.0", ring })!,
                    "original API accepts marked ring item type " + ring.m_shared.m_itemType);
            Check(!(bool)api.GetMethod("Accepts")!.Invoke(null, new object[] { "ring.0", plain })!,
                "ordinary utility item cannot enter a ring cell");
            items.AddRange(new[] { first, second, plain });
            Check(Put(first, "ring.0") && first.m_gridPos == Pos(0) && first.m_stack == 1 &&
                first.m_customData["smoke.effect"] == "distinct" && items.Count == 3,
                "provider puts the same ring reference with quantity and metadata intact");
            Vector2i oldSecond = second.m_gridPos;
            Check(!Put(second, "ring.0") && !Put(second, "ring.2") && second.m_gridPos == oldSecond,
                "occupied and locked ring targets reject without moving source");
            Check(Effects().Length == 1 && ReferenceEquals(Effects()[0], first) && !first.m_equipped,
                "original PoV effect enumeration owns stored ring without native equipped duplication");
            pending.Add(first);
            Check(Effects().Length == 0, "pending ring contributes no native effect");
            pending.Remove(first);
            hooks.Patch(AccessTools.Method(state, "RingCount"), prefix: new HarmonyMethod(typeof(Program), nameof(RingCapacity)));
            ringCapacity = 6;
            Layout();
            Check(Enumerable.Range(0, 6).All(Open) && !Open(6) && !Open(9) && Visible().Count == 6,
                "six granted rings open only the first six reserved identities");
            Check(Put(second, "ring.5") && Effects().Length == 2, "unlocked extra ring participates in actual effects");
            ringCapacity = 2; Layout();
            Check(Visible().Count == 3 && Visible().Contains(mapped["ring.5"]) && Effects().Length == 1,
                "refunded node keeps occupied locked cell accessible but removes its effect");
            Check(Take("ring.5") && second.m_gridPos.y < (int)Call("GetFixedRegularRows")!,
                "locked ring can be withdrawn into backpack");
            Layout(); Check(Visible().Count == 2, "empty locked ring no longer reserves panel space");
            enabled.Value = false; Layout();
            Check(Visible().Count == 1 && Effects().Length == 0 && Take("ring.0"),
                "Equipment Off preserves only occupied recovery cell and allows withdrawal");
            enabled.Value = true; Layout();
            // Native Tick used to unequip before attempting the move. Fill both
            // destinations and prove the deferred call preserves the worn item.
            var blocker = Ring(3);
            items.Add(blocker);
            Check(Put(first, "ring.0") && Put(second, "ring.1"), "fill base ring destinations");
            blocker.m_equipped = true; belt.SetValue(player, blocker);
            hooks.Patch(AccessTools.Method(typeof(Humanoid), "UnequipItem"),
                prefix: new HarmonyMethod(typeof(Program), nameof(RingUnequip)));
            utility.GetMethod("Tick", Statics)!.Invoke(null, new object[] { player });
            Check(ReferenceEquals(belt.GetValue(player), blocker) && blocker.m_equipped && blocker.m_gridPos == new Vector2i(3, 0),
                "failed native ring hand-off preserves worn ring and its coordinate");
            Check(Take("ring.1"), "free one ring destination");
            utility.GetMethod("Tick", Statics)!.Invoke(null, new object[] { player });
            Check(belt.GetValue(player) == null && !blocker.m_equipped && blocker.m_gridPos == Pos(1) &&
                Effects().Count(it => ReferenceEquals(it, blocker)) == 1,
                "successful native hand-off clears old native equipment exactly once");
            // The native ordinal save format must map by role, not UI index.
            items.Clear(); items.Add(first); items.Add(second);
            first.m_gridPos = new Vector2i(0, 10); // native rowStart=4, ordinal48
            second.m_gridPos = new Vector2i(5, 10); // ordinal53
            data["valheiman.equipment.v1.rowStart"] = "4";
            Call("CompletePathOfValheimanLoad", player);
            Check(first.m_gridPos == Pos(0) && second.m_gridPos == Pos(5) && items.Count == 2,
                "native ring save coordinates reproject without cloning, including locked occupied ring");
            Layout();
            var cols = plugin.GetField("_povEquipmentColumns", Statics)!;
            var rows = plugin.GetField("_povEquipmentRows", Statics)!;
            cols.SetValue(null, 3); rows.SetValue(null, 3);
            ringCapacity = 6; Layout(); cols.SetValue(null, 3); rows.SetValue(null, 3);
            Check((float)Call("GetPathOfValheimanRingPanelRows")! == .5f &&
                (Vector3)Call("GetPathOfValheimanRingPosition", 5, 70f, new Rect(0, -70, 70, 70))! == new Vector3(175, -210, 0),
                "six half-sized rings occupy one half-height row under three equipment columns");
            cols.SetValue(null, 2);
            Check((float)Call("GetPathOfValheimanRingPanelRows")! == 1f &&
                (Vector3)Call("GetPathOfValheimanRingPosition", 4, 70f, new Rect(0, -70, 70, 70))! == new Vector3(0, -245, 0),
                "narrow equipment panel wraps ring five onto second miniature row");
            foreach (Rect rect in new[] { new Rect(0, -70, 70, 70), new Rect(-35, -35, 70, 70), new Rect(-62, 0, 62, 64) })
            {
                Vector3 point = (Vector3)Call("GetPathOfValheimanRingPosition", 0, 70f, rect)!;
                Check(point.x + rect.xMin * .5f == rect.xMin && point.y + rect.yMax * .5f == -210 + rect.yMax,
                    "miniature cell preserves original left/top bounds for pivot and size " + rect);
            }
            Check(!((IList)plugin.GetField("PovVisibleActions", Statics)!.GetValue(null)!).Contains(mapped["ring.0"]) &&
                !((IList)Call("GetQuickPanelSlots", player)!).Contains(mapped["ring.0"]),
                "rings never enter ordinary quick bank or PoV action HUD");
            CheckRingPanelOffset(plugin, cols);
        }
        finally
        {
            hooks.UnpatchSelf();
            items.Clear(); items.AddRange(savedItems);
            data.Clear(); foreach (var pair in savedData) data.Add(pair.Key, pair.Value);
            pending.Clear(); pending.UnionWith(savedPending);
            belt.SetValue(player, oldBelt); enabled.Value = oldEnabled;
            Layout();
        }
    }

    private static void CheckRingPanelOffset(Type plugin, FieldInfo columns)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        object panels = plugin.GetField("InventoryPanels", Statics)!.GetValue(null)!;
        FieldInfo offset = panels.GetType().GetField("QuickSlotsPanelRuntimeOffset")!;
        FieldInfo dragging = panels.GetType().GetField("DraggingQuickSlotsPanelOffset")!;
        object? oldOffset = offset.GetValue(panels), oldDragging = dragging.GetValue(panels);
        Vector2 baseline = (Vector2)plugin.GetField("QuickSlotsPanelFixedOffset", Statics)!.GetValue(null)!;
        try
        {
            columns.SetValue(null, 3); offset.SetValue(panels, baseline); dragging.SetValue(panels, false);
            Check((Vector3)Call("GetQuickSlotPanelDisplayOffset", 70f)! == (Vector3)(baseline + new Vector2(0, -35)),
                "default quick panel makes exactly one miniature row of room");
            Vector2 custom = baseline + new Vector2(12, -8); offset.SetValue(panels, custom);
            Check((Vector3)Call("GetQuickSlotPanelDisplayOffset", 70f)! == (Vector3)custom,
                "user positioned quick panel remains absolute");
            offset.SetValue(panels, baseline); dragging.SetValue(panels, true);
            Check((Vector3)Call("GetQuickSlotPanelDisplayOffset", 70f)! == (Vector3)baseline,
                "dragging does not apply the default offset twice");
            dragging.SetValue(panels, false); Call("ClearPathOfValheimanStoragePanels");
            Check((Vector3)Call("GetQuickSlotPanelDisplayOffset", 70f)! == (Vector3)baseline,
                "clearing PoV layout removes its derived default offset");
        }
        finally { offset.SetValue(panels, oldOffset); dragging.SetValue(panels, oldDragging); }
    }
}
