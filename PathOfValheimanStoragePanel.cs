using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string PovStoragePanelName = "InventorySlotsPoVStorage";
    private const int PovStorageColumns = 6;
    private static readonly Dictionary<int, RectTransform> PovStoragePanels = new();
    private static readonly List<SlotDefinition> PovVisibleStorage = new();

    private static List<SlotDefinition> GetPathOfValheimanPanelSlots(Player player, Inventory inventory)
    {
        PovVisibleStorage.Clear();
        foreach (var pair in PovSlots)
        {
            SlotDefinition slot = pair.Value;
            if (slot.Kind != SlotKind.Quick) continue;
            if (IsQuickSlotUnlocked(player, slot) || TryGetSlotGridPos(inventory, slot, out Vector2i pos) && inventory.GetItemAt(pos.x, pos.y) != null)
                PovVisibleStorage.Add(slot);
        }
        return PovVisibleStorage;
    }

    internal static void ClearPathOfValheimanStoragePanels()
    {
        foreach (var panel in PovStoragePanels.Values)
            if (panel != null) Object.Destroy(panel.gameObject);
        PovStoragePanels.Clear();
        PovVisibleStorage.Clear();
    }
}
