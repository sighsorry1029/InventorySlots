using System;
using System.Collections.Generic;
using HarmonyLib;

#if INVENTORY_SLOTS
using ServerSupportPlugin = InventorySlots.InventorySlotsPlugin;
namespace InventorySlots;
#else
using ServerSupportPlugin = InventoryActions.InventoryActionsPlugin;
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    private const string ServerSupportVersionRpc = ModGUID + ".ServerSupportVersion";
    private static ZNet? _serverSupportNetwork;
    private static readonly Dictionary<ZRpc, ServerSupportPeer> ServerSupportPeers = new();

    private sealed class ServerSupportPeer
    {
        internal bool Received;
        internal string? Version;
        internal bool Rejected;
        internal bool Compatible => Received && string.Equals(Version, ModVersion, StringComparison.Ordinal);
    }

    // A capability belongs to this connection, never to a remembered server name.
    // GetServerPeer only returns an accepted, connected server peer; an absent
    // handshake keeps that connection on the vanilla container protocol.
    internal static bool UsesVanillaContainerProtocol
    {
        get
        {
            ZNet? network = ZNet.instance;
            if (network == null || network.IsServer()) return false;
            ZNetPeer? server = network.GetServerPeer();
            return server != null && server.m_server &&
                   (!ReferenceEquals(network, _serverSupportNetwork) ||
                    !ServerSupportPeers.TryGetValue(server.m_rpc, out ServerSupportPeer? state) ||
                    !state.Compatible);
        }
    }

    internal static void RegisterServerSupport(ZNet network, ZNetPeer peer)
    {
        if (!ReferenceEquals(_serverSupportNetwork, network))
        {
            ServerSupportPeers.Clear();
            _serverSupportNetwork = network;
        }
        else if (!network.IsServer() && !ServerSupportPeers.ContainsKey(peer.m_rpc))
        {
            // Reconnecting in the same ZNet must not inherit its previous peer.
            ServerSupportPeers.Clear();
        }

        if (ServerSupportPeers.ContainsKey(peer.m_rpc)) return;
        ServerSupportPeer state = new();
        ServerSupportPeers.Add(peer.m_rpc, state);
        peer.m_rpc.Register<string>(ServerSupportVersionRpc, (rpc, version) =>
        {
            if (!ReferenceEquals(ZNet.instance, network) ||
                !ReferenceEquals(_serverSupportNetwork, network) ||
                !ReferenceEquals(rpc, peer.m_rpc) ||
                !ServerSupportPeers.TryGetValue(rpc, out ServerSupportPeer? current) ||
                !ReferenceEquals(current, state) || state.Received)
            {
                return;
            }

            state.Version = version;
            state.Received = true;
        });

        // Send once, before native ServerHandshake/PeerInfo. Both Steam and
        // PlayFab RPC streams preserve this order. Vanilla peers ignore this
        // unknown RPC; no mod RPC probes are sent during container operations.
        peer.m_rpc.Invoke(ServerSupportVersionRpc, ModVersion);
    }

    internal static bool ValidateServerSupport(ZNet network, ZRpc rpc)
    {
        ServerSupportPeer? state = null;
        if (ReferenceEquals(_serverSupportNetwork, network)) ServerSupportPeers.TryGetValue(rpc, out state);

        // ServerSync remains optional on clients. On a modded server, this
        // additional gate requires the same mod/version before accepting peers.
        // A client distinguishes silence (vanilla) from an explicit mismatch.
        if (network.IsServer() ? state?.Compatible == true : state == null || !state.Received || state.Compatible)
        {
            return true;
        }

        if (state == null || !state.Rejected)
        {
            Log.LogWarning($"{ModName} version check failed (local: {ModVersion}, " +
                           $"remote: {state?.Version ?? "no mod version"}). A server with {ModName} requires matching clients.");
            if (state != null) state.Rejected = true;
        }

        if (network.IsServer()) rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);
        else ZNet.SetExternalError(ZNet.ConnectionStatus.ErrorVersion);
        return false;
    }

    internal static void RemoveServerSupportPeer(ZNet network, ZNetPeer peer)
    {
        if (ReferenceEquals(_serverSupportNetwork, network)) ServerSupportPeers.Remove(peer.m_rpc);
    }

    internal static void ResetServerSupport(ZNet network)
    {
        if (!ReferenceEquals(_serverSupportNetwork, network)) return;
        ServerSupportPeers.Clear();
        _serverSupportNetwork = null;
    }
}

[HarmonyPatch(typeof(ZNet), "OnNewConnection")]
internal static class OptionalServerSupportConnectionPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ZNet __instance, ZNetPeer peer) => ServerSupportPlugin.RegisterServerSupport(__instance, peer);
}

[HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
internal static class OptionalServerSupportPeerInfoPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ZNet __instance, ZRpc rpc) => ServerSupportPlugin.ValidateServerSupport(__instance, rpc);
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
internal static class OptionalServerSupportDisconnectPatch
{
    private static void Prefix(ZNet __instance, ZNetPeer peer) => ServerSupportPlugin.RemoveServerSupportPeer(__instance, peer);
}

[HarmonyPatch(typeof(ZNet), "StopAll")]
internal static class OptionalServerSupportShutdownPatch
{
    private static void Prefix(ZNet __instance) => ServerSupportPlugin.ResetServerSupport(__instance);
}
