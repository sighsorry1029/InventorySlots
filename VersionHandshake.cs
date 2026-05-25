using System;
using System.Collections.Generic;
using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
public static class RegisterAndCheckVersion
{
    private static void Prefix(ZNetPeer peer)
    {
        peer.m_rpc.Register($"{InventorySlotsPlugin.ModName}_VersionCheck", new Action<ZRpc, ZPackage>(RpcHandlers.RPC_InventorySlots_Version));
        ZPackage zpackage = new();
        zpackage.Write(InventorySlotsPlugin.ModVersion);
        peer.m_rpc.Invoke($"{InventorySlotsPlugin.ModName}_VersionCheck", zpackage);
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
public static class VerifyClient
{
    private static bool Prefix(ZRpc rpc, ZNet __instance)
    {
        if (!__instance.IsServer() || RpcHandlers.ValidatedPeers.Contains(rpc))
        {
            return true;
        }

        InventorySlotsPlugin.Log.LogWarning($"Peer ({rpc.m_socket.GetHostName()}) never sent InventorySlots version, disconnecting");
        rpc.Invoke("Error", 3);
        return false;
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
public static class RemoveDisconnectedPeerFromVerified
{
    private static void Prefix(ZNetPeer peer, ZNet __instance)
    {
        if (__instance.IsServer())
        {
            _ = RpcHandlers.ValidatedPeers.Remove(peer.m_rpc);
        }
    }
}

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ShowConnectError))]
public static class ShowConnectionError
{
    private static bool Prepare() => !InventorySlotsPlugin.IsDedicatedServer;

    private static void Postfix(FejdStartup __instance)
    {
        if (__instance.m_connectionFailedPanel.activeSelf && !string.IsNullOrWhiteSpace(InventorySlotsPlugin.ConnectionError))
        {
            __instance.m_connectionFailedError.fontSizeMax = 25;
            __instance.m_connectionFailedError.fontSizeMin = 15;
            __instance.m_connectionFailedError.text += $"\n{InventorySlotsPlugin.ConnectionError}";
        }
    }
}

public static class RpcHandlers
{
    public static readonly List<ZRpc> ValidatedPeers = new();

    public static void RPC_InventorySlots_Version(ZRpc rpc, ZPackage pkg)
    {
        string? version = pkg.ReadString();
        if (version != InventorySlotsPlugin.ModVersion)
        {
            InventorySlotsPlugin.ConnectionError = $"{InventorySlotsPlugin.ModName} Installed: {InventorySlotsPlugin.ModVersion}\nNeeded: {version}";
            if (!ZNet.instance.IsServer())
            {
                return;
            }

            InventorySlotsPlugin.Log.LogWarning($"Peer ({rpc.m_socket.GetHostName()}) has incompatible InventorySlots version, disconnecting");
            rpc.Invoke("Error", 3);
            return;
        }

        if (ZNet.instance.IsServer())
        {
            ValidatedPeers.Add(rpc);
        }
    }
}
