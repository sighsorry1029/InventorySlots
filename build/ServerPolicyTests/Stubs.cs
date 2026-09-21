using System;
using System.Collections.Generic;

public class ZRpc
{
    public readonly Dictionary<string, Action<ZRpc, string>> Handlers = new();
    public readonly List<(string Name, object[] Arguments)> Sent = new();
    public void Register<T>(string name, Action<ZRpc, T> action) => Handlers[name] = (rpc, value) => action(rpc, (T)(object)value);
    public void Invoke(string name, params object[] args) => Sent.Add((name, args));
}

public class ZNetPeer
{
    public ZRpc m_rpc = new();
    public bool m_server;
}

public class ZNet
{
    public enum ConnectionStatus { None, Connecting, Connected, ErrorVersion }
    public static ZNet? instance;
    public static ConnectionStatus ExternalError;
    public bool Server;
    public bool Connected;
    public ZNetPeer? Peer;
    public bool IsServer() => Server;
    public ZNetPeer? GetServerPeer() => !Server && Connected ? Peer : null;
    public static void SetExternalError(ConnectionStatus error) => ExternalError = error;
    public void Disconnect(ZNetPeer peer) { }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { Type = type; Method = method; }
        public Type Type { get; }
        public string Method { get; }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPriority : Attribute
    {
        public HarmonyPriority(int priority) { Priority = priority; }
        public int Priority { get; }
    }
    public static class Priority { public const int First = 800; }
}

public sealed class TestLog
{
    public readonly List<string> Warnings = new();
    public void LogWarning(string message) => Warnings.Add(message);
}

#if INVENTORY_SLOTS
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions
{
    public sealed partial class InventoryActionsPlugin
#endif
    {
        internal const string ModName = "Test inventory mod";
        internal const string ModVersion = "2.3.4";
        internal const string ModGUID = "test.inventory.mod";
        internal static readonly TestLog Log = new();
    }
}
