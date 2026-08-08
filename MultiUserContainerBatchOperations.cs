using System;
using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum MultiUserContainerBatchKind
    {
        TakeAll,
        PlaceStacks,
        AreaPlaceStacks,
        AreaFavoriteRestock
    }

    private sealed class MultiUserContainerBatchItem
    {
        public Vector2i SourcePosition;
        public ItemData Identity = null!;
        public int RemainingAmount;
        public int ExpectedStack;
        public bool MovedAny;
    }

    private sealed class MultiUserContainerBatchTarget
    {
        public Container Container = null!;
        public ZDOID ContainerId;
        public bool HasContainerId;
        public bool MovedAny;
    }

    private sealed class MultiUserContainerBatchState
    {
        public MultiUserContainerBatchKind Kind;
        public ZDOID ContainerId;
        public readonly List<MultiUserContainerBatchTarget> Containers = new();
        public readonly List<MultiUserContainerBatchItem> Items = new();
        public int ContainerIndex;
        public int ItemIndex;
        public int PendingAmount;
        public int MovedStacks;
        public int MovedAmount;
        public int ChangedContainerVfxCount;
        public float AreaRangeSquared;
        public bool WaitingForTransfer;
    }

    private static MultiUserContainerBatchState? _multiUserContainerBatch;

    internal static bool IsMultiUserContainerBatchInteractionBlocked(
        InventoryGui? gui)
    {
        MultiUserContainerBatchState? batch = _multiUserContainerBatch;
        if (batch == null ||
            gui == null ||
            IsUnityNull(gui))
        {
            return false;
        }

        if (gui.m_currentContainer == null)
        {
            return false;
        }

        return IsSameMultiUserContainerBatchContainer(
            batch,
            gui.m_currentContainer);
    }

    internal static bool TryStartMultiUserContainerTakeAllBatch(
        Container container)
    {
        if (!TryGetMultiUserContainerBatchContext(
                container,
                out _,
                out _,
                out Inventory containerInventory,
                out ZDOID containerId))
        {
            return false;
        }

        List<MultiUserContainerBatchItem> items = new();
        foreach (ItemData item in containerInventory.m_inventory
                     .Where(item => item?.m_shared != null)
                     .OrderBy(item => item.m_gridPos.y)
                     .ThenBy(item => item.m_gridPos.x))
        {
            if (!TryCreateMultiUserContainerBatchItem(
                    item,
                    out MultiUserContainerBatchItem? batchItem))
            {
                return false;
            }

            items.Add(batchItem!);
        }

        if (items.Count == 0)
        {
            return true;
        }

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        _multiUserContainerBatch = new MultiUserContainerBatchState
        {
            Kind = MultiUserContainerBatchKind.TakeAll,
            ContainerId = containerId
        };
        _multiUserContainerBatch.Items.AddRange(items);
        UpdateMultiUserContainerBatchRuntime();
        return true;
    }

    internal static bool TryHandleMultiUserContainerAreaQuickStack(
        Container anchorContainer) =>
        TryHandleMultiUserContainerAreaBatch(
            anchorContainer,
            MultiUserContainerBatchKind.AreaPlaceStacks);

    internal static bool TryHandleMultiUserContainerAreaRestock(
        Container anchorContainer) =>
        TryHandleMultiUserContainerAreaBatch(
            anchorContainer,
            MultiUserContainerBatchKind.AreaFavoriteRestock);

    private static bool TryHandleMultiUserContainerAreaBatch(
        Container anchorContainer,
        MultiUserContainerBatchKind kind)
    {
        if (!IsBuiltInMultiUserChestEnabled ||
            kind is not (MultiUserContainerBatchKind.AreaPlaceStacks or
                MultiUserContainerBatchKind.AreaFavoriteRestock))
        {
            return false;
        }

        if (_multiUserContainerBatch != null ||
            _pendingMultiUserContainerTransfer != null)
        {
            ShowMultiUserContainerNotReady();
            return true;
        }

        Player? player = Player.m_localPlayer;
        Inventory? playerInventory =
            player != null && !IsUnityNull(player)
                ? ((Humanoid)player).GetInventory()
                : null;
        if (player == null ||
            IsUnityNull(player) ||
            player.m_isLoading ||
            playerInventory == null ||
            anchorContainer == null ||
            IsUnityNull(anchorContainer) ||
            anchorContainer.m_inventory == null)
        {
            return false;
        }

        ContainerAccessMode anchorAccess = GetContainerAccessMode(
            anchorContainer,
            allowLocalWithoutZNetView:
                anchorContainer.m_nview == null);
        bool anchorDirect =
            anchorAccess == ContainerAccessMode.DirectOwner;
        bool anchorRemote =
            anchorAccess == ContainerAccessMode.MultiUserChestRemote &&
            CanUseBuiltInRemoteAreaContainer(player, anchorContainer);
        if (!anchorDirect && !anchorRemote)
        {
            return false;
        }

        if (!HasContainerPlayerAccess(
                player,
                anchorContainer,
                flashGuardStone: true))
        {
            ShowMultiUserContainerNotReady();
            return true;
        }

        bool areaForQuickStack =
            kind == MultiUserContainerBatchKind.AreaPlaceStacks;
        List<Container> containers = GetActionContainers(
            player,
            anchorContainer,
            areaForQuickStack,
            includeBuiltInRemote: true);
        bool hasRemote = containers.Any(container =>
            GetContainerAccessMode(container) ==
                ContainerAccessMode.MultiUserChestRemote &&
            CanUseBuiltInRemoteAreaContainer(player, container));
        if (!hasRemote)
        {
            return false;
        }

        List<ItemData> candidates = playerInventory.m_inventory
            .Where(item => areaForQuickStack
                ? ShouldQuickStackItem(
                    player,
                    playerInventory,
                    item,
                    includeHotbar: false)
                : ShouldRestockItem(player, playerInventory, item))
            .ToList();
        candidates.Sort((left, right) =>
            -CompareGridOrder(left.m_gridPos, right.m_gridPos));

        float configuredAreaRange = Math.Max(
            0f,
            areaForQuickStack
                ? _areaQuickStackRange?.Value ?? 0f
                : _areaRestockRange?.Value ?? 0f);
        MultiUserContainerBatchState batch = new()
        {
            Kind = kind,
            AreaRangeSquared =
                configuredAreaRange * configuredAreaRange
        };
        foreach (Container container in containers)
        {
            ZDO? zdo = container.m_nview?.GetZDO();
            batch.Containers.Add(new MultiUserContainerBatchTarget
            {
                Container = container,
                ContainerId = zdo?.m_uid ?? ZDOID.None,
                HasContainerId = zdo != null
            });
        }

        foreach (ItemData candidate in candidates)
        {
            if (!TryCreateMultiUserContainerBatchItem(
                    candidate,
                    out MultiUserContainerBatchItem? batchItem))
            {
                ShowMultiUserContainerNotReady();
                return true;
            }

            batchItem!.ExpectedStack = candidate.m_stack;
            if (!areaForQuickStack)
            {
                batchItem.RemainingAmount = Math.Max(
                    0,
                    GetRestockTargetStack(candidate) -
                    candidate.m_stack);
            }

            batch.Items.Add(batchItem);
        }

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        _multiUserContainerBatch = batch;
        UpdateMultiUserContainerBatchRuntime();
        return true;
    }

    internal static bool TryStartMultiUserContainerPlaceStacksBatch(
        Container container)
    {
        if (!TryGetMultiUserContainerBatchContext(
                container,
                out Player player,
                out Inventory playerInventory,
                out _,
                out ZDOID containerId))
        {
            return false;
        }

        List<ItemData> candidates = playerInventory.m_inventory
            .Where(item => ShouldQuickStackItem(
                player,
                playerInventory,
                item,
                includeHotbar: false))
            .ToList();
        candidates.Sort((left, right) =>
            -CompareGridOrder(left.m_gridPos, right.m_gridPos));

        List<MultiUserContainerBatchItem> items = new();
        foreach (ItemData candidate in candidates)
        {
            if (!TryCreateMultiUserContainerBatchItem(
                    candidate,
                    out MultiUserContainerBatchItem? batchItem))
            {
                return false;
            }

            items.Add(batchItem!);
        }

        if (items.Count == 0)
        {
            ShowContainerActionResult(
                player,
                "$inventoryslots_action_stack",
                "Stack",
                moved: 0);
            return true;
        }

        InventoryGui.instance?.SetupDragItem(null, null, 0);
        _multiUserContainerBatch = new MultiUserContainerBatchState
        {
            Kind = MultiUserContainerBatchKind.PlaceStacks,
            ContainerId = containerId
        };
        _multiUserContainerBatch.Items.AddRange(items);
        UpdateMultiUserContainerBatchRuntime();
        return true;
    }

    internal static void UpdateMultiUserContainerBatchRuntime()
    {
        MultiUserContainerBatchState? batch = _multiUserContainerBatch;
        if (batch == null ||
            batch.WaitingForTransfer ||
            _pendingMultiUserContainerTransfer != null)
        {
            return;
        }

        if (IsAreaMultiUserContainerBatch(batch))
        {
            UpdateMultiUserContainerAreaBatchRuntime(batch);
            return;
        }

        if (!TryGetCurrentMultiUserContainerBatchContext(
                batch,
                out Container container,
                out Player player,
                out Inventory playerInventory,
                out Inventory containerInventory))
        {
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        while (batch.ItemIndex < batch.Items.Count)
        {
            MultiUserContainerBatchItem batchItem =
                batch.Items[batch.ItemIndex];
            Inventory sourceInventory =
                batch.Kind == MultiUserContainerBatchKind.TakeAll
                    ? containerInventory
                    : playerInventory;
            ItemData? current = sourceInventory.GetItemAt(
                batchItem.SourcePosition.x,
                batchItem.SourcePosition.y);
            if (current?.m_shared == null ||
                current.m_stack != batchItem.RemainingAmount ||
                !IsExactMultiUserContainerItemMatch(
                    batchItem.Identity,
                    current,
                    requiredStack: 1))
            {
                FinishMultiUserContainerBatch(showResult: true);
                return;
            }

            bool hasStep;
            Vector2i target;
            int amount;
            if (batch.Kind == MultiUserContainerBatchKind.TakeAll)
            {
                hasStep = TryPlanNextMultiUserContainerTakeAllStep(
                    player,
                    playerInventory,
                    current,
                    batchItem.RemainingAmount,
                    out target,
                    out amount);
            }
            else
            {
                if (!ShouldQuickStackItem(
                        player,
                        playerInventory,
                        current,
                        includeHotbar: false) ||
                    !DoesMultiUserContainerAcceptPlaceStacksItem(
                        containerInventory,
                        current))
                {
                    batch.ItemIndex++;
                    continue;
                }

                hasStep = TryPlanNextMultiUserContainerPlaceStacksStep(
                    containerInventory,
                    current,
                    batchItem.RemainingAmount,
                    out target,
                    out amount);
            }

            if (!hasStep)
            {
                batch.ItemIndex++;
                continue;
            }

            batch.PendingAmount = amount;
            batch.WaitingForTransfer = true;
            bool started =
                batch.Kind == MultiUserContainerBatchKind.TakeAll
                    ? TryStartMultiUserContainerRemove(
                        container,
                        playerInventory,
                        current,
                        amount,
                        target,
                        MultiUserContainerRecoveryMode.BatchInventoryFirst)
                    : TryStartMultiUserContainerAdd(
                        container,
                        playerInventory,
                        current,
                        amount,
                        target,
                        MultiUserContainerRecoveryMode.BatchInventoryFirst);
            if (!started)
            {
                batch.WaitingForTransfer = false;
                batch.PendingAmount = 0;
                FinishMultiUserContainerBatch(showResult: true);
            }

            return;
        }

        FinishMultiUserContainerBatch(showResult: true);
    }

    private static void UpdateMultiUserContainerAreaBatchRuntime(
        MultiUserContainerBatchState batch)
    {
        if (!TryGetMultiUserContainerAreaBatchContext(
                batch,
                out Player player,
                out Inventory playerInventory))
        {
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        if (batch.Containers.Count == 0)
        {
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        Container? anchor =
            TryResolveMultiUserContainerBatchTarget(batch.Containers[0]);
        float maximumInteractionDistance =
            MultiUserContainerMaximumInteractionDistance;
        if (anchor == null ||
            anchor.m_inventory == null ||
            !CanHandleContainerAreaAction(player, anchor) ||
            !HasContainerPlayerAccess(
                player,
                anchor,
                flashGuardStone: false) ||
            (player.transform.position - anchor.transform.position)
                .sqrMagnitude >
            maximumInteractionDistance * maximumInteractionDistance)
        {
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        while (batch.ContainerIndex < batch.Containers.Count)
        {
            MultiUserContainerBatchTarget target =
                batch.Containers[batch.ContainerIndex];
            Container? container =
                TryResolveMultiUserContainerBatchTarget(target);
            if (container == null ||
                container.m_inventory == null ||
                batch.ContainerIndex > 0 &&
                (container.transform.position -
                 anchor.transform.position).sqrMagnitude >
                batch.AreaRangeSquared ||
                !HasContainerPlayerAccess(
                    player,
                    container,
                    flashGuardStone: false))
            {
                AdvanceMultiUserContainerAreaBatchContainer(batch);
                continue;
            }

            ContainerAccessMode accessMode = GetContainerAccessMode(
                container,
                allowLocalWithoutZNetView: !target.HasContainerId);
            if (accessMode == ContainerAccessMode.DirectOwner)
            {
                if (batch.ContainerIndex > 0 &&
                    !IsBuiltInMultiUserContainerEligible(container) &&
                    IsContainerInUse(container))
                {
                    AdvanceMultiUserContainerAreaBatchContainer(batch);
                    continue;
                }

                if (!TryProcessDirectMultiUserContainerAreaBatchTarget(
                        batch,
                        target,
                        player,
                        playerInventory,
                        container))
                {
                    FinishMultiUserContainerBatch(showResult: true);
                    return;
                }

                AdvanceMultiUserContainerAreaBatchContainer(batch);
                continue;
            }

            if (accessMode != ContainerAccessMode.MultiUserChestRemote ||
                !CanUseBuiltInRemoteAreaContainer(player, container))
            {
                AdvanceMultiUserContainerAreaBatchContainer(batch);
                continue;
            }

            if (!TryStartNextRemoteMultiUserContainerAreaBatchStep(
                    batch,
                    player,
                    playerInventory,
                    container))
            {
                FinishMultiUserContainerBatch(showResult: true);
                return;
            }

            if (batch.WaitingForTransfer)
            {
                return;
            }

            AdvanceMultiUserContainerAreaBatchContainer(batch);
        }

        FinishMultiUserContainerBatch(showResult: true);
    }

    internal static void OnMultiUserContainerTransferCompleted(
        bool committedAndObserved)
    {
        MultiUserContainerBatchState? batch = _multiUserContainerBatch;
        if (batch == null || !batch.WaitingForTransfer)
        {
            return;
        }

        batch.WaitingForTransfer = false;
        if (IsAreaMultiUserContainerBatch(batch))
        {
            CompleteMultiUserContainerAreaBatchStep(
                batch,
                committedAndObserved);
            return;
        }

        if (!committedAndObserved ||
            batch.PendingAmount <= 0 ||
            batch.ItemIndex < 0 ||
            batch.ItemIndex >= batch.Items.Count)
        {
            batch.PendingAmount = 0;
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        MultiUserContainerBatchItem item = batch.Items[batch.ItemIndex];
        if (batch.PendingAmount > item.RemainingAmount)
        {
            batch.PendingAmount = 0;
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        item.RemainingAmount -= batch.PendingAmount;
        batch.PendingAmount = 0;
        if (!item.MovedAny)
        {
            item.MovedAny = true;
            batch.MovedStacks++;
        }

        if (item.RemainingAmount == 0)
        {
            batch.ItemIndex++;
        }
    }

    private static void CompleteMultiUserContainerAreaBatchStep(
        MultiUserContainerBatchState batch,
        bool committedAndObserved)
    {
        if (!committedAndObserved ||
            batch.PendingAmount <= 0 ||
            batch.ContainerIndex < 0 ||
            batch.ContainerIndex >= batch.Containers.Count ||
            batch.ItemIndex < 0 ||
            batch.ItemIndex >= batch.Items.Count)
        {
            batch.PendingAmount = 0;
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        MultiUserContainerBatchItem item = batch.Items[batch.ItemIndex];
        int amount = batch.PendingAmount;
        if (amount > item.RemainingAmount)
        {
            batch.PendingAmount = 0;
            FinishMultiUserContainerBatch(showResult: true);
            return;
        }

        item.RemainingAmount -= amount;
        if (batch.Kind == MultiUserContainerBatchKind.AreaPlaceStacks)
        {
            item.ExpectedStack -= amount;
            if (item.RemainingAmount == 0 && !item.MovedAny)
            {
                item.MovedAny = true;
                batch.MovedStacks++;
            }
        }
        else
        {
            item.ExpectedStack += amount;
            batch.MovedAmount += amount;
        }

        batch.PendingAmount = 0;
        MarkMultiUserContainerAreaBatchTargetMoved(batch);
        if (item.RemainingAmount == 0)
        {
            batch.ItemIndex++;
        }
    }

    internal static void CancelMultiUserContainerBatch(
        bool includeAreaBatch = true)
    {
        if (!includeAreaBatch &&
            IsAreaMultiUserContainerBatch(_multiUserContainerBatch))
        {
            return;
        }

        _multiUserContainerBatch = null;
    }

    private static bool IsAreaMultiUserContainerBatch(
        MultiUserContainerBatchState? batch) =>
        batch != null &&
        batch.Kind is MultiUserContainerBatchKind.AreaPlaceStacks or
            MultiUserContainerBatchKind.AreaFavoriteRestock;

    private static bool IsMultiUserContainerAreaBatchActive() =>
        IsAreaMultiUserContainerBatch(_multiUserContainerBatch);

    private static bool TryGetMultiUserContainerAreaBatchContext(
        MultiUserContainerBatchState batch,
        out Player player,
        out Inventory playerInventory)
    {
        player = null!;
        playerInventory = null!;
        Player? localPlayer = Player.m_localPlayer;
        Inventory? localInventory =
            localPlayer != null && !IsUnityNull(localPlayer)
                ? ((Humanoid)localPlayer).GetInventory()
                : null;
        if (!IsAreaMultiUserContainerBatch(batch) ||
            !IsBuiltInMultiUserChestEnabled ||
            localPlayer == null ||
            IsUnityNull(localPlayer) ||
            localPlayer.m_isLoading ||
            localInventory == null)
        {
            return false;
        }

        player = localPlayer;
        playerInventory = localInventory;
        return true;
    }

    private static Container? TryResolveMultiUserContainerBatchTarget(
        MultiUserContainerBatchTarget target)
    {
        if (target == null)
        {
            return null;
        }

        Container? current = target.Container;
        if (current != null && !IsUnityNull(current))
        {
            if (!target.HasContainerId)
            {
                return current;
            }

            ZDO? currentZdo =
                current.m_nview != null &&
                current.m_nview.IsValid()
                    ? current.m_nview.GetZDO()
                    : null;
            if (currentZdo != null &&
                currentZdo.m_uid.Equals(target.ContainerId))
            {
                return current;
            }
        }

        if (!target.HasContainerId)
        {
            return null;
        }

        for (int index = InventoryContainers.KnownContainers.Count - 1;
             index >= 0;
             index--)
        {
            Container candidate =
                InventoryContainers.KnownContainers[index];
            if (candidate == null || IsUnityNull(candidate))
            {
                continue;
            }

            ZDO? candidateZdo =
                candidate.m_nview != null &&
                candidate.m_nview.IsValid()
                    ? candidate.m_nview.GetZDO()
                    : null;
            if (candidateZdo == null ||
                !candidateZdo.m_uid.Equals(target.ContainerId))
            {
                continue;
            }

            target.Container = candidate;
            return candidate;
        }

        return null;
    }

    private static void AdvanceMultiUserContainerAreaBatchContainer(
        MultiUserContainerBatchState batch)
    {
        batch.ContainerIndex++;
        batch.ItemIndex = 0;
        batch.PendingAmount = 0;
    }

    private static bool TryProcessDirectMultiUserContainerAreaBatchTarget(
        MultiUserContainerBatchState batch,
        MultiUserContainerBatchTarget target,
        Player player,
        Inventory playerInventory,
        Container container)
    {
        Inventory containerInventory = container.GetInventory();
        if (containerInventory == null)
        {
            return false;
        }

        while (batch.ItemIndex < batch.Items.Count)
        {
            MultiUserContainerBatchItem batchItem =
                batch.Items[batch.ItemIndex];
            if (batchItem.RemainingAmount <= 0)
            {
                batch.ItemIndex++;
                continue;
            }

            ItemData? current = playerInventory.GetItemAt(
                batchItem.SourcePosition.x,
                batchItem.SourcePosition.y);
            if (current?.m_shared == null ||
                current.m_stack != batchItem.ExpectedStack ||
                !IsExactMultiUserContainerItemMatch(
                    batchItem.Identity,
                    current,
                    requiredStack: 1))
            {
                return false;
            }

            int before = current.m_stack;
            int moved;
            if (batch.Kind ==
                MultiUserContainerBatchKind.AreaPlaceStacks)
            {
                if (!ShouldQuickStackItem(
                        player,
                        playerInventory,
                        current,
                        includeHotbar: false))
                {
                    batchItem.RemainingAmount = 0;
                    batch.ItemIndex++;
                    continue;
                }

                _ = QuickStackItemsIntoContainer(
                    playerInventory,
                    containerInventory,
                    new List<ItemData> { current });
                int after = playerInventory.m_inventory.Contains(current)
                    ? current.m_stack
                    : 0;
                moved = before - after;
                if (moved < 0 || moved > batchItem.RemainingAmount)
                {
                    return false;
                }

                if (moved > 0)
                {
                    batchItem.ExpectedStack = after;
                    batchItem.RemainingAmount -= moved;
                    if (batchItem.RemainingAmount == 0 &&
                        !batchItem.MovedAny)
                    {
                        batchItem.MovedAny = true;
                        batch.MovedStacks++;
                    }
                }
            }
            else
            {
                if (!ShouldRestockItem(
                        player,
                        playerInventory,
                        current))
                {
                    batchItem.RemainingAmount = 0;
                    batch.ItemIndex++;
                    continue;
                }

                int expectedTargetStack =
                    batchItem.ExpectedStack +
                    batchItem.RemainingAmount;
                if (GetRestockTargetStack(current) !=
                    expectedTargetStack)
                {
                    return false;
                }

                _ = RestockTargetsFromContainer(
                    playerInventory,
                    containerInventory,
                    new List<ItemData> { current },
                    ContainerTakeStacksMode.AreaFavoriteRestock);
                if (!playerInventory.m_inventory.Contains(current))
                {
                    return false;
                }

                int after = current.m_stack;
                moved = after - before;
                if (moved < 0 || moved > batchItem.RemainingAmount)
                {
                    return false;
                }

                if (moved > 0)
                {
                    batchItem.ExpectedStack = after;
                    batchItem.RemainingAmount -= moved;
                    batch.MovedAmount += moved;
                }
            }

            if (moved > 0)
            {
                MarkMultiUserContainerAreaBatchTargetMoved(
                    batch,
                    target);
                playerInventory.Changed();
            }

            batch.ItemIndex++;
        }

        return true;
    }

    private static bool TryStartNextRemoteMultiUserContainerAreaBatchStep(
        MultiUserContainerBatchState batch,
        Player player,
        Inventory playerInventory,
        Container container)
    {
        Inventory containerInventory = container.GetInventory();
        if (containerInventory == null)
        {
            return false;
        }

        while (batch.ItemIndex < batch.Items.Count)
        {
            MultiUserContainerBatchItem batchItem =
                batch.Items[batch.ItemIndex];
            if (batchItem.RemainingAmount <= 0)
            {
                batch.ItemIndex++;
                continue;
            }

            ItemData? current = playerInventory.GetItemAt(
                batchItem.SourcePosition.x,
                batchItem.SourcePosition.y);
            if (current?.m_shared == null ||
                current.m_stack != batchItem.ExpectedStack ||
                !IsExactMultiUserContainerItemMatch(
                    batchItem.Identity,
                    current,
                    requiredStack: 1))
            {
                return false;
            }

            Vector2i targetPosition;
            int amount;
            ItemData transferItem;
            if (batch.Kind ==
                MultiUserContainerBatchKind.AreaPlaceStacks)
            {
                if (batchItem.RemainingAmount !=
                        batchItem.ExpectedStack ||
                    !ShouldQuickStackItem(
                        player,
                        playerInventory,
                        current,
                        includeHotbar: false))
                {
                    batchItem.RemainingAmount = 0;
                    batch.ItemIndex++;
                    continue;
                }

                if (!DoesMultiUserContainerAcceptPlaceStacksItem(
                        containerInventory,
                        current) ||
                    !TryPlanNextMultiUserContainerPlaceStacksStep(
                        containerInventory,
                        current,
                        batchItem.RemainingAmount,
                        out targetPosition,
                        out amount))
                {
                    batch.ItemIndex++;
                    continue;
                }

                transferItem = current;
            }
            else
            {
                if (!ShouldRestockItem(
                        player,
                        playerInventory,
                        current))
                {
                    batchItem.RemainingAmount = 0;
                    batch.ItemIndex++;
                    continue;
                }

                int expectedTargetStack =
                    batchItem.ExpectedStack +
                    batchItem.RemainingAmount;
                if (GetRestockTargetStack(current) !=
                    expectedTargetStack)
                {
                    return false;
                }

                transferItem = null!;
                amount = 0;
                targetPosition = current.m_gridPos;
                int free = Math.Max(
                    0,
                    current.m_shared.m_maxStackSize -
                    current.m_stack);
                for (int sourceIndex =
                         containerInventory.m_inventory.Count - 1;
                     sourceIndex >= 0;
                     sourceIndex--)
                {
                    ItemData? source =
                        containerInventory.m_inventory[sourceIndex];
                    int candidateAmount = Math.Min(
                        Math.Min(
                            batchItem.RemainingAmount,
                            source?.m_stack ?? 0),
                        free);
                    if (candidateAmount <= 0 ||
                        source?.m_shared == null ||
                        !CanRestockFromContainerItem(
                            current,
                            source) ||
                        !CanStackEntireMultiUserContainerItem(
                            source,
                            current,
                            candidateAmount))
                    {
                        continue;
                    }

                    transferItem = source;
                    amount = candidateAmount;
                    break;
                }

                if (transferItem?.m_shared == null || amount <= 0)
                {
                    batch.ItemIndex++;
                    continue;
                }
            }

            batch.PendingAmount = amount;
            batch.WaitingForTransfer = true;
            bool started =
                batch.Kind ==
                MultiUserContainerBatchKind.AreaPlaceStacks
                    ? TryStartMultiUserContainerAdd(
                        container,
                        playerInventory,
                        transferItem,
                        amount,
                        targetPosition,
                        MultiUserContainerRecoveryMode.BatchInventoryFirst)
                    : TryStartMultiUserContainerRemove(
                        container,
                        playerInventory,
                        transferItem,
                        amount,
                        targetPosition,
                        MultiUserContainerRecoveryMode.InventoryFirst);
            if (!started)
            {
                batch.WaitingForTransfer = false;
                batch.PendingAmount = 0;
                return false;
            }

            return true;
        }

        return true;
    }

    private static void MarkMultiUserContainerAreaBatchTargetMoved(
        MultiUserContainerBatchState batch)
    {
        if (batch.ContainerIndex < 0 ||
            batch.ContainerIndex >= batch.Containers.Count)
        {
            return;
        }

        MarkMultiUserContainerAreaBatchTargetMoved(
            batch,
            batch.Containers[batch.ContainerIndex]);
    }

    private static void MarkMultiUserContainerAreaBatchTargetMoved(
        MultiUserContainerBatchState batch,
        MultiUserContainerBatchTarget target)
    {
        if (target.MovedAny)
        {
            return;
        }

        target.MovedAny = true;
        int vfxLimit = IsContainerActionSuccessFxEnabled()
            ? ContainerActionSuccessVfxLimit
            : 0;
        Container? container =
            TryResolveMultiUserContainerBatchTarget(target);
        if (container != null)
        {
            batch.ChangedContainerVfxCount =
                TryBroadcastChangedContainerActionSuccessVfx(
                    container,
                    vfxLimit,
                    batch.ChangedContainerVfxCount);
        }
    }

    private static bool TryGetMultiUserContainerBatchContext(
        Container container,
        out Player player,
        out Inventory playerInventory,
        out Inventory containerInventory,
        out ZDOID containerId)
    {
        player = null!;
        playerInventory = null!;
        containerInventory = null!;
        containerId = default;
        if (_multiUserContainerBatch != null ||
            _pendingMultiUserContainerTransfer != null ||
            !IsBuiltInMultiUserChestEnabled ||
            container == null ||
            IsUnityNull(container) ||
            GetContainerAccessMode(
                container,
                allowLocalWithoutZNetView: true) !=
            ContainerAccessMode.MultiUserChestRemote ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            InventoryGui.instance == null ||
            IsUnityNull(InventoryGui.instance) ||
            InventoryGui.instance.m_currentContainer != container)
        {
            return false;
        }

        Player? localPlayer = Player.m_localPlayer;
        ZDO? zdo = container.m_nview.GetZDO();
        Inventory? localInventory = localPlayer != null &&
                                    !IsUnityNull(localPlayer)
            ? ((Humanoid)localPlayer).GetInventory()
            : null;
        Inventory? remoteInventory = container.GetInventory();
        if (localPlayer == null ||
            IsUnityNull(localPlayer) ||
            localPlayer.m_isLoading ||
            zdo == null ||
            localInventory == null ||
            remoteInventory == null)
        {
            return false;
        }

        player = localPlayer;
        playerInventory = localInventory;
        containerInventory = remoteInventory;
        containerId = zdo.m_uid;
        return true;
    }

    private static bool TryGetCurrentMultiUserContainerBatchContext(
        MultiUserContainerBatchState batch,
        out Container container,
        out Player player,
        out Inventory playerInventory,
        out Inventory containerInventory)
    {
        container = null!;
        player = null!;
        playerInventory = null!;
        containerInventory = null!;
        InventoryGui? gui = InventoryGui.instance;
        Player? localPlayer = Player.m_localPlayer;
        if (batch == null ||
            gui == null ||
            IsUnityNull(gui) ||
            localPlayer == null ||
            IsUnityNull(localPlayer) ||
            localPlayer.m_isLoading ||
            gui.m_currentContainer == null ||
            !IsSameMultiUserContainerBatchContainer(
                batch,
                gui.m_currentContainer))
        {
            return false;
        }

        Container currentContainer = gui.m_currentContainer;
        if (GetContainerAccessMode(
                currentContainer,
                allowLocalWithoutZNetView: true) !=
            ContainerAccessMode.MultiUserChestRemote)
        {
            return false;
        }

        Inventory? localInventory =
            ((Humanoid)localPlayer).GetInventory();
        Inventory? remoteInventory = currentContainer.GetInventory();
        if (localInventory == null || remoteInventory == null)
        {
            return false;
        }

        container = currentContainer;
        player = localPlayer;
        playerInventory = localInventory;
        containerInventory = remoteInventory;
        return true;
    }

    private static bool IsSameMultiUserContainerBatchContainer(
        MultiUserContainerBatchState batch,
        Container container)
    {
        if (batch == null ||
            container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return false;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        return zdo != null && zdo.m_uid.Equals(batch.ContainerId);
    }

    private static bool TryCreateMultiUserContainerBatchItem(
        ItemData item,
        out MultiUserContainerBatchItem? batchItem)
    {
        batchItem = null;
        if (item?.m_shared == null || item.m_stack <= 0)
        {
            return false;
        }

        ItemData identity;
        try
        {
            identity = item.Clone();
        }
        catch
        {
            return false;
        }

        batchItem = new MultiUserContainerBatchItem
        {
            SourcePosition = item.m_gridPos,
            Identity = identity,
            RemainingAmount = item.m_stack,
            ExpectedStack = item.m_stack
        };
        return true;
    }

    private static bool TryPlanNextMultiUserContainerTakeAllStep(
        Player player,
        Inventory playerInventory,
        ItemData source,
        int remainingAmount,
        out Vector2i target,
        out int amount)
    {
        target = new Vector2i(-1, -1);
        amount = 0;
        List<Vector2i> actionSlots = GetPlayerActionSlots(
            player,
            playerInventory,
            includeHotbar: false,
            blockFavorites: true);
        HashSet<Vector2i> allowedSlots = new(actionSlots);
        if (source.m_shared.m_maxStackSize > 1 &&
            CanUseContainerActionStacking(source))
        {
            foreach (ItemData stackTarget in
                     GetSafeTakeAllStackTargets(
                         playerInventory,
                         source,
                         allowedSlots))
            {
                int free = Math.Max(
                    0,
                    stackTarget.m_shared.m_maxStackSize -
                    stackTarget.m_stack);
                int candidateAmount = Math.Min(
                    Math.Min(source.m_stack, remainingAmount),
                    free);
                if (candidateAmount <= 0 ||
                    !CanAddWithinInventoryLimits(
                        playerInventory,
                        source,
                        candidateAmount,
                        out _))
                {
                    continue;
                }

                target = stackTarget.m_gridPos;
                amount = candidateAmount;
                return true;
            }
        }

        foreach (Vector2i empty in GetSafeTakeAllEmptySlots(
                     playerInventory,
                     actionSlots))
        {
            int candidateAmount = Math.Min(
                Math.Min(source.m_stack, remainingAmount),
                Math.Max(1, source.m_shared.m_maxStackSize));
            if (candidateAmount <= 0 ||
                !CanAddWithinInventoryLimits(
                    playerInventory,
                    source,
                    candidateAmount,
                    out _))
            {
                continue;
            }

            target = empty;
            amount = candidateAmount;
            return true;
        }

        return false;
    }

    private static bool DoesMultiUserContainerAcceptPlaceStacksItem(
        Inventory containerInventory,
        ItemData source)
    {
        string sourceName = source.m_shared?.m_name ?? "";
        return !string.IsNullOrEmpty(sourceName) &&
               containerInventory.m_inventory.Any(
                   item => item?.m_shared != null &&
                           string.Equals(
                               item.m_shared.m_name,
                               sourceName,
                               StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryPlanNextMultiUserContainerPlaceStacksStep(
        Inventory containerInventory,
        ItemData source,
        int remainingAmount,
        out Vector2i target,
        out int amount)
    {
        target = new Vector2i(-1, -1);
        amount = 0;
        foreach (ItemData stackTarget in containerInventory.m_inventory
                     .Where(item => item?.m_shared != null)
                     .OrderBy(item => item.m_gridPos.y)
                     .ThenBy(item => item.m_gridPos.x))
        {
            int free = Math.Max(
                0,
                stackTarget.m_shared.m_maxStackSize -
                stackTarget.m_stack);
            int candidateAmount = Math.Min(
                Math.Min(source.m_stack, remainingAmount),
                free);
            if (candidateAmount <= 0 ||
                !CanStackEntireMultiUserContainerItem(
                    source,
                    stackTarget,
                    candidateAmount))
            {
                continue;
            }

            target = stackTarget.m_gridPos;
            amount = candidateAmount;
            return true;
        }

        int width = containerInventory.GetWidth();
        int height = containerInventory.GetHeight();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (containerInventory.GetItemAt(x, y) != null)
                {
                    continue;
                }

                target = new Vector2i(x, y);
                amount = Math.Min(
                    Math.Min(source.m_stack, remainingAmount),
                    Math.Max(1, source.m_shared.m_maxStackSize));
                return amount > 0;
            }
        }

        return false;
    }

    private static void FinishMultiUserContainerBatch(bool showResult)
    {
        MultiUserContainerBatchState? batch = _multiUserContainerBatch;
        _multiUserContainerBatch = null;
        if (batch == null)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (IsAreaMultiUserContainerBatch(batch))
        {
            int moved = batch.Kind ==
                MultiUserContainerBatchKind.AreaPlaceStacks
                    ? batch.MovedStacks
                    : batch.MovedAmount;
            if (moved > 0)
            {
                Inventory? playerInventory =
                    player != null && !IsUnityNull(player)
                        ? ((Humanoid)player).GetInventory()
                        : null;
                playerInventory?.Changed();
                ClearCraftingRequirementAvailabilityCache();

                if (IsContainerActionSuccessFxEnabled() &&
                    batch.Containers.Count > 0)
                {
                    Container? anchor =
                        TryResolveMultiUserContainerBatchTarget(
                            batch.Containers[0]);
                    if (anchor != null)
                    {
                        BroadcastContainerActionSuccessFx(
                            anchor,
                            ContainerActionSuccessSfxKind);
                    }
                }
            }

            if (showResult &&
                player != null &&
                !IsUnityNull(player))
            {
                bool quickStack = batch.Kind ==
                    MultiUserContainerBatchKind.AreaPlaceStacks;
                ShowContainerActionResult(
                    player,
                    quickStack
                        ? "$inventoryslots_action_stack"
                        : "$inventoryslots_action_take_stacks",
                    quickStack ? "Stack" : "Take stacks",
                    moved);
            }

            return;
        }

        if (showResult &&
            batch.Kind == MultiUserContainerBatchKind.PlaceStacks &&
            player != null &&
            !IsUnityNull(player))
        {
            ShowContainerActionResult(
                player,
                "$inventoryslots_action_stack",
                "Stack",
                batch.MovedStacks);
        }
    }
}
