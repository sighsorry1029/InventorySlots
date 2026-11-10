using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // These lists describe presentation only. SlotDefinitions and its storage
    // indices must never be reordered when a YAML slot changes size.
    private static readonly List<SlotDefinition> FullEquipmentPanelSlots = new();
    private static readonly List<SlotDefinition> SmallEquipmentPanelSlots = new();
    private const float SmallEquipmentSlotScale = 0.5f;
    private static int _equipmentPanelColumns = 1;
    private static int _equipmentPanelRows = CustomSlotPanelRows;

    private static bool IsSmallEquipmentPanelSlot(SlotDefinition slot) =>
        IsPathOfValheimanRingSlot(slot) ||
        slot.SmallSlot && IsEquipmentPanelSlot(slot) &&
        slot.Id is not ("helmet" or "chest" or "legs" or "cape");

    private static void UpdateInventoryPanelSlots(Player player, Inventory inventory, int quickCount)
    {
        UpdatePathOfValheimanActionSlots(player, inventory, quickCount);
        UpdateEquipmentPanelSlots(player, inventory);
    }

    private static void UpdateEquipmentPanelSlots(Player player, Inventory inventory)
    {
        FullEquipmentPanelSlots.Clear();
        SmallEquipmentPanelSlots.Clear();
        // Apply the existing discovery and occupied-slot recovery rules before
        // partitioning the visible cells. Smallness must not unlock equipment.
        foreach (SlotDefinition slot in GetCustomPanelSlots(player, inventory))
        {
            (IsSmallEquipmentPanelSlot(slot) ? SmallEquipmentPanelSlots : FullEquipmentPanelSlots).Add(slot);
        }
        UpdatePathOfValheimanRingSlots(player, inventory);
        SmallEquipmentPanelSlots.AddRange(PovVisibleRings);
        _equipmentPanelColumns = Math.Max(1, GetCustomPanelColumns(FullEquipmentPanelSlots.Count));
        _equipmentPanelRows = FullEquipmentPanelSlots.Count > 0 ? CustomSlotPanelRows : 0;
    }

    private static float GetSmallEquipmentPanelRows() =>
        Mathf.CeilToInt(SmallEquipmentPanelSlots.Count / (float)(_equipmentPanelColumns * 2)) * SmallEquipmentSlotScale;

    private static Vector3 GetSmallEquipmentPanelPosition(int index, float spacing, Rect cellRect) =>
        new Vector3(index % (_equipmentPanelColumns * 2) * spacing * SmallEquipmentSlotScale,
            -(_equipmentPanelRows + index / (_equipmentPanelColumns * 2) * SmallEquipmentSlotScale) * spacing, 0f) +
        new Vector3(cellRect.xMin, cellRect.yMax, 0f) * (1f - SmallEquipmentSlotScale);

    private static Vector3 GetQuickSlotPanelDisplayOffset(float spacing)
    {
        Vector3 offset = InventoryPanels.QuickSlotsPanelRuntimeOffset;
        // Only the default pose makes room for small rows. User-placed panels
        // retain their absolute saved position, including while being dragged.
        if (IsQuickSlotPanelAtDefaultPosition() && !InventoryPanels.DraggingQuickSlotsPanelOffset)
            offset.y -= GetSmallEquipmentPanelRows() * spacing;
        return offset;
    }

    private static bool IsQuickSlotPanelAtDefaultPosition() =>
        !InventoryClient.ClientState.Inventory.QuickSlotsPanelManuallyPlaced &&
        InventoryPanels.QuickSlotsPanelRuntimeOffset == QuickSlotsPanelFixedOffset;

    internal static void ClearEquipmentPanelLayout()
    {
        FullEquipmentPanelSlots.Clear();
        SmallEquipmentPanelSlots.Clear();
        _equipmentPanelColumns = 1;
        _equipmentPanelRows = CustomSlotPanelRows;
        _quickHudLayoutVersion = -1;
    }
}
