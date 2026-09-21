using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

#if INVENTORY_SLOTS
using NativeContainerPlugin = InventorySlots.InventorySlotsPlugin;
namespace InventorySlots;
#else
using NativeContainerPlugin = InventoryActions.InventoryActionsPlugin;
namespace InventoryActions;
#endif

internal enum NativeContainerHandoffResult { Pending, Granted, Failed }

// A vanilla peer already knows this ownership request. Its response has no request
// ID, so each unreturned request must keep its chest fenced even after cancellation.
internal static class NativeContainerHandoff
{
    private const float RequestTimeout = 5f;
    private static readonly Dictionary<ZDOID, Request> Requests = new();
    private static ConditionalWeakTable<Container, Request> _responseOwners = new();
    private static Request? _active;
    private static ZNet? _network;
    private static long _session;

    private sealed class Request
    {
        internal Container? Container;
        internal Player? Player;
        internal ZDO? Zdo;
        internal ZDOID Id;
        internal long Owner;
        internal ushort OwnerRevision;
        internal uint DataRevision;
        internal float Deadline;
        internal bool Adapter;
        internal bool Cancelled;
        internal bool? Response;
    }

    private static class Access
    {
        internal static readonly AccessTools.FieldRef<Container, ZNetView> View =
            AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
        internal static readonly AccessTools.FieldRef<Container, uint> LoadedRevision =
            AccessTools.FieldRefAccess<Container, uint>("m_lastRevision");
        internal static readonly AccessTools.FieldRef<Container, bool> Loading =
            AccessTools.FieldRefAccess<Container, bool>("m_loading");
        internal static readonly Func<Container, bool> Load =
            AccessTools.MethodDelegate<Func<Container, bool>>(
                AccessTools.DeclaredMethod(typeof(Container), "Load"));
    }

    internal static bool CanBegin
    {
        get
        {
            RefreshSession();
            if (_session == 0 || _active != null)
            {
                return false;
            }

            foreach (Request request in Requests.Values)
            {
                if (!request.Cancelled)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal static bool TryBegin(Container container, Player player)
    {
        if (!CanBegin || player == null || player != Player.m_localPlayer ||
            player.GetPlayerID() == 0 || !TryGetView(container, out ZNetView view, out ZDO zdo) ||
            Requests.ContainsKey(zdo.m_uid) || zdo.GetOwner() == 0 || zdo.GetOwner() == _session ||
            zdo.OwnerRevision == ushort.MaxValue)
        {
            return false;
        }

        Request request = new()
        {
            Container = container,
            Player = player,
            Zdo = zdo,
            Id = zdo.m_uid,
            Owner = zdo.GetOwner(),
            OwnerRevision = zdo.OwnerRevision,
            DataRevision = zdo.DataRevision,
            Deadline = Time.realtimeSinceStartup + RequestTimeout,
            Adapter = true
        };
        Track(container, request);
        _active = request;
        try
        {
            // Address the owner captured above, rather than rerouting the request
            // if ownership changes between validation and dispatch.
            view.InvokeRPC(request.Owner, "RPC_RequestStack", player.GetPlayerID());
            return true;
        }
        catch
        {
            // The transport may already have queued the call before throwing.
            Retire(request);
            return false;
        }
    }

    internal static NativeContainerHandoffResult Poll(Container container)
    {
        RefreshSession();
        Request? request = _active;
        if (request == null || !ReferenceEquals(request.Container, container))
        {
            return NativeContainerHandoffResult.Failed;
        }

        if (request.Player == null || request.Player != Player.m_localPlayer ||
            !TryGetView(container, out ZNetView view, out ZDO zdo) ||
            !ReferenceEquals(request.Zdo, zdo) || request.Id != zdo.m_uid ||
            request.Response == false || zdo.DataRevision < request.DataRevision)
        {
            return Fail(request);
        }

        bool originalOwner = zdo.GetOwner() == request.Owner && zdo.OwnerRevision == request.OwnerRevision;
        bool grantedOwner = view.IsOwner() && zdo.GetOwner() == _session &&
                            zdo.OwnerRevision == request.OwnerRevision + 1;
        if (!originalOwner && !grantedOwner)
        {
            // Returning to this client after another handoff is a different grant.
            return Fail(request);
        }

        if (request.Response != true || !grantedOwner)
        {
            return NativeContainerHandoffResult.Pending;
        }

        // ForceSendZDO only queues data: the bool RPC is not a freshness proof.
        // The newly observed owner revision arrived in a ZDOData snapshot carrying
        // the inventory. An unchanged DataRevision is valid for an unchanged chest.
        // Load directly: CheckForChanges also writes visual/in-use metadata.
        uint dataRevision = zdo.DataRevision;
        bool loaded = RefreshInventory(container);

        if (!TryGetView(container, out ZNetView currentView, out ZDO currentZdo) ||
            !ReferenceEquals(zdo, currentZdo) || !currentView.IsOwner() ||
            currentZdo.GetOwner() != _session || currentZdo.OwnerRevision != request.OwnerRevision + 1 ||
            currentZdo.DataRevision != dataRevision || container.GetInventory() == null)
        {
            return Fail(request);
        }

        if (!loaded)
        {
            // For example, Load refuses a locally open chest. Do not bypass it.
            return NativeContainerHandoffResult.Pending;
        }

        Retire(request);
        return NativeContainerHandoffResult.Granted;
    }

    internal static bool RefreshInventory(Container container)
    {
        if (!TryGetView(container, out ZNetView view, out ZDO zdo) || !view.IsOwner())
        {
            return false;
        }
        uint dataRevision = zdo.DataRevision;
        ushort ownerRevision = zdo.OwnerRevision;
        long owner = zdo.GetOwner();
        try
        {
            Access.Load(container);
            return TryGetView(container, out ZNetView currentView, out ZDO currentZdo) &&
                   ReferenceEquals(zdo, currentZdo) && currentView.IsOwner() &&
                   currentZdo.GetOwner() == owner && currentZdo.OwnerRevision == ownerRevision &&
                   currentZdo.DataRevision == dataRevision && !Access.Loading(container) &&
                   Access.LoadedRevision(container) == dataRevision && container.GetInventory() != null;
        }
        catch
        {
            return false;
        }
    }

    internal static void Cancel(Container? container = null)
    {
        RefreshSession();
        Request? request = _active;
        if (request != null && (ReferenceEquals(container, null) || ReferenceEquals(request.Container, container)))
        {
            Retire(request);
        }
    }

    internal static bool IsRequestBlocked(Container container)
    {
        RefreshSession();
        return TryGetView(container, out _, out ZDO zdo) && Requests.ContainsKey(zdo.m_uid);
    }

    internal static void ClearSession()
    {
        _active = null;
        Requests.Clear();
        _responseOwners = new ConditionalWeakTable<Container, Request>();
        _network = null;
        _session = 0;
    }

    private static NativeContainerHandoffResult Fail(Request request)
    {
        Retire(request);
        return NativeContainerHandoffResult.Failed;
    }

    private static void Retire(Request request)
    {
        if (ReferenceEquals(_active, request))
        {
            _active = null;
        }

        request.Cancelled = true;
        request.Container = null;
        request.Player = null;
        request.Zdo = null;
        if (request.Response.HasValue)
        {
            Requests.Remove(request.Id);
        }
        // Without a response there is no safe timeout for removing this fence.
        // Keep only the ID/responder until the late RPC or network shutdown.
    }

    private static void RefreshSession()
    {
        ZNet? network = ZNet.instance;
        long session = network != null ? ZNet.GetUID() : 0;
        if (!ReferenceEquals(network, _network) || session != _session)
        {
            ClearSession();
            _network = network;
            _session = session;
        }

        // Expiry leaves the response fence, but releases Unity/player references.
        foreach (Request request in Requests.Values)
        {
            if (!request.Cancelled && Time.realtimeSinceStartup >= request.Deadline)
            {
                // A response-complete request must be retired outside enumeration.
                if (request.Response.HasValue)
                {
                    continue;
                }
                Retire(request);
            }
        }

        if (_active != null && Time.realtimeSinceStartup >= _active.Deadline)
        {
            Retire(_active);
        }
    }

    private static bool TryGetView(Container container, out ZNetView view, out ZDO zdo)
    {
        view = null!;
        zdo = null!;
        if (container == null)
        {
            return false;
        }
        view = Access.View(container);
        if (view == null || !view.IsValid())
        {
            return false;
        }
        zdo = view.GetZDO();
        return zdo != null && !zdo.m_uid.IsNone();
    }

    private static void Track(Container container, Request request)
    {
        Requests.Add(request.Id, request);
        _responseOwners.Remove(container);
        _responseOwners.Add(container, request);
    }

    private static bool TryGetResponseRequest(Container container, out Request request)
    {
        request = null!;
        if (container is null)
        {
            return false;
        }
        // A queued callback can outlive a valid nview. Keep its association weakly
        // so losing the view cannot send a cancelled reply into vanilla StackAll.
        if (_responseOwners.TryGetValue(container, out Request? tracked) &&
            Requests.TryGetValue(tracked.Id, out Request? pending) && ReferenceEquals(tracked, pending))
        {
            request = tracked;
            return true;
        }
        if (TryGetView(container, out _, out ZDO zdo) && Requests.TryGetValue(zdo.m_uid, out Request? byId))
        {
            request = byId;
            return true;
        }
        return false;
    }

    // Track ordinary requests too: their untagged response must not be adopted by
    // a subsequently started area action. Run after other StackAll prefixes.
    internal static bool BeforeNativeStackAll(Container container, bool runOriginal)
    {
        if (!runOriginal)
        {
            return true;
        }
        RefreshSession();
        if (_active != null || IsRequestBlocked(container))
        {
            return false;
        }
        if (!NativeContainerPlugin.UsesVanillaContainerProtocol || _session == 0 ||
            !TryGetView(container, out _, out ZDO zdo))
        {
            return true;
        }

        Track(container, new Request
        {
            Id = zdo.m_uid,
            Owner = zdo.GetOwner(),
            Deadline = Time.realtimeSinceStartup + RequestTimeout
        });
        return true;
    }

    // false means this response belongs to the adapter and must never reach the
    // vanilla inventory.StackAll callback, including denied and cancelled replies.
    internal static bool BeforeNativeStackResponse(Container container, long sender, bool granted)
    {
        RefreshSession();
        if (!TryGetResponseRequest(container, out Request request))
        {
            return true;
        }
        if (sender != request.Owner || request.Response.HasValue)
        {
            return false;
        }

        bool allowVanilla = !request.Adapter && !request.Cancelled;
        request.Response = granted;
        if (!request.Adapter || request.Cancelled)
        {
            Retire(request);
        }
        return allowVanilla;
    }

    internal static void BeforeContainerDestroyed(Container container)
    {
        if (TryGetResponseRequest(container, out Request request))
        {
            Retire(request);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.StackAll))]
    private static class StackAllPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(Container __instance, bool __runOriginal) =>
            BeforeNativeStackAll(__instance, __runOriginal);
    }

    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    private static class StackResponsePatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Container __instance, long uid, bool granted) =>
            BeforeNativeStackResponse(__instance, uid, granted);
    }

    [HarmonyPatch(typeof(Container), "OnDestroyed")]
    private static class DestroyedPatch
    {
        private static void Prefix(Container __instance) => BeforeContainerDestroyed(__instance);
    }

    // Both Shutdown and ShutdownWithoutSave converge here, including suspension.
    // Object identity/session UID alone need not change at a connection boundary.
    [HarmonyPatch(typeof(ZNet), "StopAll")]
    private static class StopAllPatch
    {
        private static void Postfix() => ClearSession();
    }
}
