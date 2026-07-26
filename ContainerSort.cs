using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string RpcRequestSort = "InventorySlots_RequestSortV2";
    private const string RpcSortResponse = "InventorySlots_SortResponseV2";
    private const float ContainerSortRequestTimeout = 2f;
    private static Container? _pendingSortContainer;
    private static int _pendingSortRequestId;
    private static int _nextSortRequestId = 1;
    private static long _pendingSortOwner;
    private static float _pendingSortStartedAt = -1f;

    private static void SortCurrentContainer(Player? player)
    {
        if (player == null || player.m_isLoading || InventoryGui.instance == null)
        {
            return;
        }

        Container container = InventoryGui.instance.m_currentContainer;
        if (container == null || container.m_inventory == null)
        {
            return;
        }

        ContainerAccessMode accessMode = GetContainerAccessMode(container, allowLocalWithoutZNetView: true);
        if (accessMode == ContainerAccessMode.DirectOwner)
        {
            SortContainerInventory(container);
            return;
        }

        if (accessMode != ContainerAccessMode.MultiUserChestRemote ||
            IsContainerSortRequestPending(container))
        {
            return;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        long owner = zdo != null ? zdo.GetOwner() : 0L;
        if (owner == 0L)
        {
            return;
        }

        int requestId = GetNextContainerSortRequestId();
        _pendingSortContainer = container;
        _pendingSortRequestId = requestId;
        _pendingSortOwner = owner;
        _pendingSortStartedAt = Time.unscaledTime;
        container.m_nview.InvokeRPC(RpcRequestSort, requestId, player.GetPlayerID());
    }

    private static int SortContainerInventory(Container container)
    {
        if (container == null || container.m_inventory == null)
        {
            return 0;
        }

        List<Vector2i> allowedSlots = GetAllInventorySlots(container.m_inventory);
        return SortInventoryInternal(container.m_inventory, allowedSlots, item => item?.m_shared != null);
    }

    internal static void RegisterContainer(Container container)
    {
        if (container == null)
        {
            return;
        }

        if (!InventoryContainers.KnownContainers.Contains(container))
        {
            InventoryContainers.KnownContainers.Add(container);
        }

        RegisterContainerRpcs(container);
        RegisterMultiUserContainerRpcs(container);
    }

    internal static void UnregisterContainer(Container container)
    {
        if (container != null)
        {
            UnregisterMultiUserContainerRpcs(container);
            ClearPendingContainerSortRequest(container);
            InventoryContainers.KnownContainers.Remove(container);
        }
    }

    private static void RegisterContainerRpcs(Container container)
    {
        if (container == null)
        {
            return;
        }

        if (container.m_nview == null)
        {
            container.m_nview = container.m_rootObjectOverride != null ? container.m_rootObjectOverride.GetComponent<ZNetView>() : container.GetComponent<ZNetView>();
        }

        if (container.m_nview == null)
        {
            return;
        }

        container.m_nview.Unregister(RpcRequestSort);
        container.m_nview.Unregister(RpcSortResponse);
        container.m_nview.Register<int, long>(RpcRequestSort, (sender, requestId, requesterPlayerId) =>
            RPC_RequestSort(container, sender, requestId, requesterPlayerId));
        container.m_nview.Register<int, bool>(RpcSortResponse, (sender, requestId, success) =>
            RPC_SortResponse(container, sender, requestId, success));
    }

    private static void RPC_RequestSort(Container container, long sender, int requestId, long requesterPlayerId)
    {
        bool success = CanProcessContainerSortRpc(container, sender, requesterPlayerId);
        if (success)
        {
            SortContainerInventory(container);
        }

        if (sender != 0L && container?.m_nview != null && container.m_nview.IsValid())
        {
            container.m_nview.InvokeRPC(sender, RpcSortResponse, requestId, success);
        }
    }

    private static void RPC_SortResponse(Container container, long sender, int requestId, bool success)
    {
        if (container == null ||
            _pendingSortContainer != container ||
            _pendingSortRequestId != requestId ||
            _pendingSortOwner != sender)
        {
            return;
        }

        ClearPendingContainerSortRequest();
        if (!success && !IsUnityNull(Player.m_localPlayer))
        {
            Player.m_localPlayer.Message(
                MessageHud.MessageType.Center,
                LocalizeUi("$inventoryslots_container_not_ready", "Container is not ready."),
                0,
                null);
        }
    }

    private static bool IsContainerSortRequestPending(Container container)
    {
        if (_pendingSortContainer == null)
        {
            ClearPendingContainerSortRequest();
            return false;
        }

        if (Time.unscaledTime - _pendingSortStartedAt >= ContainerSortRequestTimeout)
        {
            ClearPendingContainerSortRequest();
            return false;
        }

        return _pendingSortContainer == container;
    }

    private static int GetNextContainerSortRequestId()
    {
        if (_nextSortRequestId <= 0)
        {
            _nextSortRequestId = 1;
        }

        return _nextSortRequestId++;
    }

    private static void ClearPendingContainerSortRequest(Container? container = null)
    {
        if (container != null && _pendingSortContainer != container)
        {
            return;
        }

        _pendingSortContainer = null;
        _pendingSortRequestId = 0;
        _pendingSortOwner = 0L;
        _pendingSortStartedAt = -1f;
    }

    private static void SortPlayerInventory(Player? player)
    {
        if (player == null || player.m_isLoading)
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return;
        }

        List<Vector2i> allowedSlots = GetPlayerActionSlots(player, inventory, includeHotbar: false, blockFavorites: true);
        HashSet<Vector2i> allowedSet = new(allowedSlots);
        InventoryGui.instance.SetupDragItem(null, null, 0);
        SortInventoryInternal(inventory, allowedSlots, item => item?.m_shared != null && allowedSet.Contains(item.m_gridPos) && !IsFavoriteProtected(player, inventory, item));
    }

    private static int SortInventoryInternal(Inventory inventory, List<Vector2i> allowedSlots, Func<ItemData, bool> shouldSort)
    {
        if (inventory == null || allowedSlots.Count == 0)
        {
            return 0;
        }

        List<ItemData> toSort = inventory.m_inventory.Where(shouldSort).ToList();
        if (toSort.Count == 0)
        {
            return 0;
        }

        bool changed = MergeSortableStacks(toSort, inventory);

        Dictionary<ItemData, SortKey> sortKeys = toSort.ToDictionary(item => item, GetInventoryItemSortKey);
        int inventoryWidth = Mathf.Max(1, inventory.GetWidth());
        Dictionary<ItemData, int> originalIndices = toSort.ToDictionary(item => item, item => GetInventoryOriginalSortIndex(item, inventoryWidth));
        toSort.Sort((a, b) => CompareItemsForSort(a, b, sortKeys[a], sortKeys[b], originalIndices[a], originalIndices[b]));

        int moved = 0;
        int count = Math.Min(toSort.Count, allowedSlots.Count);
        for (int i = 0; i < count; i++)
        {
            if (toSort[i].m_gridPos != allowedSlots[i])
            {
                moved++;
                changed = true;
                toSort[i].m_gridPos = allowedSlots[i];
            }
        }

        if (changed)
        {
            inventory.Changed();
        }

        return moved;
    }

    private static int GetInventoryOriginalSortIndex(ItemData item, int inventoryWidth) =>
        item.m_gridPos.y * inventoryWidth + item.m_gridPos.x;

    private static bool MergeSortableStacks(List<ItemData> toMerge, Inventory inventory)
    {
        bool changed = false;
        List<List<ItemData>> grouped = toMerge
            .Where(item => item?.m_shared != null && item.m_stack < item.m_shared.m_maxStackSize && CanUseContainerActionStacking(item))
            .GroupBy(item => new { item.m_shared.m_name, item.m_quality, item.m_worldLevel })
            .Select(grouping => grouping.ToList())
            .ToList();

        foreach (List<ItemData> group in grouped)
        {
            if (group.Count <= 1)
            {
                continue;
            }

            int total = group.Sum(item => item.m_stack);
            int maxStack = group[0].m_shared.m_maxStackSize;
            foreach (ItemData item in group)
            {
                if (total <= 0)
                {
                    if (item.m_stack != 0)
                    {
                        item.m_stack = 0;
                        changed = true;
                    }

                    inventory.RemoveItem(item);
                    toMerge.Remove(item);
                    changed = true;
                    continue;
                }

                int nextStack = Math.Min(maxStack, total);
                if (item.m_stack != nextStack)
                {
                    item.m_stack = nextStack;
                    changed = true;
                }

                total -= item.m_stack;
            }
        }

        return changed;
    }

    private static List<Vector2i> GetAllInventorySlots(Inventory inventory)
    {
        List<Vector2i> slots = new();
        for (int y = 0; y < inventory.GetHeight(); y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                slots.Add(new Vector2i(x, y));
            }
        }

        return slots;
    }
}
