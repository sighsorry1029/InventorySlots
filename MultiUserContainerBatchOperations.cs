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
        PlaceStacks
    }

    private sealed class MultiUserContainerBatchItem
    {
        public Vector2i SourcePosition;
        public ItemData Identity = null!;
        public int RemainingAmount;
        public bool MovedAny;
    }

    private sealed class MultiUserContainerBatchState
    {
        public MultiUserContainerBatchKind Kind;
        public ZDOID ContainerId;
        public readonly List<MultiUserContainerBatchItem> Items = new();
        public int ItemIndex;
        public int PendingAmount;
        public int MovedStacks;
        public bool WaitingForTransfer;
    }

    private static MultiUserContainerBatchState? _multiUserContainerBatch;

    internal static bool IsMultiUserContainerBatchInteractionBlocked(
        InventoryGui? gui)
    {
        MultiUserContainerBatchState? batch = _multiUserContainerBatch;
        if (batch == null ||
            gui == null ||
            IsUnityNull(gui) ||
            gui.m_currentContainer == null)
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

    internal static void OnMultiUserContainerTransferCompleted(
        bool committedAndObserved)
    {
        MultiUserContainerBatchState? batch = _multiUserContainerBatch;
        if (batch == null || !batch.WaitingForTransfer)
        {
            return;
        }

        batch.WaitingForTransfer = false;
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

    internal static void CancelMultiUserContainerBatch()
    {
        _multiUserContainerBatch = null;
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
            RemainingAmount = item.m_stack
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
        if (!showResult ||
            batch == null ||
            batch.Kind != MultiUserContainerBatchKind.PlaceStacks)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player != null && !IsUnityNull(player))
        {
            ShowContainerActionResult(
                player,
                "$inventoryslots_action_stack",
                "Stack",
                batch.MovedStacks);
        }
    }
}
