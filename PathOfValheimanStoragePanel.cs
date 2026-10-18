using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly List<SlotDefinition> PovVisibleActions = new();
    private static int _povActionColumnOffset;
    private static int _povQuickColumns = QuickSlotPanelColumns;
    private static int _povQuickContentRows;
    private static ObjectDB? _povQuiverCatalog;
    private static int _povQuiverCatalogCount = -1;
    private static float _povQuiverCatalogNextCheck;
    private static bool _povQuiverAvailable;

    // Visual grouping is deliberately independent of Kind: utility cells remain
    // PoV-owned storage and must not acquire InventorySlots' equipment effects.
    private static bool IsEquipmentPanelSlot(SlotDefinition slot) => slot.Kind != SlotKind.Quick ||
        slot.PovCell is "utility" or "utility.wisp" or "utility.wishbone";

    private static bool IsOccupiedSlot(Inventory? inventory, SlotDefinition slot) =>
        inventory != null && TryGetSlotGridPos(inventory, slot, out Vector2i pos) && inventory.GetItemAt(pos.x, pos.y) != null;

    private static void UpdatePathOfValheimanPanelSlots(Player player, Inventory inventory, int quickCount)
    {
        PovVisibleActions.Clear();
        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (slot.PovCell == null || IsEquipmentPanelSlot(slot) || IsUnsupportedPathOfValheimanCell(slot.PovCell)) continue;
            if (!IsQuickSlotUnlocked(player, slot) && !IsOccupiedSlot(inventory, slot)) continue;
            PovVisibleActions.Add(slot);
        }
        // Keep the original Z/X/C bank and HUD indices intact. PoV's keys and
        // node/capacity rules own the adjacent bank; no second UseItem dispatch.
        // Do not reserve a full bank for locked cells: one visible PoV slot
        // beside Z/X/C needs four columns, not six. Row wrapping stays unchanged.
        _povActionColumnOffset = Math.Min(QuickSlotPanelColumns, quickCount);
        _povQuickColumns = Math.Max(1, _povActionColumnOffset + Math.Min(QuickSlotPanelColumns, PovVisibleActions.Count));
        _povQuickContentRows = Math.Max(GetQuickPanelRows(quickCount), GetQuickPanelRows(PovVisibleActions.Count));
    }

    private static Vector3 GetPathOfValheimanActionPosition(int index, float spacing) =>
        new((_povActionColumnOffset + index % QuickSlotPanelColumns) * spacing,
            -(index / QuickSlotPanelColumns) * spacing, 0);

    private static void RefreshPathOfValheimanQuiverAvailability()
    {
        if (_pov == null) return;
        ObjectDB db = ObjectDB.instance;
        int count = db != null ? db.m_items?.Count ?? 0 : 0;
        if (_povQuiverCatalog == db && _povQuiverCatalogCount == count && Time.unscaledTime < _povQuiverCatalogNextCheck) return;
        _povQuiverCatalog = db;
        _povQuiverCatalogCount = count;
        _povQuiverCatalogNextCheck = Time.unscaledTime + 2f;
        _povQuiverAvailable = false;
        if (db != null && db.m_items != null)
            foreach (GameObject prefab in db.m_items)
            {
                if (prefab == null) continue;
                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared != null && _pov.Accepts("quiver", drop.m_itemData))
                { _povQuiverAvailable = true; break; }
            }
        // Also refresh carried custom-data capacity and late external registrations.
        InvalidateVisibleCustomPanelSlots();
    }

    private static bool IsPathOfValheimanQuiverVisible(Player player, Inventory? inventory, SlotDefinition slot) =>
        IsOccupiedSlot(inventory, slot) || _pov?.QuiverItem(player) != null ||
        IsEquipmentSlotUnlocked(player, inventory, slot) && (_povQuiverAvailable || InventoryContainsAcceptedSlotItem(inventory, slot));

    private static string GetPathOfValheimanSlotBindingText(SlotDefinition slot)
    {
        if (IsEquipmentPanelSlot(slot) || _pov == null) return slot.Name;
        string key = _pov.KeyLabel(slot.PovCell!);
        return string.IsNullOrEmpty(key) ? slot.Name : key + " · " + slot.Name;
    }

    internal static void ClearPathOfValheimanStoragePanels()
    {
        PovVisibleActions.Clear();
        _povQuiverCatalog = null;
        _povQuiverCatalogCount = -1;
        _povQuiverCatalogNextCheck = 0;
        _povQuiverAvailable = false;
        _povQuickColumns = QuickSlotPanelColumns;
    }
}
