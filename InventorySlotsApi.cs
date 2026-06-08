using System;
using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public static class InventorySlotsApi
{
    public static bool TryGetCustomEquippedItem(Player player, Func<ItemData?, bool> predicate, out ItemData? item) =>
        InventorySlotsPlugin.TryGetCustomEquippedItemForApi(player, predicate, out item);

    public static bool TryGetCustomEquipmentVisualRoots(VisEquipment visEquipment, ItemData item, List<GameObject> roots) =>
        InventorySlotsPlugin.TryGetCustomEquipmentVisualRootsForApi(visEquipment, item, roots);
}
