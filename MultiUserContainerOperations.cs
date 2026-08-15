using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float MultiUserContainerRetryInterval = 0.75f;
    private const float MultiUserContainerRequestTimeout = 5f;
    private const float MultiUserContainerReceiptPollInterval = 0.25f;
    private const float MultiUserContainerCommittedProjectionTimeout = 5f;
    private const float MultiUserContainerLocalRecoveryRetryInterval = 1f;
    private const int MultiUserContainerMaximumSendAttempts = 5;

    private enum MultiUserContainerRecoveryMode
    {
        InventoryFirst,
        RegularInventoryFirst,
        BatchInventoryFirst,
        ConsumeAfterInventory,
        WorldFirst
    }

    private enum MultiUserContainerLocalPlacementPolicy
    {
        AnyUsable,
        RegularAndHotbar,
        ContainerBatch
    }

    private enum MultiUserContainerWorldDeliveryResult
    {
        NotAttempted,
        Succeeded,
        DefinitelyNotSpawned,
        Uncertain
    }

    private sealed class PendingMultiUserContainerTransfer
    {
        public Container Container = null!;
        public ZDOID ContainerId;
        public long RequesterPeerId;
        public long Owner;
        public HashSet<long> RequestOwners = new();
        public MultiUserContainerRequest Request = null!;
        public byte[] RequestBytes = null!;
        public byte[] RequestDigest = null!;
        public Inventory? LocalInventory;
        public ItemData? LocalEscrow;
        public Vector2i PreferredLocalPosition;
        public Inventory? Projection;
        public float StartedAt;
        public float LastSentAt;
        public float LastReceiptCheckAt;
        public int SendAttempts;
        public bool TimeoutNotified;
        public bool ResponseApplied;
        public bool AuthoritativeStateObserved;
        public float ResponseAppliedAt;
        public bool AuthorityChangedOrReloaded;
        public bool TerminalFailureReceived;
        public bool LocalRecoveryPending;
        public ItemData? PendingRecoveryItem;
        public bool AcknowledgementPending;
        public bool PermanentlyDestroyed;
        public MultiUserContainerRecoveryMode RecoveryMode;
        public bool CompletionActionAttempted;
        public MultiUserContainerWorldDeliveryResult WorldDeliveryResult;
    }

    private sealed class PendingMultiUserContainerLocalRecovery
    {
        public Inventory? Inventory;
        public ItemData Item = null!;
        public Vector2i PreferredPosition;
        public float NextAttemptAt;
        public MultiUserContainerLocalPlacementPolicy PlacementPolicy;
    }

    private static PendingMultiUserContainerTransfer? _pendingMultiUserContainerTransfer;
    private static readonly List<PendingMultiUserContainerLocalRecovery>
        PendingMultiUserContainerLocalRecoveries = new();
    private static int _nextMultiUserContainerRequestId =
        Math.Max(1, Guid.NewGuid().GetHashCode() & int.MaxValue);

    internal static bool IsMultiUserContainerInteractionPending(
        InventoryGui gui,
        InventoryGrid grid,
        ItemData item,
        Vector2i position)
    {
        if (IsMultiUserContainerBatchInteractionBlocked(gui))
        {
            return true;
        }

        PendingMultiUserContainerTransfer? pending = _pendingMultiUserContainerTransfer;
        if (pending == null || gui == null || grid == null)
        {
            return false;
        }

        Inventory? gridInventory = grid.GetInventory();
        Inventory containerInventory = pending.Container != null && !IsUnityNull(pending.Container)
            ? pending.Container.GetInventory()
            : null!;
        return gridInventory == containerInventory ||
               gridInventory == pending.Projection ||
               gui.m_dragInventory == containerInventory ||
               gui.m_dragInventory == pending.Projection ||
               gridInventory == pending.LocalInventory &&
               (position == pending.PreferredLocalPosition ||
                item != null && pending.LocalEscrow != null &&
                IsExactMultiUserContainerItemMatch(pending.LocalEscrow, item, 1));
    }

    internal static bool TryRouteMultiUserContainerAutoMove(
        Inventory targetInventory,
        Inventory sourceInventory,
        ItemData item)
    {
        if (!TryGetMultiUserContainerTransferDirection(
                targetInventory,
                sourceInventory,
                out Container? remoteContainer,
                out bool addingToContainer,
                out bool removingFromContainer,
                out bool movingInsideContainer))
        {
            return false;
        }

        if (item?.m_shared == null)
        {
            return true;
        }

        if (addingToContainer)
        {
            _ = TryStartMultiUserContainerAdd(
                remoteContainer!,
                sourceInventory,
                item,
                item.m_stack,
                requestedTarget: null);
        }
        else if (removingFromContainer)
        {
            _ = TryStartMultiUserContainerRemove(
                remoteContainer!,
                targetInventory,
                item,
                item.m_stack,
                requestedTarget: null);
        }
        else if (movingInsideContainer)
        {
            ShowMultiUserContainerNotReady();
        }

        return true;
    }

    internal static bool TryRouteMultiUserContainerPositionalMove(
        Inventory targetInventory,
        Inventory sourceInventory,
        ItemData item,
        int amount,
        int x,
        int y,
        out bool result)
    {
        result = false;
        if (!TryGetMultiUserContainerTransferDirection(
                targetInventory,
                sourceInventory,
                out Container? remoteContainer,
                out bool addingToContainer,
                out bool removingFromContainer,
                out bool movingInsideContainer))
        {
            return false;
        }

        Vector2i target = new(x, y);
        if (addingToContainer)
        {
            result = TryStartMultiUserContainerAdd(
                remoteContainer!,
                sourceInventory,
                item,
                amount,
                target);
        }
        else if (removingFromContainer)
        {
            result = TryStartMultiUserContainerRemove(
                remoteContainer!,
                targetInventory,
                item,
                amount,
                target);
        }
        else if (movingInsideContainer)
        {
            result = TryStartMultiUserContainerMove(
                remoteContainer!,
                item,
                amount,
                target);
        }

        return true;
    }

    internal static bool TryRouteMultiUserContainerDropItem(
        InventoryGrid targetGrid,
        Inventory sourceInventory,
        ItemData item,
        int amount,
        Vector2i targetPosition,
        out bool result)
    {
        result = false;
        Inventory? targetInventory = targetGrid?.GetInventory();
        if (targetInventory == null)
        {
            return false;
        }

        if (item?.m_shared != null &&
            amount == item.m_stack &&
            TryGetMultiUserContainerTransferDirection(
                targetInventory,
                sourceInventory,
                out Container? remoteContainer,
                out bool addingToContainer,
                out bool removingFromContainer,
                out bool movingInsideContainer))
        {
            ItemData? occupied = targetInventory.GetItemAt(
                targetPosition.x,
                targetPosition.y);
            bool occupiedByDifferentItem =
                occupied != null &&
                occupied != item &&
                (item.m_shared.m_maxStackSize <= 1 ||
                 occupied.m_shared.m_maxStackSize <= 1 ||
                 !CanStackMultiUserContainerItems(
                     item,
                     occupied,
                     requiredStack: 1));
            if (occupiedByDifferentItem)
            {
                if (addingToContainer)
                {
                    result = TryStartMultiUserContainerExchange(
                        remoteContainer!,
                        sourceInventory,
                        occupied!,
                        item);
                    return true;
                }

                if (removingFromContainer)
                {
                    result = TryStartMultiUserContainerExchange(
                        remoteContainer!,
                        targetInventory,
                        item,
                        occupied!);
                    return true;
                }

                if (movingInsideContainer)
                {
                    result = TryStartMultiUserContainerSwap(
                        remoteContainer!,
                        item,
                        occupied!);
                    return true;
                }
            }
        }

        return TryRouteMultiUserContainerPositionalMove(
            targetInventory,
            sourceInventory,
            item!,
            amount,
            targetPosition.x,
            targetPosition.y,
            out result);
    }

    internal static bool TryHandleMultiUserContainerRightClick(
        InventoryGui gui,
        InventoryGrid grid,
        ItemData item,
        Vector2i position)
    {
        if (!IsBuiltInMultiUserChestEnabled ||
            gui == null ||
            grid == null ||
            item?.m_shared == null ||
            !TryGetBuiltInRemoteContainer(grid.GetInventory(), out Container? container) ||
            gui.m_currentContainer != container)
        {
            return false;
        }

        Player? player = Player.m_localPlayer;
        if (item.m_shared.m_questItem)
        {
            player?.Message(
                MessageHud.MessageType.Center,
                "$msg_cantconsume",
                0,
                null);
            return true;
        }

        if (item.m_shared.m_itemType != ItemData.ItemType.Consumable)
        {
            ShowMultiUserContainerNotReady();
            return true;
        }

        if (player == null ||
            IsUnityNull(player) ||
            !player.CanConsumeItem(item, checkWorldLevel: true))
        {
            return true;
        }

        if (!TryStartMultiUserContainerRemove(
                container!,
                player.GetInventory(),
                item,
                amount: 1,
                requestedTarget: null,
                recoveryMode:
                    MultiUserContainerRecoveryMode.ConsumeAfterInventory))
        {
            ShowMultiUserContainerNotReady();
        }

        return true;
    }

    internal static bool TryHandleMultiUserContainerDropOutside(InventoryGui gui)
    {
        if (!IsBuiltInMultiUserChestEnabled ||
            gui == null ||
            gui.m_dragGo == null ||
            gui.m_dragItem?.m_shared == null ||
            gui.m_dragInventory == null ||
            !TryGetBuiltInRemoteContainer(gui.m_dragInventory, out Container? container) ||
            gui.m_currentContainer != container)
        {
            return false;
        }

        if (_pendingMultiUserContainerTransfer != null ||
            gui.m_dragItem.m_shared.m_questItem)
        {
            if (gui.m_dragItem.m_shared.m_questItem)
            {
                Player.m_localPlayer?.Message(
                    MessageHud.MessageType.Center,
                    "$msg_cantdrop",
                    0,
                    null);
            }

            return true;
        }

        if (TryStartMultiUserContainerWorldDrop(
                container!,
                gui.m_dragItem,
                gui.m_dragAmount))
        {
            gui.SetupDragItem(null, null, 1);
        }
        else
        {
            ShowMultiUserContainerNotReady();
        }

        return true;
    }

    internal static bool TryHandleMultiUserContainerDropSelectedItem(
        InventoryGui gui,
        InventoryGrid grid,
        ItemData item,
        Vector2i position)
    {
        if (!IsBuiltInMultiUserChestEnabled ||
            gui == null ||
            grid == null ||
            gui.m_dragGo != null ||
            gui.m_dragItem != null ||
            item?.m_shared == null ||
            !TryGetBuiltInRemoteContainer(grid.GetInventory(), out Container? container) ||
            gui.m_currentContainer != container)
        {
            return false;
        }

        if (_pendingMultiUserContainerTransfer != null ||
            item.m_shared.m_questItem)
        {
            if (item.m_shared.m_questItem)
            {
                Player.m_localPlayer?.Message(
                    MessageHud.MessageType.Center,
                    "$msg_cantdrop",
                    0,
                    null);
            }

            return true;
        }

        if (!TryStartMultiUserContainerWorldDrop(
                container!,
                item,
                item.m_stack))
        {
            ShowMultiUserContainerNotReady();
        }

        return true;
    }

    private static bool TryStartMultiUserContainerAdd(
        Container container,
        Inventory sourceInventory,
        ItemData item,
        int amount,
        Vector2i? requestedTarget,
        MultiUserContainerRecoveryMode recoveryMode =
            MultiUserContainerRecoveryMode.InventoryFirst)
    {
        if (!CanStartMultiUserContainerTransfer(container, sourceInventory, item, amount) ||
            !IsLocalPlayerInventory(sourceInventory))
        {
            return false;
        }

        Inventory containerInventory = container.GetInventory();
        Vector2i target;
        if (requestedTarget.HasValue)
        {
            target = requestedTarget.Value;
            if (!CanAddEntireMultiUserContainerItemAt(containerInventory, item, amount, target))
            {
                ShowMultiUserContainerUnsupportedDestination();
                return false;
            }
        }
        else if (!TryFindMultiUserContainerDestination(containerInventory, item, amount, out target))
        {
            ShowMultiUserContainerNotReady();
            return false;
        }

        ItemData transferItem;
        ItemData escrow;
        try
        {
            transferItem = item.Clone();
            escrow = item.Clone();
        }
        catch
        {
            return false;
        }

        transferItem.m_stack = amount;
        transferItem.m_equipped = false;
        escrow.m_stack = amount;
        escrow.m_equipped = false;
        int previousTargetStack =
            containerInventory.GetItemAt(target.x, target.y)?.m_stack ?? 0;

        MultiUserContainerRequest request = CreateMultiUserContainerRequest(
            MultiUserContainerOperation.Add,
            transferItem,
            amount,
            item.m_gridPos,
            target,
            previousTargetStack);
        Vector2i sourcePosition = item.m_gridPos;
        int sourceStackBefore = item.m_stack;
        try
        {
            _ = sourceInventory.ContainsItem(item) &&
                sourceInventory.RemoveItem(item, amount);
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest source removal callback failed: {exception.Message}");
        }

        int sourceStackAfter = sourceInventory.ContainsItem(item)
            ? item.m_stack
            : 0;
        int removedAmount = Math.Max(0, sourceStackBefore - sourceStackAfter);
        if (removedAmount != amount)
        {
            if (removedAmount > 0)
            {
                escrow.m_stack = removedAmount;
                RestoreMultiUserContainerLocalEscrow(
                    sourceInventory,
                    escrow,
                    sourcePosition,
                    GetMultiUserContainerLocalPlacementPolicy(
                        recoveryMode));
            }

            return false;
        }

        return TryStartPreparedMultiUserContainerRequest(
            container,
            request,
            sourceInventory,
            escrow,
            sourcePosition,
            recoveryMode);
    }

    private static bool TryStartMultiUserContainerRemove(
        Container container,
        Inventory destinationInventory,
        ItemData item,
        int amount,
        Vector2i? requestedTarget,
        MultiUserContainerRecoveryMode recoveryMode =
            MultiUserContainerRecoveryMode.InventoryFirst)
    {
        if (!CanStartMultiUserContainerTransfer(container, destinationInventory, item, amount) ||
            !IsLocalPlayerInventory(destinationInventory))
        {
            return false;
        }

        bool inventoryLimitResult = false;
        if (!TryValidatePlayerInventoryLimit(
                destinationInventory,
                item,
                amount,
                ref inventoryLimitResult))
        {
            return false;
        }

        Vector2i target;
        if (requestedTarget.HasValue)
        {
            target = requestedTarget.Value;
            if (!CanReceiveEntireMultiUserContainerItemAt(destinationInventory, item, amount, target))
            {
                ShowMultiUserContainerUnsupportedDestination();
                return false;
            }
        }
        else if (!TryFindLocalMultiUserContainerDestination(destinationInventory, item, amount, out target))
        {
            ShowMultiUserContainerNotReady();
            return false;
        }

        return TryStartMultiUserContainerRequest(
            container,
            MultiUserContainerOperation.Remove,
            item,
            amount,
            item.m_gridPos,
            target,
            destinationInventory,
            localEscrow: null,
            preferredLocalPosition: target,
            expectedTargetStack: -1,
            recoveryMode: recoveryMode);
    }

    private static bool TryStartMultiUserContainerWorldDrop(
        Container container,
        ItemData item,
        int amount)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            IsUnityNull(player) ||
            item?.m_shared == null ||
            item.m_shared.m_questItem ||
            !CanStartMultiUserContainerTransfer(
                container,
                player.GetInventory(),
                item,
                amount))
        {
            return false;
        }

        return TryStartMultiUserContainerRequest(
            container,
            MultiUserContainerOperation.Remove,
            item,
            amount,
            item.m_gridPos,
            new Vector2i(-1, -1),
            player.GetInventory(),
            localEscrow: null,
            preferredLocalPosition: new Vector2i(-1, -1),
            expectedTargetStack: -1,
            recoveryMode: MultiUserContainerRecoveryMode.WorldFirst);
    }

    private static bool TryStartMultiUserContainerMove(
        Container container,
        ItemData item,
        int amount,
        Vector2i target)
    {
        if (!CanStartMultiUserContainerTransfer(container, container.GetInventory(), item, amount))
        {
            return false;
        }

        ItemData? targetItem = container.GetInventory().GetItemAt(target.x, target.y);
        if (targetItem != null &&
            targetItem != item &&
            !CanStackEntireMultiUserContainerItem(item, targetItem, amount))
        {
            ShowMultiUserContainerUnsupportedDestination();
            return false;
        }

        return TryStartMultiUserContainerRequest(
            container,
            MultiUserContainerOperation.Move,
            item,
            amount,
            item.m_gridPos,
            target,
            localInventory: null,
            localEscrow: null,
            preferredLocalPosition: new Vector2i(-1, -1),
            expectedTargetStack: targetItem?.m_stack ?? 0);
    }

    private static bool TryStartMultiUserContainerExchange(
        Container container,
        Inventory localInventory,
        ItemData containerItem,
        ItemData localItem)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            IsUnityNull(player) ||
            !IsLocalPlayerInventory(localInventory) ||
            !CanStartMultiUserContainerTransfer(
                container,
                localInventory,
                containerItem,
                containerItem.m_stack) ||
            localItem?.m_shared == null ||
            localItem.m_customData == null ||
            localItem.m_equipped ||
            localItem.m_shared.m_questItem ||
            containerItem.m_equipped ||
            containerItem.m_shared.m_questItem ||
            !localInventory.ContainsItem(localItem) ||
            !InventoryActionCellPolicyCore.CanUseContainerActionSource(
                GetInventoryCellKind(
                    player,
                    localInventory,
                    localItem.m_gridPos),
                includeHotbar: true))
        {
            ShowMultiUserContainerUnsupportedDestination();
            return false;
        }

        Inventory containerInventory = container.GetInventory();
        ItemData? currentContainerItem = containerInventory.GetItemAt(
            containerItem.m_gridPos.x,
            containerItem.m_gridPos.y);
        if (currentContainerItem == null ||
            currentContainerItem != containerItem ||
            !IsExactMultiUserContainerItemMatch(
                containerItem,
                currentContainerItem,
                containerItem.m_stack))
        {
            ShowMultiUserContainerNotReady();
            return false;
        }

        ItemData expectedContainerItem;
        ItemData counterpart;
        ItemData escrow;
        try
        {
            expectedContainerItem = containerItem.Clone();
            counterpart = localItem.Clone();
            escrow = localItem.Clone();
        }
        catch
        {
            return false;
        }

        expectedContainerItem.m_equipped = false;
        counterpart.m_equipped = false;
        escrow.m_equipped = false;
        Vector2i localPosition = localItem.m_gridPos;
        MultiUserContainerRequest request =
            CreateMultiUserContainerRequest(
                MultiUserContainerOperation.Exchange,
                expectedContainerItem,
                expectedContainerItem.m_stack,
                expectedContainerItem.m_gridPos,
                localPosition,
                expectedTargetStack: -1,
                counterpartItem: counterpart);

        int localStackBefore = localItem.m_stack;
        try
        {
            _ = localInventory.ContainsItem(localItem) &&
                localInventory.RemoveItem(localItem, localStackBefore);
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest exchange escrow callback failed: {exception.Message}");
        }

        int localStackAfter = localInventory.ContainsItem(localItem)
            ? localItem.m_stack
            : 0;
        int removedAmount = Math.Max(
            0,
            localStackBefore - localStackAfter);
        if (removedAmount != localStackBefore)
        {
            if (removedAmount > 0)
            {
                escrow.m_stack = removedAmount;
                RestoreMultiUserContainerLocalEscrow(
                    localInventory,
                    escrow,
                    localPosition,
                    MultiUserContainerLocalPlacementPolicy.RegularAndHotbar);
            }

            return false;
        }

        if (!CanAddWithinInventoryLimits(
                localInventory,
                expectedContainerItem,
                expectedContainerItem.m_stack,
                out _))
        {
            RestoreMultiUserContainerLocalEscrow(
                localInventory,
                escrow,
                localPosition,
                MultiUserContainerLocalPlacementPolicy.RegularAndHotbar);
            return false;
        }

        return TryStartPreparedMultiUserContainerRequest(
            container,
            request,
            localInventory,
            escrow,
            localPosition,
            MultiUserContainerRecoveryMode.RegularInventoryFirst);
    }

    private static bool TryStartMultiUserContainerSwap(
        Container container,
        ItemData source,
        ItemData target)
    {
        if (source?.m_shared == null ||
            target?.m_shared == null ||
            source == target ||
            source.m_equipped ||
            target.m_equipped ||
            source.m_shared.m_questItem ||
            target.m_shared.m_questItem ||
            !CanStartMultiUserContainerTransfer(
                container,
                container.GetInventory(),
                source,
                source.m_stack))
        {
            ShowMultiUserContainerUnsupportedDestination();
            return false;
        }

        Inventory inventory = container.GetInventory();
        if (inventory.GetItemAt(source.m_gridPos.x, source.m_gridPos.y) != source ||
            inventory.GetItemAt(target.m_gridPos.x, target.m_gridPos.y) != target)
        {
            ShowMultiUserContainerNotReady();
            return false;
        }

        ItemData expectedSource;
        ItemData expectedTarget;
        try
        {
            expectedSource = source.Clone();
            expectedTarget = target.Clone();
        }
        catch
        {
            return false;
        }

        expectedSource.m_equipped = false;
        expectedTarget.m_equipped = false;
        MultiUserContainerRequest request =
            CreateMultiUserContainerRequest(
                MultiUserContainerOperation.Swap,
                expectedSource,
                expectedSource.m_stack,
                expectedSource.m_gridPos,
                expectedTarget.m_gridPos,
                expectedTargetStack: -1,
                counterpartItem: expectedTarget);
        return TryStartPreparedMultiUserContainerRequest(
            container,
            request,
            localInventory: null,
            localEscrow: null,
            preferredLocalPosition: new Vector2i(-1, -1));
    }

    private static bool TryStartMultiUserContainerRequest(
        Container container,
        MultiUserContainerOperation operation,
        ItemData item,
        int amount,
        Vector2i sourcePosition,
        Vector2i targetPosition,
        Inventory? localInventory,
        ItemData? localEscrow,
        Vector2i preferredLocalPosition,
        int expectedTargetStack,
        MultiUserContainerRecoveryMode recoveryMode =
            MultiUserContainerRecoveryMode.InventoryFirst)
    {
        if (!CanStartMultiUserContainerTransfer(container, localInventory, item, amount))
        {
            return false;
        }

        ItemData expected;
        try
        {
            expected = item.Clone();
        }
        catch
        {
            return false;
        }

        expected.m_equipped = false;
        MultiUserContainerRequest request = CreateMultiUserContainerRequest(
            operation,
            expected,
            amount,
            sourcePosition,
            targetPosition,
            expectedTargetStack);
        return TryStartPreparedMultiUserContainerRequest(
            container,
            request,
            localInventory,
            localEscrow,
            preferredLocalPosition,
            recoveryMode);
    }

    private static bool TryStartPreparedMultiUserContainerRequest(
        Container container,
        MultiUserContainerRequest request,
        Inventory? localInventory,
        ItemData? localEscrow,
        Vector2i preferredLocalPosition,
        MultiUserContainerRecoveryMode recoveryMode =
            MultiUserContainerRecoveryMode.InventoryFirst)
    {
        bool pendingPublished = false;
        try
        {
            if (_pendingMultiUserContainerTransfer != null ||
                !TryGetMultiUserContainerOwner(container, out long owner) ||
                !TryWriteMultiUserContainerRequest(request, out ZPackage? package))
            {
                return false;
            }

            byte[] requestBytes = package!.GetArray();
            if (!TryComputeMultiUserContainerDigest(
                    requestBytes,
                    out byte[]? requestDigest) ||
                requestDigest == null)
            {
                return false;
            }

            float now = Time.unscaledTime;
            PendingMultiUserContainerTransfer pending = new()
            {
                Container = container,
                ContainerId = container.m_nview.GetZDO().m_uid,
                RequesterPeerId = ZNet.GetUID(),
                Owner = owner,
                RequestOwners = new HashSet<long> { owner },
                Request = request,
                RequestBytes = requestBytes,
                RequestDigest = requestDigest,
                LocalInventory = localInventory,
                LocalEscrow = localEscrow,
                PreferredLocalPosition = preferredLocalPosition,
                RecoveryMode = recoveryMode,
                StartedAt = now,
                LastSentAt = now,
                SendAttempts = 1
            };
            _pendingMultiUserContainerTransfer = pending;
            pendingPublished = true;

            try
            {
                container.m_nview.InvokeRPC(
                    MultiUserContainerRequestRpc,
                    package);
            }
            catch (Exception exception)
            {
                // The request may already have reached the owner. Keep the
                // published pending state and escrow so the normal receipt poll
                // and bounded resend path can resolve it without duplication.
                Log.LogWarning(
                    $"Built-in multi-user chest initial request send failed; retrying: {exception.Message}");
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest request preparation failed: {exception.Message}");
            return false;
        }
        finally
        {
            if (!pendingPublished &&
                localEscrow != null &&
                localInventory != null)
            {
                try
                {
                    RestoreMultiUserContainerLocalEscrow(
                        localInventory,
                        localEscrow,
                        preferredLocalPosition,
                        GetMultiUserContainerLocalPlacementPolicy(
                            recoveryMode));
                }
                catch (Exception exception)
                {
                    Log.LogWarning(
                        $"Built-in multi-user chest request rollback failed: {exception.Message}");
                }
            }
        }
    }

    private static MultiUserContainerRequest CreateMultiUserContainerRequest(
        MultiUserContainerOperation operation,
        ItemData item,
        int amount,
        Vector2i sourcePosition,
        Vector2i targetPosition,
        int expectedTargetStack,
        ItemData? counterpartItem = null)
    {
        Player player = Player.m_localPlayer;
        return new MultiUserContainerRequest
        {
            RequestId = GetNextMultiUserContainerRequestId(),
            Operation = operation,
            RequesterPlayerId = player != null && !IsUnityNull(player) ? player.GetPlayerID() : 0L,
            SourcePosition = sourcePosition,
            TargetPosition = targetPosition,
            ExpectedTargetStack = expectedTargetStack,
            Amount = amount,
            Item = item,
            CounterpartItem = counterpartItem
        };
    }

    private static int GetNextMultiUserContainerRequestId()
    {
        if (_nextMultiUserContainerRequestId <= 0)
        {
            _nextMultiUserContainerRequestId = 1;
        }

        return _nextMultiUserContainerRequestId++;
    }

    private static bool CanStartMultiUserContainerTransfer(
        Container container,
        Inventory? relatedInventory,
        ItemData item,
        int amount)
    {
        return IsBuiltInMultiUserChestEnabled &&
               _pendingMultiUserContainerTransfer == null &&
               container != null &&
               !IsUnityNull(container) &&
               IsBuiltInMultiUserContainerEligible(container) &&
               item?.m_shared != null &&
               item.m_customData != null &&
               amount > 0 &&
               amount <= item.m_stack &&
               amount <= Math.Max(1, item.m_shared.m_maxStackSize) &&
               Player.m_localPlayer != null &&
               !IsUnityNull(Player.m_localPlayer) &&
               !Player.m_localPlayer.m_isLoading;
    }

    private static bool TryGetMultiUserContainerTransferDirection(
        Inventory targetInventory,
        Inventory sourceInventory,
        out Container? remoteContainer,
        out bool addingToContainer,
        out bool removingFromContainer,
        out bool movingInsideContainer)
    {
        remoteContainer = null;
        addingToContainer = false;
        removingFromContainer = false;
        movingInsideContainer = false;
        if (!IsBuiltInMultiUserChestEnabled ||
            targetInventory == null ||
            sourceInventory == null)
        {
            return false;
        }

        bool targetIsRemote = TryGetBuiltInRemoteContainer(targetInventory, out Container? targetContainer);
        bool sourceIsRemote = TryGetBuiltInRemoteContainer(sourceInventory, out Container? sourceContainer);
        if (!targetIsRemote && !sourceIsRemote)
        {
            return false;
        }

        if (targetIsRemote && sourceIsRemote)
        {
            if (targetContainer != sourceContainer)
            {
                remoteContainer = targetContainer;
                return true;
            }

            remoteContainer = targetContainer;
            movingInsideContainer = true;
            return true;
        }

        if (targetIsRemote)
        {
            remoteContainer = targetContainer;
            addingToContainer = true;
            return true;
        }

        remoteContainer = sourceContainer;
        removingFromContainer = true;
        return true;
    }

    private static bool TryGetBuiltInRemoteContainer(Inventory? inventory, out Container? container)
    {
        container = null;
        if (!IsBuiltInMultiUserChestEnabled || inventory == null)
        {
            return false;
        }

        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (pending != null &&
            pending.Projection == inventory &&
            pending.Container != null &&
            !IsUnityNull(pending.Container))
        {
            container = pending.Container;
            return true;
        }

        InventoryGui? gui = InventoryGui.instance;
        Container? current = gui != null ? gui.m_currentContainer : null;
        if (current != null &&
            !IsUnityNull(current) &&
            current.GetInventory() == inventory &&
            IsBuiltInMultiUserContainerEligible(current) &&
            current.m_nview != null &&
            current.m_nview.IsValid() &&
            !current.m_nview.IsOwner())
        {
            container = current;
            return true;
        }

        for (int index = InventoryContainers.KnownContainers.Count - 1; index >= 0; index--)
        {
            Container known = InventoryContainers.KnownContainers[index];
            if (known == null || IsUnityNull(known))
            {
                InventoryContainers.KnownContainers.RemoveAt(index);
                continue;
            }

            if (known.GetInventory() != inventory ||
                !IsBuiltInMultiUserContainerEligible(known) ||
                known.m_nview == null ||
                !known.m_nview.IsValid() ||
                known.m_nview.IsOwner())
            {
                continue;
            }

            container = known;
            return true;
        }

        return false;
    }

    private static bool IsBuiltInMultiUserContainerEligible(Container container)
    {
        if (container == null ||
            IsUnityNull(container) ||
            container.m_inventory == null ||
            container.GetType() != typeof(Container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            IsMultiUserChestIgnored(container) ||
            container.m_piece == null ||
            !container.m_piece.IsPlacedByPlayer() ||
            container.m_wagon != null ||
            container.GetComponent<TombStone>() != null ||
            container.GetComponentInParent<TombStone>() != null ||
            container.m_nview.GetComponent<Player>() != null ||
            container.transform.root.GetComponentInChildren<Ship>() != null)
        {
            return false;
        }

        foreach (Component component in container.GetComponents<Component>())
        {
            string componentType = component?.GetType().FullName ?? "";
            if (string.Equals(componentType, "DrawerContainer", StringComparison.Ordinal) ||
                string.Equals(componentType, "OdinShip.ShipContainer", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetMultiUserContainerOwner(Container container, out long owner)
    {
        owner = 0L;
        ZDO? zdo = container?.m_nview != null ? container.m_nview.GetZDO() : null;
        if (zdo == null)
        {
            return false;
        }

        owner = zdo.GetOwner();
        return owner != 0L && owner != ZNet.GetUID();
    }

    private static bool IsLocalPlayerInventory(Inventory inventory)
    {
        Player? player = Player.m_localPlayer;
        return player != null &&
               !IsUnityNull(player) &&
               ((Humanoid)player).GetInventory() == inventory;
    }

    private static bool CanAddEntireMultiUserContainerItemAt(
        Inventory inventory,
        ItemData incoming,
        int amount,
        Vector2i targetPosition)
    {
        if (!IsMultiUserContainerPositionInBounds(inventory, targetPosition))
        {
            return false;
        }

        ItemData? target = inventory.GetItemAt(targetPosition.x, targetPosition.y);
        return target == null || CanStackEntireMultiUserContainerItem(incoming, target, amount);
    }

    private static bool CanStackEntireMultiUserContainerItem(
        ItemData incoming,
        ItemData target,
        int amount)
    {
        if (incoming?.m_shared == null ||
            target?.m_shared == null ||
            incoming.m_shared.m_maxStackSize <= 1 ||
            target.m_shared.m_maxStackSize != incoming.m_shared.m_maxStackSize ||
            amount <= 0 ||
            target.m_stack > incoming.m_shared.m_maxStackSize - amount)
        {
            return false;
        }

        return CanStackMultiUserContainerItems(
            incoming,
            target,
            requiredStack: 1);
    }

    private static bool TryFindLocalMultiUserContainerDestination(
        Inventory inventory,
        ItemData incoming,
        int amount,
        out Vector2i destination)
    {
        destination = new Vector2i(-1, -1);
        Player? player = Player.m_localPlayer;
        if (player == null || IsUnityNull(player) || inventory == null || incoming?.m_shared == null)
        {
            return false;
        }

        foreach (ItemData target in inventory.m_inventory
                     .Where(target => target?.m_shared != null)
                     .OrderBy(target => target.m_gridPos.y)
                     .ThenBy(target => target.m_gridPos.x))
        {
            if (CanUseCell(player, inventory, incoming, target.m_gridPos) &&
                CanStackEntireMultiUserContainerItem(incoming, target, amount))
            {
                destination = target.m_gridPos;
                return true;
            }
        }

        return TryFindFreeAutomaticPlacementCell(
            player,
            inventory,
            incoming,
            out destination);
    }

    private static bool CanReceiveEntireMultiUserContainerItemAt(
        Inventory inventory,
        ItemData incoming,
        int amount,
        Vector2i targetPosition)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            IsUnityNull(player) ||
            !IsMultiUserContainerPositionInBounds(inventory, targetPosition) ||
            !CanUseCell(player, inventory, incoming, targetPosition))
        {
            return false;
        }

        ItemData? target = inventory.GetItemAt(targetPosition.x, targetPosition.y);
        return target == null || CanStackEntireMultiUserContainerItem(incoming, target, amount);
    }

    private static void HandleMultiUserContainerResponse(
        Container? container,
        long sender,
        MultiUserContainerResponse response,
        bool fromDurableReceipt = false,
        ZDO? durableReceiptZdo = null)
    {
        PendingMultiUserContainerTransfer? pending = _pendingMultiUserContainerTransfer;
        bool hasContainer =
            container != null &&
            !IsUnityNull(container);
        bool hasDetachedReceiptContext =
            fromDurableReceipt &&
            durableReceiptZdo != null &&
            pending != null &&
            durableReceiptZdo.m_uid.Equals(pending.ContainerId);
        if (pending == null ||
            (!hasContainer && !hasDetachedReceiptContext) ||
            (hasContainer && pending.Container != container) ||
            pending.Request.RequestId != response.RequestId ||
            pending.Request.Operation != response.Operation ||
            pending.Request.SourcePosition != response.SourcePosition ||
            pending.Request.TargetPosition != response.TargetPosition)
        {
            return;
        }

        ZDO? responseZdo = durableReceiptZdo ??
                           container?.m_nview?.GetZDO();
        ObservePendingMultiUserContainerOwner(
            pending,
            responseZdo?.GetOwner() ?? 0L);
        if (!pending.RequestOwners.Contains(sender))
        {
            return;
        }

        if (pending.ResponseApplied || pending.TerminalFailureReceived)
        {
            return;
        }

        bool validSuccess = response.Success &&
                            response.Failure == MultiUserContainerFailure.None &&
                            response.Amount == pending.Request.Amount &&
                            IsValidMultiUserContainerSuccessPayload(pending.Request, response);
        bool validFailure = !response.Success &&
                            response.Failure != MultiUserContainerFailure.None &&
                            response.Amount == 0 &&
                            response.Item == null;
        if (!validSuccess && !validFailure)
        {
            return;
        }

        if (!validSuccess &&
            !fromDurableReceipt &&
            (!hasContainer ||
             !IsCurrentMultiUserContainerOwnerResponse(
                 container!,
                 sender)))
        {
            return;
        }

        // Once ownership changes, neither a live failure nor a later owner's
        // receipt can prove that an earlier owner did not already commit.
        // Success remains safe to accept; failure waits for explicit fencing.
        if (!validSuccess &&
            pending.AuthorityChangedOrReloaded)
        {
            return;
        }

        if (!validSuccess)
        {
            pending.TerminalFailureReceived = true;
            pending.ResponseAppliedAt = Time.unscaledTime;
            pending.Projection = null;
            pending.AcknowledgementPending = true;
            if (pending.LocalEscrow != null)
            {
                pending.PendingRecoveryItem = pending.LocalEscrow;
                pending.LocalRecoveryPending = true;
                _ = TrySecurePendingMultiUserContainerLocalRecovery(pending);
            }

            ShowMultiUserContainerNotReady();
            if (pending.LocalRecoveryPending)
            {
                return;
            }

            if (!hasContainer ||
                !TryAcknowledgePendingMultiUserContainerResponse(
                    pending,
                    container!))
            {
                return;
            }

            CompletePendingMultiUserContainerTransfer(
                pending,
                committedAndObserved: false);
            return;
        }

        pending.ResponseApplied = true;
        pending.ResponseAppliedAt = Time.unscaledTime;
        pending.AcknowledgementPending = true;
        bool responseSecured = true;
        switch (response.Operation)
        {
            case MultiUserContainerOperation.Add:
            case MultiUserContainerOperation.Move:
            case MultiUserContainerOperation.Swap:
                break;
            case MultiUserContainerOperation.Remove:
            case MultiUserContainerOperation.Exchange:
                if (response.Item != null)
                {
                    pending.LocalRecoveryPending = true;
                    pending.PendingRecoveryItem = response.Item;
                    responseSecured =
                        TrySecurePendingMultiUserContainerLocalRecovery(pending);
                }

                ClearCraftingRequirementAvailabilityCache();
                break;
        }

        if (responseSecured && hasContainer)
        {
            _ = TryAcknowledgePendingMultiUserContainerResponse(
                pending,
                container!);
        }

        if (!pending.LocalRecoveryPending &&
            !pending.AcknowledgementPending &&
            pending.AuthoritativeStateObserved &&
            _pendingMultiUserContainerTransfer == pending)
        {
            CompletePendingMultiUserContainerTransfer(
                pending,
                committedAndObserved: true);
        }

        if (InventoryGui.instance != null && !IsUnityNull(InventoryGui.instance))
        {
            InventoryGui.instance.UpdateCraftingPanel();
        }
    }

    private static bool IsCurrentMultiUserContainerOwnerResponse(
        Container container,
        long sender)
    {
        if (sender == 0L ||
            container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return false;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        return zdo != null && zdo.GetOwner() == sender;
    }

    private static bool IsValidMultiUserContainerSuccessPayload(
        MultiUserContainerRequest request,
        MultiUserContainerResponse response)
    {
        bool itemRequired = request.Operation is
            MultiUserContainerOperation.Remove or
            MultiUserContainerOperation.Exchange;
        if (!itemRequired)
        {
            return response.Item == null;
        }

        if (response.Item == null ||
            response.Item.m_stack != request.Amount ||
            response.Item.m_equipped)
        {
            return false;
        }

        return IsExactMultiUserContainerItemMatch(
            request.Item,
            response.Item,
            request.Amount);
    }

    internal static void UpdateMultiUserContainerRuntime()
    {
        ProcessPendingMultiUserContainerLocalRecoveries();
        PruneMultiUserContainerOwnerResponseCaches();
        PendingMultiUserContainerTransfer? pending = _pendingMultiUserContainerTransfer;
        if (pending == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        _ = TrySecurePendingMultiUserContainerLocalRecovery(pending);
        if (pending.PermanentlyDestroyed)
        {
            if (!pending.LocalRecoveryPending &&
                (pending.ResponseApplied ||
                 pending.TerminalFailureReceived))
            {
                CompletePendingMultiUserContainerTransfer(
                    pending,
                    committedAndObserved: false);
            }

            return;
        }

        Container container = pending.Container;
        if (container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            if (!pending.ResponseApplied &&
                !pending.TerminalFailureReceived &&
                now - pending.LastReceiptCheckAt >=
                MultiUserContainerReceiptPollInterval)
            {
                pending.LastReceiptCheckAt = now;
                _ = TryResolveDetachedPendingMultiUserContainerDurableReceipt(
                    pending);
            }

            return;
        }

        if (!pending.ResponseApplied &&
            !pending.TerminalFailureReceived &&
            now - pending.LastReceiptCheckAt >=
            MultiUserContainerReceiptPollInterval)
        {
            pending.LastReceiptCheckAt = now;
            _ = TryResolvePendingMultiUserContainerDurableReceipt(container);
            if (_pendingMultiUserContainerTransfer != pending)
            {
                return;
            }
        }

        if (pending.TerminalFailureReceived)
        {
            if (!pending.LocalRecoveryPending &&
                TryAcknowledgePendingMultiUserContainerResponse(
                    pending,
                    container))
            {
                CompletePendingMultiUserContainerTransfer(
                    pending,
                    committedAndObserved: false);
                return;
            }

            CloseMultiUserContainerAfterRecoveryTimeout(
                pending,
                container,
                now);
            return;
        }

        if (pending.ResponseApplied)
        {
            if (pending.LocalRecoveryPending ||
                !TryAcknowledgePendingMultiUserContainerResponse(
                    pending,
                    container))
            {
                CloseMultiUserContainerAfterRecoveryTimeout(
                    pending,
                    container,
                    now);
                return;
            }

            try
            {
                container.CheckForChanges();
            }
            catch
            {
                // Keep the committed projection and interaction barrier until the
                // normal ZDO load path succeeds or the bounded UI timeout closes it.
            }

            if (_pendingMultiUserContainerTransfer != pending)
            {
                return;
            }

            if (pending.AuthoritativeStateObserved)
            {
                CompletePendingMultiUserContainerTransfer(
                    pending,
                    committedAndObserved: true);
                return;
            }

            if (now - pending.ResponseAppliedAt >=
                MultiUserContainerCommittedProjectionTimeout)
            {
                CompletePendingMultiUserContainerTransfer(
                    pending,
                    committedAndObserved: false);
                InventoryGui? gui = InventoryGui.instance;
                if (gui != null &&
                    !IsUnityNull(gui) &&
                    gui.m_currentContainer == container)
                {
                    gui.CloseContainer();
                }
            }

            return;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        long currentOwner = zdo?.GetOwner() ?? 0L;
        if (currentOwner == 0L)
        {
            return;
        }

        if (currentOwner != pending.Owner)
        {
            ObservePendingMultiUserContainerOwner(
                pending,
                currentOwner);
        }

        if (now - pending.StartedAt >= MultiUserContainerRequestTimeout &&
            !pending.TimeoutNotified)
        {
            pending.TimeoutNotified = true;
            ShowMultiUserContainerNotReady();
        }

        float retryInterval =
            pending.SendAttempts < MultiUserContainerMaximumSendAttempts
                ? MultiUserContainerRetryInterval
                : MultiUserContainerRequestTimeout;
        if (now - pending.LastSentAt < retryInterval)
        {
            return;
        }

        pending.LastSentAt = now;
        pending.SendAttempts = Math.Min(
            MultiUserContainerMaximumSendAttempts,
            pending.SendAttempts + 1);
        try
        {
            container.m_nview.InvokeRPC(
                MultiUserContainerRequestRpc,
                new ZPackage(pending.RequestBytes));
        }
        catch (Exception exception)
        {
            // Preserve the pending request and escrow. The same request id and
            // digest can be retried safely, while a transient send failure must
            // not abort the rest of the plugin's Update cycle.
            Log.LogWarning(
                $"Built-in multi-user chest request resend failed; retrying: {exception.Message}");
        }
    }

    internal static void ShutdownMultiUserContainerRuntime()
    {
        CancelMultiUserContainerBatch();
        PendingMultiUserContainerTransfer? pending = _pendingMultiUserContainerTransfer;
        if (pending != null)
        {
            bool resolved = false;
            if (pending.Container != null &&
                !IsUnityNull(pending.Container))
            {
                resolved = TryResolvePendingMultiUserContainerDurableReceipt(
                    pending.Container);
            }

            if (!resolved &&
                _pendingMultiUserContainerTransfer == pending)
            {
                _ = TryResolveDetachedPendingMultiUserContainerDurableReceipt(
                    pending);
            }

            pending = _pendingMultiUserContainerTransfer;
        }

        if (pending != null &&
            pending.LocalRecoveryPending &&
            pending.PendingRecoveryItem != null)
        {
            bool secured =
                TrySecurePendingMultiUserContainerLocalRecovery(pending);
            if (!secured &&
                pending.WorldDeliveryResult !=
                MultiUserContainerWorldDeliveryResult.Uncertain)
            {
                QueueMultiUserContainerLocalRecovery(
                    pending.LocalInventory,
                    pending.PendingRecoveryItem,
                    pending.PreferredLocalPosition,
                    GetMultiUserContainerLocalPlacementPolicy(
                        pending.RecoveryMode));
            }

            if (secured)
            {
                pending.LocalRecoveryPending = false;
                pending.PendingRecoveryItem = null;
                if (pending.TerminalFailureReceived)
                {
                    pending.LocalEscrow = null;
                }
            }
        }

        if (pending != null &&
            !pending.LocalRecoveryPending &&
            pending.Container != null &&
            !IsUnityNull(pending.Container))
        {
            _ = TryAcknowledgePendingMultiUserContainerResponse(
                pending,
                pending.Container);
        }

        bool committedLocalEscrow =
            pending != null &&
            (pending.Request.Operation is
                MultiUserContainerOperation.Add or
                MultiUserContainerOperation.Exchange) &&
            pending.ResponseApplied;
        if (pending != null &&
            !committedLocalEscrow &&
            !pending.TerminalFailureReceived &&
            !pending.LocalRecoveryPending &&
            !pending.PermanentlyDestroyed &&
            pending.LocalEscrow != null &&
            pending.LocalInventory != null)
        {
            RestoreMultiUserContainerLocalEscrow(
                pending.LocalInventory,
                pending.LocalEscrow,
                pending.PreferredLocalPosition,
                GetMultiUserContainerLocalPlacementPolicy(
                    pending.RecoveryMode));
        }

        if (pending != null)
        {
            CompletePendingMultiUserContainerTransfer(
                pending,
                committedAndObserved: false);
        }
        MultiUserContainerOwnerStates.Clear();
        ProcessPendingMultiUserContainerLocalRecoveries();
        if (PendingMultiUserContainerLocalRecoveries.Count > 0)
        {
            Log.LogWarning(
                $"{PendingMultiUserContainerLocalRecoveries.Count} built-in multi-user chest item recovery operation(s) are still pending.");
        }
    }

    private static bool IsMultiUserContainerRequestVisibleInInventory(
        PendingMultiUserContainerTransfer pending,
        Inventory inventory)
    {
        if (pending == null || inventory == null)
        {
            return false;
        }

        MultiUserContainerRequest request = pending.Request;
        if (request.Operation == MultiUserContainerOperation.Add)
        {
            Vector2i targetPosition = request.TargetPosition;
            if (!IsMultiUserContainerPositionInBounds(
                    inventory,
                    targetPosition))
            {
                return false;
            }

            ItemData? target = inventory.GetItemAt(
                targetPosition.x,
                targetPosition.y);
            int expectedStack =
                request.ExpectedTargetStack + request.Amount;
            return target != null &&
                   CanStackMultiUserContainerItems(
                       request.Item,
                       target,
                       requiredStack: 1) &&
                   target.m_stack >= expectedStack;
        }

        if (request.Operation == MultiUserContainerOperation.Remove)
        {
            return IsMultiUserContainerSourceRemainderVisible(
                inventory,
                request);
        }

        if (request.Operation == MultiUserContainerOperation.Exchange)
        {
            ItemData? replacement = inventory.GetItemAt(
                request.SourcePosition.x,
                request.SourcePosition.y);
            return request.CounterpartItem != null &&
                   replacement != null &&
                   replacement.m_stack ==
                   request.CounterpartItem.m_stack &&
                   IsExactMultiUserContainerItemMatch(
                       request.CounterpartItem,
                       replacement,
                       request.CounterpartItem.m_stack);
        }

        if (request.Operation == MultiUserContainerOperation.Swap)
        {
            ItemData? swappedTarget = inventory.GetItemAt(
                request.TargetPosition.x,
                request.TargetPosition.y);
            ItemData? swappedSource = inventory.GetItemAt(
                request.SourcePosition.x,
                request.SourcePosition.y);
            return request.CounterpartItem != null &&
                   swappedTarget != null &&
                   swappedSource != null &&
                   swappedTarget.m_stack == request.Item.m_stack &&
                   swappedSource.m_stack ==
                   request.CounterpartItem.m_stack &&
                   IsExactMultiUserContainerItemMatch(
                       request.Item,
                       swappedTarget,
                       request.Item.m_stack) &&
                   IsExactMultiUserContainerItemMatch(
                       request.CounterpartItem,
                       swappedSource,
                       request.CounterpartItem.m_stack);
        }

        if (request.Operation != MultiUserContainerOperation.Move)
        {
            return false;
        }

        if (request.SourcePosition == request.TargetPosition)
        {
            ItemData? unchanged = inventory.GetItemAt(
                request.SourcePosition.x,
                request.SourcePosition.y);
            return unchanged != null &&
                   IsExactMultiUserContainerItemMatch(
                       request.Item,
                       unchanged,
                       request.Amount);
        }

        if (!IsMultiUserContainerPositionInBounds(
                inventory,
                request.TargetPosition) ||
            !IsMultiUserContainerSourceRemainderVisible(
                inventory,
                request))
        {
            return false;
        }

        ItemData? movedTarget = inventory.GetItemAt(
            request.TargetPosition.x,
            request.TargetPosition.y);
        int movedTargetStack =
            request.ExpectedTargetStack + request.Amount;
        return movedTarget != null &&
               CanStackMultiUserContainerItems(
                   request.Item,
                   movedTarget,
                   requiredStack: 1) &&
               movedTarget.m_stack >= movedTargetStack;
    }

    private static bool IsMultiUserContainerSourceRemainderVisible(
        Inventory inventory,
        MultiUserContainerRequest request)
    {
        if (!IsMultiUserContainerPositionInBounds(
                inventory,
                request.SourcePosition))
        {
            return false;
        }

        ItemData? source = inventory.GetItemAt(
            request.SourcePosition.x,
            request.SourcePosition.y);
        int expectedRemainder = request.Item.m_stack - request.Amount;
        if (expectedRemainder <= 0)
        {
            return source == null ||
                   !IsExactMultiUserContainerItemMatch(
                       request.Item,
                       source,
                       requiredStack: 1);
        }

        return source != null &&
               IsExactMultiUserContainerItemMatch(
                   request.Item,
                   source,
                   requiredStack: 1) &&
               source.m_stack == expectedRemainder;
    }

    internal static void RebindPendingMultiUserContainer(
        Container container)
    {
        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (pending == null ||
            container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        if (zdo == null || !zdo.m_uid.Equals(pending.ContainerId))
        {
            return;
        }

        bool containerInstanceChanged =
            pending.Container == null ||
            IsUnityNull(pending.Container) ||
            pending.Container != container;
        pending.Container = container;
        if (containerInstanceChanged)
        {
            // The previous owner-side in-memory quarantine is tied to the old
            // component instance. A failure after rebind cannot prove that the
            // earlier instance did not commit without leaving a receipt.
            pending.AuthorityChangedOrReloaded = true;
        }

        long reboundOwner = zdo.GetOwner();
        ObservePendingMultiUserContainerOwner(
            pending,
            reboundOwner);

        pending.Projection = null;
        pending.LastSentAt = float.MinValue;
        _ = TryResolvePendingMultiUserContainerDurableReceipt(container);
    }

    private static void ObservePendingMultiUserContainerOwner(
        PendingMultiUserContainerTransfer pending,
        long currentOwner)
    {
        if (pending == null ||
            currentOwner == 0L ||
            currentOwner == pending.Owner)
        {
            return;
        }

        pending.Owner = currentOwner;
        pending.RequestOwners.Add(currentOwner);
        pending.SendAttempts = 0;
        pending.LastSentAt = float.MinValue;
        pending.AuthorityChangedOrReloaded = true;
    }

    internal static void SuspendPendingMultiUserContainer(
        Container container)
    {
        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (pending == null || pending.Container != container)
        {
            return;
        }

        _ = TryResolvePendingMultiUserContainerDurableReceipt(container);
        if (_pendingMultiUserContainerTransfer != pending)
        {
            return;
        }

        if (IsMultiUserContainerAreaBatchActive())
        {
            FinishMultiUserContainerBatch(showResult: true);
        }

        if (pending.ResponseApplied &&
            !pending.LocalRecoveryPending &&
            !pending.AcknowledgementPending)
        {
            CompletePendingMultiUserContainerTransfer(
                pending,
                committedAndObserved: false);
            return;
        }

        pending.Container = null!;
        pending.Projection = null;
    }

    internal static void OnMultiUserContainerPermanentlyDestroyed(
        Container container,
        ZDO zdo)
    {
        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (pending == null ||
            container == null ||
            IsUnityNull(container) ||
            zdo == null ||
            !zdo.m_uid.Equals(pending.ContainerId))
        {
            return;
        }

        // OnZDODestroyed is distinct from zone unloading. Rebind temporarily so
        // an exact receipt can still settle the request before the instance and
        // its ZDO disappear for good.
        pending.Container = container;
        _ = TryResolvePendingMultiUserContainerDurableReceipt(container);
        pending = _pendingMultiUserContainerTransfer;
        if (pending == null ||
            !zdo.m_uid.Equals(pending.ContainerId))
        {
            return;
        }

        _ = TrySecurePendingMultiUserContainerLocalRecovery(pending);
        pending.PermanentlyDestroyed = true;
        pending.Container = null!;
        pending.Projection = null;
        if (!pending.LocalRecoveryPending &&
            (pending.ResponseApplied ||
             pending.TerminalFailureReceived))
        {
            CompletePendingMultiUserContainerTransfer(
                pending,
                committedAndObserved: false);
        }

        ShowMultiUserContainerNotReady();
    }

    private static void CompletePendingMultiUserContainerTransfer(
        PendingMultiUserContainerTransfer pending,
        bool committedAndObserved)
    {
        if (pending == null ||
            _pendingMultiUserContainerTransfer != pending)
        {
            return;
        }

        _pendingMultiUserContainerTransfer = null;
        OnMultiUserContainerTransferCompleted(
            committedAndObserved);
    }

    internal static void OnMultiUserContainerZdoDestroyed(ZDO zdo)
    {
        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (pending == null ||
            zdo == null ||
            !zdo.m_uid.Equals(pending.ContainerId))
        {
            return;
        }

        _ = TryResolvePendingMultiUserContainerDurableReceipt(zdo);
        pending = _pendingMultiUserContainerTransfer;
        if (pending == null ||
            !zdo.m_uid.Equals(pending.ContainerId))
        {
            return;
        }

        Container? container = pending.Container;
        if (container != null && !IsUnityNull(container))
        {
            OnMultiUserContainerPermanentlyDestroyed(container, zdo);
            return;
        }

        pending.PermanentlyDestroyed = true;
        pending.Projection = null;
        ShowMultiUserContainerNotReady();
    }

    private static bool TryResolveDetachedPendingMultiUserContainerDurableReceipt(
        PendingMultiUserContainerTransfer pending)
    {
        if (pending == null ||
            _pendingMultiUserContainerTransfer != pending ||
            ZDOMan.instance == null)
        {
            return false;
        }

        ZDO? zdo = ZDOMan.instance.GetZDO(pending.ContainerId);
        return zdo != null &&
               TryResolvePendingMultiUserContainerDurableReceipt(zdo);
    }

    private static bool RestoreMultiUserContainerLocalEscrow(
        Inventory inventory,
        ItemData escrow,
        Vector2i preferredPosition,
        MultiUserContainerLocalPlacementPolicy placementPolicy =
            MultiUserContainerLocalPlacementPolicy.AnyUsable)
    {
        bool secured = RecoverMultiUserContainerItemLocally(
            inventory,
            escrow,
            escrow.m_stack,
            preferredPosition,
            placementPolicy: placementPolicy);
        InventoryGui? gui = InventoryGui.instance;
        if (gui != null &&
            !IsUnityNull(gui) &&
            gui.m_dragItem != null &&
            gui.m_dragInventory == inventory &&
            !inventory.ContainsItem(gui.m_dragItem))
        {
            // Full-stack escrow removes the original live ItemData reference.
            // Never leave InventoryGui holding that stale reference after restoring
            // a clone or queueing recovery; a later vanilla drop could re-add it.
            gui.SetupDragItem(null, null, 1);
        }

        return secured;
    }

    private static MultiUserContainerLocalPlacementPolicy
        GetMultiUserContainerLocalPlacementPolicy(
            MultiUserContainerRecoveryMode recoveryMode)
    {
        return recoveryMode switch
        {
            MultiUserContainerRecoveryMode.RegularInventoryFirst =>
                MultiUserContainerLocalPlacementPolicy.RegularAndHotbar,
            MultiUserContainerRecoveryMode.BatchInventoryFirst =>
                MultiUserContainerLocalPlacementPolicy.ContainerBatch,
            _ => MultiUserContainerLocalPlacementPolicy.AnyUsable
        };
    }

    private static bool RecoverMultiUserContainerItemLocally(
        Inventory? inventory,
        ItemData item,
        int amount,
        Vector2i preferredPosition,
        MultiUserContainerLocalPlacementPolicy placementPolicy =
            MultiUserContainerLocalPlacementPolicy.AnyUsable)
    {
        if (item?.m_shared == null || amount <= 0 || amount > item.m_stack)
        {
            return false;
        }

        try
        {
            if (inventory != null &&
                TryInsertMultiUserContainerItemIntoLocalInventory(
                    inventory,
                    item,
                    amount,
                    preferredPosition,
                    out _,
                    placementPolicy))
            {
                return true;
            }
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest local inventory recovery failed: {exception.Message}");
        }

        item.m_stack = amount;
        QueueMultiUserContainerLocalRecovery(
            inventory,
            item,
            preferredPosition,
            placementPolicy);
        return false;
    }

    private static void QueueMultiUserContainerLocalRecovery(
        Inventory? inventory,
        ItemData? item,
        Vector2i preferredPosition,
        MultiUserContainerLocalPlacementPolicy placementPolicy =
            MultiUserContainerLocalPlacementPolicy.AnyUsable)
    {
        if (item?.m_shared == null ||
            PendingMultiUserContainerLocalRecoveries.Any(
                recovery => ReferenceEquals(recovery.Item, item)))
        {
            return;
        }

        PendingMultiUserContainerLocalRecoveries.Add(
            new PendingMultiUserContainerLocalRecovery
            {
                Inventory = inventory,
                Item = item,
                PreferredPosition = preferredPosition,
                NextAttemptAt = Time.unscaledTime +
                                MultiUserContainerLocalRecoveryRetryInterval,
                PlacementPolicy = placementPolicy
            });
    }

    private static bool TrySecurePendingMultiUserContainerLocalRecovery(
        PendingMultiUserContainerTransfer pending)
    {
        if (!pending.LocalRecoveryPending)
        {
            return true;
        }

        ItemData? recoveryItem = pending.PendingRecoveryItem;
        if (recoveryItem == null)
        {
            return false;
        }

        ItemData? securedInventoryItem = null;
        bool secured;
        switch (pending.RecoveryMode)
        {
            case MultiUserContainerRecoveryMode.RegularInventoryFirst:
                secured =
                    pending.LocalInventory != null &&
                    TryInsertMultiUserContainerItemIntoLocalInventory(
                        pending.LocalInventory,
                        recoveryItem,
                        recoveryItem.m_stack,
                        pending.PreferredLocalPosition,
                        out securedInventoryItem,
                        MultiUserContainerLocalPlacementPolicy.RegularAndHotbar);
                break;
            case MultiUserContainerRecoveryMode.BatchInventoryFirst:
                secured =
                    pending.LocalInventory != null &&
                    TryInsertMultiUserContainerItemIntoLocalInventory(
                        pending.LocalInventory,
                        recoveryItem,
                        recoveryItem.m_stack,
                        pending.PreferredLocalPosition,
                        out securedInventoryItem,
                        MultiUserContainerLocalPlacementPolicy.ContainerBatch);
                break;
            case MultiUserContainerRecoveryMode.ConsumeAfterInventory:
                secured =
                    pending.LocalInventory != null &&
                    TryInsertMultiUserContainerItemIntoLocalInventory(
                        pending.LocalInventory,
                        recoveryItem,
                        recoveryItem.m_stack,
                        pending.PreferredLocalPosition,
                        out securedInventoryItem);
                break;
            case MultiUserContainerRecoveryMode.WorldFirst:
                MultiUserContainerWorldDeliveryResult worldDeliveryResult =
                    TryDeliverPendingMultiUserContainerRecoveryToWorld(
                        pending,
                        recoveryItem);
                secured =
                    worldDeliveryResult ==
                    MultiUserContainerWorldDeliveryResult.Succeeded;
                if (worldDeliveryResult ==
                        MultiUserContainerWorldDeliveryResult.DefinitelyNotSpawned &&
                    pending.LocalInventory != null)
                {
                    secured =
                        TryInsertMultiUserContainerItemIntoLocalInventory(
                            pending.LocalInventory,
                            recoveryItem,
                            recoveryItem.m_stack,
                            pending.PreferredLocalPosition,
                            out securedInventoryItem);
                }

                break;
            default:
                secured =
                    pending.LocalInventory != null &&
                    TryInsertMultiUserContainerItemIntoLocalInventory(
                        pending.LocalInventory,
                        recoveryItem,
                        recoveryItem.m_stack,
                        pending.PreferredLocalPosition,
                        out securedInventoryItem);
                break;
        }

        if (!secured)
        {
            return false;
        }

        pending.LocalRecoveryPending = false;
        pending.PendingRecoveryItem = null;
        if (pending.TerminalFailureReceived)
        {
            pending.LocalEscrow = null;
        }

        if (pending.RecoveryMode ==
                MultiUserContainerRecoveryMode.ConsumeAfterInventory &&
            securedInventoryItem != null)
        {
            TryConsumeSecuredMultiUserContainerItem(
                pending,
                securedInventoryItem);
        }

        return true;
    }

    private static MultiUserContainerWorldDeliveryResult
        TryDeliverPendingMultiUserContainerRecoveryToWorld(
        PendingMultiUserContainerTransfer pending,
        ItemData recoveryItem)
    {
        if (pending.WorldDeliveryResult !=
            MultiUserContainerWorldDeliveryResult.NotAttempted)
        {
            return pending.WorldDeliveryResult;
        }

        // Reserve the one-shot attempt before entering ItemDrop callbacks. Those
        // callbacks can re-enter this runtime; fail closed until the call returns.
        pending.WorldDeliveryResult =
            MultiUserContainerWorldDeliveryResult.Uncertain;
        MultiUserContainerWorldDeliveryResult deliveryResult =
            DeliverMultiUserContainerItemToWorld(
                recoveryItem,
                recoveryItem.m_stack);
        pending.WorldDeliveryResult = deliveryResult;
        if (pending.WorldDeliveryResult ==
            MultiUserContainerWorldDeliveryResult.Uncertain)
        {
            Log.LogError(
                "Built-in multi-user chest world-drop result is uncertain; " +
                "local fallback and acknowledgement are blocked to avoid duplication.");
        }

        return pending.WorldDeliveryResult;
    }

    private static void TryConsumeSecuredMultiUserContainerItem(
        PendingMultiUserContainerTransfer pending,
        ItemData securedItem)
    {
        if (pending.CompletionActionAttempted)
        {
            return;
        }

        // Mark before invoking vanilla/mod callbacks. If a callback throws after
        // applying an effect, retrying could consume or apply it a second time.
        pending.CompletionActionAttempted = true;
        Player? player = Player.m_localPlayer;
        Inventory? inventory = pending.LocalInventory;
        if (player == null ||
            IsUnityNull(player) ||
            inventory == null ||
            !IsLocalPlayerInventory(inventory) ||
            securedItem?.m_shared == null ||
            !inventory.ContainsItem(securedItem))
        {
            return;
        }

        try
        {
            player.UseItem(
                inventory,
                securedItem,
                fromInventoryGui: true);
        }
        catch (Exception exception)
        {
            // The item was secured before invoking UseItem. Whether a mod threw
            // before or after applying its effect, leaving the remaining item in
            // the real player inventory is safer than retrying the action.
            Log.LogWarning(
                $"Built-in multi-user chest consume callback failed: {exception.Message}");
        }
    }

    private static bool TryAcknowledgePendingMultiUserContainerResponse(
        PendingMultiUserContainerTransfer pending,
        Container container)
    {
        if (!pending.AcknowledgementPending)
        {
            return true;
        }

        if (container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return false;
        }

        try
        {
            AcknowledgeMultiUserContainerResponse(
                container,
                pending.Request.RequestId,
                pending.Request.RequesterPlayerId,
                pending.RequestDigest);
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest acknowledgement failed: {exception.Message}");
            return false;
        }

        pending.AcknowledgementPending = false;
        return true;
    }

    private static void CloseMultiUserContainerAfterRecoveryTimeout(
        PendingMultiUserContainerTransfer pending,
        Container container,
        float now)
    {
        if (now - pending.ResponseAppliedAt <
            MultiUserContainerCommittedProjectionTimeout)
        {
            return;
        }

        InventoryGui? gui = InventoryGui.instance;
        if (gui != null &&
            !IsUnityNull(gui) &&
            gui.m_currentContainer == container)
        {
            gui.CloseContainer();
        }
    }

    private static void ProcessPendingMultiUserContainerLocalRecoveries()
    {
        float now = Time.unscaledTime;
        for (int index = PendingMultiUserContainerLocalRecoveries.Count - 1;
             index >= 0;
             index--)
        {
            PendingMultiUserContainerLocalRecovery recovery =
                PendingMultiUserContainerLocalRecoveries[index];
            if (now < recovery.NextAttemptAt)
            {
                continue;
            }

            if (recovery.Inventory != null &&
                TryInsertMultiUserContainerItemIntoLocalInventory(
                    recovery.Inventory,
                    recovery.Item,
                    recovery.Item.m_stack,
                    recovery.PreferredPosition,
                    out _,
                    recovery.PlacementPolicy))
            {
                PendingMultiUserContainerLocalRecoveries.RemoveAt(index);
                continue;
            }

            recovery.NextAttemptAt =
                now + MultiUserContainerLocalRecoveryRetryInterval;
        }
    }

    private static bool TryInsertMultiUserContainerItemIntoLocalInventory(
        Inventory inventory,
        ItemData item,
        int amount,
        Vector2i preferredPosition,
        out ItemData? securedItem,
        MultiUserContainerLocalPlacementPolicy placementPolicy =
            MultiUserContainerLocalPlacementPolicy.AnyUsable)
    {
        securedItem = null;
        if (!IsLocalPlayerInventory(inventory) ||
            item?.m_shared == null ||
            amount <= 0 ||
            amount > item.m_stack ||
            !CanAddWithinInventoryLimits(inventory, item, amount, out _))
        {
            return false;
        }

        Vector2i destination = preferredPosition;
        bool canUsePreferred =
            (placementPolicy ==
                 MultiUserContainerLocalPlacementPolicy.AnyUsable ||
             IsMultiUserContainerLocalCellAllowed(
                 inventory,
                 destination,
                 placementPolicy)) &&
            CanReceiveEntireMultiUserContainerItemAt(
                inventory,
                item,
                amount,
                destination);
        if (!canUsePreferred &&
            !(placementPolicy !=
                  MultiUserContainerLocalPlacementPolicy.AnyUsable
                ? TryFindRestrictedMultiUserContainerDestination(
                    inventory,
                    item,
                    amount,
                    placementPolicy,
                    out destination)
                : TryFindLocalMultiUserContainerDestination(
                    inventory,
                    item,
                    amount,
                    out destination)))
        {
            return false;
        }

        ItemData? target = inventory.GetItemAt(destination.x, destination.y);
        if (target != null)
        {
            if (!CanStackEntireMultiUserContainerItem(item, target, amount))
            {
                return false;
            }

            target.m_stack += amount;
            MergeStackMetadata(target, item);
            NotifyMultiUserContainerInventoryChanged(inventory);
            securedItem = target;
            return true;
        }

        ItemData inserted;
        try
        {
            inserted = item.Clone();
        }
        catch
        {
            return false;
        }

        inserted.m_stack = amount;
        inserted.m_gridPos = destination;
        inserted.m_equipped = false;
        inventory.m_inventory.Add(inserted);
        NotifyMultiUserContainerInventoryChanged(inventory);
        securedItem = inserted;
        try
        {
            OnPlayerInventoryItemPlaced(
                inventory,
                inserted,
                destination,
                result: true);
        }
        catch (Exception exception)
        {
            // The item was already inserted. Keep the recovery settled when the
            // placement callback fails after insertion; retrying would duplicate it.
            Log.LogWarning(
                $"Built-in multi-user chest placement callback failed: {exception.Message}");
            return inventory.ContainsItem(inserted);
        }

        return true;
    }

    private static bool IsMultiUserContainerLocalCellAllowed(
        Inventory inventory,
        Vector2i position,
        MultiUserContainerLocalPlacementPolicy placementPolicy)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            IsUnityNull(player) ||
            !IsLocalPlayerInventory(inventory))
        {
            return false;
        }

        InventoryCellKind kind = GetInventoryCellKind(
            player,
            inventory,
            position);
        return placementPolicy switch
        {
            MultiUserContainerLocalPlacementPolicy.RegularAndHotbar =>
                InventoryActionCellPolicyCore.CanUseContainerActionSource(
                    kind,
                    includeHotbar: true),
            MultiUserContainerLocalPlacementPolicy.ContainerBatch =>
                GetPlayerActionSlots(
                        player,
                        inventory,
                        includeHotbar: false,
                        blockFavorites: true)
                    .Contains(position),
            _ => true
        };
    }

    private static bool TryFindRestrictedMultiUserContainerDestination(
        Inventory inventory,
        ItemData incoming,
        int amount,
        MultiUserContainerLocalPlacementPolicy placementPolicy,
        out Vector2i destination)
    {
        destination = new Vector2i(-1, -1);
        Player? player = Player.m_localPlayer;
        if (player == null ||
            IsUnityNull(player) ||
            !IsLocalPlayerInventory(inventory) ||
            incoming?.m_shared == null)
        {
            return false;
        }

        bool batchPolicy =
            placementPolicy ==
            MultiUserContainerLocalPlacementPolicy.ContainerBatch;
        List<Vector2i> regularCells = GetPlayerActionSlots(
            player,
            inventory,
            includeHotbar: !batchPolicy,
            blockFavorites: batchPolicy);
        foreach (Vector2i cell in regularCells)
        {
            ItemData? target = inventory.GetItemAt(cell.x, cell.y);
            if (target != null &&
                CanStackEntireMultiUserContainerItem(
                    incoming,
                    target,
                    amount))
            {
                destination = cell;
                return true;
            }
        }

        foreach (Vector2i cell in regularCells)
        {
            if (inventory.GetItemAt(cell.x, cell.y) == null)
            {
                destination = cell;
                return true;
            }
        }

        return false;
    }

    private static MultiUserContainerWorldDeliveryResult
        DeliverMultiUserContainerItemToWorld(ItemData? item, int amount)
    {
        Player? player = Player.m_localPlayer;
        if (player == null ||
            IsUnityNull(player) ||
            item?.m_shared == null ||
            amount <= 0)
        {
            return MultiUserContainerWorldDeliveryResult.DefinitelyNotSpawned;
        }

        Transform playerTransform = player.transform;
        ItemDrop drop;
        try
        {
            drop = ItemDrop.DropItem(
                item,
                amount,
                playerTransform.position + playerTransform.forward + playerTransform.up,
                playerTransform.rotation);
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest local item drop failed: {exception.Message}");
            // ItemDrop.DropItem may throw after creating its network object.
            // Treat the outcome as unknown instead of risking an inventory copy.
            return MultiUserContainerWorldDeliveryResult.Uncertain;
        }

        if (drop == null || IsUnityNull(drop))
        {
            return MultiUserContainerWorldDeliveryResult.Uncertain;
        }

        try
        {
            drop.OnPlayerDrop();
            Rigidbody? body = drop.GetComponent<Rigidbody>();
            if (body != null && !IsUnityNull(body))
            {
                body.linearVelocity = (playerTransform.forward + Vector3.up) * 5f;
            }

            player.m_zanim.SetTrigger("interact");
            player.m_dropEffects.Create(playerTransform.position, Quaternion.identity);
            ItemData dropped = drop.m_itemData;
            player.Message(
                MessageHud.MessageType.TopLeft,
                "$msg_dropped " + dropped.m_shared.m_name,
                dropped.m_stack,
                dropped.GetIcon());
        }
        catch (Exception exception)
        {
            // The networked item already exists. Keeping a recovery copy after this
            // point would duplicate it, so cosmetic failures are still success.
            Log.LogWarning(
                $"Built-in multi-user chest local drop effects failed: {exception.Message}");
        }

        return MultiUserContainerWorldDeliveryResult.Succeeded;
    }

    private static void ShowMultiUserContainerNotReady()
    {
        Player? player = Player.m_localPlayer;
        if (player != null && !IsUnityNull(player))
        {
            player.Message(
                MessageHud.MessageType.Center,
                LocalizeUi("$inventoryslots_container_not_ready", "Container is not ready."),
                0,
                null);
        }
    }

    private static void ShowMultiUserContainerUnsupportedDestination()
    {
        Player? player = Player.m_localPlayer;
        if (player != null && !IsUnityNull(player))
        {
            player.Message(
                MessageHud.MessageType.Center,
                LocalizeUi(
                    "$inventoryslots_multi_user_chest_use_empty_slot",
                    "Use an empty slot or a matching stack."),
                0,
                null);
        }
    }

    internal static bool TryHandleMultiUserContainerOpen(
        Container container,
        long sender,
        long playerId)
    {
        if (!IsBuiltInMultiUserChestEnabled ||
            container == null ||
            IsUnityNull(container) ||
            !IsBuiltInMultiUserContainerEligible(container) ||
            container.m_nview == null ||
            !container.m_nview.IsOwner())
        {
            return false;
        }

        bool granted = sender != 0L &&
                       playerId != 0L &&
                       TryGetRpcSenderPlayer(sender, playerId, out Player? requester) &&
                       requester != null &&
                       !IsUnityNull(requester) &&
                       container.CheckAccess(playerId) &&
                       (requester.transform.position - container.transform.position).sqrMagnitude <=
                       MultiUserContainerMaximumInteractionDistance *
                       MultiUserContainerMaximumInteractionDistance;
        if (granted)
        {
            ZDO? zdo = container.m_nview.GetZDO();
            if (zdo != null && ZDOMan.instance != null)
            {
                try
                {
                    ZDOMan.instance.ForceSendZDO(sender, zdo.m_uid);
                }
                catch (Exception exception)
                {
                    // The normal ZDO update remains available; opening the UI does
                    // not need to fail just because this eager send did.
                    Log.LogWarning(
                        $"Built-in multi-user chest open force-send failed: {exception.Message}");
                }
            }
        }

        container.m_nview.InvokeRPC(sender, "OpenRespons", granted);
        return true;
    }

    internal static bool TryUpdateMultiUserRemoteContainer(
        InventoryGui gui,
        Player player)
    {
        if (!IsBuiltInMultiUserChestEnabled ||
            gui == null ||
            player == null ||
            gui.m_currentContainer == null ||
            !TryGetBuiltInRemoteContainer(gui.m_currentContainer.GetInventory(), out Container? container) ||
            container != gui.m_currentContainer)
        {
            return false;
        }

        if (!gui.m_animator.GetBool("visible"))
        {
            return true;
        }

        container!.CheckForChanges();
        gui.m_container.gameObject.SetActive(true);
        gui.m_containerGrid.UpdateInventory(container.GetInventory(), null, gui.m_dragItem);
        gui.m_containerName.text = Localization.instance.Localize(container.GetInventory().GetName());
        if (gui.m_firstContainerUpdate)
        {
            gui.m_containerGrid.ResetView();
            gui.m_firstContainerUpdate = false;
            gui.m_containerHoldTime = 0f;
            gui.m_containerHoldState = 0;
        }

        if (Vector3.Distance(container.transform.position, player.transform.position) > gui.m_autoCloseDistance)
        {
            gui.CloseContainer();
            return true;
        }

        if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
        {
            gui.m_containerHoldTime += Time.deltaTime;
            if (gui.m_containerHoldTime > gui.m_containerHoldPlaceStackDelay &&
                gui.m_containerHoldState == 0)
            {
                gui.m_containerHoldState = 1;
                if (!TryHandleMultiUserContainerAreaQuickStack(
                        container))
                {
                    ShowMultiUserContainerNotReady();
                }
            }
            else if (gui.m_containerHoldTime >
                     gui.m_containerHoldPlaceStackDelay + gui.m_containerHoldExitDelay &&
                     gui.m_containerHoldState == 1)
            {
                gui.Hide();
            }
        }
        else if (gui.m_containerHoldState >= 0)
        {
            gui.m_containerHoldState = -1;
        }

        return true;
    }
}
