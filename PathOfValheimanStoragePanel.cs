using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly List<SlotDefinition> PovVisibleActions = new();
    private static readonly List<SlotDefinition> PovVisibleRings = new();
    private const float PovRingScale = 0.5f;
    private static int _povEquipmentColumns = 1;
    private static int _povEquipmentRows = CustomSlotPanelRows;
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
        slot.PovCell is "utility" or "utility.wisp" or "utility.wishbone" || IsPathOfValheimanRingSlot(slot);

    private static bool IsPathOfValheimanRingSlot(SlotDefinition slot) =>
        slot.PovCell != null && slot.PovCell.StartsWith("ring.", StringComparison.Ordinal);

    private static float GetPathOfValheimanRingPanelRows() =>
        Mathf.CeilToInt(PovVisibleRings.Count / (float)(_povEquipmentColumns * 2)) * PovRingScale;

    private static Vector3 GetPathOfValheimanRingPosition(int index, float spacing, Rect cellRect) =>
        new Vector3(index % (_povEquipmentColumns * 2) * spacing * PovRingScale,
            -(_povEquipmentRows + index / (_povEquipmentColumns * 2) * PovRingScale) * spacing, 0f) +
        new Vector3(cellRect.xMin, cellRect.yMax, 0f) * (1f - PovRingScale);

    private static Vector3 GetQuickSlotPanelDisplayOffset(float spacing)
    {
        Vector3 offset = InventoryPanels.QuickSlotsPanelRuntimeOffset;
        // Saved positions are always normalized to coordinates, including the
        // default. Only that default gains room; do not save the derived offset.
        if (InventoryPanels.QuickSlotsPanelRuntimeOffset == QuickSlotsPanelFixedOffset &&
            !InventoryPanels.DraggingQuickSlotsPanelOffset)
            offset.y -= GetPathOfValheimanRingPanelRows() * spacing;
        return offset;
    }

    internal static void RestorePathOfValheimanRingIconScale(InventoryGrid grid)
    {
        if (_pov?.HasRings != true || InventoryGui.instance == null ||
            grid != InventoryGui.instance.m_playerGrid || _pov.ProviderId() != ModGUID) return;
        Inventory inventory = grid.GetInventory();
        if (inventory == null || grid.m_elements == null) return;
        foreach (SlotDefinition slot in PovVisibleRings)
        {
            if (!TryGetSlotGridPos(inventory, slot, out Vector2i pos)) continue;
            int index = pos.y * inventory.GetWidth() + pos.x;
            if (index < 0 || index >= grid.m_elements.Count) continue;
            InventoryElement element = grid.m_elements[index];
            if (!IsUnityNull(element) && element.m_icon != null &&
                element.gameObject.transform.localScale.x == PovRingScale)
                element.m_icon.rectTransform.localScale = Vector3.one;
        }
    }

    private static bool KeepPathOfValheimanStatPanelLayout(InventoryGui __0, ref bool ___Columned)
    {
        if (_pov == null || _pov.ProviderId() != ModGUID || __0 != InventoryGui.instance ||
            InventoryPanels.PlayerStatPanelHost == null || !InventoryPanels.PlayerStatPanelHost.gameObject.activeInHierarchy)
            return true;
        ___Columned = false;
        return false;
    }

    private static bool IsOccupiedSlot(Inventory? inventory, SlotDefinition slot) =>
        inventory != null && TryGetSlotGridPos(inventory, slot, out Vector2i pos) && inventory.GetItemAt(pos.x, pos.y) != null;

    private static void UpdatePathOfValheimanPanelSlots(Player player, Inventory inventory, int quickCount, int equipmentCount)
    {
        UpdatePathOfValheimanActionSlots(player, inventory, quickCount);
        UpdatePathOfValheimanEquipmentPanelSlots(player, inventory, equipmentCount);
    }

    private static void UpdatePathOfValheimanEquipmentPanelSlots(Player player, Inventory inventory, int equipmentCount)
    {
        PovVisibleRings.Clear();
        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (!IsPathOfValheimanRingSlot(slot) || IsUnsupportedPathOfValheimanCell(slot.PovCell!)) continue;
            // Keep an occupied, now-locked cell reachable for withdrawal.
            if (IsQuickSlotUnlocked(player, slot) || IsOccupiedSlot(inventory, slot)) PovVisibleRings.Add(slot);
        }
        _povEquipmentColumns = Math.Max(1, GetCustomPanelColumns(equipmentCount));
        _povEquipmentRows = equipmentCount > 0 ? CustomSlotPanelRows : 0;
    }

    private static void UpdatePathOfValheimanActionSlots(Player player, Inventory inventory, int quickCount)
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
        PovVisibleRings.Clear();
        _povEquipmentColumns = 1;
        _povEquipmentRows = CustomSlotPanelRows;
        _povQuiverCatalog = null;
        _povQuiverCatalogCount = -1;
        _povQuiverCatalogNextCheck = 0;
        _povQuiverAvailable = false;
        _povQuickColumns = QuickSlotPanelColumns;
    }
}

// PoV shrinks ring icons inside ordinary cells at priority 200. Our half-sized
// cells already provide that sizing; keep their inner icons at full scale.
[HarmonyPatch(typeof(InventoryGrid), "UpdateGui", new[] { typeof(Player), typeof(ItemDrop.ItemData) })]
internal static class PathOfValheimanRingIconScalePatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(InventoryGrid __instance) =>
        InventorySlotsPlugin.RestorePathOfValheimanRingIconScale(__instance);
}
