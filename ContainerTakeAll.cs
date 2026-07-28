using System;
using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool TryHandleSafeTakeAll(InventoryGui gui)
    {
        Player player = Player.m_localPlayer;
        if (player == null || player.m_isLoading || gui == null || gui.m_currentContainer == null || gui.m_currentContainer.m_inventory == null)
        {
            return false;
        }

        if (IsMultiUserContainerAreaBatchActive())
        {
            ShowMultiUserContainerNotReady();
            return true;
        }

        Container container = gui.m_currentContainer;
        ContainerAccessMode accessMode = GetContainerAccessMode(container, allowLocalWithoutZNetView: true);
        if (accessMode == ContainerAccessMode.MultiUserChestRemote)
        {
            if (IsBuiltInMultiUserChestEnabled)
            {
                if (!TryStartMultiUserContainerTakeAllBatch(container))
                {
                    ShowMultiUserContainerNotReady();
                }

                return true;
            }

            return false;
        }

        if (accessMode != ContainerAccessMode.DirectOwner)
        {
            player.Message(MessageHud.MessageType.Center, LocalizeUi("$inventoryslots_container_not_ready", "Container is not ready."), 0, null);
            return true;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        Inventory containerInventory = container.m_inventory;
        if (playerInventory == null || containerInventory == null)
        {
            return false;
        }

        TombStone? tombstone = container.GetComponent<TombStone>();
        bool isTombstone = tombstone != null;
        const bool includeHotbar = false;

        gui.SetupDragItem(null, null, 0);
        int movedStacks = SafeTakeAllItems(player, playerInventory, containerInventory, includeHotbar);
        if (movedStacks > 0)
        {
            playerInventory.Changed();
            containerInventory.Changed();
            ClearCraftingRequirementAvailabilityCache();
        }

        if (isTombstone && containerInventory.NrOfItems() == 0)
        {
            tombstone!.OnTakeAllSuccess();
        }

        return true;
    }

    private static int SafeTakeAllItems(Player player, Inventory playerInventory, Inventory containerInventory, bool includeHotbar)
    {
        List<Vector2i> actionSlots = GetPlayerActionSlots(player, playerInventory, includeHotbar, blockFavorites: true);
        HashSet<Vector2i> allowedSlots = new(actionSlots);
        List<Vector2i> emptySlots = GetSafeTakeAllEmptySlots(playerInventory, actionSlots);
        List<ItemData> sourceItems = containerInventory.m_inventory
            .Where(item => item?.m_shared != null)
            .ToList();
        sourceItems.Sort((left, right) => CompareGridOrder(left.m_gridPos, right.m_gridPos));

        int movedStacks = 0;
        foreach (ItemData source in sourceItems)
        {
            if (!containerInventory.m_inventory.Contains(source))
            {
                continue;
            }

            int before = source.m_stack;
            int movedAmount = MoveContainerItemSafelyToPlayer(playerInventory, containerInventory, source, emptySlots, allowedSlots);
            if (movedAmount > 0 && (!containerInventory.m_inventory.Contains(source) || source.m_stack < before))
            {
                movedStacks++;
            }
        }

        return movedStacks;
    }

    private static int MoveContainerItemSafelyToPlayer(Inventory playerInventory, Inventory containerInventory, ItemData source, List<Vector2i> emptySlots, HashSet<Vector2i> allowedSlots)
    {
        int movedAmount = 0;
        if (source.m_shared != null &&
            source.m_shared.m_maxStackSize > 1 &&
            CanUseContainerActionStacking(source))
        {
            List<ItemData> stackTargets = GetSafeTakeAllStackTargets(playerInventory, source, allowedSlots);
            foreach (ItemData target in stackTargets)
            {
                if (!containerInventory.m_inventory.Contains(source) || source.m_stack <= 0)
                {
                    break;
                }

                int amount = Math.Min(target.m_shared.m_maxStackSize - target.m_stack, source.m_stack);
                if (amount <= 0)
                {
                    continue;
                }

                int before = source.m_stack;
                bool movedOk = playerInventory.MoveItemToThis(containerInventory, source, amount, target.m_gridPos.x, target.m_gridPos.y);
                int moved = CountMovedFromContainerSource(containerInventory, source, before, amount, movedOk);
                movedAmount += moved;
            }
        }

        while (containerInventory.m_inventory.Contains(source) && source.m_stack > 0 && emptySlots.Count > 0)
        {
            Vector2i slot = emptySlots[0];
            emptySlots.RemoveAt(0);
            int before = source.m_stack;
            int requestedAmount = source.m_stack;
            bool movedOk = playerInventory.MoveItemToThis(containerInventory, source, requestedAmount, slot.x, slot.y);
            int moved = CountMovedFromContainerSource(containerInventory, source, before, requestedAmount, movedOk);
            movedAmount += moved;
            if (moved == 0)
            {
                break;
            }
        }

        return movedAmount;
    }

    private static List<ItemData> GetSafeTakeAllStackTargets(Inventory playerInventory, ItemData source, HashSet<Vector2i> allowedSlots)
    {
        return playerInventory.m_inventory
            .Where(target => target?.m_shared != null &&
                             allowedSlots.Contains(target.m_gridPos) &&
                             target.m_shared.m_maxStackSize > 1 &&
                             CanUseContainerActionStacking(target) &&
                             CanShareInventoryStack(target, source) &&
                             target.m_stack < target.m_shared.m_maxStackSize)
            .OrderBy(target => target.m_gridPos.y)
            .ThenBy(target => target.m_gridPos.x)
            .ToList();
    }

    private static List<Vector2i> GetSafeTakeAllEmptySlots(Inventory playerInventory, List<Vector2i> actionSlots)
    {
        return actionSlots
            .Where(slot => playerInventory.GetItemAt(slot.x, slot.y) == null)
            .ToList();
    }

}
