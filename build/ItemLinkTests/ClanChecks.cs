using System;
using System.Collections.Generic;
using InventorySlots;
using UnityEngine;

// Compile the real optional adapter and RPC callbacks; simulate only transport,
// platform permission, plugin discovery and Clan's public API at their boundaries.
namespace BepInEx
{
    public class BaseUnityPlugin { public bool isActiveAndEnabled = true; }
    public sealed class PluginInfo { public BaseUnityPlugin Instance = null!; }
}
namespace BepInEx.Bootstrap
{
    public static class Chainloader { public static Dictionary<string, BepInEx.PluginInfo> PluginInfos = new(); }
}
namespace Splatform
{
    internal struct PlatformUserID { }
    internal enum Permission { CommunicateWithUsingText }
    internal enum RelationsManagerPermissionResult { Denied, Granted, GrantedRequiresFiltering }
    internal static class RelationsManager
    {
        internal static readonly Queue<Action<RelationsManagerPermissionResult>> Pending = new();
        internal static void CheckPermissionAsync(PlatformUserID user, Permission permission, bool send,
            Action<RelationsManagerPermissionResult> callback) => Pending.Enqueue(callback);
        internal static void Finish() => Pending.Dequeue()(RelationsManagerPermissionResult.Granted);
    }
}
namespace Clan
{
    public sealed class ClanPlugin : BepInEx.BaseUnityPlugin { }
    public static class ClanApi
    {
        public static event Action<string, long, string, Action<string>>? ClientChatFormatting;
        public static string ChatClanId { get; set; } = "alpha";
        public static HashSet<long> Members = new() { 5, 7 };
        public static bool CanShareChatWithPeer(string id, long uid) => id == ChatClanId && Members.Contains(uid);
        internal static int Handlers => ClientChatFormatting?.GetInvocationList().Length ?? 0;
        internal static string Receive(long author, string body)
        {
            string result = body;
            ClientChatFormatting?.Invoke(ChatClanId, author, body, replacement => result = replacement);
            return result;
        }
    }
}
internal sealed class Player { internal static Player? m_localPlayer = new(); }
internal static class ZNet { internal static long GetUID() => 5; }
internal sealed class ZRoutedRpc
{
    internal static ZRoutedRpc instance = new();
    internal readonly List<(long Peer, string Method)> Sends = new();
    internal void InvokeRoutedRPC(long peer, string method, params object[] args) => Sends.Add((peer, method));
}
internal sealed class ZPackage
{
    private readonly byte[] _data;
    internal ZPackage(byte[] data) { _data = data; }
    internal int Size() => _data.Length;
    internal byte[] GetArray() => _data;
}
internal static class CensorShittyWords { internal static void Filter(string value, out string filtered) => filtered = value; }
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
    {
        private static readonly ItemLinkStore ItemLinks = new();
        private const string ItemLinkRequestRpc = "request", ItemLinkResponseRpc = "response";
        private static ZRoutedRpc? _itemLinkRegisteredRpc = ZRoutedRpc.instance;
        private static Chat? _itemLinkChat;
        private static int _itemLinkGeneration = 0;
        private sealed class TestLog { internal void LogWarning(string message) { } }
        private static readonly TestLog Log = new();
        private static bool TryGetItemLinkUser(long id, out Splatform.PlatformUserID user) { user = default; return id != 0; }

        internal static void RunClanChecks(Action<bool, string> check)
        {
            _itemLinkChat = Chat.instance = new Chat();
            Time.unscaledTime = 100;
            UpdateClanItemLinks();
            check(Clan.ClanApi.Handlers == 0, "Missing Clan has no hard dependency or subscription");
            var plugin = new Clan.ClanPlugin();
            BepInEx.Bootstrap.Chainloader.PluginInfos["sighsorry.Clan"] = new BepInEx.PluginInfo { Instance = plugin };
            Time.unscaledTime += 3;
            UpdateClanItemLinks(); UpdateClanItemLinks();
            check(Clan.ClanApi.Handlers == 1, "Lazy optional binding subscribes exactly once");
            const string token = "0123456789abcdef";
            string marker = ItemLinkWire.MarkerText("Hammer", token);
            byte[] data = ItemLinkWire.Encode(new ItemLinkSnapshot { Prefab = "Hammer", Label = "Hammer", Body = "safe snapshot" });
            ItemLinks.AddOffer(token, data, Time.unscaledTime);
            check(ItemLinks.GetOffer(token, Time.unscaledTime, 7, CanShareClanItemLink) == null, "Draft/queued send cannot publish private offer");
            string line = Clan.ClanApi.Receive(7, marker);
            check(line.Contains("isitem:7:" + token) && !line.Contains("isitem:5:"), "Server-attested author defines link identity");
            check(ItemLinks.GetOffer(token, Time.unscaledTime, 7, CanShareClanItemLink) == null, "Other author's echo cannot expose our draft");
            Clan.ClanApi.Receive(5, marker);
            check(ItemLinks.GetOffer(token, Time.unscaledTime) == null &&
                ItemLinks.GetOffer(token, Time.unscaledTime, 7, CanShareClanItemLink) != null &&
                ItemLinks.GetOffer(token, Time.unscaledTime, 9, CanShareClanItemLink) == null,
                "Accepted own echo publishes only to current clan members");

            ReceiveItemLinkRequest(9, token, token);
            check(Splatform.RelationsManager.Pending.Count == 0, "Outsider rejected before platform permission work");
            ReceiveItemLinkRequest(7, token, token);
            Clan.ClanApi.Members.Remove(7);
            Splatform.RelationsManager.Finish();
            check(ZRoutedRpc.instance.Sends.Count == 0, "Kick during async permission check suppresses private response");
            Clan.ClanApi.Members.Add(7);
            ReceiveItemLinkRequest(7, token, token);
            Splatform.RelationsManager.Finish();
            check(ZRoutedRpc.instance.Sends.Count == 1 && ZRoutedRpc.instance.Sends[0] == (7, "response"), "Permitted member receives snapshot after permission check");

            ItemLinkStore.Entry remote = ItemLinks.Find(ItemLinkStore.Key(7, token), Time.unscaledTime)!;
            RequestItemLink(remote);
            ReceiveItemLinkResponse(7, token, remote.Nonce, new ZPackage(data));
            Clan.ClanApi.Members.Remove(7);
            Splatform.RelationsManager.Finish();
            check(remote.Snapshot == null, "Receiver also rechecks sender membership after async permission");
            Clan.ClanApi.Members.Add(7);
            Time.unscaledTime += 5;
            RequestItemLink(remote);
            ReceiveItemLinkResponse(7, token, remote.Nonce, new ZPackage(data));
            Clan.ClanApi.ChatClanId = "beta";
            UpdateClanItemLinks();
            Splatform.RelationsManager.Finish();
            check(remote.Snapshot == null && ItemLinks.Find(ItemLinkStore.Key(7, token), Time.unscaledTime) == null,
                "Clan switch removes private entries and prevents late responses");
            check(ItemLinks.GetOffer(token, Time.unscaledTime, 7, CanShareClanItemLink) == null, "Old private offers do not follow player to another clan");

            // Explicit native re-sharing is independent of future clan membership.
            ItemLinks.MarkSent(marker, Time.unscaledTime);
            ItemLinkStore.Entry native = ItemLinks.Observe(7, token, "Hammer", Time.unscaledTime);
            Clan.ClanApi.Receive(7, marker);
            Clan.ClanApi.ChatClanId = "gamma";
            UpdateClanItemLinks();
            check(ItemLinks.GetOffer(token, Time.unscaledTime) != null &&
                ItemLinks.Find(ItemLinkStore.Key(7, token), Time.unscaledTime) == native,
                "Native re-sharing and native receipt survive clan scope cleanup");

            Enabled = false;
            check(Clan.ClanApi.Receive(7, marker) == marker, "Disabled feature callback leaves readable label");
            Enabled = true;
            plugin.isActiveAndEnabled = false;
            UpdateClanItemLinks();
            check(Clan.ClanApi.Handlers == 0 && !CanShareClanItemLink("gamma", 7), "Disabled optional plugin unsubscribes and fails closed");
            plugin.isActiveAndEnabled = true;
            Time.unscaledTime += 3;
            UpdateClanItemLinks();
            check(Clan.ClanApi.Handlers == 1, "Reenabled plugin binds once again");
            UnbindClanItemLinks(); UnbindClanItemLinks();
            check(Clan.ClanApi.Handlers == 0, "Repeated teardown is safe");
            ItemLinks.Clear();
        }
    }
}
internal static class ClanChecks
{
    internal static void Run(Action<bool, string> check) => InventorySlotsPlugin.RunClanChecks(check);
}
