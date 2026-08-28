using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string MultiUserContainerRequestRpc = "InventorySlots_MUC_Request_v3";
    private const string MultiUserContainerResponseRpc = "InventorySlots_MUC_Response_v3";
    private const string MultiUserContainerAcknowledgeRpc = "InventorySlots_MUC_Ack_v3";
    private const string MultiUserContainerReceiptZdoKey =
        "sighsorry.InventorySlots.MUC.Receipts.v3";
    private const byte MultiUserContainerProtocolVersion = 3;
    private const int MultiUserContainerMaxRequestPackageBytes = 96 * 1024;
    private const int MultiUserContainerMaxResponsePackageBytes = 48 * 1024;
    private const int MultiUserContainerMaxDurableReceiptResponseBytes = 48 * 1024;
    private const int MultiUserContainerMaxDurableReceiptBlobBytes = 64 * 1024;
    private const int MultiUserContainerMaxDurableReceiptCount = 128;
    private const long MultiUserContainerAcknowledgedReceiptLifetimeTicks =
        30 * TimeSpan.TicksPerSecond;
    private const int MultiUserContainerOwnerResponseCacheLimit = 128;
    private const int MultiUserContainerOwnerResponseCacheByteLimit = 32 * 1024 * 1024;
    private const float MultiUserContainerOwnerResponseCacheLifetime = 30f;
    private const float MultiUserContainerMaximumInteractionDistance = 10f;

    private enum MultiUserContainerOperation : byte
    {
        Add = 1,
        Remove = 2,
        Move = 3,
        Exchange = 4,
        Swap = 5
    }

    private enum MultiUserContainerFailure : byte
    {
        None = 0,
        Disabled = 1,
        InvalidRequest = 2,
        AccessDenied = 3,
        OwnerChanged = 4,
        ItemChanged = 5,
        DestinationChanged = 6
    }

    private sealed class MultiUserContainerRequest
    {
        public int RequestId;
        public MultiUserContainerOperation Operation;
        public long RequesterPlayerId;
        public Vector2i SourcePosition;
        public Vector2i TargetPosition;
        public int ExpectedTargetStack;
        public int Amount;
        public ItemData Item = null!;
        public ItemData? CounterpartItem;
    }

    private sealed class MultiUserContainerResponse
    {
        public int RequestId;
        public MultiUserContainerOperation Operation;
        public bool Success;
        public MultiUserContainerFailure Failure;
        public int Amount;
        public Vector2i SourcePosition;
        public Vector2i TargetPosition;
        public ItemData? Item;
    }

    private readonly struct MultiUserContainerOwnerRequestKey : IEquatable<MultiUserContainerOwnerRequestKey>
    {
        public MultiUserContainerOwnerRequestKey(long sender, int requestId)
        {
            Sender = sender;
            RequestId = requestId;
        }

        public long Sender { get; }
        public int RequestId { get; }

        public bool Equals(MultiUserContainerOwnerRequestKey other) =>
            Sender == other.Sender && RequestId == other.RequestId;

        public override bool Equals(object? obj) =>
            obj is MultiUserContainerOwnerRequestKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Sender.GetHashCode() * 397) ^ RequestId;
            }
        }
    }

    private sealed class MultiUserContainerOwnerResponse
    {
        public float CreatedAt;
        public byte[] RequestDigest = null!;
        public byte[] ResponseBytes = null!;
    }

    private sealed class MultiUserContainerUncertainRequest
    {
        public byte[] RequestDigest = null!;
    }

    private sealed class MultiUserContainerDurableReceipt
    {
        public bool Acknowledged;
        public long RequesterPlayerId;
        public long Sender;
        public int RequestId;
        public long CreatedUtcTicks;
        public byte[] RequestDigest = null!;
        public byte[] ResponseBytes = null!;
    }

    private sealed class MultiUserContainerOwnerState
    {
        public readonly Dictionary<MultiUserContainerOwnerRequestKey, MultiUserContainerOwnerResponse> Responses = new();
        public readonly Dictionary<MultiUserContainerOwnerRequestKey, MultiUserContainerUncertainRequest> UncertainRequests = new();
        public int TotalResponseBytes;
    }

    private static readonly Dictionary<Container, MultiUserContainerOwnerState> MultiUserContainerOwnerStates = new();

    internal static void RegisterMultiUserContainerRpcs(Container container)
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

        // Registration is unconditional because the synchronized setting can arrive
        // after Container.Awake. The handlers themselves remain fail-closed.
        container.m_nview.Unregister(MultiUserContainerRequestRpc);
        container.m_nview.Unregister(MultiUserContainerResponseRpc);
        container.m_nview.Unregister(MultiUserContainerAcknowledgeRpc);
        container.m_nview.Register<ZPackage>(
            MultiUserContainerRequestRpc,
            (sender, package) => RPC_MultiUserContainerRequest(container, sender, package));
        container.m_nview.Register<ZPackage>(
            MultiUserContainerResponseRpc,
            (sender, package) => RPC_MultiUserContainerResponse(container, sender, package));
        container.m_nview.Register<ZPackage>(
            MultiUserContainerAcknowledgeRpc,
            (sender, package) => RPC_MultiUserContainerAcknowledge(container, sender, package));
        RebindPendingMultiUserContainer(container);
    }

    internal static void UnregisterMultiUserContainerRpcs(Container container)
    {
        if (container == null || IsUnityNull(container))
        {
            return;
        }

        if (container.m_nview != null && !IsUnityNull(container.m_nview))
        {
            container.m_nview.Unregister(MultiUserContainerRequestRpc);
            container.m_nview.Unregister(MultiUserContainerResponseRpc);
            container.m_nview.Unregister(MultiUserContainerAcknowledgeRpc);
        }

        SuspendPendingMultiUserContainer(container);
        MultiUserContainerOwnerStates.Remove(container);
    }

    private static void RPC_MultiUserContainerRequest(Container container, long sender, ZPackage package)
    {
        if (container == null ||
            IsUnityNull(container) ||
            package == null ||
            package.Size() <= 0 ||
            package.Size() > MultiUserContainerMaxRequestPackageBytes ||
            !TryReadMultiUserContainerRequestHeader(package, out MultiUserContainerRequest? request) ||
            request == null)
        {
            return;
        }

        if (!TryComputeMultiUserContainerDigest(package, out byte[]? requestDigest))
        {
            return;
        }

        MultiUserContainerOwnerState ownerState = GetMultiUserContainerOwnerState(container);
        MultiUserContainerOwnerRequestKey key = new(sender, request.RequestId);
        if (ownerState.UncertainRequests.TryGetValue(
                key,
                out MultiUserContainerUncertainRequest? uncertain))
        {
            if (!AreMultiUserContainerDigestsEqual(
                    uncertain.RequestDigest,
                    requestDigest!))
            {
                MultiUserContainerResponse conflict =
                    CreateMultiUserContainerResponse(request);
                conflict.Failure =
                    MultiUserContainerFailure.InvalidRequest;
                SendMultiUserContainerResponse(
                    container,
                    sender,
                    conflict);
            }

            return;
        }

        if (ownerState.Responses.TryGetValue(key, out MultiUserContainerOwnerResponse? cached))
        {
            if (AreMultiUserContainerDigestsEqual(cached.RequestDigest, requestDigest!))
            {
                SendMultiUserContainerResponse(container, sender, cached.ResponseBytes);
            }
            else
            {
                MultiUserContainerResponse conflict = CreateMultiUserContainerResponse(request);
                conflict.Failure = MultiUserContainerFailure.InvalidRequest;
                SendMultiUserContainerResponse(container, sender, conflict);
            }

            return;
        }

        if (TryReadMultiUserContainerDurableReceipt(
                container,
                request.RequesterPlayerId,
                request.RequestId,
                out MultiUserContainerDurableReceipt? durableReceipt) &&
            durableReceipt != null &&
            durableReceipt.Sender == sender &&
            durableReceipt.RequestId == request.RequestId)
        {
            if (AreMultiUserContainerDigestsEqual(
                    durableReceipt.RequestDigest,
                    requestDigest!))
            {
                if (!durableReceipt.Acknowledged)
                {
                    CacheMultiUserContainerOwnerResponse(
                        ownerState,
                        key,
                        durableReceipt.RequestDigest,
                        durableReceipt.ResponseBytes);
                    SendMultiUserContainerResponse(
                        container,
                        sender,
                        durableReceipt.ResponseBytes);
                }
            }
            else
            {
                MultiUserContainerResponse conflict =
                    CreateMultiUserContainerResponse(request);
                conflict.Failure = MultiUserContainerFailure.InvalidRequest;
                SendMultiUserContainerResponse(container, sender, conflict);
            }

            return;
        }

        if (!CanIdentifyMultiUserContainerRequester(
                container,
                sender,
                request,
                out Player? requester))
        {
            MultiUserContainerResponse denied = CreateMultiUserContainerResponse(request);
            denied.Failure = MultiUserContainerFailure.AccessDenied;
            SendMultiUserContainerResponse(container, sender, denied);
            return;
        }

        if (!CanProcessNewMultiUserContainerRequest(
                container,
                requester!,
                request.RequesterPlayerId))
        {
            MultiUserContainerResponse denied = CreateMultiUserContainerResponse(request);
            denied.Failure = IsBuiltInMultiUserChestEnabled
                ? MultiUserContainerFailure.AccessDenied
                : MultiUserContainerFailure.Disabled;
            SendMultiUserContainerResponse(container, sender, denied);
            return;
        }

        if (ownerState.Responses.Count +
            ownerState.UncertainRequests.Count >=
            MultiUserContainerOwnerResponseCacheLimit ||
            ownerState.TotalResponseBytes >
            MultiUserContainerOwnerResponseCacheByteLimit - package.Size())
        {
            MultiUserContainerResponse busy = CreateMultiUserContainerResponse(request);
            busy.Failure = MultiUserContainerFailure.OwnerChanged;
            SendMultiUserContainerResponse(container, sender, busy);
            return;
        }

        if (!TryReadMultiUserContainerRequestItem(package, request))
        {
            MultiUserContainerResponse invalid = CreateMultiUserContainerResponse(request);
            invalid.Failure = MultiUserContainerFailure.InvalidRequest;
            SendMultiUserContainerResponse(container, sender, invalid);
            return;
        }

        if (!CanPersistMultiUserContainerDurableReceipt(
                container,
                sender,
                request,
                requestDigest!))
        {
            MultiUserContainerResponse busy =
                CreateMultiUserContainerResponse(request);
            busy.Failure = MultiUserContainerFailure.OwnerChanged;
            SendMultiUserContainerResponse(container, sender, busy);
            return;
        }

        MultiUserContainerResponse response = ProcessMultiUserContainerRequest(
            container,
            sender,
            request,
            out List<ItemData>? transactionRollbackItems,
            out bool transactionOutcomeUncertain);
        if (transactionOutcomeUncertain)
        {
            QuarantineMultiUserContainerOwnerRequest(
                ownerState,
                key,
                requestDigest!);
            Log.LogError(
                "Built-in multi-user chest could not verify its transaction rollback; the request will remain pending.");
            return;
        }

        if (!TryWriteMultiUserContainerResponse(response, out ZPackage? responsePackage))
        {
            if (response.Success && transactionRollbackItems != null)
            {
                if (!RestoreMultiUserContainerInventory(
                        container,
                        container.GetInventory(),
                        transactionRollbackItems))
                {
                    QuarantineMultiUserContainerOwnerRequest(
                        ownerState,
                        key,
                        requestDigest!);
                    Log.LogError(
                        "Built-in multi-user chest could not verify rollback after response serialization failed.");
                    return;
                }
            }

            response = CreateMultiUserContainerResponse(request);
            response.Failure = MultiUserContainerFailure.InvalidRequest;
            if (!TryWriteMultiUserContainerResponse(response, out responsePackage))
            {
                return;
            }
        }

        byte[] responseBytes = responsePackage!.GetArray();
        if (!TryWriteMultiUserContainerDurableReceipt(
                container,
                sender,
                request.RequesterPlayerId,
                request.RequestId,
                requestDigest!,
                responseBytes,
                out bool receiptOutcomeUncertain))
        {
            if (receiptOutcomeUncertain)
            {
                QuarantineMultiUserContainerOwnerRequest(
                    ownerState,
                    key,
                    requestDigest!);
                Log.LogError(
                    "Built-in multi-user chest could not verify its latest transaction receipt; the request will remain pending.");
                return;
            }

            if (response.Success && transactionRollbackItems != null)
            {
                if (!RestoreMultiUserContainerInventory(
                        container,
                        container.GetInventory(),
                        transactionRollbackItems))
                {
                    QuarantineMultiUserContainerOwnerRequest(
                        ownerState,
                        key,
                        requestDigest!);
                    Log.LogError(
                        "Built-in multi-user chest could not verify rollback after receipt persistence failed.");
                }
            }

            Log.LogWarning(
                "Built-in multi-user chest could not persist its latest transaction receipt.");
            return;
        }

        CacheMultiUserContainerOwnerResponse(
            ownerState,
            key,
            requestDigest!,
            responseBytes);
        SendMultiUserContainerResponse(container, sender, responseBytes);
    }

    private static void RPC_MultiUserContainerResponse(Container container, long sender, ZPackage package)
    {
        if (container == null ||
            IsUnityNull(container) ||
            package == null ||
            package.Size() <= 0 ||
            package.Size() > MultiUserContainerMaxResponsePackageBytes ||
            !TryReadMultiUserContainerResponse(package, out MultiUserContainerResponse? response) ||
            response == null)
        {
            return;
        }

        HandleMultiUserContainerResponse(container, sender, response);
    }

    private static void RPC_MultiUserContainerAcknowledge(
        Container container,
        long sender,
        ZPackage package)
    {
        if (container == null ||
            IsUnityNull(container) ||
            sender == 0L ||
            package == null ||
            package.Size() <= 0 ||
            package.Size() > 256 ||
            !TryReadMultiUserContainerAcknowledge(
                package,
                out int requestId,
                out long requesterPlayerId,
                out byte[]? requestDigest) ||
            requestDigest == null)
        {
            return;
        }

        MultiUserContainerOwnerRequestKey key = new(sender, requestId);
        if (TryReadMultiUserContainerDurableReceipt(
                container,
                requesterPlayerId,
                requestId,
                out MultiUserContainerDurableReceipt? durableReceipt) &&
            durableReceipt != null &&
            durableReceipt.Sender == sender &&
            durableReceipt.RequestId == requestId &&
            AreMultiUserContainerDigestsEqual(
                durableReceipt.RequestDigest,
                requestDigest))
        {
            if (AcknowledgeMultiUserContainerDurableReceipt(
                    container,
                    requesterPlayerId,
                    requestId,
                    sender) &&
                MultiUserContainerOwnerStates.TryGetValue(
                    container,
                    out MultiUserContainerOwnerState? ownerState))
            {
                if (ownerState.Responses.TryGetValue(
                        key,
                        out MultiUserContainerOwnerResponse? cached) &&
                    AreMultiUserContainerDigestsEqual(
                        cached.RequestDigest,
                        requestDigest))
                {
                    ownerState.TotalResponseBytes = Math.Max(
                        0,
                        ownerState.TotalResponseBytes -
                        cached.ResponseBytes.Length);
                    ownerState.Responses.Remove(key);
                }

                if (ownerState.UncertainRequests.TryGetValue(
                        key,
                        out MultiUserContainerUncertainRequest? uncertain) &&
                    AreMultiUserContainerDigestsEqual(
                        uncertain.RequestDigest,
                        requestDigest))
                {
                    ownerState.UncertainRequests.Remove(key);
                }
            }
        }
    }

    private static bool CanIdentifyMultiUserContainerRequester(
        Container container,
        long sender,
        MultiUserContainerRequest request,
        out Player? requester)
    {
        requester = null;
        if (sender == 0L ||
            request.RequestId <= 0 ||
            request.RequesterPlayerId == 0L ||
            request.Amount <= 0 ||
            !Enum.IsDefined(typeof(MultiUserContainerOperation), request.Operation) ||
            !TryGetRpcSenderPlayer(
                sender,
                request.RequesterPlayerId,
                out requester) ||
            requester == null ||
            IsUnityNull(requester))
        {
            return false;
        }

        return true;
    }

    private static bool CanProcessNewMultiUserContainerRequest(
        Container container,
        Player requester,
        long requesterPlayerId)
    {
        float maximumDistance = MultiUserContainerMaximumInteractionDistance;
        return IsBuiltInMultiUserChestEnabled &&
               container.m_nview != null &&
               container.m_nview.IsValid() &&
               container.m_nview.IsOwner() &&
               IsBuiltInMultiUserContainerEligible(container) &&
               requester != null &&
               !IsUnityNull(requester) &&
               container.CheckAccess(requesterPlayerId) &&
               (requester.transform.position - container.transform.position).sqrMagnitude <=
               maximumDistance * maximumDistance;
    }

    private static bool TryComputeMultiUserContainerDigest(
        ZPackage package,
        out byte[]? digest)
    {
        digest = null;
        if (package == null)
        {
            return false;
        }

        try
        {
            return TryComputeMultiUserContainerDigest(
                package.GetArray(),
                out digest);
        }
        catch
        {
            digest = null;
            return false;
        }
    }

    private static bool TryComputeMultiUserContainerDigest(
        byte[] bytes,
        out byte[]? digest)
    {
        digest = null;
        if (bytes == null || bytes.Length <= 0)
        {
            return false;
        }

        try
        {
            using SHA256 sha256 = SHA256.Create();
            digest = sha256.ComputeHash(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool AreMultiUserContainerDigestsEqual(
        byte[] left,
        byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        int difference = 0;
        for (int index = 0; index < left.Length; index++)
        {
            difference |= left[index] ^ right[index];
        }

        return difference == 0;
    }

    private static MultiUserContainerResponse ProcessMultiUserContainerRequest(
        Container container,
        long sender,
        MultiUserContainerRequest request,
        out List<ItemData>? rollbackItems,
        out bool outcomeUncertain)
    {
        rollbackItems = null;
        outcomeUncertain = false;
        MultiUserContainerResponse response = CreateMultiUserContainerResponse(request);
        if (!IsBuiltInMultiUserChestEnabled)
        {
            response.Failure = MultiUserContainerFailure.Disabled;
            return response;
        }

        if (container.m_nview == null ||
            !container.m_nview.IsValid() ||
            !container.m_nview.IsOwner())
        {
            response.Failure = MultiUserContainerFailure.OwnerChanged;
            return response;
        }

        if (IsMultiUserChestIgnored(container) ||
            sender == 0L ||
            request.RequesterPlayerId == 0L ||
            !IsRpcSenderForPlayer(sender, request.RequesterPlayerId) ||
            !container.CheckAccess(request.RequesterPlayerId))
        {
            response.Failure = MultiUserContainerFailure.AccessDenied;
            return response;
        }

        Inventory inventory = container.GetInventory();
        if (inventory == null ||
            !IsValidMultiUserContainerRequest(inventory, request) ||
            !TryCaptureMultiUserContainerInventory(
                inventory,
                out rollbackItems))
        {
            response.Failure = MultiUserContainerFailure.InvalidRequest;
            return response;
        }

        response.Success = TryApplyMultiUserContainerRequestToInventory(
            inventory,
            request,
            out ItemData? responseItem,
            out MultiUserContainerFailure failure);
        response.Amount = response.Success ? request.Amount : 0;
        response.Item = response.Success ? responseItem : null;
        response.Failure = failure;

        if (response.Success &&
            !TryPersistMultiUserContainerInventory(container))
        {
            outcomeUncertain = !RestoreMultiUserContainerInventory(
                container,
                inventory,
                rollbackItems!);
            response.Success = false;
            response.Amount = 0;
            response.Item = null;
            response.Failure = MultiUserContainerFailure.OwnerChanged;
        }

        return response;
    }

    private static bool TryCaptureMultiUserContainerInventory(
        Inventory inventory,
        out List<ItemData>? items)
    {
        items = null;
        try
        {
            items = inventory.m_inventory
                .ConvertAll(item => item.Clone());
            return true;
        }
        catch
        {
            items = null;
            return false;
        }
    }

    private static bool RestoreMultiUserContainerInventory(
        Container container,
        Inventory inventory,
        List<ItemData> items)
    {
        try
        {
            inventory.m_inventory.Clear();
            inventory.m_inventory.AddRange(items);
            NotifyMultiUserContainerInventoryChanged(inventory);
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest rollback callback failed: {exception.Message}");
        }

        return TryPersistMultiUserContainerInventory(container);
    }

    private static bool TryPersistMultiUserContainerInventory(Container container)
    {
        if (container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            !container.m_nview.IsOwner())
        {
            return false;
        }

        try
        {
            container.Save();
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest save callback failed: {exception.Message}");
        }

        try
        {
            ZPackage expectedPackage = new();
            container.GetInventory().Save(expectedPackage);
            ZDO? zdo = container.m_nview.GetZDO();
            return zdo != null &&
                   string.Equals(
                       zdo.GetString(ZDOVars.s_items),
                       expectedPackage.GetBase64(),
                       StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest save verification failed: {exception.Message}");
            return false;
        }
    }

    private static bool IsValidMultiUserContainerRequest(Inventory inventory, MultiUserContainerRequest request)
    {
        if (request.RequestId <= 0 ||
            !Enum.IsDefined(typeof(MultiUserContainerOperation), request.Operation) ||
            request.Item?.m_shared == null ||
            !MultiUserContainerTransferCore.CanTransferAmount(
                request.Item.m_stack,
                request.Item.m_shared.m_maxStackSize,
                request.Amount))
        {
            return false;
        }

        bool sourceRequired = request.Operation != MultiUserContainerOperation.Add;
        bool targetRequired = request.Operation is
            MultiUserContainerOperation.Add or
            MultiUserContainerOperation.Move or
            MultiUserContainerOperation.Swap;
        bool targetStackValid = request.Operation is
            MultiUserContainerOperation.Remove or
            MultiUserContainerOperation.Exchange or
            MultiUserContainerOperation.Swap
                ? request.ExpectedTargetStack == -1
                : request.ExpectedTargetStack >= 0 &&
                  request.ExpectedTargetStack <=
                  request.Item.m_shared.m_maxStackSize;
        bool counterpartRequired = request.Operation is
            MultiUserContainerOperation.Exchange or
            MultiUserContainerOperation.Swap;
        bool counterpartValid = counterpartRequired
            ? IsValidMultiUserContainerMutationItem(request.CounterpartItem) &&
              !request.CounterpartItem!.m_equipped &&
              request.CounterpartItem.m_gridPos == request.TargetPosition &&
              request.Amount == request.Item.m_stack &&
              request.TargetPosition.x >= 0 &&
              request.TargetPosition.y >= 0 &&
              (request.Operation != MultiUserContainerOperation.Swap ||
               request.SourcePosition != request.TargetPosition) &&
              !request.Item.m_shared.m_questItem &&
              !request.CounterpartItem.m_shared.m_questItem
            : request.CounterpartItem == null;
        return (!sourceRequired || IsValidMultiUserContainerCoordinate(inventory, request.SourcePosition)) &&
               (!targetRequired || IsValidMultiUserContainerCoordinate(inventory, request.TargetPosition)) &&
               targetStackValid &&
               counterpartValid;
    }

    private static MultiUserContainerResponse CreateMultiUserContainerResponse(MultiUserContainerRequest request)
    {
        return new MultiUserContainerResponse
        {
            RequestId = request.RequestId,
            Operation = request.Operation,
            Success = false,
            Failure = MultiUserContainerFailure.InvalidRequest,
            Amount = 0,
            SourcePosition = request.SourcePosition,
            TargetPosition = request.TargetPosition
        };
    }

    private static MultiUserContainerOwnerState GetMultiUserContainerOwnerState(Container container)
    {
        if (!MultiUserContainerOwnerStates.TryGetValue(container, out MultiUserContainerOwnerState? state))
        {
            state = new MultiUserContainerOwnerState();
            MultiUserContainerOwnerStates.Add(container, state);
        }

        return state;
    }

    private static void CacheMultiUserContainerOwnerResponse(
        MultiUserContainerOwnerState ownerState,
        MultiUserContainerOwnerRequestKey key,
        byte[] requestDigest,
        byte[] responseBytes)
    {
        if (ownerState == null ||
            requestDigest == null ||
            responseBytes == null ||
            responseBytes.Length <= 0 ||
            responseBytes.Length > MultiUserContainerMaxResponsePackageBytes)
        {
            return;
        }

        if (ownerState.Responses.TryGetValue(
                key,
                out MultiUserContainerOwnerResponse? previous))
        {
            ownerState.TotalResponseBytes = Math.Max(
                0,
                ownerState.TotalResponseBytes - previous.ResponseBytes.Length);
            ownerState.Responses.Remove(key);
        }

        if (ownerState.Responses.Count +
            ownerState.UncertainRequests.Count >=
            MultiUserContainerOwnerResponseCacheLimit ||
            ownerState.TotalResponseBytes >
            MultiUserContainerOwnerResponseCacheByteLimit - responseBytes.Length)
        {
            return;
        }

        ownerState.Responses[key] = new MultiUserContainerOwnerResponse
        {
            CreatedAt = Time.unscaledTime,
            RequestDigest = requestDigest,
            ResponseBytes = responseBytes
        };
        ownerState.TotalResponseBytes += responseBytes.Length;
    }

    private static void QuarantineMultiUserContainerOwnerRequest(
        MultiUserContainerOwnerState ownerState,
        MultiUserContainerOwnerRequestKey key,
        byte[] requestDigest)
    {
        if (ownerState == null ||
            requestDigest == null ||
            requestDigest.Length != 32 ||
            ownerState.UncertainRequests.ContainsKey(key) ||
            ownerState.Responses.Count +
            ownerState.UncertainRequests.Count >=
            MultiUserContainerOwnerResponseCacheLimit)
        {
            return;
        }

        ownerState.UncertainRequests[key] =
            new MultiUserContainerUncertainRequest
            {
                RequestDigest = (byte[])requestDigest.Clone()
            };
    }

    private static bool CanPersistMultiUserContainerDurableReceipt(
        Container container,
        long sender,
        MultiUserContainerRequest request,
        byte[] requestDigest)
    {
        MultiUserContainerResponse projected =
            CreateMultiUserContainerResponse(request);
        projected.Success = true;
        projected.Failure = MultiUserContainerFailure.None;
        projected.Amount = request.Amount;
        if (request.Operation is
            MultiUserContainerOperation.Remove or
            MultiUserContainerOperation.Exchange)
        {
            Inventory inventory = container.GetInventory();
            ItemData? current = inventory.GetItemAt(
                request.SourcePosition.x,
                request.SourcePosition.y);
            if (current == null ||
                !MultiUserContainerTransferCore.MatchesExpectedStackState(
                    request.Item.m_stack,
                    current.m_stack) ||
                !IsExactMultiUserContainerItemMatch(
                    request.Item,
                    current,
                    request.Amount))
            {
                // A changed source produces a small failure response and no mutation.
                projected.Success = false;
                projected.Failure = MultiUserContainerFailure.ItemChanged;
                projected.Amount = 0;
            }
            else
            {
                try
                {
                    projected.Item = current.Clone();
                    projected.Item.m_stack = request.Amount;
                    projected.Item.m_equipped = false;
                }
                catch
                {
                    return false;
                }
            }
        }

        if (!TryWriteMultiUserContainerResponse(
                projected,
                out ZPackage? projectedPackage))
        {
            return false;
        }

        byte[] projectedBytes = projectedPackage!.GetArray();
        return projectedBytes.Length <=
               MultiUserContainerMaxDurableReceiptResponseBytes &&
               TryReadMultiUserContainerDurableReceipts(
                   container,
                   out List<MultiUserContainerDurableReceipt>? receipts) &&
               TryUpsertMultiUserContainerDurableReceipt(
                   receipts!,
                   new MultiUserContainerDurableReceipt
                   {
                       Acknowledged = false,
                       RequesterPlayerId = request.RequesterPlayerId,
                       Sender = sender,
                       RequestId = request.RequestId,
                       CreatedUtcTicks = DateTime.UtcNow.Ticks,
                       RequestDigest = requestDigest,
                       ResponseBytes = projectedBytes
                   },
                   out _);
    }

    private static bool TryReadMultiUserContainerDurableReceipt(
        Container container,
        long requesterPlayerId,
        int requestId,
        out MultiUserContainerDurableReceipt? receipt)
    {
        receipt = null;
        ZDO? zdo =
            container != null &&
            !IsUnityNull(container) &&
            container.m_nview != null &&
            !IsUnityNull(container.m_nview)
                ? container.m_nview.GetZDO()
                : null;
        return zdo != null &&
               TryReadMultiUserContainerDurableReceipt(
                   zdo,
                   requesterPlayerId,
                   requestId,
                   out receipt);
    }

    private static bool TryReadMultiUserContainerDurableReceipt(
        ZDO zdo,
        long requesterPlayerId,
        int requestId,
        out MultiUserContainerDurableReceipt? receipt)
    {
        receipt = null;
        if (requesterPlayerId == 0L ||
            requestId <= 0 ||
            !TryReadMultiUserContainerDurableReceipts(
                zdo,
                out List<MultiUserContainerDurableReceipt>? receipts))
        {
            return false;
        }

        receipt = receipts!.Find(
            candidate =>
                candidate.RequesterPlayerId == requesterPlayerId &&
                candidate.RequestId == requestId);
        return receipt != null;
    }

    private static bool TryWriteMultiUserContainerDurableReceipt(
        Container container,
        long sender,
        long requesterPlayerId,
        int requestId,
        byte[] requestDigest,
        byte[] responseBytes,
        out bool outcomeUncertain)
    {
        outcomeUncertain = false;
        if (sender == 0L ||
            requesterPlayerId == 0L ||
            requestId <= 0 ||
            requestDigest == null ||
            responseBytes == null ||
            responseBytes.Length <= 0 ||
            responseBytes.Length >
            MultiUserContainerMaxDurableReceiptResponseBytes ||
            !TryReadMultiUserContainerDurableReceipts(
                container,
                out List<MultiUserContainerDurableReceipt>? receipts) ||
            !TryUpsertMultiUserContainerDurableReceipt(
                receipts!,
                new MultiUserContainerDurableReceipt
                {
                    Acknowledged = false,
                    RequesterPlayerId = requesterPlayerId,
                    Sender = sender,
                    RequestId = requestId,
                    CreatedUtcTicks = DateTime.UtcNow.Ticks,
                    RequestDigest = requestDigest,
                    ResponseBytes = responseBytes
                },
                out byte[]? receiptBytes))
        {
            return false;
        }

        ZDO? zdo = container.m_nview?.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        try
        {
            zdo.Set(MultiUserContainerReceiptZdoKey, receiptBytes!);
            byte[]? stored =
                zdo.GetByteArray(MultiUserContainerReceiptZdoKey);
            if (!AreMultiUserContainerDigestsEqual(
                    stored ?? Array.Empty<byte>(),
                    receiptBytes!))
            {
                outcomeUncertain = true;
                return false;
            }
        }
        catch (Exception exception)
        {
            outcomeUncertain = true;
            Log.LogWarning(
                $"Built-in multi-user chest receipt save failed: {exception.Message}");
            return false;
        }

        try
        {
            ZDOMan.instance?.ForceSendZDO(sender, zdo.m_uid);
        }
        catch (Exception exception)
        {
            // The exact receipt is already verified in the ZDO. A later normal ZDO
            // update, client poll, or immutable retry can deliver the result.
            Log.LogWarning(
                $"Built-in multi-user chest receipt force-send failed: {exception.Message}");
        }

        return true;
    }

    private static bool AcknowledgeMultiUserContainerDurableReceipt(
        Container container,
        long requesterPlayerId,
        int requestId,
        long sender)
    {
        if (requesterPlayerId == 0L ||
            requestId <= 0 ||
            !TryReadMultiUserContainerDurableReceipts(
                container,
                out List<MultiUserContainerDurableReceipt>? receipts))
        {
            return false;
        }

        MultiUserContainerDurableReceipt? receipt = receipts!.Find(
            candidate =>
                candidate.RequesterPlayerId == requesterPlayerId &&
                candidate.RequestId == requestId);
        if (receipt == null)
        {
            return false;
        }

        receipt.Acknowledged = true;
        receipt.CreatedUtcTicks = DateTime.UtcNow.Ticks;
        receipt.ResponseBytes = Array.Empty<byte>();
        if (!TrySerializeMultiUserContainerDurableReceipts(
                receipts,
                out byte[] receiptBytes))
        {
            return false;
        }

        ZDO? zdo = container.m_nview?.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        try
        {
            zdo.Set(MultiUserContainerReceiptZdoKey, receiptBytes);
            byte[]? stored =
                zdo.GetByteArray(MultiUserContainerReceiptZdoKey);
            if (!AreMultiUserContainerDigestsEqual(
                    stored ?? Array.Empty<byte>(),
                    receiptBytes))
            {
                return false;
            }

        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Built-in multi-user chest receipt cleanup failed: {exception.Message}");
            return false;
        }

        try
        {
            ZDOMan.instance?.ForceSendZDO(sender, zdo.m_uid);
        }
        catch (Exception exception)
        {
            // The tombstone is already verified and can travel with a later ZDO update.
            Log.LogWarning(
                $"Built-in multi-user chest receipt cleanup force-send failed: {exception.Message}");
        }

        return true;
    }

    private static bool TryReadMultiUserContainerDurableReceipts(
        Container container,
        out List<MultiUserContainerDurableReceipt>? receipts)
    {
        ZDO? zdo =
            container != null &&
            !IsUnityNull(container) &&
            container.m_nview != null &&
            !IsUnityNull(container.m_nview)
                ? container.m_nview.GetZDO()
                : null;
        if (zdo == null)
        {
            receipts = null;
            return false;
        }

        return TryReadMultiUserContainerDurableReceipts(
            zdo,
            out receipts);
    }

    private static bool TryReadMultiUserContainerDurableReceipts(
        ZDO zdo,
        out List<MultiUserContainerDurableReceipt>? receipts)
    {
        receipts = new List<MultiUserContainerDurableReceipt>();
        if (zdo == null)
        {
            receipts = null;
            return false;
        }

        byte[]? receiptBytes =
            zdo.GetByteArray(MultiUserContainerReceiptZdoKey);
        if (receiptBytes == null || receiptBytes.Length == 0)
        {
            return true;
        }

        if (receiptBytes.Length > MultiUserContainerMaxDurableReceiptBlobBytes)
        {
            receipts = null;
            return false;
        }

        try
        {
            ZPackage package = new(receiptBytes);
            if (package.ReadByte() != MultiUserContainerProtocolVersion)
            {
                receipts = null;
                return false;
            }

            int count = package.ReadInt();
            if (count < 0 || count > MultiUserContainerMaxDurableReceiptCount)
            {
                receipts = null;
                return false;
            }

            HashSet<(long PlayerId, int RequestId)> receiptIds = new();
            for (int index = 0; index < count; index++)
            {
                MultiUserContainerDurableReceipt receipt = new()
                {
                    Acknowledged = package.ReadBool(),
                    RequesterPlayerId = package.ReadLong(),
                    Sender = package.ReadLong(),
                    RequestId = package.ReadInt(),
                    CreatedUtcTicks = package.ReadLong()
                };
                if (!TryReadBoundedMultiUserContainerByteArray(
                        package,
                        32,
                        out byte[] requestDigest) ||
                    !TryReadBoundedMultiUserContainerByteArray(
                        package,
                        MultiUserContainerMaxDurableReceiptResponseBytes,
                        out byte[] responseBytes))
                {
                    receipts = null;
                    return false;
                }

                receipt.RequestDigest = requestDigest;
                receipt.ResponseBytes = responseBytes;
                if (receipt.RequesterPlayerId == 0L ||
                    receipt.Sender == 0L ||
                    receipt.RequestId <= 0 ||
                    receipt.RequestDigest.Length != 32 ||
                    receipt.ResponseBytes.Length >
                    MultiUserContainerMaxDurableReceiptResponseBytes ||
                    (receipt.Acknowledged &&
                     receipt.ResponseBytes.Length != 0) ||
                    (!receipt.Acknowledged &&
                     receipt.ResponseBytes.Length <= 0) ||
                    !receiptIds.Add((
                        receipt.RequesterPlayerId,
                        receipt.RequestId)))
                {
                    receipts = null;
                    return false;
                }

                long receiptCutoff =
                    DateTime.UtcNow.Ticks -
                    MultiUserContainerAcknowledgedReceiptLifetimeTicks;
                if ((!receipt.Acknowledged ||
                     receipt.CreatedUtcTicks >= receiptCutoff) &&
                    receipt.CreatedUtcTicks > 0L)
                {
                    receipts.Add(receipt);
                }
            }

            if (package.GetPos() != package.Size())
            {
                receipts = null;
                return false;
            }

            return true;
        }
        catch
        {
            receipts = null;
            return false;
        }
    }

    private static bool TryUpsertMultiUserContainerDurableReceipt(
        List<MultiUserContainerDurableReceipt> receipts,
        MultiUserContainerDurableReceipt receipt,
        out byte[]? receiptBytes)
    {
        receiptBytes = null;
        receipts.RemoveAll(
            current =>
                current.RequesterPlayerId == receipt.RequesterPlayerId &&
                current.RequestId == receipt.RequestId);
        if (receipts.Count >= MultiUserContainerMaxDurableReceiptCount)
        {
            return false;
        }

        receipts.Add(receipt);
        if (!TrySerializeMultiUserContainerDurableReceipts(
                receipts,
                out byte[] serialized))
        {
            return false;
        }

        receiptBytes = serialized;
        return true;
    }

    private static bool TrySerializeMultiUserContainerDurableReceipts(
        List<MultiUserContainerDurableReceipt> receipts,
        out byte[] receiptBytes)
    {
        receiptBytes = Array.Empty<byte>();
        if (receipts.Count <= 0 ||
            receipts.Count > MultiUserContainerMaxDurableReceiptCount)
        {
            return false;
        }

        try
        {
            ZPackage package = new();
            package.Write(MultiUserContainerProtocolVersion);
            package.Write(receipts.Count);
            foreach (MultiUserContainerDurableReceipt receipt in receipts)
            {
                package.Write(receipt.Acknowledged);
                package.Write(receipt.RequesterPlayerId);
                package.Write(receipt.Sender);
                package.Write(receipt.RequestId);
                package.Write(receipt.CreatedUtcTicks);
                package.Write(receipt.RequestDigest);
                package.Write(receipt.ResponseBytes);
            }

            if (package.Size() > MultiUserContainerMaxDurableReceiptBlobBytes)
            {
                return false;
            }

            receiptBytes = package.GetArray();
            return true;
        }
        catch
        {
            receiptBytes = Array.Empty<byte>();
            return false;
        }
    }

    private static bool TryReadMultiUserContainerAcknowledge(
        ZPackage package,
        out int requestId,
        out long requesterPlayerId,
        out byte[]? requestDigest)
    {
        requestId = 0;
        requesterPlayerId = 0L;
        requestDigest = null;
        try
        {
            package.SetPos(0);
            if (package.ReadByte() != MultiUserContainerProtocolVersion)
            {
                return false;
            }

            requestId = package.ReadInt();
            requesterPlayerId = package.ReadLong();
            if (!TryReadBoundedMultiUserContainerByteArray(
                    package,
                    32,
                    out byte[] digest))
            {
                return false;
            }

            requestDigest = digest;
            return requestId > 0 &&
                   requesterPlayerId != 0L &&
                   requestDigest.Length == 32 &&
                   package.GetPos() == package.Size();
        }
        catch
        {
            requestId = 0;
            requesterPlayerId = 0L;
            requestDigest = null;
            return false;
        }
    }

    private static bool TryReadBoundedMultiUserContainerByteArray(
        ZPackage package,
        int maximumBytes,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (package == null ||
            maximumBytes < 0 ||
            package.Size() - package.GetPos() < sizeof(int))
        {
            return false;
        }

        int length = package.ReadInt();
        int remaining = package.Size() - package.GetPos();
        if (length < 0 ||
            length > maximumBytes ||
            length > remaining)
        {
            return false;
        }

        bytes = package.ReadByteArray(length);
        return bytes.Length == length;
    }

    private static void PruneMultiUserContainerOwnerResponseCaches()
    {
        float cutoff = Time.unscaledTime - MultiUserContainerOwnerResponseCacheLifetime;
        List<Container> deadContainers = new();
        foreach (KeyValuePair<Container, MultiUserContainerOwnerState> ownerEntry in MultiUserContainerOwnerStates)
        {
            Container container = ownerEntry.Key;
            if (container == null || IsUnityNull(container))
            {
                deadContainers.Add(container!);
                continue;
            }

            List<MultiUserContainerOwnerRequestKey> expired = new();
            foreach (KeyValuePair<MultiUserContainerOwnerRequestKey, MultiUserContainerOwnerResponse> responseEntry in ownerEntry.Value.Responses)
            {
                if (responseEntry.Value.CreatedAt < cutoff)
                {
                    expired.Add(responseEntry.Key);
                }
            }

            foreach (MultiUserContainerOwnerRequestKey key in expired)
            {
                if (ownerEntry.Value.Responses.TryGetValue(
                        key,
                        out MultiUserContainerOwnerResponse? expiredResponse))
                {
                    ownerEntry.Value.TotalResponseBytes = Math.Max(
                        0,
                        ownerEntry.Value.TotalResponseBytes -
                        expiredResponse.ResponseBytes.Length);
                }

                ownerEntry.Value.Responses.Remove(key);
            }
        }

        foreach (Container container in deadContainers)
        {
            MultiUserContainerOwnerStates.Remove(container);
        }
    }

    private static void SendMultiUserContainerResponse(
        Container container,
        long target,
        MultiUserContainerResponse response)
    {
        if (target == 0L ||
            container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            !TryWriteMultiUserContainerResponse(response, out ZPackage? package))
        {
            return;
        }

        container.m_nview.InvokeRPC(target, MultiUserContainerResponseRpc, package);
    }

    private static void SendMultiUserContainerResponse(
        Container container,
        long target,
        byte[] responseBytes)
    {
        if (target == 0L ||
            responseBytes == null ||
            responseBytes.Length <= 0 ||
            responseBytes.Length > MultiUserContainerMaxResponsePackageBytes ||
            container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid())
        {
            return;
        }

        container.m_nview.InvokeRPC(
            target,
            MultiUserContainerResponseRpc,
            new ZPackage(responseBytes));
    }

    internal static void AcknowledgeMultiUserContainerResponse(
        Container container,
        int requestId,
        long requesterPlayerId,
        byte[] requestDigest)
    {
        if (container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            !container.m_nview.IsValid() ||
            requestId <= 0 ||
            requesterPlayerId == 0L ||
            requestDigest == null ||
            requestDigest.Length != 32)
        {
            return;
        }

        ZPackage package = new();
        package.Write(MultiUserContainerProtocolVersion);
        package.Write(requestId);
        package.Write(requesterPlayerId);
        package.Write(requestDigest);
        container.m_nview.InvokeRPC(
            MultiUserContainerAcknowledgeRpc,
            package);
    }

    internal static bool TryResolvePendingMultiUserContainerDurableReceipt(
        Container container)
    {
        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (container == null ||
            IsUnityNull(container) ||
            pending == null ||
            pending.ResponseApplied ||
            pending.TerminalFailureReceived ||
            pending.Container != container ||
            container.m_nview == null ||
            IsUnityNull(container.m_nview))
        {
            return false;
        }

        ZDO? zdo = container.m_nview?.GetZDO();
        return zdo != null &&
               TryResolvePendingMultiUserContainerDurableReceipt(
                   pending,
                   container,
                   zdo);
    }

    internal static bool TryResolvePendingMultiUserContainerDurableReceipt(
        ZDO zdo)
    {
        PendingMultiUserContainerTransfer? pending =
            _pendingMultiUserContainerTransfer;
        if (zdo == null ||
            pending == null ||
            pending.ResponseApplied ||
            pending.TerminalFailureReceived ||
            !zdo.m_uid.Equals(pending.ContainerId))
        {
            return false;
        }

        Container? container = pending.Container;
        if (container == null ||
            IsUnityNull(container) ||
            container.m_nview == null ||
            IsUnityNull(container.m_nview) ||
            container.m_nview.GetZDO() != zdo)
        {
            container = null;
        }

        return TryResolvePendingMultiUserContainerDurableReceipt(
            pending,
            container,
            zdo);
    }

    private static bool TryResolvePendingMultiUserContainerDurableReceipt(
        PendingMultiUserContainerTransfer pending,
        Container? container,
        ZDO zdo)
    {
        ObservePendingMultiUserContainerOwner(
            pending,
            zdo.GetOwner());
        if (!TryReadMultiUserContainerDurableReceipt(
                zdo,
                pending.Request.RequesterPlayerId,
                pending.Request.RequestId,
                out MultiUserContainerDurableReceipt? receipt) ||
            receipt == null ||
            receipt.Acknowledged ||
            receipt.Sender != pending.RequesterPeerId ||
            receipt.RequestId != pending.Request.RequestId ||
            !AreMultiUserContainerDigestsEqual(
                receipt.RequestDigest,
                pending.RequestDigest))
        {
            return false;
        }

        ZPackage responsePackage = new(receipt.ResponseBytes);
        if (!TryReadMultiUserContainerResponse(
                responsePackage,
                out MultiUserContainerResponse? response) ||
            response == null)
        {
            return false;
        }

        long responseSender = pending.Owner;
        if (!pending.RequestOwners.Contains(responseSender))
        {
            foreach (long knownOwner in pending.RequestOwners)
            {
                responseSender = knownOwner;
                break;
            }
        }

        HandleMultiUserContainerResponse(
            container,
            responseSender,
            response,
            fromDurableReceipt: true,
            durableReceiptZdo: zdo);
        return pending.ResponseApplied ||
               _pendingMultiUserContainerTransfer != pending;
    }

    private static bool TryWriteMultiUserContainerRequest(
        MultiUserContainerRequest request,
        out ZPackage? package)
    {
        package = null;
        if (request == null || request.Item == null)
        {
            return false;
        }

        ZPackage result = new();
        result.Write(MultiUserContainerProtocolVersion);
        result.Write(request.RequestId);
        result.Write((byte)request.Operation);
        result.Write(request.RequesterPlayerId);
        result.Write(request.SourcePosition);
        result.Write(request.TargetPosition);
        result.Write(request.ExpectedTargetStack);
        result.Write(request.Amount);
        if (!TryWriteMultiUserContainerItem(result, request.Item) ||
            !TryWriteMultiUserContainerItem(result, request.CounterpartItem) ||
            result.Size() > MultiUserContainerMaxRequestPackageBytes)
        {
            return false;
        }

        package = result;
        return true;
    }

    private static bool TryReadMultiUserContainerRequestHeader(
        ZPackage package,
        out MultiUserContainerRequest? request)
    {
        request = null;
        try
        {
            package.SetPos(0);
            if (package.ReadByte() != MultiUserContainerProtocolVersion)
            {
                return false;
            }

            MultiUserContainerRequest decoded = new()
            {
                RequestId = package.ReadInt(),
                Operation = (MultiUserContainerOperation)package.ReadByte(),
                RequesterPlayerId = package.ReadLong(),
                SourcePosition = package.ReadVector2i(),
                TargetPosition = package.ReadVector2i(),
                ExpectedTargetStack = package.ReadInt(),
                Amount = package.ReadInt()
            };
            request = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadMultiUserContainerRequestItem(
        ZPackage package,
        MultiUserContainerRequest request)
    {
        try
        {
            if (!TryReadMultiUserContainerItem(package, out ItemData? item) ||
                item == null ||
                !TryReadMultiUserContainerItem(package, out ItemData? counterpartItem) ||
                package.GetPos() != package.Size())
            {
                return false;
            }

            item.m_equipped = false;
            request.Item = item;
            if (counterpartItem != null)
            {
                counterpartItem.m_equipped = false;
            }

            request.CounterpartItem = counterpartItem;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWriteMultiUserContainerResponse(
        MultiUserContainerResponse response,
        out ZPackage? package)
    {
        package = null;
        if (response == null)
        {
            return false;
        }

        ZPackage result = new();
        result.Write(MultiUserContainerProtocolVersion);
        result.Write(response.RequestId);
        result.Write((byte)response.Operation);
        result.Write(response.Success);
        result.Write((byte)response.Failure);
        result.Write(response.Amount);
        result.Write(response.SourcePosition);
        result.Write(response.TargetPosition);
        if (!TryWriteMultiUserContainerItem(result, response.Item) ||
            result.Size() > MultiUserContainerMaxResponsePackageBytes)
        {
            return false;
        }

        package = result;
        return true;
    }

    private static bool TryReadMultiUserContainerResponse(
        ZPackage package,
        out MultiUserContainerResponse? response)
    {
        response = null;
        try
        {
            package.SetPos(0);
            if (package.ReadByte() != MultiUserContainerProtocolVersion)
            {
                return false;
            }

            MultiUserContainerResponse decoded = new()
            {
                RequestId = package.ReadInt(),
                Operation = (MultiUserContainerOperation)package.ReadByte(),
                Success = package.ReadBool(),
                Failure = (MultiUserContainerFailure)package.ReadByte(),
                Amount = package.ReadInt(),
                SourcePosition = package.ReadVector2i(),
                TargetPosition = package.ReadVector2i()
            };
            if (!TryReadMultiUserContainerItem(package, out ItemData? item) ||
                package.GetPos() != package.Size() ||
                decoded.RequestId <= 0 ||
                !Enum.IsDefined(typeof(MultiUserContainerOperation), decoded.Operation) ||
                !Enum.IsDefined(typeof(MultiUserContainerFailure), decoded.Failure) ||
                decoded.Amount < 0)
            {
                return false;
            }

            if (item != null)
            {
                item.m_equipped = false;
            }

            decoded.Item = item;
            response = decoded;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is System.IO.EndOfStreamException ||
            exception is System.IO.IOException)
        {
            return false;
        }
    }
}
