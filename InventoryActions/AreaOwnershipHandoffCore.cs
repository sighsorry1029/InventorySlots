using System;

namespace InventoryActions;

internal enum AreaContainerActionKind
{
    QuickStack = 1,
    Restock = 2
}

internal enum AreaOwnershipHandoffPhase
{
    Idle,
    AwaitingResponse,
    AwaitingOwnership,
    Executing
}

internal enum AreaOwnershipObservedOwner
{
    Unknown,
    ExpectedResponder,
    LocalRequester,
    Other
}

internal enum AreaOwnershipGrantTokenStatus
{
    Missing,
    Matching,
    Other
}

internal enum AreaOwnershipHandoffDecision
{
    None,
    Execute,
    Denied,
    Timeout,
    OwnerChanged,
    Unloaded,
    GrantReplaced,
    Unavailable
}

internal readonly struct AreaOwnershipRequestIdentity : IEquatable<AreaOwnershipRequestIdentity>
{
    public AreaOwnershipRequestIdentity(
        int requestId,
        long containerUserId,
        uint containerObjectId,
        AreaContainerActionKind action)
    {
        RequestId = requestId;
        ContainerUserId = containerUserId;
        ContainerObjectId = containerObjectId;
        Action = action;
    }

    public int RequestId { get; }
    public long ContainerUserId { get; }
    public uint ContainerObjectId { get; }
    public AreaContainerActionKind Action { get; }

    public bool Equals(AreaOwnershipRequestIdentity other) =>
        RequestId == other.RequestId &&
        ContainerUserId == other.ContainerUserId &&
        ContainerObjectId == other.ContainerObjectId &&
        Action == other.Action;

    public override bool Equals(object? obj) =>
        obj is AreaOwnershipRequestIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = RequestId;
            hash = (hash * 397) ^ ContainerUserId.GetHashCode();
            hash = (hash * 397) ^ (int)ContainerObjectId;
            hash = (hash * 397) ^ (int)Action;
            return hash;
        }
    }
}

internal sealed class AreaOwnershipHandoffCore
{
    private AreaOwnershipRequestIdentity _identity;
    private long _expectedResponderUid;
    private float _deadlineAt = -1f;

    public AreaOwnershipHandoffPhase Phase { get; private set; }
    public long GrantToken { get; private set; }
    public AreaOwnershipRequestIdentity Identity => _identity;
    public long ExpectedResponderUid => _expectedResponderUid;

    public bool TryBegin(
        AreaOwnershipRequestIdentity identity,
        long expectedResponderUid,
        float responseDeadlineAt)
    {
        if (Phase != AreaOwnershipHandoffPhase.Idle ||
            identity.RequestId <= 0 ||
            identity.ContainerUserId == 0L ||
            identity.ContainerObjectId == 0U ||
            !IsSupportedAction(identity.Action) ||
            expectedResponderUid == 0L)
        {
            return false;
        }

        _identity = identity;
        _expectedResponderUid = expectedResponderUid;
        _deadlineAt = responseDeadlineAt;
        GrantToken = 0L;
        Phase = AreaOwnershipHandoffPhase.AwaitingResponse;
        return true;
    }

    public AreaOwnershipHandoffDecision ReceiveResponse(
        AreaOwnershipRequestIdentity identity,
        long senderUid,
        bool granted,
        long grantToken,
        float now,
        float ownershipDeadlineAt)
    {
        if (Phase != AreaOwnershipHandoffPhase.AwaitingResponse ||
            !_identity.Equals(identity) ||
            senderUid != _expectedResponderUid)
        {
            return AreaOwnershipHandoffDecision.None;
        }

        if (now >= _deadlineAt)
        {
            Clear();
            return AreaOwnershipHandoffDecision.Timeout;
        }

        if (!granted || grantToken == 0L)
        {
            Clear();
            return AreaOwnershipHandoffDecision.Denied;
        }

        GrantToken = grantToken;
        _deadlineAt = ownershipDeadlineAt;
        Phase = AreaOwnershipHandoffPhase.AwaitingOwnership;
        return AreaOwnershipHandoffDecision.None;
    }

    public AreaOwnershipHandoffDecision Observe(
        float now,
        bool loaded,
        AreaOwnershipObservedOwner observedOwner,
        bool netViewIsOwner,
        AreaOwnershipGrantTokenStatus tokenStatus,
        bool canExecute)
    {
        if (Phase is AreaOwnershipHandoffPhase.Idle or AreaOwnershipHandoffPhase.Executing)
        {
            return AreaOwnershipHandoffDecision.None;
        }

        if (!loaded)
        {
            Clear();
            return AreaOwnershipHandoffDecision.Unloaded;
        }

        if (now >= _deadlineAt)
        {
            Clear();
            return AreaOwnershipHandoffDecision.Timeout;
        }

        if (observedOwner == AreaOwnershipObservedOwner.Other)
        {
            Clear();
            return AreaOwnershipHandoffDecision.OwnerChanged;
        }

        if (Phase == AreaOwnershipHandoffPhase.AwaitingResponse)
        {
            return AreaOwnershipHandoffDecision.None;
        }

        if (observedOwner != AreaOwnershipObservedOwner.LocalRequester || !netViewIsOwner)
        {
            return AreaOwnershipHandoffDecision.None;
        }

        if (tokenStatus == AreaOwnershipGrantTokenStatus.Other)
        {
            Clear();
            return AreaOwnershipHandoffDecision.GrantReplaced;
        }

        if (tokenStatus == AreaOwnershipGrantTokenStatus.Missing)
        {
            return AreaOwnershipHandoffDecision.None;
        }

        if (!canExecute)
        {
            Clear();
            return AreaOwnershipHandoffDecision.Unavailable;
        }

        Phase = AreaOwnershipHandoffPhase.Executing;
        return AreaOwnershipHandoffDecision.Execute;
    }

    public void CompleteExecution()
    {
        if (Phase == AreaOwnershipHandoffPhase.Executing)
        {
            Clear();
        }
    }

    public void Cancel() => Clear();

    private static bool IsSupportedAction(AreaContainerActionKind action) =>
        action is AreaContainerActionKind.QuickStack or AreaContainerActionKind.Restock;

    private void Clear()
    {
        _identity = default;
        _expectedResponderUid = 0L;
        _deadlineAt = -1f;
        GrantToken = 0L;
        Phase = AreaOwnershipHandoffPhase.Idle;
    }
}
