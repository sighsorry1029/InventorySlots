using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void ValidateAndProjectInventory(Player player, Inventory inventory)
    {
        bool changed = false;
        List<ItemData> items = new(inventory.m_inventory.Count);
        foreach (ItemData item in inventory.m_inventory)
        {
            if (item != null)
            {
                items.Add(item);
            }
        }

        foreach (ItemData item in items)
        {
            if (IsExternalReservedCell(item.m_gridPos, includeRestockableSlots: true))
            {
                continue;
            }

            if (IsOutOfBounds(inventory, item.m_gridPos))
            {
                if (TryMoveToFirstFreeRegularCell(player, inventory, item))
                {
                    changed = true;
                }
                continue;
            }

            if (IsLockedRowCell(player, item.m_gridPos))
            {
                continue;
            }

            if (TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? slot))
            {
                if (slot == null || !IsSpecialSlotUnlocked(player, inventory, slot))
                {
                    continue;
                }
                else if (!slot.Accepts(item))
                {
                    if (TryGetCanonicalEquippedSlot(player, inventory, item, out SlotDefinition? canonicalSlot) && canonicalSlot != slot)
                    {
                        continue;
                    }

                    if (TryReleaseItemToRegularInventory(player, inventory, item, $"slot '{slot.Id}' no longer accepts it"))
                    {
                        changed = true;
                    }
                }
                else if (!InventorySafety.SuppressSlotAutoEquip &&
                         CanAutoAdoptGridSlot(item, slot) &&
                         FindItemForSlot(player, inventory, slot) != item &&
                         TryEquipIntoSlot(player, inventory, item, slot))
                {
                    changed = true;
                }
            }
        }

        changed |= ResolveOverlappingItems(player, inventory);

        foreach (SlotDefinition slot in SlotDefinitions)
        {
            ItemData? item = FindItemForSlotIncludingGridCandidate(player, inventory, slot);
            if (item == null)
            {
                continue;
            }

            Vector2i target = GetSlotGridPos(inventory, slot);
            ItemData? blocking = inventory.GetItemAt(target.x, target.y);
            if (blocking != null && blocking != item)
            {
                if (TryMoveBlockingItemToCanonicalSlot(player, inventory, blocking, item, slot))
                {
                    changed = true;
                }
                else
                {
                    if (TryReleaseItemToRegularInventory(player, inventory, blocking, $"it blocks slot '{slot.Id}'"))
                    {
                        changed = true;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            if (item.m_gridPos != target)
            {
                item.m_gridPos = target;
                changed = true;
            }

            changed |= RestoreSlotEquipmentState(player, inventory, item, slot);
        }

        changed |= ClearMissingCustomEquipment(player, inventory);

        if (changed)
        {
            inventory.Changed();
            RefreshExternalEquipmentEffects(player);
        }
    }

    private static bool TryGetCanonicalEquippedSlot(Player player, Inventory inventory, ItemData item, out SlotDefinition? slot)
    {
        slot = null;
        if (player == null || inventory == null || item == null || !inventory.ContainsItem(item))
        {
            return false;
        }

        foreach (SlotDefinition candidate in SlotDefinitions)
        {
            if (candidate.Kind == SlotKind.Quick || !CanUseSpecialSlot(player, inventory, item, candidate))
            {
                continue;
            }

            if (FindItemForSlotIncludingGridCandidate(player, inventory, candidate) == item)
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryMoveBlockingItemToCanonicalSlot(Player player, Inventory inventory, ItemData blocking, ItemData incoming, SlotDefinition incomingSlot)
    {
        if (!TryGetCanonicalEquippedSlot(player, inventory, blocking, out SlotDefinition? blockingSlot) ||
            blockingSlot == null ||
            blockingSlot == incomingSlot)
        {
            return false;
        }

        Vector2i target = GetSlotGridPos(inventory, blockingSlot);
        ItemData? targetItem = inventory.GetItemAt(target.x, target.y);
        if (targetItem != null && targetItem != incoming)
        {
            return false;
        }

        if (blocking.m_gridPos == target)
        {
            return false;
        }

        blocking.m_gridPos = target;
        return true;
    }

    private static bool ResolveOverlappingItems(Player player, Inventory inventory)
    {
        bool changed = false;
        int moved = 0;
        int unresolved = 0;
        Dictionary<Vector2i, List<ItemData>> overlaps = new();

        foreach (ItemData item in inventory.m_inventory)
        {
            if (item == null || IsOutOfBounds(inventory, item.m_gridPos))
            {
                continue;
            }

            if (!overlaps.TryGetValue(item.m_gridPos, out List<ItemData> itemsAtPosition))
            {
                itemsAtPosition = new List<ItemData>(2);
                overlaps[item.m_gridPos] = itemsAtPosition;
            }

            itemsAtPosition.Add(item);
        }

        foreach (KeyValuePair<Vector2i, List<ItemData>> pair in overlaps)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            Vector2i position = pair.Key;
            if (IsExternalReservedCell(position, includeRestockableSlots: true))
            {
                continue;
            }

            List<ItemData> overlappingItems = pair.Value;
            ItemData keeper = SelectOverlapKeeper(player, inventory, position, overlappingItems);
            foreach (ItemData item in overlappingItems)
            {
                if (item == keeper)
                {
                    continue;
                }

                if (TryRelocateOverlappingItem(player, inventory, item))
                {
                    moved++;
                    changed = true;
                }
                else if (TryMoveOverlappingItemToOverflowPreservationCell(inventory, item))
                {
                    moved++;
                    changed = true;
                }
                else
                {
                    unresolved++;
                }
            }
        }

        return changed;
    }

    private static ItemData SelectOverlapKeeper(Player player, Inventory inventory, Vector2i position, List<ItemData> items)
    {
        if (TryGetSlotAtGridPos(inventory, position, out SlotDefinition? slot))
        {
            ItemData? slotItem = FindItemForSlotIncludingGridCandidate(player, inventory, slot!);
            if (slotItem != null && items.Contains(slotItem) && slot!.Accepts(slotItem))
            {
                return slotItem;
            }

            foreach (ItemData item in items)
            {
                if (slot!.Accepts(item))
                {
                    return item;
                }
            }
        }

        return items[0];
    }

    private static bool TryRelocateOverlappingItem(Player player, Inventory inventory, ItemData item)
    {
        if (TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? sourceSlot) && sourceSlot!.Kind == SlotKind.Quick)
        {
            return TryMoveOverlappingQuickSlotItemToEmptyQuickSlot(player, inventory, item);
        }

        if (TryGetCanonicalEquippedSlot(player, inventory, item, out SlotDefinition? canonicalSlot) && canonicalSlot != null)
        {
            Vector2i target = GetSlotGridPos(inventory, canonicalSlot);
            if (CanUseCell(player, inventory, item, target) && CellContainsOnly(inventory, target, item))
            {
                item.m_gridPos = target;
                return true;
            }
        }

        return TryMoveToFirstFreeRegularCell(player, inventory, item);
    }

    private static bool TryMoveOverlappingItemToOverflowPreservationCell(Inventory inventory, ItemData item)
    {
        if (inventory == null || item == null || !inventory.ContainsItem(item))
        {
            return false;
        }

        InventorySlotSafetyCore.GridCell cell = InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
            inventory.GetWidth(),
            inventory.GetHeight(),
            new InventorySlotSafetyCore.GridCell(item.m_gridPos.x, item.m_gridPos.y),
            (x, y) =>
            {
                foreach (ItemData other in inventory.m_inventory)
                {
                    if (other != null && other != item && other.m_gridPos.x == x && other.m_gridPos.y == y)
                    {
                        return true;
                    }
                }

                return false;
            });
        Vector2i target = new(cell.X, cell.Y);
        if (target == item.m_gridPos)
        {
            return false;
        }

        item.m_gridPos = target;
        return true;
    }

    private static bool TryMoveOverlappingQuickSlotItemToEmptyQuickSlot(Player player, Inventory inventory, ItemData item)
    {
        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (slot.Kind != SlotKind.Quick || !CanUseSpecialSlot(player, inventory, item, slot))
            {
                continue;
            }

            Vector2i target = GetSlotGridPos(inventory, slot);
            if (target == item.m_gridPos ||
                !CanUseCell(player, inventory, item, target) ||
                inventory.GetItemAt(target.x, target.y) != null)
            {
                continue;
            }

            item.m_gridPos = target;
            return true;
        }

        return false;
    }

    private static bool ClearMissingCustomEquipment(Player player, Inventory inventory)
    {
        bool changed = false;
        foreach (ItemData item in inventory.m_inventory)
        {
            if (!IsInventorySlotsCustomEquipped(item))
            {
                continue;
            }

            if (!TryGetSlotById(item.m_customData[SlotIdKey], out SlotDefinition? slot) || !slot!.Accepts(item))
            {
                string slotId = item.m_customData.TryGetValue(SlotIdKey, out string id) ? id : "<missing>";
                if (TryReleaseItemToRegularInventory(player, inventory, item, $"custom slot '{slotId}' no longer exists or no longer accepts it"))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool TryReleaseItemToRegularInventory(Player player, Inventory inventory, ItemData item, string reason)
    {
        if (item == null || !inventory.ContainsItem(item))
        {
            return false;
        }

        if (IsUsableRegularCell(inventory, player, item.m_gridPos) &&
            inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y) == item)
        {
            ClearSlotRecoveryWarning(item, reason);
            UnequipInventorySlotsItem(player, item);
            return true;
        }

        if (TryMoveToFirstFreeRegularCell(player, inventory, item))
        {
            ClearSlotRecoveryWarning(item, reason);
            UnequipInventorySlotsItem(player, item);
            return true;
        }

        WarnUnableToReleaseSlotItem(item, reason);
        return false;
    }

    private static void WarnUnableToReleaseSlotItem(ItemData item, string reason)
    {
        string key = GetSlotRecoveryWarningKey(item, reason);
        if (!InventorySafety.SlotRecoveryWarnings.Add(key))
        {
            return;
        }

        string itemName = item.m_shared?.m_name ?? GetItemPrefabName(item);
        string slotId = item.m_customData != null && item.m_customData.TryGetValue(SlotIdKey, out string id) ? id : "<none>";
        Log.LogWarning($"Unable to move {itemName} out of InventorySlots slot state because {reason} and no regular inventory cell is free. The item was left in place with slot id '{slotId}'. Free an inventory cell and reload or reconnect to let InventorySlots recover it.");
    }

    private static void ClearSlotRecoveryWarning(ItemData item, string reason)
    {
        InventorySafety.SlotRecoveryWarnings.Remove(GetSlotRecoveryWarningKey(item, reason));
    }

    private static string GetSlotRecoveryWarningKey(ItemData item, string reason)
    {
        string itemKey = !string.IsNullOrWhiteSpace(GetItemPrefabName(item)) ? GetItemPrefabName(item) : item.m_shared?.m_name ?? "<unknown>";
        string slotId = item.m_customData != null && item.m_customData.TryGetValue(SlotIdKey, out string id) ? id : "<none>";
        return $"{slotId}|{itemKey}|{reason}";
    }

}
