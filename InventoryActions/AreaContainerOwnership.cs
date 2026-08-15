using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private const string AreaOwnershipRequestRpc = "InventoryActions_AreaOwnershipRequestV1";
    private const string AreaOwnershipResponseRpc = "InventoryActions_AreaOwnershipResponseV1";
    private const string AreaOwnershipLeaseRequesterKey = "InventoryActions_AreaLeaseRequester";
    private const string AreaOwnershipLeaseRequestIdKey = "InventoryActions_AreaLeaseRequestId";
    private const string AreaOwnershipLeaseActionKey = "InventoryActions_AreaLeaseAction";
    private const string AreaOwnershipLeaseExpiryKey = "InventoryActions_AreaLeaseExpiry";
    private const string AreaOwnershipLeaseTokenKey = "InventoryActions_AreaLeaseToken";
    private const int AreaOwnershipProtocolVersion = 1;
    private const int AreaOwnershipMaximumPackageBytes = 256;
    private const float AreaOwnershipResponseTimeout = 2.5f;
    private const float AreaOwnershipPropagationTimeout = 3f;
    private const float AreaOwnershipMaximumAnchorDistance = 8f;
    private static readonly long AreaOwnershipLeaseDurationTicks = TimeSpan.FromSeconds(8).Ticks;

    private static readonly AreaOwnershipHandoffCore AreaOwnershipHandoff = new();
    private static AreaContainerTransferSession? _areaContainerTransfer;
    private static int _nextAreaOwnershipRequestId = 1;

    private enum AreaOwnershipFailure
    {
        None,
        InvalidRequest,
        NotOwner,
        Unsupported,
        Busy,
        InUse,
        NoAccess,
        OutOfRange,
        Unavailable
    }

    private sealed class AreaContainerTransferSession
    {
        public Player Player = null!;
        public Inventory PlayerInventory = null!;
        public Container Anchor = null!;
        public ZDOID AnchorId;
        public AreaContainerActionKind Action;
        public List<Container> Targets = null!;
        public int NextTargetIndex;
        public Container? PendingTarget;
        public AreaOwnershipRequestIdentity PendingIdentity;
        public long PendingGrantToken;
        public AreaOwnershipHandoffDecision PendingDecision;
        public uint GrantDataRevision;
        public ushort GrantOwnerRevision;
        public long GrantExpiresAt;
        public int TotalMoved;
        public bool PlayerInventoryChanged;
        public int ChangedContainerVfxCount;
        public int VfxLimit;
    }

    private static bool TryStartAreaContainerTransfer(
        Player player,
        Inventory playerInventory,
        Container anchor,
        AreaContainerActionKind action)
    {
        if (_areaContainerTransfer != null ||
            AreaOwnershipHandoff.Phase != AreaOwnershipHandoffPhase.Idle ||
            player == null ||
            playerInventory == null ||
            anchor == null ||
            !TryGetContainerId(anchor, out ZDOID anchorId))
        {
            return false;
        }

        List<Container> targets = GetActionContainers(player, anchor, action);
        InventoryGui.instance?.SetupDragItem(null, null, 0);
        _areaContainerTransfer = new AreaContainerTransferSession
        {
            Player = player,
            PlayerInventory = playerInventory,
            Anchor = anchor,
            AnchorId = anchorId,
            Action = action,
            Targets = targets,
            VfxLimit = IsContainerActionSuccessFxEnabled()
                ? ContainerActionSuccessVfxLimit
                : 0
        };

        ContinueAreaContainerTransfer();
        return true;
    }

    private static void UpdateAreaContainerTransfer(Player player)
    {
        AreaContainerTransferSession? session = _areaContainerTransfer;
        if (session == null)
        {
            return;
        }

        if (player == null ||
            session.Player != player ||
            session.PlayerInventory != GetPlayerInventory(player) ||
            player.m_isLoading ||
            player.IsDead() ||
            ((Character)player).InCutscene() ||
            player.IsTeleporting() ||
            InventoryGui.IsVisible() ||
            IsUnityNull(session.Anchor) ||
            !IsAreaContainerEligible(session.Anchor) ||
            (player.transform.position - session.Anchor.transform.position).sqrMagnitude >
            AreaOwnershipMaximumAnchorDistance * AreaOwnershipMaximumAnchorDistance)
        {
            CancelAreaContainerTransfer();
            return;
        }

        if (session.PendingDecision != AreaOwnershipHandoffDecision.None)
        {
            FinishPendingAreaContainerWithoutMutation(session);
            ContinueAreaContainerTransfer();
            return;
        }

        Container? target = session.PendingTarget;
        if (target == null || AreaOwnershipHandoff.Phase == AreaOwnershipHandoffPhase.Idle)
        {
            if (target != null)
            {
                session.PendingTarget = null;
                session.NextTargetIndex++;
            }

            ContinueAreaContainerTransfer();
            return;
        }

        bool loaded = !IsUnityNull(target) &&
                      target.m_nview != null &&
                      target.m_nview.IsValid() &&
                      target.m_nview.GetZDO() != null;
        ZDO? zdo = loaded ? target!.m_nview!.GetZDO() : null;
        long localUid = ZNet.instance != null ? ZNet.GetUID() : 0L;
        long observedOwnerUid = zdo?.GetOwner() ?? 0L;
        AreaOwnershipObservedOwner observedOwner = GetObservedAreaOwnership(
            observedOwnerUid,
            localUid,
            AreaOwnershipHandoff.ExpectedResponderUid);
        bool grantDataArrived = zdo != null &&
                                HasReachedAreaDataRevision(
                                    zdo.DataRevision,
                                    session.GrantDataRevision);
        AreaOwnershipGrantTokenStatus tokenStatus = grantDataArrived
            ? GetAreaOwnershipGrantTokenStatus(
                zdo,
                AreaOwnershipHandoff.Identity,
                AreaOwnershipHandoff.GrantToken,
                localUid)
            : AreaOwnershipGrantTokenStatus.Missing;
        bool canExecute = grantDataArrived &&
                          zdo != null &&
                          zdo.OwnerRevision == session.GrantOwnerRevision &&
                          GetNetworkTimeTicks() <= session.GrantExpiresAt &&
                          CanUseAreaContainerNow(
                              session.Player,
                              target,
                              session.Anchor,
                              session.Action,
                              requireDirectOwner: true);

        AreaOwnershipHandoffDecision decision = AreaOwnershipHandoff.Observe(
            Time.unscaledTime,
            loaded,
            observedOwner,
            loaded && target!.m_nview!.IsOwner(),
            tokenStatus,
            canExecute);
        if (decision == AreaOwnershipHandoffDecision.None)
        {
            return;
        }

        if (decision == AreaOwnershipHandoffDecision.Execute)
        {
            ExecuteGrantedAreaContainer(session, target);
        }
        else
        {
            FinishPendingAreaContainerWithoutMutation(session);
        }

        ContinueAreaContainerTransfer();
    }

    private static void ContinueAreaContainerTransfer()
    {
        AreaContainerTransferSession? session = _areaContainerTransfer;
        if (session == null ||
            session.PendingTarget != null ||
            AreaOwnershipHandoff.Phase != AreaOwnershipHandoffPhase.Idle)
        {
            return;
        }

        while (_areaContainerTransfer == session &&
               session.NextTargetIndex < session.Targets.Count)
        {
            Container target = session.Targets[session.NextTargetIndex];
            if (IsUnityNull(target) ||
                !CanUseAreaContainerNow(
                    session.Player,
                    target,
                    session.Anchor,
                    session.Action,
                    requireDirectOwner: false))
            {
                session.NextTargetIndex++;
                continue;
            }

            if (CanMutateContainerDirectly(target))
            {
                // Claim the target before any inventory or effect callback can throw.
                // A partially completed mutation must never be retried next frame.
                session.NextTargetIndex++;
                int moved = 0;
                try
                {
                    target.CheckForChanges();
                    if (CanUseAreaContainerNow(
                            session.Player,
                            target,
                            session.Anchor,
                            session.Action,
                            requireDirectOwner: true) &&
                        HasLoadedCurrentContainerRevision(target))
                    {
                        moved = ExecuteAreaContainerTransfer(session, target);
                    }
                }
                catch (Exception exception)
                {
                    Log.LogWarning($"Direct area container transfer failed safely: {exception.Message}");
                    FlushAreaTransferInventoriesAfterFailure(session, target);
                }

                RecordAreaContainerTransfer(session, target, moved);
                continue;
            }

            if (!TryBeginAreaOwnershipRequest(session, target))
            {
                session.NextTargetIndex++;
                continue;
            }

            return;
        }

        if (_areaContainerTransfer == session &&
            session.NextTargetIndex >= session.Targets.Count)
        {
            CompleteAreaContainerTransfer(session);
        }
    }

    private static bool TryBeginAreaOwnershipRequest(
        AreaContainerTransferSession session,
        Container target)
    {
        // MultiUserChest does not expose all secondary users or pending item RPCs.
        // Never steal ownership while that external transaction layer is active.
        if (HasExternalMultiUserChestActive ||
            target == null ||
            target.m_nview == null ||
            !target.m_nview.IsValid() ||
            !target.m_nview.HasOwner() ||
            target.m_nview.IsOwner() ||
            ZNet.instance == null)
        {
            return false;
        }

        ZDO? zdo = target.m_nview.GetZDO();
        if (zdo == null || zdo.GetOwner() == 0L)
        {
            return false;
        }

        int requestId = GetNextAreaOwnershipRequestId();
        AreaOwnershipRequestIdentity identity = new(
            requestId,
            zdo.m_uid.UserID,
            zdo.m_uid.ID,
            session.Action);
        long expectedOwner = zdo.GetOwner();
        if (!AreaOwnershipHandoff.TryBegin(
                identity,
                expectedOwner,
                Time.unscaledTime + AreaOwnershipResponseTimeout))
        {
            return false;
        }

        session.PendingTarget = target;
        session.PendingIdentity = identity;
        session.PendingGrantToken = 0L;
        session.PendingDecision = AreaOwnershipHandoffDecision.None;
        session.GrantDataRevision = 0U;
        session.GrantOwnerRevision = 0;
        session.GrantExpiresAt = 0L;

        try
        {
            ZPackage package = new();
            package.Write(AreaOwnershipProtocolVersion);
            package.Write(requestId);
            package.Write((int)session.Action);
            package.Write(session.Player.GetPlayerID());
            package.Write(session.AnchorId);
            package.Write(zdo.m_uid);
            package.Write(expectedOwner);
            package.Write(zdo.OwnerRevision);
            target.m_nview.InvokeRPC(AreaOwnershipRequestRpc, package);
            return true;
        }
        catch (Exception exception)
        {
            Log.LogWarning($"Area container ownership request failed: {exception.Message}");
            AreaOwnershipHandoff.Cancel();
            session.PendingTarget = null;
            return false;
        }
    }

    private static void ExecuteGrantedAreaContainer(
        AreaContainerTransferSession session,
        Container target)
    {
        AreaOwnershipRequestIdentity identity = AreaOwnershipHandoff.Identity;
        long grantToken = AreaOwnershipHandoff.GrantToken;
        session.PendingTarget = null;
        session.PendingDecision = AreaOwnershipHandoffDecision.None;
        session.NextTargetIndex++;

        int moved = 0;
        try
        {
            target.CheckForChanges();
            if (CanExecuteGrantedAreaContainer(
                    session,
                    target,
                    identity,
                    grantToken))
            {
                moved = ExecuteAreaContainerTransfer(session, target);
            }
        }
        catch (Exception exception)
        {
            Log.LogWarning($"Granted area container transfer failed safely: {exception.Message}");
            FlushAreaTransferInventoriesAfterFailure(session, target);
        }
        finally
        {
            AreaOwnershipHandoff.CompleteExecution();
            session.PendingIdentity = default;
            session.PendingGrantToken = 0L;

            ClearAreaOwnershipLeaseIfMatching(
                target,
                identity,
                grantToken,
                ZNet.instance != null ? ZNet.GetUID() : 0L);
        }

        RecordAreaContainerTransfer(session, target, moved);
    }

    private static bool CanExecuteGrantedAreaContainer(
        AreaContainerTransferSession session,
        Container target,
        AreaOwnershipRequestIdentity identity,
        long grantToken)
    {
        if (target == null ||
            IsUnityNull(target) ||
            target.m_nview == null ||
            !target.m_nview.IsValid() ||
            !target.m_nview.IsOwner())
        {
            return false;
        }

        ZDO? zdo = target.m_nview.GetZDO();
        return zdo != null &&
               zdo.GetOwner() == ZNet.GetUID() &&
               zdo.OwnerRevision == session.GrantOwnerRevision &&
               HasReachedAreaDataRevision(zdo.DataRevision, session.GrantDataRevision) &&
               target.m_lastRevision == zdo.DataRevision &&
               GetNetworkTimeTicks() <= session.GrantExpiresAt &&
               GetAreaOwnershipGrantTokenStatus(
                   zdo,
                   identity,
                   grantToken,
                   ZNet.GetUID()) == AreaOwnershipGrantTokenStatus.Matching &&
               CanUseAreaContainerNow(
                   session.Player,
                   target,
                   session.Anchor,
                   session.Action,
                   requireDirectOwner: true);
    }

    private static int ExecuteAreaContainerTransfer(
        AreaContainerTransferSession session,
        Container target)
    {
        if (session.Action == AreaContainerActionKind.QuickStack)
        {
            List<ItemData> candidates = GetQuickStackCandidates(
                session.Player,
                session.PlayerInventory);
            return QuickStackItemsIntoContainer(
                session.PlayerInventory,
                target.m_inventory,
                candidates);
        }

        List<ItemData> targets = GetRestockTargets(
            session.Player,
            session.PlayerInventory,
            RestockMode.AreaFavoriteRestock);
        return RestockTargetsFromContainer(
            session.PlayerInventory,
            target.m_inventory,
            targets,
            RestockMode.AreaFavoriteRestock);
    }

    private static void RecordAreaContainerTransfer(
        AreaContainerTransferSession session,
        Container target,
        int moved)
    {
        if (moved <= 0)
        {
            return;
        }

        session.TotalMoved += moved;
        session.PlayerInventoryChanged = true;
        session.ChangedContainerVfxCount = TryBroadcastChangedContainerActionSuccessVfx(
            target,
            session.VfxLimit,
            session.ChangedContainerVfxCount);
    }

    private static void FlushAreaTransferInventoriesAfterFailure(
        AreaContainerTransferSession session,
        Container target)
    {
        try
        {
            session.PlayerInventory.Changed();
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Failed to flush player inventory after an area transfer error: {exception.Message}");
        }

        try
        {
            target.m_inventory?.Changed();
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Failed to flush container inventory after an area transfer error: {exception.Message}");
        }
    }

    private static void CompleteAreaContainerTransfer(
        AreaContainerTransferSession session)
    {
        _areaContainerTransfer = null;
        if (session.PlayerInventoryChanged)
        {
            session.PlayerInventory.Changed();
            if (session.VfxLimit > 0)
            {
                BroadcastContainerActionSuccessFx(
                    session.Anchor,
                    ContainerActionSuccessSfxKind);
            }
        }

        if (session.Action == AreaContainerActionKind.QuickStack)
        {
            ShowContainerActionResult(
                session.Player,
                "$inventoryactions_action_stack",
                "Stack",
                session.TotalMoved);
        }
        else
        {
            ShowContainerActionResult(
                session.Player,
                "$inventoryactions_action_take_stacks",
                "Take stacks",
                session.TotalMoved);
        }
    }

    private static void FinishPendingAreaContainerWithoutMutation(
        AreaContainerTransferSession session)
    {
        Container? target = session.PendingTarget;
        AreaOwnershipRequestIdentity identity = session.PendingIdentity;
        long grantToken = session.PendingGrantToken;
        AreaOwnershipHandoff.Cancel();
        session.PendingTarget = null;
        session.PendingIdentity = default;
        session.PendingGrantToken = 0L;
        session.PendingDecision = AreaOwnershipHandoffDecision.None;
        session.NextTargetIndex++;

        if (target != null && !IsUnityNull(target))
        {
            ClearAreaOwnershipLeaseIfMatching(
                target,
                identity,
                grantToken,
                ZNet.instance != null ? ZNet.GetUID() : 0L);
        }
    }

    private static void CancelAreaContainerTransfer()
    {
        AreaContainerTransferSession? session = _areaContainerTransfer;
        Container? target = session?.PendingTarget;
        AreaOwnershipRequestIdentity identity = session?.PendingIdentity ?? default;
        long grantToken = session?.PendingGrantToken ?? 0L;
        Inventory? changedInventory = session?.PlayerInventoryChanged == true
            ? session.PlayerInventory
            : null;

        _areaContainerTransfer = null;
        AreaOwnershipHandoff.Cancel();
        if (session != null)
        {
            session.PendingTarget = null;
            session.PendingIdentity = default;
            session.PendingGrantToken = 0L;
            session.PendingDecision = AreaOwnershipHandoffDecision.None;
        }

        if (target != null && !IsUnityNull(target))
        {
            ClearAreaOwnershipLeaseIfMatching(
                target,
                identity,
                grantToken,
                ZNet.instance != null ? ZNet.GetUID() : 0L);
        }

        if (changedInventory != null)
        {
            try
            {
                changedInventory.Changed();
            }
            catch (Exception exception)
            {
                Log.LogWarning(
                    $"Failed to flush player inventory after cancelling an area transfer: {exception.Message}");
            }
        }
    }

    internal static void RegisterAreaOwnershipRpcs(Container container)
    {
        if (container == null || IsUnityNull(container))
        {
            return;
        }

        if (container.m_nview == null)
        {
            container.m_nview = container.m_rootObjectOverride != null
                ? container.m_rootObjectOverride.GetComponent<ZNetView>()
                : container.GetComponent<ZNetView>();
        }

        if (container.m_nview == null || IsUnityNull(container.m_nview))
        {
            return;
        }

        container.m_nview.Unregister(AreaOwnershipRequestRpc);
        container.m_nview.Unregister(AreaOwnershipResponseRpc);
        container.m_nview.Unregister(ContainerActionSuccessFxRpc);
        container.m_nview.Register<ZPackage>(
            AreaOwnershipRequestRpc,
            (sender, package) => RPC_RequestAreaOwnership(container, sender, package));
        container.m_nview.Register<ZPackage>(
            AreaOwnershipResponseRpc,
            (sender, package) => RPC_AreaOwnershipResponse(container, sender, package));
        container.m_nview.Register<int>(
            ContainerActionSuccessFxRpc,
            (_, effectKind) =>
                RPC_ContainerActionSuccessFx(container, effectKind));
    }

    internal static void UnregisterAreaOwnershipRpcs(Container container)
    {
        if (container == null || IsUnityNull(container))
        {
            return;
        }

        if (container.m_nview != null && !IsUnityNull(container.m_nview))
        {
            container.m_nview.Unregister(AreaOwnershipRequestRpc);
            container.m_nview.Unregister(AreaOwnershipResponseRpc);
            container.m_nview.Unregister(ContainerActionSuccessFxRpc);
        }

        if (_areaContainerTransfer?.PendingTarget == container)
        {
            _areaContainerTransfer.PendingDecision =
                AreaOwnershipHandoffDecision.Unloaded;
        }
    }

    private static void RPC_RequestAreaOwnership(
        Container container,
        long sender,
        ZPackage package)
    {
        if (!TryReadAreaOwnershipRequest(
                package,
                out AreaOwnershipRequestIdentity identity,
                out long requesterPlayerId,
                out ZDOID anchorId,
                out ZDOID targetId,
                out long expectedOwner,
                out ushort expectedOwnerRevision))
        {
            return;
        }

        AreaOwnershipFailure failure = ValidateAreaOwnershipRequest(
            container,
            sender,
            identity,
            requesterPlayerId,
            anchorId,
            targetId,
            expectedOwner,
            expectedOwnerRevision,
            out ZDO? zdo);
        long token = 0L;
        long expiresAt = 0L;
        uint grantDataRevision = 0U;
        ushort grantOwnerRevision = 0;
        bool granted = false;
        if (failure == AreaOwnershipFailure.None && zdo != null)
        {
            expiresAt = GetNetworkTimeTicks() + AreaOwnershipLeaseDurationTicks;
            token = CreateAreaOwnershipGrantToken();
            try
            {
                zdo.Set(AreaOwnershipLeaseRequesterKey, sender);
                zdo.Set(AreaOwnershipLeaseRequestIdKey, identity.RequestId);
                zdo.Set(AreaOwnershipLeaseActionKey, (int)identity.Action);
                zdo.Set(AreaOwnershipLeaseExpiryKey, expiresAt);
                zdo.Set(AreaOwnershipLeaseTokenKey, token);
                grantDataRevision = zdo.DataRevision;
                ZDOMan.instance.ForceSendZDO(sender, zdo.m_uid);
                zdo.SetOwner(sender);
                grantOwnerRevision = zdo.OwnerRevision;
                granted = zdo.GetOwner() == sender;
                if (!granted)
                {
                    failure = AreaOwnershipFailure.Unavailable;
                    ClearAreaOwnershipLeaseIfMatching(
                        container,
                        identity,
                        token,
                        sender);
                }
            }
            catch (Exception exception)
            {
                Log.LogWarning($"Area ownership grant failed safely: {exception.Message}");
                failure = AreaOwnershipFailure.Unavailable;
                granted = false;
                ClearAreaOwnershipLeaseIfMatching(
                    container,
                    identity,
                    token,
                    sender);
            }
        }

        SendAreaOwnershipResponse(
            container,
            sender,
            identity,
            granted,
            failure,
            token,
            grantDataRevision,
            grantOwnerRevision,
            expiresAt);
    }

    private static void RPC_AreaOwnershipResponse(
        Container container,
        long sender,
        ZPackage package)
    {
        if (!TryReadAreaOwnershipResponse(
                package,
                out AreaOwnershipRequestIdentity identity,
                out bool granted,
                out _,
                out long token,
                out uint grantDataRevision,
                out ushort grantOwnerRevision,
                out long expiresAt))
        {
            return;
        }

        AreaContainerTransferSession? session = _areaContainerTransfer;
        if (session == null ||
            session.PendingTarget != container ||
            !AreaOwnershipHandoff.Identity.Equals(identity))
        {
            return;
        }

        AreaOwnershipHandoffPhase previousPhase = AreaOwnershipHandoff.Phase;
        AreaOwnershipHandoffDecision decision =
            AreaOwnershipHandoff.ReceiveResponse(
                identity,
                sender,
                granted,
                token,
                Time.unscaledTime,
                Time.unscaledTime + AreaOwnershipPropagationTimeout);
        if (previousPhase == AreaOwnershipHandoffPhase.AwaitingResponse &&
            AreaOwnershipHandoff.Phase == AreaOwnershipHandoffPhase.AwaitingOwnership)
        {
            session.PendingGrantToken = token;
            session.GrantDataRevision = grantDataRevision;
            session.GrantOwnerRevision = grantOwnerRevision;
            session.GrantExpiresAt = expiresAt;
        }
        else if (decision != AreaOwnershipHandoffDecision.None)
        {
            session.PendingDecision = decision;
        }
    }

    private static AreaOwnershipFailure ValidateAreaOwnershipRequest(
        Container container,
        long sender,
        AreaOwnershipRequestIdentity identity,
        long requesterPlayerId,
        ZDOID anchorId,
        ZDOID targetId,
        long expectedOwner,
        ushort expectedOwnerRevision,
        out ZDO? zdo)
    {
        zdo = null;
        if (container == null ||
            container.m_nview == null ||
            sender == 0L ||
            requesterPlayerId == 0L ||
            identity.RequestId <= 0 ||
            identity.Action is not (AreaContainerActionKind.QuickStack or AreaContainerActionKind.Restock))
        {
            return AreaOwnershipFailure.InvalidRequest;
        }

        zdo = container.m_nview.GetZDO();
        if (zdo == null ||
            zdo.m_uid != targetId ||
            identity.ContainerUserId != targetId.UserID ||
            identity.ContainerObjectId != targetId.ID)
        {
            return AreaOwnershipFailure.InvalidRequest;
        }

        if (HasExternalMultiUserChestActive)
        {
            return AreaOwnershipFailure.Unsupported;
        }

        if (!container.m_nview.IsValid() ||
            !container.m_nview.IsOwner() ||
            zdo.GetOwner() != ZNet.GetUID() ||
            expectedOwner != ZNet.GetUID() ||
            zdo.OwnerRevision != expectedOwnerRevision)
        {
            return AreaOwnershipFailure.NotOwner;
        }

        long existingToken = zdo.GetLong(AreaOwnershipLeaseTokenKey, 0L);
        long existingExpiry = zdo.GetLong(AreaOwnershipLeaseExpiryKey, 0L);
        if (existingToken != 0L && existingExpiry > GetNetworkTimeTicks())
        {
            return AreaOwnershipFailure.Busy;
        }

        if (!TryGetRpcSenderPlayer(
                sender,
                requesterPlayerId,
                out Player? requester) ||
            requester == null)
        {
            return AreaOwnershipFailure.InvalidRequest;
        }

        Container? anchor = ResolveKnownAreaContainer(anchorId);
        if (anchor == null ||
            !IsAreaContainerEligible(anchor) ||
            !HasContainerPlayerAccessForRpc(requesterPlayerId, anchor) ||
            (requester.transform.position - anchor.transform.position).sqrMagnitude >
            AreaOwnershipMaximumAnchorDistance * AreaOwnershipMaximumAnchorDistance)
        {
            return AreaOwnershipFailure.OutOfRange;
        }

        if (!IsAreaContainerEligible(container))
        {
            return AreaOwnershipFailure.Unavailable;
        }

        if (IsContainerInUse(container) || IsContainerInUse(anchor))
        {
            return AreaOwnershipFailure.InUse;
        }

        if (!HasContainerPlayerAccessForRpc(requesterPlayerId, container))
        {
            return AreaOwnershipFailure.NoAccess;
        }

        float range = GetAreaContainerRange(identity.Action);
        if (range <= 0f ||
            (container.transform.position - anchor.transform.position).sqrMagnitude >
            range * range)
        {
            return AreaOwnershipFailure.OutOfRange;
        }

        return AreaOwnershipFailure.None;
    }

    private static bool TryGetRpcSenderPlayer(
        long sender,
        long requesterPlayerId,
        out Player? requester)
    {
        requester = null;
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == null ||
                IsUnityNull(player) ||
                player.GetPlayerID() != requesterPlayerId ||
                player.m_nview == null ||
                !player.m_nview.IsValid())
            {
                continue;
            }

            ZDO? playerZdo = player.m_nview.GetZDO();
            if (playerZdo != null && playerZdo.GetOwner() == sender)
            {
                requester = player;
                return true;
            }
        }

        return false;
    }

    private static bool HasContainerPlayerAccessForRpc(
        long playerId,
        Container container)
    {
        if (container == null || !container.CheckAccess(playerId))
        {
            return false;
        }

        if (!container.m_checkGuardStone)
        {
            return true;
        }

        bool deniedByWard = false;
        foreach (PrivateArea area in PrivateArea.m_allAreas)
        {
            if (area == null ||
                IsUnityNull(area) ||
                !area.IsEnabled() ||
                !area.IsInside(container.transform.position, 0f))
            {
                continue;
            }

            bool permitted = area.m_piece != null &&
                             area.m_piece.GetCreator() == playerId ||
                             area.GetPermittedPlayers().Any(entry => entry.Key == playerId);
            if (permitted)
            {
                return true;
            }

            deniedByWard = true;
        }

        return !deniedByWard;
    }

    private static Container? ResolveKnownAreaContainer(ZDOID id)
    {
        for (int i = Runtime.KnownContainers.Count - 1; i >= 0; i--)
        {
            Container container = Runtime.KnownContainers[i];
            if (container == null || IsUnityNull(container))
            {
                Runtime.KnownContainers.RemoveAt(i);
                continue;
            }

            if (TryGetContainerId(container, out ZDOID candidateId) &&
                candidateId == id)
            {
                return container;
            }
        }

        return null;
    }

    private static bool CanUseAreaContainerNow(
        Player player,
        Container container,
        Container anchor,
        AreaContainerActionKind action,
        bool requireDirectOwner)
    {
        // MUC 0.6.1 does not expose all secondary users or pending positional
        // item RPCs, so even a locally owned chest cannot be proven idle.
        if (HasExternalMultiUserChestActive ||
            player == null ||
            player.m_isLoading ||
            container == null ||
            anchor == null ||
            !IsAreaContainerEligible(container) ||
            !IsAreaContainerEligible(anchor) ||
            IsContainerInUse(container) ||
            IsContainerInUse(anchor) ||
            !HasContainerPlayerAccess(player, container) ||
            !HasContainerPlayerAccess(player, anchor))
        {
            return false;
        }

        if ((player.transform.position - anchor.transform.position).sqrMagnitude >
            AreaOwnershipMaximumAnchorDistance * AreaOwnershipMaximumAnchorDistance)
        {
            return false;
        }

        float range = GetAreaContainerRange(action);
        if (range <= 0f ||
            (container.transform.position - anchor.transform.position).sqrMagnitude >
            range * range)
        {
            return false;
        }

        if (requireDirectOwner)
        {
            return CanMutateContainerDirectly(container);
        }

        return CanMutateContainerDirectly(container) ||
               CanRequestAreaOwnership(container);
    }

    private static bool HasLoadedCurrentContainerRevision(Container container)
    {
        if (container == null ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            !container.m_nview.IsOwner())
        {
            return false;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        return zdo != null &&
               zdo.GetOwner() == ZNet.GetUID() &&
               container.m_lastRevision == zdo.DataRevision;
    }

    private static bool CanRequestAreaOwnership(Container container)
    {
        return !HasExternalMultiUserChestActive &&
               container != null &&
               container.m_nview != null &&
               container.m_nview.IsValid() &&
               container.m_nview.HasOwner() &&
               !container.m_nview.IsOwner();
    }

    private static float GetAreaContainerRange(AreaContainerActionKind action) =>
        action == AreaContainerActionKind.QuickStack
            ? _areaQuickStackRange.Value
            : _areaRestockRange.Value;

    private static AreaOwnershipObservedOwner GetObservedAreaOwnership(
        long observedOwner,
        long localUid,
        long expectedResponder)
    {
        if (observedOwner == 0L)
        {
            return AreaOwnershipObservedOwner.Unknown;
        }

        if (observedOwner == localUid)
        {
            return AreaOwnershipObservedOwner.LocalRequester;
        }

        return observedOwner == expectedResponder
            ? AreaOwnershipObservedOwner.ExpectedResponder
            : AreaOwnershipObservedOwner.Other;
    }

    private static AreaOwnershipGrantTokenStatus GetAreaOwnershipGrantTokenStatus(
        ZDO? zdo,
        AreaOwnershipRequestIdentity identity,
        long grantToken,
        long requesterUid)
    {
        if (zdo == null || grantToken == 0L)
        {
            return AreaOwnershipGrantTokenStatus.Missing;
        }

        long observedToken = zdo.GetLong(AreaOwnershipLeaseTokenKey, 0L);
        if (observedToken == 0L)
        {
            return AreaOwnershipGrantTokenStatus.Missing;
        }

        bool matching = observedToken == grantToken &&
                        zdo.GetLong(AreaOwnershipLeaseRequesterKey, 0L) == requesterUid &&
                        zdo.GetInt(AreaOwnershipLeaseRequestIdKey, 0) == identity.RequestId &&
                        zdo.GetInt(AreaOwnershipLeaseActionKey, 0) == (int)identity.Action;
        return matching
            ? AreaOwnershipGrantTokenStatus.Matching
            : AreaOwnershipGrantTokenStatus.Other;
    }

    private static void ClearAreaOwnershipLeaseIfMatching(
        Container container,
        AreaOwnershipRequestIdentity identity,
        long grantToken,
        long leaseRequesterUid)
    {
        try
        {
            if (container == null ||
                IsUnityNull(container) ||
                leaseRequesterUid == 0L ||
                container.m_nview == null ||
                !container.m_nview.IsValid() ||
                !container.m_nview.IsOwner())
            {
                return;
            }

            ZDO? zdo = container.m_nview.GetZDO();
            if (zdo == null)
            {
                return;
            }

            long observedToken = zdo.GetLong(AreaOwnershipLeaseTokenKey, 0L);
            bool identityMatches = identity.RequestId > 0 &&
                                   zdo.m_uid.UserID == identity.ContainerUserId &&
                                   zdo.m_uid.ID == identity.ContainerObjectId &&
                                   zdo.GetLong(
                                       AreaOwnershipLeaseRequesterKey,
                                       0L) == leaseRequesterUid &&
                                   zdo.GetInt(AreaOwnershipLeaseRequestIdKey, 0) == identity.RequestId &&
                                   zdo.GetInt(AreaOwnershipLeaseActionKey, 0) == (int)identity.Action;
            if (observedToken != 0L &&
                identityMatches &&
                (grantToken == 0L || observedToken == grantToken))
            {
                zdo.Set(AreaOwnershipLeaseTokenKey, 0L);
            }
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Failed to clear an area ownership lease safely: {exception.Message}");
        }
    }

    private static void SendAreaOwnershipResponse(
        Container container,
        long target,
        AreaOwnershipRequestIdentity identity,
        bool granted,
        AreaOwnershipFailure failure,
        long token,
        uint grantDataRevision,
        ushort grantOwnerRevision,
        long expiresAt)
    {
        if (target == 0L ||
            container == null ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return;
        }

        try
        {
            ZPackage package = new();
            package.Write(AreaOwnershipProtocolVersion);
            package.Write(identity.RequestId);
            package.Write(identity.ContainerUserId);
            package.Write(identity.ContainerObjectId);
            package.Write((int)identity.Action);
            package.Write(granted);
            package.Write((int)failure);
            package.Write(token);
            package.Write(grantDataRevision);
            package.Write(grantOwnerRevision);
            package.Write(expiresAt);
            container.m_nview.InvokeRPC(target, AreaOwnershipResponseRpc, package);
        }
        catch (Exception exception)
        {
            Log.LogWarning($"Area ownership response failed: {exception.Message}");
        }
    }

    private static bool TryReadAreaOwnershipRequest(
        ZPackage package,
        out AreaOwnershipRequestIdentity identity,
        out long requesterPlayerId,
        out ZDOID anchorId,
        out ZDOID targetId,
        out long expectedOwner,
        out ushort expectedOwnerRevision)
    {
        identity = default;
        requesterPlayerId = 0L;
        anchorId = ZDOID.None;
        targetId = ZDOID.None;
        expectedOwner = 0L;
        expectedOwnerRevision = 0;
        if (package == null ||
            package.Size() <= 0 ||
            package.Size() > AreaOwnershipMaximumPackageBytes)
        {
            return false;
        }

        try
        {
            int protocol = package.ReadInt();
            int requestId = package.ReadInt();
            AreaContainerActionKind action =
                (AreaContainerActionKind)package.ReadInt();
            requesterPlayerId = package.ReadLong();
            anchorId = package.ReadZDOID();
            targetId = package.ReadZDOID();
            expectedOwner = package.ReadLong();
            expectedOwnerRevision = package.ReadUShort();
            identity = new AreaOwnershipRequestIdentity(
                requestId,
                targetId.UserID,
                targetId.ID,
                action);
            return protocol == AreaOwnershipProtocolVersion &&
                   requestId > 0 &&
                   targetId != ZDOID.None &&
                   package.GetPos() == package.Size();
        }
        catch
        {
            identity = default;
            return false;
        }
    }

    private static bool TryReadAreaOwnershipResponse(
        ZPackage package,
        out AreaOwnershipRequestIdentity identity,
        out bool granted,
        out AreaOwnershipFailure failure,
        out long token,
        out uint grantDataRevision,
        out ushort grantOwnerRevision,
        out long expiresAt)
    {
        identity = default;
        granted = false;
        failure = AreaOwnershipFailure.InvalidRequest;
        token = 0L;
        grantDataRevision = 0U;
        grantOwnerRevision = 0;
        expiresAt = 0L;
        if (package == null ||
            package.Size() <= 0 ||
            package.Size() > AreaOwnershipMaximumPackageBytes)
        {
            return false;
        }

        try
        {
            int protocol = package.ReadInt();
            int requestId = package.ReadInt();
            long containerUserId = package.ReadLong();
            uint containerObjectId = package.ReadUInt();
            AreaContainerActionKind action =
                (AreaContainerActionKind)package.ReadInt();
            granted = package.ReadBool();
            failure = (AreaOwnershipFailure)package.ReadInt();
            token = package.ReadLong();
            grantDataRevision = package.ReadUInt();
            grantOwnerRevision = package.ReadUShort();
            expiresAt = package.ReadLong();
            identity = new AreaOwnershipRequestIdentity(
                requestId,
                containerUserId,
                containerObjectId,
                action);
            return protocol == AreaOwnershipProtocolVersion &&
                   requestId > 0 &&
                   containerUserId != 0L &&
                   containerObjectId != 0U &&
                   package.GetPos() == package.Size();
        }
        catch
        {
            identity = default;
            return false;
        }
    }

    private static bool TryGetContainerId(
        Container container,
        out ZDOID id)
    {
        id = ZDOID.None;
        if (container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return false;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        id = zdo.m_uid;
        return id != ZDOID.None;
    }

    private static int GetNextAreaOwnershipRequestId()
    {
        if (_nextAreaOwnershipRequestId <= 0)
        {
            _nextAreaOwnershipRequestId = 1;
        }

        return _nextAreaOwnershipRequestId++;
    }

    private static long CreateAreaOwnershipGrantToken()
    {
        long token;
        do
        {
            token = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
        }
        while (token == 0L);

        return token;
    }

    private static long GetNetworkTimeTicks() =>
        ZNet.instance != null
            ? ZNet.instance.GetTime().Ticks
            : DateTime.UtcNow.Ticks;

    private static bool HasReachedAreaDataRevision(
        uint observed,
        uint required) =>
        unchecked((int)(observed - required)) >= 0;

    internal static bool TryRejectContainerRequestDuringAreaLease(
        Container container,
        long requesterUid,
        string denialResponseRpc)
    {
        if (container == null ||
            requesterUid == 0L ||
            string.IsNullOrWhiteSpace(denialResponseRpc) ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            !container.m_nview.IsOwner())
        {
            return false;
        }

        ZDO? zdo = container.m_nview.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        long token = zdo.GetLong(AreaOwnershipLeaseTokenKey, 0L);
        if (token == 0L)
        {
            return false;
        }

        long expiresAt = zdo.GetLong(AreaOwnershipLeaseExpiryKey, 0L);
        if (expiresAt <= GetNetworkTimeTicks())
        {
            zdo.Set(AreaOwnershipLeaseTokenKey, 0L);
            return false;
        }

        container.m_nview.InvokeRPC(
            requesterUid,
            denialResponseRpc,
            false);
        return true;
    }
}
