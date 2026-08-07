using System;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool TryFindMultiUserContainerDestination(
        Inventory inventory,
        ItemData incoming,
        int amount,
        out Vector2i destination)
    {
        destination = new Vector2i(-1, -1);
        if (!IsValidMultiUserContainerInventory(inventory) ||
            !IsValidMultiUserContainerMutationItem(incoming) ||
            amount <= 0 ||
            amount > incoming.m_stack ||
            amount > incoming.m_shared.m_maxStackSize)
        {
            return false;
        }

        MultiUserContainerItemSnapshot? incomingSnapshot =
            CreateMultiUserContainerItemSnapshot(incoming);
        if (incomingSnapshot == null)
        {
            return false;
        }

        int width = inventory.GetWidth();
        int height = inventory.GetHeight();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ItemData? target = inventory.GetItemAt(x, y);
                if (!CanAcceptEntireMultiUserContainerStack(
                        incomingSnapshot,
                        incoming.m_shared.m_maxStackSize,
                        target,
                        amount))
                {
                    continue;
                }

                destination = new Vector2i(x, y);
                return true;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (inventory.GetItemAt(x, y) != null)
                {
                    continue;
                }

                destination = new Vector2i(x, y);
                return true;
            }
        }

        return false;
    }

    internal static bool TryApplyMultiUserContainerAdd(
        Inventory inventory,
        ItemData incoming,
        int amount,
        Vector2i targetPosition,
        int expectedTargetStack)
    {
        if (!IsValidMultiUserContainerCoordinate(inventory, targetPosition) ||
            !IsValidMultiUserContainerMutationItem(incoming) ||
            amount <= 0 ||
            amount > incoming.m_stack ||
            amount > incoming.m_shared.m_maxStackSize ||
            expectedTargetStack < 0)
        {
            return false;
        }

        ItemData itemToAdd;
        try
        {
            itemToAdd = incoming.Clone();
        }
        catch
        {
            return false;
        }

        if (!IsValidMultiUserContainerMutationItem(itemToAdd))
        {
            return false;
        }

        itemToAdd.m_stack = amount;
        itemToAdd.m_gridPos = targetPosition;
        itemToAdd.m_equipped = false;

        ItemData? target = inventory.GetItemAt(targetPosition.x, targetPosition.y);
        if (target == null)
        {
            if (!MultiUserContainerTransferCore.MatchesExpectedStackState(
                    expectedTargetStack,
                    actualStack: null))
            {
                return false;
            }

            inventory.m_inventory.Add(itemToAdd);
            NotifyMultiUserContainerInventoryChanged(inventory);
            return true;
        }

        MultiUserContainerItemSnapshot? incomingSnapshot =
            CreateMultiUserContainerItemSnapshot(itemToAdd);
        if (!MultiUserContainerTransferCore.MatchesExpectedStackState(
                expectedTargetStack,
                target.m_stack) ||
            incomingSnapshot == null ||
            !CanAcceptEntireMultiUserContainerStack(
                incomingSnapshot,
                itemToAdd.m_shared.m_maxStackSize,
                target,
                amount))
        {
            return false;
        }

        target.m_stack += amount;
        target.m_equipped = false;
        MergeStackMetadata(target, itemToAdd);
        NotifyMultiUserContainerInventoryChanged(inventory);
        return true;
    }

    internal static bool TryApplyMultiUserContainerRemove(
        Inventory inventory,
        ItemData expected,
        int amount,
        Vector2i sourcePosition,
        out ItemData removed)
    {
        removed = null!;
        if (!IsValidMultiUserContainerCoordinate(inventory, sourcePosition) ||
            !IsValidMultiUserContainerMutationItem(expected) ||
            expected.m_gridPos != sourcePosition ||
            amount <= 0 ||
            amount > expected.m_stack)
        {
            return false;
        }

        ItemData? current = inventory.GetItemAt(sourcePosition.x, sourcePosition.y);
        if (!IsValidMultiUserContainerMutationItem(current) ||
            current!.m_gridPos != sourcePosition ||
            !MultiUserContainerTransferCore.MatchesExpectedStackState(
                expected.m_stack,
                current.m_stack) ||
            !IsExactMultiUserContainerItemMatch(expected, current, amount))
        {
            return false;
        }

        ItemData removedClone;
        try
        {
            removedClone = current.Clone();
        }
        catch
        {
            return false;
        }

        removedClone.m_stack = amount;
        removedClone.m_gridPos = sourcePosition;
        removedClone.m_equipped = false;

        if (amount == current.m_stack)
        {
            if (!inventory.m_inventory.Remove(current))
            {
                return false;
            }
        }
        else
        {
            current.m_stack -= amount;
        }

        removed = removedClone;
        NotifyMultiUserContainerInventoryChanged(inventory);
        return true;
    }

    internal static bool TryApplyMultiUserContainerMove(
        Inventory inventory,
        ItemData expected,
        int amount,
        Vector2i sourcePosition,
        Vector2i targetPosition,
        int expectedTargetStack)
    {
        if (!IsValidMultiUserContainerCoordinate(inventory, sourcePosition) ||
            !IsValidMultiUserContainerCoordinate(inventory, targetPosition) ||
            !IsValidMultiUserContainerMutationItem(expected) ||
            expected.m_gridPos != sourcePosition ||
            amount <= 0 ||
            amount > expected.m_stack ||
            expectedTargetStack < 0)
        {
            return false;
        }

        ItemData? source = inventory.GetItemAt(sourcePosition.x, sourcePosition.y);
        if (!IsValidMultiUserContainerMutationItem(source) ||
            source!.m_gridPos != sourcePosition ||
            !MultiUserContainerTransferCore.MatchesExpectedStackState(
                expected.m_stack,
                source.m_stack) ||
            !IsExactMultiUserContainerItemMatch(expected, source, amount))
        {
            return false;
        }

        if (sourcePosition == targetPosition)
        {
            return MultiUserContainerTransferCore.MatchesExpectedStackState(
                expectedTargetStack,
                source.m_stack);
        }

        ItemData? target = inventory.GetItemAt(targetPosition.x, targetPosition.y);
        if (target == null)
        {
            if (!MultiUserContainerTransferCore.MatchesExpectedStackState(
                    expectedTargetStack,
                    actualStack: null))
            {
                return false;
            }

            if (amount == source.m_stack)
            {
                source.m_gridPos = targetPosition;
                source.m_equipped = false;
                NotifyMultiUserContainerInventoryChanged(inventory);
                return true;
            }

            ItemData split;
            try
            {
                split = source.Clone();
            }
            catch
            {
                return false;
            }

            split.m_stack = amount;
            split.m_gridPos = targetPosition;
            split.m_equipped = false;

            source.m_stack -= amount;
            inventory.m_inventory.Add(split);
            NotifyMultiUserContainerInventoryChanged(inventory);
            return true;
        }

        MultiUserContainerItemSnapshot? sourceSnapshot =
            CreateMultiUserContainerItemSnapshot(source);
        if (!MultiUserContainerTransferCore.MatchesExpectedStackState(
                expectedTargetStack,
                target.m_stack) ||
            sourceSnapshot == null ||
            !CanAcceptEntireMultiUserContainerStack(
                sourceSnapshot,
                source.m_shared.m_maxStackSize,
                target,
                amount))
        {
            return false;
        }

        target.m_stack += amount;
        target.m_equipped = false;
        MergeStackMetadata(target, source);
        source.m_stack -= amount;
        if (source.m_stack == 0)
        {
            inventory.m_inventory.Remove(source);
        }

        NotifyMultiUserContainerInventoryChanged(inventory);
        return true;
    }

    internal static bool TryApplyMultiUserContainerExchange(
        Inventory inventory,
        ItemData expectedContainerItem,
        ItemData incoming,
        Vector2i containerPosition,
        out ItemData displaced)
    {
        displaced = null!;
        if (!IsValidMultiUserContainerCoordinate(inventory, containerPosition) ||
            !IsValidMultiUserContainerMutationItem(expectedContainerItem) ||
            !IsValidMultiUserContainerMutationItem(incoming) ||
            expectedContainerItem.m_gridPos != containerPosition ||
            expectedContainerItem.m_equipped ||
            incoming.m_equipped ||
            expectedContainerItem.m_shared.m_questItem ||
            incoming.m_shared.m_questItem)
        {
            return false;
        }

        ItemData? current = inventory.GetItemAt(
            containerPosition.x,
            containerPosition.y);
        if (!IsValidMultiUserContainerMutationItem(current) ||
            current!.m_gridPos != containerPosition ||
            current.m_equipped ||
            !MultiUserContainerTransferCore.MatchesExpectedStackState(
                expectedContainerItem.m_stack,
                current.m_stack) ||
            !IsExactMultiUserContainerItemMatch(
                expectedContainerItem,
                current,
                expectedContainerItem.m_stack))
        {
            return false;
        }

        int currentIndex = inventory.m_inventory.IndexOf(current);
        if (currentIndex < 0)
        {
            return false;
        }

        ItemData displacedClone;
        ItemData replacement;
        try
        {
            displacedClone = current.Clone();
            replacement = incoming.Clone();
        }
        catch
        {
            return false;
        }

        displacedClone.m_gridPos = containerPosition;
        displacedClone.m_equipped = false;
        replacement.m_gridPos = containerPosition;
        replacement.m_equipped = false;
        if (!IsValidMultiUserContainerMutationItem(displacedClone) ||
            !IsValidMultiUserContainerMutationItem(replacement))
        {
            return false;
        }

        inventory.m_inventory[currentIndex] = replacement;
        displaced = displacedClone;
        NotifyMultiUserContainerInventoryChanged(inventory);
        return true;
    }

    internal static bool TryApplyMultiUserContainerSwap(
        Inventory inventory,
        ItemData expectedSource,
        ItemData expectedTarget,
        Vector2i sourcePosition,
        Vector2i targetPosition)
    {
        if (!IsValidMultiUserContainerCoordinate(inventory, sourcePosition) ||
            !IsValidMultiUserContainerCoordinate(inventory, targetPosition) ||
            sourcePosition == targetPosition ||
            !IsValidMultiUserContainerMutationItem(expectedSource) ||
            !IsValidMultiUserContainerMutationItem(expectedTarget) ||
            expectedSource.m_gridPos != sourcePosition ||
            expectedTarget.m_gridPos != targetPosition ||
            expectedSource.m_equipped ||
            expectedTarget.m_equipped)
        {
            return false;
        }

        ItemData? source = inventory.GetItemAt(
            sourcePosition.x,
            sourcePosition.y);
        ItemData? target = inventory.GetItemAt(
            targetPosition.x,
            targetPosition.y);
        if (!IsValidMultiUserContainerMutationItem(source) ||
            !IsValidMultiUserContainerMutationItem(target) ||
            source!.m_gridPos != sourcePosition ||
            target!.m_gridPos != targetPosition ||
            source.m_equipped ||
            target.m_equipped ||
            !MultiUserContainerTransferCore.MatchesExpectedStackState(
                expectedSource.m_stack,
                source.m_stack) ||
            !MultiUserContainerTransferCore.MatchesExpectedStackState(
                expectedTarget.m_stack,
                target.m_stack) ||
            !IsExactMultiUserContainerItemMatch(
                expectedSource,
                source,
                expectedSource.m_stack) ||
            !IsExactMultiUserContainerItemMatch(
                expectedTarget,
                target,
                expectedTarget.m_stack))
        {
            return false;
        }

        source.m_gridPos = targetPosition;
        target.m_gridPos = sourcePosition;
        NotifyMultiUserContainerInventoryChanged(inventory);
        return true;
    }

    private static bool IsValidMultiUserContainerInventory(Inventory? inventory) =>
        inventory != null &&
        inventory.m_inventory != null &&
        inventory.GetWidth() > 0 &&
        inventory.GetHeight() > 0;

    private static bool IsValidMultiUserContainerCoordinate(
        Inventory? inventory,
        Vector2i position) =>
        IsValidMultiUserContainerInventory(inventory) &&
        position.x >= 0 &&
        position.y >= 0 &&
        position.x < inventory!.GetWidth() &&
        position.y < inventory.GetHeight();

    private static bool IsValidMultiUserContainerMutationItem(ItemData? item)
    {
        if (item == null ||
            item.m_shared == null ||
            item.m_customData == null ||
            item.m_crafterName == null ||
            item.m_dropPrefab == null ||
            string.IsNullOrWhiteSpace(item.m_dropPrefab.name) ||
            item.m_stack <= 0 ||
            item.m_shared.m_maxStackSize <= 0 ||
            item.m_stack > item.m_shared.m_maxStackSize ||
            item.m_quality <= 0 ||
            item.m_variant < 0 ||
            item.m_worldLevel < 0 ||
            float.IsNaN(item.m_durability) ||
            float.IsInfinity(item.m_durability) ||
            item.m_durability < 0f)
        {
            return false;
        }

        return CreateMultiUserContainerItemSnapshot(item) != null;
    }

    private static bool IsExactMultiUserContainerItemMatch(
        ItemData expected,
        ItemData actual,
        int requiredStack)
    {
        MultiUserContainerItemSnapshot? expectedSnapshot =
            CreateMultiUserContainerItemSnapshot(expected);
        MultiUserContainerItemSnapshot? actualSnapshot =
            CreateMultiUserContainerItemSnapshot(actual);
        return MultiUserContainerTransferCore.IsExactMatch(
            expectedSnapshot,
            actualSnapshot,
            requiredStack);
    }

    private static bool CanStackMultiUserContainerItems(
        ItemData incoming,
        ItemData target,
        int requiredStack)
    {
        MultiUserContainerItemSnapshot? incomingSnapshot =
            CreateMultiUserContainerItemSnapshot(incoming);
        MultiUserContainerItemSnapshot? targetSnapshot =
            CreateMultiUserContainerItemSnapshot(target);
        return MultiUserContainerTransferCore.CanStackTogether(
            incomingSnapshot,
            targetSnapshot,
            requiredStack);
    }

    private static bool CanAcceptEntireMultiUserContainerStack(
        MultiUserContainerItemSnapshot incomingSnapshot,
        int incomingMaxStack,
        ItemData? target,
        int amount)
    {
        if (!IsValidMultiUserContainerMutationItem(target) ||
            target!.m_equipped ||
            incomingMaxStack <= 1 ||
            target.m_shared.m_maxStackSize != incomingMaxStack ||
            target.m_stack > incomingMaxStack - amount)
        {
            return false;
        }

        MultiUserContainerItemSnapshot? targetSnapshot =
            CreateMultiUserContainerItemSnapshot(target);
        return MultiUserContainerTransferCore.CanStackTogether(
            incomingSnapshot,
            targetSnapshot,
            requiredStack: 1);
    }

    private static void NotifyMultiUserContainerInventoryChanged(Inventory inventory)
    {
        try
        {
            inventory.Changed();
        }
        catch (Exception exception)
        {
            // The list mutation has already completed. Let the RPC layer cache and
            // return that committed result instead of allowing a retry to apply it twice.
            Log.LogWarning(
                $"Built-in multi-user chest inventory change callback failed: {exception.Message}");
        }
    }
}
