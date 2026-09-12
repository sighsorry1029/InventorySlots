using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ContainerAreaRequestRpc = "InventorySlots_AreaOwnershipRequestV1";
    private const string ContainerAreaResponseRpc = "InventorySlots_AreaOwnershipResponseV1";
    private const string ContainerAreaLeaseTokenKey = "InventorySlots_AreaLeaseTokenV1";
    private const string ContainerAreaLeaseRequesterKey = "InventorySlots_AreaLeaseRequesterV1";
    private const string ContainerAreaLeaseRequestKey = "InventorySlots_AreaLeaseRequestV1";
    private const string ContainerAreaLeaseActionKey = "InventorySlots_AreaLeaseActionV1";
    private const string ContainerAreaLeaseExpiryKey = "InventorySlots_AreaLeaseExpiryV1";
    private const int ContainerAreaProtocol = 1;
    private const int ContainerAreaMaximumPackageBytes = 256;
    private const float ContainerAreaResponseTimeout = 2.5f;
    private const float ContainerAreaStateTimeout = 3f;
    private const float ContainerAreaAnchorDistance = 8f;
    private static readonly long ContainerAreaLeaseTicks = TimeSpan.FromSeconds(8).Ticks;
    private static readonly ContainerAreaHandoffCore ContainerAreaHandoff = new();
    private static readonly List<Component> ContainerAreaComponents = new();
    private static ContainerAreaSession? _containerAreaSession;
    private static int _nextContainerAreaRequest = 1;

    // These members are private in the unmodified Valheim 1.0.12 assembly.
    // Resolve once, without depending on compile-time publicized visibility.
    private static readonly AccessTools.FieldRef<Container, ZNetView> ContainerAreaView =
        AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
    private static readonly AccessTools.FieldRef<Container, uint> ContainerAreaLoadedRevision =
        AccessTools.FieldRefAccess<Container, uint>("m_lastRevision");
    private static readonly AccessTools.FieldRef<Player, bool> ContainerAreaPlayerLoading =
        AccessTools.FieldRefAccess<Player, bool>("m_isLoading");
    private static readonly AccessTools.FieldRef<InventoryGui, Container> ContainerAreaGuiContainer =
        AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
    private static readonly AccessTools.FieldRef<InventoryGui, Animator> ContainerAreaGuiAnimator =
        AccessTools.FieldRefAccess<InventoryGui, Animator>("m_animator");
    private static readonly Action<Container> RefreshContainerAreaInventory =
        AccessTools.MethodDelegate<Action<Container>>(AccessTools.Method(typeof(Container), "CheckForChanges", Type.EmptyTypes));
    private static readonly Func<Container, long, bool> CheckContainerAreaAccess =
        AccessTools.MethodDelegate<Func<Container, long, bool>>(AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) }));
    private static readonly Action<Inventory, bool, bool> NotifyContainerAreaInventory =
        AccessTools.MethodDelegate<Action<Inventory, bool, bool>>(AccessTools.Method(typeof(Inventory), "Changed", new[] { typeof(bool), typeof(bool) }));
    private static readonly FieldInfo ContainerAreaWards = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
    private static readonly Func<PrivateArea, bool> IsContainerAreaWardEnabled =
        AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "IsEnabled", Type.EmptyTypes));
    private static readonly Func<PrivateArea, Vector3, float, bool> IsInsideContainerAreaWard =
        AccessTools.MethodDelegate<Func<PrivateArea, Vector3, float, bool>>(AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) }));
    private static readonly Func<PrivateArea, List<KeyValuePair<long, string>>> ContainerAreaWardPlayers =
        AccessTools.MethodDelegate<Func<PrivateArea, List<KeyValuePair<long, string>>>>(AccessTools.Method(typeof(PrivateArea), "GetPermittedPlayers", Type.EmptyTypes));

    private enum ContainerAreaFailure : byte
    {
        None, InvalidRequest, NotOwner, InUse, NoAccess, OutOfRange, Unavailable, Unsupported, Busy
    }

    private sealed class ContainerAreaSession
    {
        public Player Player = null!;
        public Inventory Inventory = null!;
        public Container Anchor = null!;
        public ZDOID AnchorId;
        public bool QuickStack;
        public bool OpenAnchor;
        public List<Container> Targets = null!;
        public int Next;
        public int Moved;
        public int Effects;
        public Container? Pending;
        public ContainerAreaRequestIdentity PendingIdentity;
        public long Token;
        public uint DataRevision;
        public ushort OwnerRevision;
        public long Expires;
        public ContainerAreaHandoffDecision Decision;
    }

    internal static bool IsContainerAreaTransferActive() => _containerAreaSession != null;

    internal static bool IsContainerAreaEligible(Container container)
    {
        ZNetView? view = GetContainerAreaView(container);
        if (view == null || !view.IsValid() || container.GetInventory() == null || container.GetType() != typeof(Container) ||
            container.m_wagon != null || container.GetComponent<TombStone>() != null ||
            container.GetComponentInParent<TombStone>() != null || view.GetComponent<Player>() != null ||
            container.transform.root.GetComponentInChildren<Ship>() != null ||
            view.GetZDO().GetBool("MUC_Ignore", false))
            return false;
        Piece? piece = container.GetComponent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer()) return false;
        container.GetComponents(ContainerAreaComponents);
        foreach (Component component in ContainerAreaComponents)
        {
            string type = component != null ? component.GetType().FullName ?? "" : "";
            if (type == "DrawerContainer" || type == "OdinShip.ShipContainer")
            {
                ContainerAreaComponents.Clear();
                return false;
            }
        }
        ContainerAreaComponents.Clear();
        return true;
    }

    internal static bool CanRequestContainerAreaOwnership(Container container)
    {
        ZNetView? view = GetContainerAreaView(container);
        return !HasExternalMultiUserChestActive && IsContainerAreaEligible(container) &&
               view != null && view.HasOwner() && !view.IsOwner();
    }

    internal static bool TryStartContainerAreaTransfer(Player player, Inventory inventory, Container anchor, bool quickStack)
    {
        if (_containerAreaSession != null ||
            player == null || inventory == null || anchor == null || ContainerAreaPlayerLoading(player))
            return false;

        ContainerAreaSession session = new()
        {
            Player = player, Inventory = inventory, Anchor = anchor,
            AnchorId = GetContainerAreaView(anchor)?.GetZDO()?.m_uid ?? ZDOID.None,
            QuickStack = quickStack, OpenAnchor = quickStack && IsOpenContainerAreaAnchor(anchor)
        };
        if (!CanUseContainerAreaTarget(session, anchor, requireOwner: false)) return false;
        session.Targets = GetActionContainers(player, anchor, quickStack, includeRemote: true);
        if (session.Targets.Count == 0) return false;
        _containerAreaSession = session;
        ContinueContainerAreaTransfer();
        return true;
    }

    internal static void UpdateContainerAreaTransfer(Player? player)
    {
        ContainerAreaSession? session = _containerAreaSession;
        if (session == null) return;
        if (player == null || player != session.Player || player != Player.m_localPlayer ||
            ContainerAreaPlayerLoading(player) || player.IsDead() || player.InCutscene() || player.IsTeleporting() ||
            ((Humanoid)player).GetInventory() != session.Inventory || !ContainerAreaSessionContextValid(session))
        {
            CancelContainerAreaTransfer();
            return;
        }
        if (session.Decision != ContainerAreaHandoffDecision.None)
        {
            FinishPendingContainerAreaTarget(session);
            ContinueContainerAreaTransfer();
            return;
        }
        Container? target = session.Pending;
        if (target == null || ContainerAreaHandoff.Phase == ContainerAreaHandoffPhase.Idle)
        {
            // Unity's destroyed-object null comparison does not clear the C#
            // reference. Release its handoff even without an OnDestroyed RPC.
            if (!ReferenceEquals(target, null) || ContainerAreaHandoff.Phase != ContainerAreaHandoffPhase.Idle)
                FinishPendingContainerAreaTarget(session);
            ContinueContainerAreaTransfer();
            return;
        }
        ZNetView? view = GetContainerAreaView(target);
        ZDO? zdo = view != null && view.IsValid() ? view.GetZDO() : null;
        long localUid = ZNet.GetUID();
        long owner = zdo?.GetOwner() ?? 0;
        ContainerAreaObservedOwner observed = owner == 0 ? ContainerAreaObservedOwner.Unknown :
            owner == localUid ? ContainerAreaObservedOwner.LocalRequester :
            owner == ContainerAreaHandoff.ExpectedResponderUid ? ContainerAreaObservedOwner.ExpectedResponder :
            ContainerAreaObservedOwner.Other;
        bool dataArrived = zdo != null && HasContainerAreaDataRevision(zdo.DataRevision, session.DataRevision);
        bool synchronized = dataArrived && zdo!.OwnerRevision == session.OwnerRevision;
        ContainerAreaGrantTokenStatus token = dataArrived
            ? GetContainerAreaTokenStatus(zdo, session.PendingIdentity, session.Token, localUid)
            : ContainerAreaGrantTokenStatus.Missing;
        // A later ownership generation cannot validate an earlier grant, even if
        // the same peer acquired the chest again before this response arrived.
        if (ContainerAreaHandoff.Phase == ContainerAreaHandoffPhase.AwaitingOwnership && zdo != null &&
            unchecked((short)(zdo.OwnerRevision - session.OwnerRevision)) > 0)
            token = ContainerAreaGrantTokenStatus.Other;
        ContainerAreaHandoffDecision decision = ContainerAreaHandoff.Observe(
            Time.unscaledTime, zdo != null, observed, view != null && view.IsOwner(), token, synchronized,
            synchronized && view != null && view.IsOwner() && token == ContainerAreaGrantTokenStatus.Matching &&
            ContainerAreaTime() <= session.Expires && CanUseContainerAreaTarget(session, target, requireOwner: true));
        if (decision == ContainerAreaHandoffDecision.None) return;
        if (decision == ContainerAreaHandoffDecision.Execute)
            ExecuteGrantedContainerAreaTarget(session, target);
        else
            FinishPendingContainerAreaTarget(session);
        ContinueContainerAreaTransfer();
    }

    private static void ContinueContainerAreaTransfer()
    {
        ContainerAreaSession? session = _containerAreaSession;
        if (session == null || session.Pending != null) return;
        while (_containerAreaSession == session && session.Next < session.Targets.Count)
        {
            Container target = session.Targets[session.Next];
            if (!CanUseContainerAreaTarget(session, target, requireOwner: false))
            {
                session.Next++;
                continue;
            }
            ZNetView? view = GetContainerAreaView(target);
            if (view == null || !view.IsValid() || view.IsOwner())
            {
                // Advance before callbacks; a partial transfer is never retried.
                session.Next++;
                ExecuteOwnedContainerAreaTarget(session, target);
                continue;
            }
            if (TryRequestContainerAreaTarget(session, target, view)) return;
            session.Next++;
        }
        if (_containerAreaSession == session)
        {
            _containerAreaSession = null;
            ContainerAreaHandoff.Cancel();
            CompleteContainerAreaTransfer(session.Player, session.Anchor, session.QuickStack, session.Moved);
        }
    }

    private static void ExecuteOwnedContainerAreaTarget(ContainerAreaSession session, Container target)
    {
        ZNetView? view = GetContainerAreaView(target);
        ZDO? zdo = view != null && view.IsValid() ? view.GetZDO() : null;
        ContainerAreaRequestIdentity identity = default;
        long token = 0;
        try
        {
            bool openAnchor = target == session.Anchor && session.OpenAnchor && IsOpenContainerAreaAnchor(target);
            // The already-open vanilla anchor is authoritative. Loading/revision
            // checks for unattended chests must not discard this first target.
            if (!openAnchor && zdo != null)
            {
                RefreshContainerAreaInventory(target);
                if (!CanUseContainerAreaTarget(session, target, requireOwner: true) ||
                    ContainerAreaLoadedRevision(target) != zdo.DataRevision) return;
            }
            if (zdo != null && !HasExternalMultiUserChestActive)
            {
                if (HasContainerAreaLease(zdo)) return;
                identity = NewContainerAreaIdentity(zdo.m_uid, session.QuickStack);
                token = NewContainerAreaToken();
                WriteContainerAreaLease(zdo, identity, ZNet.GetUID(), token, ContainerAreaTime() + ContainerAreaLeaseTicks);
            }
            int moved = ExecuteContainerAreaTransfer(session.Player, session.Inventory, target, session.QuickStack);
            if (moved > 0) FlushContainerAreaTransfer(session, target);
            RecordContainerAreaTarget(session, target, moved);
        }
        catch (Exception error)
        {
            Log.LogWarning($"Area container transfer stopped after a callback error; it will not be retried: {error.Message}");
            FlushContainerAreaTransfer(session, target);
        }
        finally { ClearContainerAreaLease(target, identity, token); }
    }

    private static bool TryRequestContainerAreaTarget(ContainerAreaSession session, Container target, ZNetView view)
    {
        if (!CanRequestContainerAreaOwnership(target) || !IsContainerAreaEligible(session.Anchor) ||
            session.AnchorId == ZDOID.None) return false;
        ZDO zdo = view.GetZDO();
        ContainerAreaRequestIdentity identity = NewContainerAreaIdentity(zdo.m_uid, session.QuickStack);
        if (!ContainerAreaHandoff.TryBegin(identity, zdo.GetOwner(), Time.unscaledTime + ContainerAreaResponseTimeout))
            return false;
        session.Pending = target;
        session.PendingIdentity = identity;
        session.Token = 0;
        session.DataRevision = 0;
        session.OwnerRevision = 0;
        session.Expires = 0;
        session.Decision = ContainerAreaHandoffDecision.None;
        try
        {
            ZPackage package = new();
            package.Write(ContainerAreaProtocol);
            package.Write(identity.RequestId);
            package.Write((byte)identity.Action);
            package.Write(session.Player.GetPlayerID());
            package.Write(session.AnchorId);
            package.Write(zdo.m_uid);
            package.Write(zdo.GetOwner());
            package.Write(zdo.OwnerRevision);
            view.InvokeRPC(ContainerAreaRequestRpc, package);
        }
        catch (Exception error)
        {
            // No item was moved. A grant may nevertheless be in flight; retain
            // this pending request until its bounded wait expires.
            Log.LogWarning($"Area ownership request send failed; awaiting its outcome: {error.Message}");
        }
        return true;
    }

    private static void ExecuteGrantedContainerAreaTarget(ContainerAreaSession session, Container target)
    {
        ContainerAreaRequestIdentity identity = session.PendingIdentity;
        long token = session.Token;
        session.Pending = null;
        session.Next++;
        try
        {
            RefreshContainerAreaInventory(target);
            ZNetView? view = GetContainerAreaView(target);
            ZDO? zdo = view?.GetZDO();
            if (zdo == null || view == null || !view.IsOwner() ||
                zdo.OwnerRevision != session.OwnerRevision ||
                !HasContainerAreaDataRevision(zdo.DataRevision, session.DataRevision) ||
                ContainerAreaLoadedRevision(target) != zdo.DataRevision || ContainerAreaTime() > session.Expires ||
                GetContainerAreaTokenStatus(zdo, identity, token, ZNet.GetUID()) != ContainerAreaGrantTokenStatus.Matching ||
                !CanUseContainerAreaTarget(session, target, requireOwner: true)) return;
            int moved = ExecuteContainerAreaTransfer(session.Player, session.Inventory, target, session.QuickStack);
            if (moved > 0) FlushContainerAreaTransfer(session, target);
            RecordContainerAreaTarget(session, target, moved);
        }
        catch (Exception error)
        {
            Log.LogWarning($"Granted area container transfer stopped after a callback error; it will not be retried: {error.Message}");
            FlushContainerAreaTransfer(session, target);
        }
        finally
        {
            ContainerAreaHandoff.CompleteExecution();
            ClearContainerAreaLease(target, identity, token);
            session.PendingIdentity = default;
            session.Token = 0;
        }
    }

    private static void RecordContainerAreaTarget(ContainerAreaSession session, Container target, int moved)
    {
        if (moved <= 0) return;
        session.Moved += moved;
        session.Effects = TryBroadcastChangedContainerActionSuccessVfx(target,
            IsContainerActionSuccessFxEnabled() ? ContainerActionSuccessVfxLimit : 0, session.Effects);
    }

    private static void FlushContainerAreaTransfer(ContainerAreaSession session, Container target)
    {
        try { NotifyContainerAreaInventory(session.Inventory, false, false); }
        catch (Exception error) { Log.LogWarning($"Area player inventory flush failed: {error.Message}"); }
        try
        {
            if (target != null && GetContainerAreaView(target)?.IsOwner() == true)
                NotifyContainerAreaInventory(target.GetInventory(), false, false);
        }
        catch (Exception error) { Log.LogWarning($"Area chest inventory flush failed: {error.Message}"); }
    }

    private static void FinishPendingContainerAreaTarget(ContainerAreaSession session)
    {
        ClearContainerAreaLease(session.Pending, session.PendingIdentity, session.Token);
        if (!ReferenceEquals(session.Pending, null)) session.Next++;
        session.Pending = null;
        session.PendingIdentity = default;
        session.Token = 0;
        session.Decision = ContainerAreaHandoffDecision.None;
        ContainerAreaHandoff.Cancel();
    }

    internal static void CancelContainerAreaTransfer()
    {
        ContainerAreaSession? session = _containerAreaSession;
        _containerAreaSession = null;
        if (session != null)
        {
            FinishPendingContainerAreaTarget(session);
            if (session.Player != null && session.Moved > 0)
                CompleteContainerAreaTransfer(session.Player, session.Anchor, session.QuickStack, session.Moved);
        }
        ContainerAreaHandoff.Cancel();
    }

    internal static void RegisterContainerAreaOwnershipRpcs(Container container)
    {
        ZNetView? view = GetContainerAreaView(container);
        if (view == null) return;
        view.Unregister(ContainerAreaRequestRpc);
        view.Unregister(ContainerAreaResponseRpc);
        view.Register<ZPackage>(ContainerAreaRequestRpc, (sender, package) => RPC_RequestContainerAreaOwnership(container, sender, package));
        view.Register<ZPackage>(ContainerAreaResponseRpc, (sender, package) => RPC_ContainerAreaOwnershipResponse(container, sender, package));
    }

    internal static void UnregisterContainerAreaOwnershipRpcs(Container container)
    {
        ZNetView? view = GetContainerAreaView(container);
        if (view != null)
        {
            view.Unregister(ContainerAreaRequestRpc);
            view.Unregister(ContainerAreaResponseRpc);
        }
        ContainerAreaSession? session = _containerAreaSession;
        if (session?.Anchor == container) CancelContainerAreaTransfer();
        else if (session?.Pending == container) session.Decision = ContainerAreaHandoffDecision.Unloaded;
    }

    private static void RPC_RequestContainerAreaOwnership(Container container, long sender, ZPackage package)
    {
        if (!TryReadContainerAreaRequest(package, out ContainerAreaRequestIdentity identity, out long playerId,
                out ZDOID anchorId, out ZDOID targetId, out long expectedOwner, out ushort expectedRevision)) return;
        ZNetView? view = GetContainerAreaView(container);
        ZDO? zdo = view?.GetZDO();
        ContainerAreaFailure failure = ValidateContainerAreaRequest(container, sender, identity, playerId,
            anchorId, targetId, expectedOwner, expectedRevision);
        long token = 0;
        long expiry = 0;
        uint dataRevision = 0;
        ushort ownerRevision = 0;
        if (failure == ContainerAreaFailure.None && view != null && zdo != null)
        {
            try
            {
                RefreshContainerAreaInventory(container);
                if (!view.IsOwner() || zdo.OwnerRevision != expectedRevision || ContainerAreaLoadedRevision(container) != zdo.DataRevision)
                    failure = ContainerAreaFailure.NotOwner;
                else
                {
                    token = NewContainerAreaToken();
                    expiry = ContainerAreaTime() + ContainerAreaLeaseTicks;
                    WriteContainerAreaLease(zdo, identity, sender, token, expiry);
                    dataRevision = zdo.DataRevision;
                    ZDOMan.instance.ForceSendZDO(sender, zdo.m_uid);
                    zdo.SetOwner(sender);
                    ownerRevision = zdo.OwnerRevision;
                    if (zdo.GetOwner() != sender) failure = ContainerAreaFailure.NotOwner;
                }
            }
            catch (Exception error)
            {
                Log.LogWarning($"Area ownership grant failed: {error.Message}");
                failure = ContainerAreaFailure.Unavailable;
            }
        }
        if (failure != ContainerAreaFailure.None) ClearContainerAreaLease(container, identity, token, sender);
        if (view == null || !view.IsValid() || sender == 0) return;
        ZPackage response = new();
        response.Write(ContainerAreaProtocol);
        response.Write(identity.RequestId);
        response.Write(targetId);
        response.Write((byte)identity.Action);
        response.Write((byte)failure);
        response.Write(token);
        response.Write(dataRevision);
        response.Write(ownerRevision);
        response.Write(expiry);
        view.InvokeRPC(sender, ContainerAreaResponseRpc, response);
    }

    private static ContainerAreaFailure ValidateContainerAreaRequest(Container target, long sender,
        ContainerAreaRequestIdentity identity, long playerId, ZDOID anchorId, ZDOID targetId,
        long expectedOwner, ushort expectedRevision)
    {
        if (HasExternalMultiUserChestActive) return ContainerAreaFailure.Unsupported;
        ZNetView? view = GetContainerAreaView(target);
        ZDO? zdo = view?.GetZDO();
        if (sender == 0 || playerId == 0 || identity.RequestId <= 0 || zdo == null || zdo.m_uid != targetId)
            return ContainerAreaFailure.InvalidRequest;
        if (view == null || !view.IsValid() || !view.IsOwner() || expectedOwner != ZNet.GetUID() ||
            zdo.GetOwner() != expectedOwner || zdo.OwnerRevision != expectedRevision)
            return ContainerAreaFailure.NotOwner;
        if (HasContainerAreaLease(zdo)) return ContainerAreaFailure.Busy;
        Player? requester = Player.GetAllPlayers().FirstOrDefault(player =>
            player != null && player.GetPlayerID() == playerId &&
            player.GetComponent<ZNetView>()?.GetZDO()?.GetOwner() == sender);
        if (requester == null) return ContainerAreaFailure.InvalidRequest;
        Container? anchor = InventoryContainers.KnownContainers.FirstOrDefault(candidate =>
            candidate != null && GetContainerAreaView(candidate)?.GetZDO()?.m_uid == anchorId);
        if (anchor == null || !IsContainerAreaEligible(anchor) || !IsContainerAreaEligible(target))
            return ContainerAreaFailure.Unavailable;
        if ((requester.transform.position - anchor.transform.position).sqrMagnitude > ContainerAreaAnchorDistance * ContainerAreaAnchorDistance ||
            (target.transform.position - anchor.transform.position).sqrMagnitude >
            Math.Pow(ContainerAreaRange(identity.Action == ContainerAreaActionKind.QuickStack), 2))
            return ContainerAreaFailure.OutOfRange;
        bool requesterOwnsAnchor = identity.Action == ContainerAreaActionKind.QuickStack &&
                                  GetContainerAreaView(anchor)?.GetZDO()?.GetOwner() == sender;
        if (!ContainerAreaUsePolicy.AllowsInUseState(target == anchor, IsContainerInUse(target),
                IsContainerInUse(anchor), requesterOwnsAnchor)) return ContainerAreaFailure.InUse;
        if (!HasContainerAreaRequesterAccess(playerId, anchor) || !HasContainerAreaRequesterAccess(playerId, target))
            return ContainerAreaFailure.NoAccess;
        return ContainerAreaFailure.None;
    }

    private static void RPC_ContainerAreaOwnershipResponse(Container container, long sender, ZPackage package)
    {
        ContainerAreaSession? session = _containerAreaSession;
        if (session == null || session.Pending != container || package == null ||
            package.Size() <= 0 || package.Size() > ContainerAreaMaximumPackageBytes) return;
        try
        {
            package.SetPos(0);
            int version = package.ReadInt();
            int request = package.ReadInt();
            ZDOID id = package.ReadZDOID();
            ContainerAreaActionKind action = (ContainerAreaActionKind)package.ReadByte();
            ContainerAreaFailure failure = (ContainerAreaFailure)package.ReadByte();
            long token = package.ReadLong();
            uint dataRevision = package.ReadUInt();
            ushort ownerRevision = package.ReadUShort();
            long expiry = package.ReadLong();
            if (version != ContainerAreaProtocol || package.GetPos() != package.Size() ||
                !Enum.IsDefined(typeof(ContainerAreaFailure), failure)) return;
            ContainerAreaHandoffPhase previous = ContainerAreaHandoff.Phase;
            ContainerAreaHandoffDecision decision = ContainerAreaHandoff.ReceiveResponse(
                new ContainerAreaRequestIdentity(request, id.UserID, id.ID, action), sender,
                failure == ContainerAreaFailure.None && expiry > ContainerAreaTime(), token,
                Time.unscaledTime, Time.unscaledTime + ContainerAreaStateTimeout);
            if (previous == ContainerAreaHandoffPhase.AwaitingResponse &&
                ContainerAreaHandoff.Phase == ContainerAreaHandoffPhase.AwaitingOwnership)
            {
                session.Token = token;
                session.DataRevision = dataRevision;
                session.OwnerRevision = ownerRevision;
                session.Expires = expiry;
            }
            else if (decision != ContainerAreaHandoffDecision.None)
            {
                session.Decision = decision;
                Log.LogDebug($"Area ownership request {session.PendingIdentity.RequestId} was not granted: {failure} ({decision}).");
            }
        }
        catch (Exception error) when (error is System.IO.IOException || error is ArgumentException) { }
    }

    private static bool TryReadContainerAreaRequest(ZPackage package, out ContainerAreaRequestIdentity identity,
        out long playerId, out ZDOID anchor, out ZDOID target, out long owner, out ushort ownerRevision)
    {
        identity = default;
        playerId = owner = 0;
        anchor = target = ZDOID.None;
        ownerRevision = 0;
        if (package == null || package.Size() <= 0 || package.Size() > ContainerAreaMaximumPackageBytes) return false;
        try
        {
            package.SetPos(0);
            int version = package.ReadInt();
            int request = package.ReadInt();
            ContainerAreaActionKind action = (ContainerAreaActionKind)package.ReadByte();
            playerId = package.ReadLong();
            anchor = package.ReadZDOID();
            target = package.ReadZDOID();
            owner = package.ReadLong();
            ownerRevision = package.ReadUShort();
            identity = new ContainerAreaRequestIdentity(request, target.UserID, target.ID, action);
            return version == ContainerAreaProtocol && request > 0 && playerId != 0 && owner != 0 &&
                   anchor != ZDOID.None && target != ZDOID.None &&
                   action is ContainerAreaActionKind.QuickStack or ContainerAreaActionKind.Restock &&
                   package.GetPos() == package.Size();
        }
        catch (Exception error) when (error is System.IO.IOException || error is ArgumentException) { return false; }
    }

    private static bool ContainerAreaSessionContextValid(ContainerAreaSession session)
    {
        if (session.Anchor == null || session.Player == null ||
            (session.Player.transform.position - session.Anchor.transform.position).sqrMagnitude >
            ContainerAreaAnchorDistance * ContainerAreaAnchorDistance) return false;
        if (!InventoryGui.IsVisible()) return true;
        return session.OpenAnchor && IsOpenContainerAreaAnchor(session.Anchor);
    }

    private static bool CanUseContainerAreaTarget(ContainerAreaSession session, Container target, bool requireOwner)
    {
        if (target == null || !ContainerAreaSessionContextValid(session) || target.GetInventory() == null ||
            !HasContainerPlayerAccess(session.Player, session.Anchor, flashGuardStone: false) ||
            !HasContainerPlayerAccess(session.Player, target, flashGuardStone: false)) return false;
        bool anchor = target == session.Anchor;
        bool openAnchor = session.OpenAnchor && IsOpenContainerAreaAnchor(session.Anchor);
        if (!ContainerAreaUsePolicy.AllowsInUseState(anchor, IsContainerInUse(target),
                IsContainerInUse(session.Anchor), openAnchor)) return false;
        if (!anchor && (!IsContainerAreaEligible(target) ||
            (target.transform.position - session.Anchor.transform.position).sqrMagnitude >
            Math.Pow(ContainerAreaRange(session.QuickStack), 2))) return false;
        ZNetView? view = GetContainerAreaView(target);
        if (view == null || !view.IsValid()) return anchor && CanMutateContainerDirectly(target, allowLocalWithoutZNetView: true);
        if (view.IsOwner()) return true;
        return !requireOwner && IsContainerAreaEligible(session.Anchor) && CanRequestContainerAreaOwnership(target);
    }

    private static bool IsOpenContainerAreaAnchor(Container anchor)
    {
        InventoryGui? gui = InventoryGui.instance;
        Animator? animator = gui != null ? ContainerAreaGuiAnimator(gui) : null;
        return gui != null && animator != null && animator.GetBool("visible") &&
               ContainerAreaGuiContainer(gui) == anchor && CanMutateContainerDirectly(anchor, allowLocalWithoutZNetView: true);
    }

    private static bool HasContainerAreaRequesterAccess(long playerId, Container container)
    {
        if (!CheckContainerAreaAccess(container, playerId)) return false;
        if (!container.m_checkGuardStone) return true;
        bool guarded = false;
        foreach (PrivateArea ward in (List<PrivateArea>)ContainerAreaWards.GetValue(null))
        {
            if (ward == null || !IsContainerAreaWardEnabled(ward) ||
                !IsInsideContainerAreaWard(ward, container.transform.position, 0f)) continue;
            guarded = true;
            Piece? piece = ward.GetComponent<Piece>();
            if (piece != null && piece.GetCreator() == playerId ||
                ContainerAreaWardPlayers(ward).Any(entry => entry.Key == playerId)) return true;
        }
        return !guarded;
    }

    private static ZNetView? GetContainerAreaView(Container? container) =>
        container != null ? ContainerAreaView(container) : null;
    private static float ContainerAreaRange(bool quickStack) =>
        Math.Max(0f, quickStack ? _areaQuickStackRange?.Value ?? 0f : _areaRestockRange?.Value ?? 0f);
    private static long ContainerAreaTime() => ZNet.instance != null ? ZNet.instance.GetTime().Ticks : DateTime.UtcNow.Ticks;
    private static bool HasContainerAreaDataRevision(uint observed, uint required) => unchecked((int)(observed - required)) >= 0;

    private static ContainerAreaRequestIdentity NewContainerAreaIdentity(ZDOID id, bool quickStack)
    {
        if (_nextContainerAreaRequest <= 0) _nextContainerAreaRequest = 1;
        return new ContainerAreaRequestIdentity(_nextContainerAreaRequest++, id.UserID, id.ID,
            quickStack ? ContainerAreaActionKind.QuickStack : ContainerAreaActionKind.Restock);
    }

    private static long NewContainerAreaToken()
    {
        long token;
        do { token = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0); } while (token == 0);
        return token;
    }

    private static void WriteContainerAreaLease(ZDO zdo, ContainerAreaRequestIdentity identity, long requester,
        long token, long expires)
    {
        zdo.Set(ContainerAreaLeaseRequesterKey, requester);
        zdo.Set(ContainerAreaLeaseRequestKey, identity.RequestId);
        zdo.Set(ContainerAreaLeaseActionKey, (int)identity.Action);
        zdo.Set(ContainerAreaLeaseExpiryKey, expires);
        zdo.Set(ContainerAreaLeaseTokenKey, token);
    }

    private static bool HasContainerAreaLease(ZDO zdo) => zdo.GetLong(ContainerAreaLeaseTokenKey) != 0 &&
        zdo.GetLong(ContainerAreaLeaseExpiryKey) > ContainerAreaTime();

    private static ContainerAreaGrantTokenStatus GetContainerAreaTokenStatus(ZDO? zdo,
        ContainerAreaRequestIdentity identity, long token, long requester)
    {
        if (zdo == null || token == 0 || zdo.GetLong(ContainerAreaLeaseTokenKey) == 0)
            return ContainerAreaGrantTokenStatus.Missing;
        return zdo.GetLong(ContainerAreaLeaseTokenKey) == token &&
               zdo.GetLong(ContainerAreaLeaseRequesterKey) == requester &&
               zdo.GetInt(ContainerAreaLeaseRequestKey) == identity.RequestId &&
               zdo.GetInt(ContainerAreaLeaseActionKey) == (int)identity.Action
            ? ContainerAreaGrantTokenStatus.Matching : ContainerAreaGrantTokenStatus.Other;
    }

    private static void ClearContainerAreaLease(Container? container, ContainerAreaRequestIdentity identity,
        long token, long requester = 0)
    {
        ZNetView? view = GetContainerAreaView(container);
        if (view == null || !view.IsValid() || !view.IsOwner() || identity.RequestId <= 0) return;
        ZDO zdo = view.GetZDO();
        requester = requester == 0 ? ZNet.GetUID() : requester;
        // With a lost response, cleanup is allowed only for our own request's
        // observed lease. Never clear a later grant or another player's lease.
        long observedToken = zdo.GetLong(ContainerAreaLeaseTokenKey);
        if (observedToken != 0 && (token == 0 || token == observedToken) &&
            zdo.m_uid.UserID == identity.ContainerUserId && zdo.m_uid.ID == identity.ContainerObjectId &&
            zdo.GetLong(ContainerAreaLeaseRequesterKey) == requester &&
            zdo.GetInt(ContainerAreaLeaseRequestKey) == identity.RequestId &&
            zdo.GetInt(ContainerAreaLeaseActionKey) == (int)identity.Action)
            zdo.Set(ContainerAreaLeaseTokenKey, 0L);
    }

    internal static bool TryRejectContainerRequestDuringAreaLease(Container container, long requesterUid, string responseRpc)
    {
        ZNetView? view = GetContainerAreaView(container);
        if (requesterUid == 0 || view == null || !view.IsValid() || !view.IsOwner() ||
            !HasContainerAreaLease(view.GetZDO())) return false;
        view.InvokeRPC(requesterUid, responseRpc, false);
        return true;
    }
}
