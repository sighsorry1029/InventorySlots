using System;
using System.Linq;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static class Program
{
    private static int _checks;
    private static void Check(bool result, string name)
    {
        _checks++;
        if (!result) throw new InvalidOperationException(name);
    }

    private static (ZNet Network, ZNetPeer Peer) Connect(bool server = false)
    {
        ZNet network = new() { Server = server };
        ZNet.instance = network;
        ZNet.ExternalError = ZNet.ConnectionStatus.None;
        ZNetPeer peer = new() { m_server = !server };
        network.Peer = peer;
        Plugin.RegisterServerSupport(network, peer);
        return (network, peer);
    }

    private static void Announce(ZNetPeer peer, string version = Plugin.ModVersion) =>
        peer.m_rpc.Handlers.Values.Single()(peer.m_rpc, version);

    private static void Main()
    {
        ZNet.instance = null;
        Check(!Plugin.UsesVanillaContainerProtocol, "No network is not remote multiplayer");
        var (client, server) = Connect();
        Check(server.m_rpc.Sent.Count == 1, "Exactly one capability announcement on connect");
        Check(server.m_rpc.Sent[0].Arguments.Single().Equals(Plugin.ModVersion), "Announce own exact version");
        Plugin.RegisterServerSupport(client, server);
        Check(server.m_rpc.Sent.Count == 1, "Duplicate registration does not send another announcement");
        server.m_rpc.Invoke("ServerHandshake");
        Check(server.m_rpc.Sent[0].Name.EndsWith(".ServerSupportVersion") && server.m_rpc.Sent[1].Name == "ServerHandshake", "Capability precedes native handshake");
        Check(!Plugin.UsesVanillaContainerProtocol, "Connecting does not expose a connected server");
        Check(Plugin.ValidateServerSupport(client, server.m_rpc), "Client is allowed to join vanilla server");
        Check(ZNet.ExternalError == ZNet.ConnectionStatus.None, "Vanilla server does not set connection error");
        client.Connected = true;
        Check(Plugin.UsesVanillaContainerProtocol, "Connected vanilla server uses vanilla container protocol");
        Announce(server);
        Check(!Plugin.UsesVanillaContainerProtocol, "Exact server capability enables mod protocol");
        Check(Plugin.ValidateServerSupport(client, server.m_rpc), "Matching mod server passes client gate");
        Announce(server, "9.9.9");
        Check(!Plugin.UsesVanillaContainerProtocol, "Duplicate announcements cannot mutate accepted session capability");

        (client, server) = Connect();
        Announce(server, "2.3.3");
        Check(!Plugin.ValidateServerSupport(client, server.m_rpc), "Client rejects explicitly older mod server");
        Check(ZNet.ExternalError == ZNet.ConnectionStatus.ErrorVersion, "Client mismatch reports native version error");
        Announce(server);
        Check(!Plugin.ValidateServerSupport(client, server.m_rpc), "Rejected first version cannot later validate this connection");

        var (host, missingClient) = Connect(server: true);
        host.Connected = true;
        Check(!Plugin.UsesVanillaContainerProtocol, "Local server never chooses remote vanilla protocol");
        Check(!Plugin.ValidateServerSupport(host, missingClient.m_rpc), "Mod server rejects missing client mod");
        Check(missingClient.m_rpc.Sent.Last().Name == "Error" && missingClient.m_rpc.Sent.Last().Arguments.Single().Equals(3), "Missing mod returns native version error");
        Check(!Plugin.ValidateServerSupport(host, new ZRpc()), "Unregistered RPC cannot bypass server gate");
        ZNetPeer validClient = new();
        Plugin.RegisterServerSupport(host, validClient);
        Announce(validClient);
        Check(Plugin.ValidateServerSupport(host, validClient.m_rpc), "Matching client joins mod server");
        Check(!Plugin.ValidateServerSupport(host, missingClient.m_rpc), "One validated client does not validate another");
        ZNetPeer wrongClient = new();
        Plugin.RegisterServerSupport(host, wrongClient);
        Announce(wrongClient, "2.3.5");
        Check(!Plugin.ValidateServerSupport(host, wrongClient.m_rpc), "Newer client also rejected by exact policy");
        Plugin.RemoveServerSupportPeer(host, missingClient);
        Check(Plugin.ValidateServerSupport(host, validClient.m_rpc), "Disconnecting one peer preserves other validation");
        Plugin.RemoveServerSupportPeer(host, validClient);
        Check(!Plugin.ValidateServerSupport(host, validClient.m_rpc), "Disconnected peer loses validation");
        Announce(validClient);
        Check(!Plugin.ValidateServerSupport(host, validClient.m_rpc), "Late packet cannot restore disconnected peer");

        (client, server) = Connect();
        Announce(server);
        client.Connected = true;
        Check(!Plugin.UsesVanillaContainerProtocol, "First modded server confirmed");
        ZNetPeer nextServer = new() { m_server = true };
        client.Peer = nextServer;
        Plugin.RegisterServerSupport(client, nextServer);
        Check(Plugin.UsesVanillaContainerProtocol, "Reconnect in same network does not inherit capability");
        Announce(server);
        Check(Plugin.UsesVanillaContainerProtocol, "Late old server announcement ignored after reconnect");
        nextServer.m_rpc.Handlers.Values.Single()(server.m_rpc, Plugin.ModVersion);
        Check(Plugin.UsesVanillaContainerProtocol, "Handler rejects message bearing another RPC identity");
        Announce(nextServer);
        Check(!Plugin.UsesVanillaContainerProtocol, "New matching server may validate itself");
        Plugin.ResetServerSupport(host);
        Check(!Plugin.UsesVanillaContainerProtocol, "Old network shutdown cannot clear new connection");
        Plugin.ResetServerSupport(client);
        Check(Plugin.UsesVanillaContainerProtocol, "Shutdown clears current capability");
        Announce(nextServer);
        Check(Plugin.UsesVanillaContainerProtocol, "Late callback after shutdown ignored");
        Plugin.RegisterServerSupport(client, nextServer);
        Announce(nextServer);
        Check(!Plugin.UsesVanillaContainerProtocol, "Fresh registration after shutdown can validate");
        Plugin.RemoveServerSupportPeer(client, nextServer);
        Check(Plugin.UsesVanillaContainerProtocol, "Client disconnect clears capability");
        Announce(nextServer);
        Check(Plugin.UsesVanillaContainerProtocol, "Late packet after client disconnect ignored");

        ZNet replacement = new() { Connected = true, Peer = nextServer };
        ZNet.instance = replacement;
        Check(Plugin.UsesVanillaContainerProtocol, "Unregistered replacement network cannot inherit old capability");
        ZNet.instance = new ZNet { Server = true, Connected = true };
        Check(!Plugin.UsesVanillaContainerProtocol, "Single player uses local protocol");
        Console.WriteLine($"PASS: {_checks} {typeof(Plugin).Namespace} server policy checks");
    }
}
