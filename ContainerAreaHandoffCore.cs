using System;

namespace InventorySlots;

internal enum ContainerAreaActionKind { QuickStack = 1, Restock = 2 }
internal enum ContainerAreaHandoffPhase { Idle, AwaitingResponse, AwaitingOwnership, Executing }
internal enum ContainerAreaObservedOwner { Unknown, ExpectedResponder, LocalRequester, Other }
internal enum ContainerAreaGrantTokenStatus { Missing, Matching, Other }
internal enum ContainerAreaHandoffDecision
{
    None, Execute, Denied, Timeout, OwnerChanged, Unloaded, GrantReplaced, Unavailable
}

internal static class ContainerAreaUsePolicy
{
    public static bool AllowsInUseState(bool targetIsAnchor, bool targetInUse,
        bool anchorInUse, bool allowOpenQuickStackAnchor) =>
        (!anchorInUse || allowOpenQuickStackAnchor) &&
        (!targetInUse || targetIsAnchor && allowOpenQuickStackAnchor);
}

internal readonly struct ContainerAreaRequestIdentity : IEquatable<ContainerAreaRequestIdentity>
{
    public ContainerAreaRequestIdentity(int requestId, long containerUserId,
        uint containerObjectId, ContainerAreaActionKind action)
    {
        RequestId = requestId;
        ContainerUserId = containerUserId;
        ContainerObjectId = containerObjectId;
        Action = action;
    }

    public int RequestId { get; }
    public long ContainerUserId { get; }
    public uint ContainerObjectId { get; }
    public ContainerAreaActionKind Action { get; }
    public bool Equals(ContainerAreaRequestIdentity other) =>
        RequestId == other.RequestId && ContainerUserId == other.ContainerUserId &&
        ContainerObjectId == other.ContainerObjectId && Action == other.Action;
    public override bool Equals(object? obj) => obj is ContainerAreaRequestIdentity other && Equals(other);
    public override int GetHashCode()
    {
        unchecked { return (((RequestId * 397) ^ ContainerUserId.GetHashCode()) * 397 ^
                            (int)ContainerObjectId) * 397 ^ (int)Action; }
    }
}

// One outstanding grant only. Item movement is performed by the requester after
// both the grant response and its authoritative ZDO state have arrived.
internal sealed class ContainerAreaHandoffCore
{
    public ContainerAreaHandoffPhase Phase { get; private set; }
    public ContainerAreaRequestIdentity Identity { get; private set; }
    public long ExpectedResponderUid { get; private set; }
    public long GrantToken { get; private set; }
    private float _deadline;

    public bool TryBegin(ContainerAreaRequestIdentity identity, long expectedResponderUid, float responseDeadlineAt)
    {
        if (Phase != ContainerAreaHandoffPhase.Idle || identity.RequestId <= 0 ||
            identity.ContainerUserId == 0 || identity.ContainerObjectId == 0 ||
            identity.Action is not (ContainerAreaActionKind.QuickStack or ContainerAreaActionKind.Restock) ||
            expectedResponderUid == 0)
            return false;
        Identity = identity;
        ExpectedResponderUid = expectedResponderUid;
        GrantToken = 0;
        _deadline = responseDeadlineAt;
        Phase = ContainerAreaHandoffPhase.AwaitingResponse;
        return true;
    }

    public ContainerAreaHandoffDecision ReceiveResponse(ContainerAreaRequestIdentity identity,
        long senderUid, bool granted, long grantToken, float now, float ownershipDeadlineAt)
    {
        if (Phase != ContainerAreaHandoffPhase.AwaitingResponse || !Identity.Equals(identity) ||
            senderUid != ExpectedResponderUid)
            return ContainerAreaHandoffDecision.None;
        if (now >= _deadline) return Finish(ContainerAreaHandoffDecision.Timeout);
        if (!granted || grantToken == 0) return Finish(ContainerAreaHandoffDecision.Denied);
        GrantToken = grantToken;
        _deadline = ownershipDeadlineAt;
        Phase = ContainerAreaHandoffPhase.AwaitingOwnership;
        return ContainerAreaHandoffDecision.None;
    }

    public ContainerAreaHandoffDecision Observe(float now, bool loaded,
        ContainerAreaObservedOwner observedOwner, bool netViewIsOwner,
        ContainerAreaGrantTokenStatus tokenStatus, bool stateSynchronized, bool canExecute)
    {
        if (Phase is ContainerAreaHandoffPhase.Idle or ContainerAreaHandoffPhase.Executing)
            return ContainerAreaHandoffDecision.None;
        if (!loaded) return Finish(ContainerAreaHandoffDecision.Unloaded);
        if (now >= _deadline) return Finish(ContainerAreaHandoffDecision.Timeout);
        if (observedOwner == ContainerAreaObservedOwner.Other)
            return Finish(ContainerAreaHandoffDecision.OwnerChanged);
        if (Phase == ContainerAreaHandoffPhase.AwaitingResponse ||
            observedOwner != ContainerAreaObservedOwner.LocalRequester || !netViewIsOwner)
            return ContainerAreaHandoffDecision.None;
        if (tokenStatus == ContainerAreaGrantTokenStatus.Other)
            return Finish(ContainerAreaHandoffDecision.GrantReplaced);
        if (tokenStatus == ContainerAreaGrantTokenStatus.Missing || !stateSynchronized)
            return ContainerAreaHandoffDecision.None;
        if (!canExecute) return Finish(ContainerAreaHandoffDecision.Unavailable);
        Phase = ContainerAreaHandoffPhase.Executing;
        return ContainerAreaHandoffDecision.Execute;
    }

    public void CompleteExecution()
    {
        if (Phase == ContainerAreaHandoffPhase.Executing) Cancel();
    }

    public void Cancel()
    {
        Phase = ContainerAreaHandoffPhase.Idle;
        Identity = default;
        ExpectedResponderUid = 0;
        GrantToken = 0;
        _deadline = -1;
    }

    private ContainerAreaHandoffDecision Finish(ContainerAreaHandoffDecision decision)
    {
        Cancel();
        return decision;
    }
}
