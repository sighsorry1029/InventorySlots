using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void ValidateAndProjectInventory(Player player, Inventory inventory)
    {
        bool changed = ReconcileCircletExtendedLegacyHelmetState(player, inventory);
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

            if (!TryGetSlotGridPos(inventory, slot, out Vector2i target))
            {
                continue;
            }

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
        changed |= ClearDuplicateCustomEquipmentAssignments(player, inventory);

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
            blockingSlot == incomingSlot ||
            !TryGetSlotGridPos(inventory, blockingSlot, out Vector2i target))
        {
            return false;
        }

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

                if (ClearDuplicateCustomEquipmentState(
                        player,
                        inventory,
                        keeper,
                        item))
                {
                    changed = true;
                }

                if (TryRelocateOverlappingItem(player, inventory, item))
                {
                    changed = true;
                }
                else if (TryMoveOverlappingItemToOverflowPreservationCell(inventory, item))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool ClearDuplicateCustomEquipmentState(
        Player player,
        Inventory inventory,
        ItemData keeper,
        ItemData duplicate)
    {
        if (!IsInventorySlotsCustomEquipped(keeper) ||
            !IsInventorySlotsCustomEquipped(duplicate) ||
            !keeper.m_customData.TryGetValue(SlotIdKey, out string keeperSlotId) ||
            !duplicate.m_customData.TryGetValue(SlotIdKey, out string duplicateSlotId) ||
            !string.Equals(
                keeperSlotId,
                duplicateSlotId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            UnequipInventorySlotsItem(player, duplicate);
        }
        catch (System.Exception exception)
        {
            Log.LogWarning(
                $"Custom-equipment cleanup failed for duplicate slot '{duplicateSlotId}'; clearing local state: {exception.Message}");
            duplicate.m_equipped = false;
            ClearItemSlot(duplicate);
            ClearVanillaEquipmentReferences((Humanoid)player, duplicate);
            try
            {
                ((Humanoid)player).SetupEquipment();
            }
            catch (System.Exception setupException)
            {
                Log.LogWarning(
                    $"Could not refresh equipment after duplicate-slot cleanup: {setupException.Message}");
            }
        }

        try
        {
            SlotDefinition? keeperSlot = GetSlotFromItemMarker(keeper);
            if (keeperSlot != null &&
                inventory.ContainsItem(keeper) &&
                IsValidCustomEquipmentAssignment(player, keeper, keeperSlot))
            {
                RestoreSlotEquipmentState(
                    player,
                    inventory,
                    keeper,
                    keeperSlot);
            }
        }
        catch (System.Exception exception)
        {
            Log.LogWarning(
                $"Could not resynchronize retained custom equipment after duplicate cleanup: {exception.Message}");
        }

        return true;
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

        if (TryGetCanonicalEquippedSlot(player, inventory, item, out SlotDefinition? canonicalSlot) &&
            canonicalSlot != null &&
            TryGetSlotGridPos(inventory, canonicalSlot, out Vector2i target))
        {
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

            if (!TryGetSlotGridPos(inventory, slot, out Vector2i target))
            {
                continue;
            }

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

            if (!TryGetSlotById(item.m_customData[SlotIdKey], out SlotDefinition? slot) ||
                !slot!.Accepts(item) ||
                !CanUseCircletExtendedCustomSlot(player, item, slot) ||
                !CanUseHipLanternCustomSlot(item, slot))
            {
                string slotId = item.m_customData.TryGetValue(SlotIdKey, out string id) ? id : "<missing>";
                if (TryReleaseItemToRegularInventory(
                        player,
                        inventory,
                        item,
                        $"custom slot '{slotId}' is no longer valid for this item"))
                {
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool ClearDuplicateCustomEquipmentAssignments(
        Player player,
        Inventory inventory)
    {
        bool changed = false;
        Dictionary<string, ItemData> keepers =
            new(System.StringComparer.OrdinalIgnoreCase);
        foreach (ItemData item in new List<ItemData>(inventory.m_inventory))
        {
            if (item == null || !inventory.ContainsItem(item))
            {
                continue;
            }

            if (!IsInventorySlotsCustomEquipped(item) ||
                !item.m_customData.TryGetValue(SlotIdKey, out string slotId) ||
                !TryGetSlotById(slotId, out SlotDefinition? slot) ||
                slot == null)
            {
                continue;
            }

            if (!keepers.TryGetValue(slotId, out ItemData keeper))
            {
                keepers[slotId] = item;
                continue;
            }

            if (!TryGetSlotGridPos(inventory, slot, out Vector2i target))
            {
                continue;
            }

            bool itemValid = IsValidCustomEquipmentAssignment(player, item, slot);
            bool keeperValid = IsValidCustomEquipmentAssignment(player, keeper, slot);
            bool itemAtCanonicalCell = item.m_gridPos == target;
            bool keeperAtCanonicalCell = keeper.m_gridPos == target;
            ItemData retained = itemValid && !keeperValid
                ? item
                : keeperValid && !itemValid
                    ? keeper
                    : itemAtCanonicalCell && !keeperAtCanonicalCell
                        ? item
                        : keeper;
            ItemData duplicate = retained == item ? keeper : item;
            if (ClearDuplicateCustomEquipmentState(
                    player,
                    inventory,
                    retained,
                    duplicate))
            {
                changed = true;
                keepers[slotId] = retained;
            }
        }

        return changed;
    }

    private static bool IsValidCustomEquipmentAssignment(
        Player player,
        ItemData item,
        SlotDefinition slot)
    {
        return item != null &&
               slot != null &&
               slot.Accepts(item) &&
               CanUseCircletExtendedCustomSlot(player, item, slot) &&
               CanUseHipLanternCustomSlot(item, slot);
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
