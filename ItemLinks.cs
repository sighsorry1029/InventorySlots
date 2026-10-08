using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ItemLinkRequestRpc = "InventorySlots_ItemLinkRequest_v1";
    private const string ItemLinkResponseRpc = "InventorySlots_ItemLinkResponse_v1";
    private static ConfigEntry<Toggle> _enableItemLinks = null!;
    private static readonly ItemLinkStore ItemLinks = new();
    private static ZRoutedRpc? _itemLinkRegisteredRpc;
    private static Chat? _itemLinkChat;
    private static long _itemLinkCharacter;
    private static int _itemLinkGeneration;
    private static bool _itemLinksWereEnabled;
    private static double _itemLinkNextPrune;
    private static readonly AccessTools.FieldRef<Chat, float> ItemLinkChatHideTimer = AccessTools.FieldRefAccess<Chat, float>("m_hideTimer");

    private static bool ItemLinksEnabled => _instance != null && _instance.isActiveAndEnabled &&
        _enableItemLinks?.Value == Toggle.On && !IsDedicatedServer;

    private static void BindItemLinksConfig()
    {
        _enableItemLinks = OrderedConfigEntry(ClientUiConfigSection, "Enable Item Tooltips in Chat", Toggle.On,
            "Share inventory and container item tooltips in chat. The chat button on a pinned item tooltip adds the item to your chat draft; press Enter to send. Crafting previews cannot be shared. With chat open, hover a shared item to view its tooltip, or press Mouse3 to pin it. Other players need InventorySlots to view shared tooltips; otherwise they see a plain item label. Tooltips show a snapshot of the sender's language and displayed stats, not a transferable item. Shared items can be opened for 10 minutes while the sender is online with sharing enabled. Pinned snapshots remain until closed or disconnected. Client-only; not synced with the server.",
            order: 855, synchronizedSetting: false);
    }

    private static void UpdateItemLinks()
    {
        bool enabled = ItemLinksEnabled;
        Chat? chat = Chat.instance;
        Player? player = Player.m_localPlayer;
        long character = player != null ? Game.instance?.GetPlayerProfile()?.GetPlayerID() ?? player.GetPlayerID() : _itemLinkCharacter;
        if (_itemLinkChat != chat || _itemLinkRegisteredRpc != ZRoutedRpc.instance ||
            _itemLinkCharacter != character || _itemLinksWereEnabled != enabled)
        {
            ResetItemLinks();
            _itemLinkChat = chat;
            _itemLinkCharacter = character;
            _itemLinksWereEnabled = enabled;
            if (ZRoutedRpc.instance == null) _itemLinkRegisteredRpc = null;
        }
        if (ZRoutedRpc.instance != null && _itemLinkRegisteredRpc != ZRoutedRpc.instance)
        {
            _itemLinkRegisteredRpc = ZRoutedRpc.instance;
            // Register once per router, including while disabled. Handlers are inert
            // when off; changing the client setting never registers duplicate keys.
            _itemLinkRegisteredRpc.Register<string, string>(ItemLinkRequestRpc, ReceiveItemLinkRequest);
            _itemLinkRegisteredRpc.Register<string, string, ZPackage>(ItemLinkResponseRpc, ReceiveItemLinkResponse);
        }
        if (!enabled || player == null || chat == null) { HideItemLinkUi(); return; }
        UpdateClanItemLinks();
        if (Time.unscaledTime >= _itemLinkNextPrune)
        {
            _itemLinkNextPrune = Time.unscaledTime + 30;
            ItemLinks.Prune(Time.unscaledTime);
        }
        UpdateItemLinkUi();
    }

    private static void ResetItemLinks()
    {
        UnbindClanItemLinks();
        ++_itemLinkGeneration;
        ItemLinks.Clear();
        DestroyItemLinkUi();
    }

    private static bool TryGetItemLinkUser(long peer, out PlatformUserID user)
    {
        user = default;
        if (peer == 0 || ZNet.instance == null) return false;
        foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
        {
            if (info.m_characterID != ZDOID.None && info.m_characterID.UserID == peer)
            {
                user = info.m_userInfo.m_id;
                return user.IsValid;
            }
        }
        return false;
    }

    private static bool ItemLinkSessionValid(int generation, ZRoutedRpc rpc) =>
        ItemLinksEnabled && generation == _itemLinkGeneration && rpc == ZRoutedRpc.instance &&
        _itemLinkChat == Chat.instance && Player.m_localPlayer != null;

    private static void ReceiveItemLinkRequest(long sender, string token, string nonce)
    {
        if (!ItemLinksEnabled || !ItemLinkWire.IsToken(token) || !ItemLinkWire.IsToken(nonce) ||
            !TryGetItemLinkUser(sender, out PlatformUserID user) ||
            ItemLinks.GetOffer(token, Time.unscaledTime, sender, CanShareClanItemLink) == null || !ItemLinks.AllowResponse(sender, Time.unscaledTime)) return;
        int generation = _itemLinkGeneration;
        ZRoutedRpc rpc = ZRoutedRpc.instance;
        RelationsManager.CheckPermissionAsync(user, Permission.CommunicateWithUsingText, true, result =>
        {
            if (!ItemLinkSessionValid(generation, rpc) ||
                result != RelationsManagerPermissionResult.Granted && result != RelationsManagerPermissionResult.GrantedRequiresFiltering) return;
            byte[]? data = ItemLinks.GetOffer(token, Time.unscaledTime, sender, CanShareClanItemLink);
            if (data == null) return;
            if (result == RelationsManagerPermissionResult.GrantedRequiresFiltering)
            {
                ItemLinkSnapshot? snapshot = ItemLinkWire.Decode(data);
                if (snapshot == null) return;
                FilterItemLinkSnapshot(snapshot);
                data = ItemLinkWire.Encode(snapshot);
            }
            rpc.InvokeRoutedRPC(sender, ItemLinkResponseRpc, token, nonce, new ZPackage(data));
        });
    }

    private static void ReceiveItemLinkResponse(long sender, string token, string nonce, ZPackage packet)
    {
        if (!ItemLinksEnabled || !ItemLinkWire.IsToken(token) || !ItemLinkWire.IsToken(nonce) || packet == null ||
            packet.Size() > ItemLinkWire.MaxBytes || !TryGetItemLinkUser(sender, out PlatformUserID user)) return;
        if (!CanUseItemLinkEntry(ItemLinks.Find(ItemLinkStore.Key(sender, token), Time.unscaledTime)) ||
            !ItemLinks.BeginResponse(sender, token, nonce, Time.unscaledTime)) return;
        ItemLinkSnapshot? snapshot = ItemLinkWire.Decode(packet.GetArray());
        if (snapshot == null) return;
        int generation = _itemLinkGeneration;
        ZRoutedRpc rpc = ZRoutedRpc.instance;
        RelationsManager.CheckPermissionAsync(user, Permission.CommunicateWithUsingText, false, result =>
        {
            if (!ItemLinkSessionValid(generation, rpc) ||
                !CanUseItemLinkEntry(ItemLinks.Find(ItemLinkStore.Key(sender, token), Time.unscaledTime)) ||
                result != RelationsManagerPermissionResult.Granted && result != RelationsManagerPermissionResult.GrantedRequiresFiltering) return;
            if (result == RelationsManagerPermissionResult.GrantedRequiresFiltering) FilterItemLinkSnapshot(snapshot);
            ItemLinks.Accept(sender, token, nonce, ItemLinkWire.Encode(snapshot), Time.unscaledTime);
        });
    }

    private static void FilterItemLinkSnapshot(ItemLinkSnapshot snapshot)
    {
        CensorShittyWords.Filter(snapshot.Label, out snapshot.Label);
        CensorShittyWords.Filter(snapshot.Body, out snapshot.Body);
        snapshot.Label = ItemLinkWire.Plain(snapshot.Label, 120);
        snapshot.Body = ItemLinkWire.Rich(snapshot.Body);
    }

    private static void RequestItemLink(ItemLinkStore.Entry entry)
    {
        if (entry.Snapshot == null && ZRoutedRpc.instance != null && CanUseItemLinkEntry(entry) && ItemLinks.Request(entry, Time.unscaledTime))
            ZRoutedRpc.instance.InvokeRoutedRPC(entry.Sender, ItemLinkRequestRpc, entry.Token, entry.Nonce);
    }

    private static bool CanUseItemLinkEntry(ItemLinkStore.Entry? entry) =>
        entry != null && (entry.ClanId.Length == 0 || CanShareClanItemLink(entry.ClanId, entry.Sender));

    private static string FormatItemLinkBody(long sender, string body, string clanId = "")
    {
        return ItemLinkWire.Rewrite(body, (token, label) =>
        {
            ItemLinks.Observe(sender, token, label, Time.unscaledTime, clanId);
            // Inherit the enclosing native/Clan channel color, including whisper
            // opacity. Underline distinguishes the link without changing its channel.
            return "<link=\"isitem:" + ItemLinkStore.Key(sender, token) + "\"><u>[" +
                   ItemLinkWire.Plain(label, 120) + "]</u></link>";
        });
    }

    private sealed class ItemLinkChatContext
    {
        internal long Sender;
        internal string Body = "";
        internal bool Consumed;
    }
    [ThreadStatic] private static ItemLinkChatContext? _itemLinkChatContext;

    [HarmonyPatch(typeof(Chat), nameof(Chat.SendText), typeof(Talker.Type), typeof(string))]
    private static class ItemLinkSentPatch
    {
        private static void Prefix(string text)
        {
            if (ItemLinksEnabled) ItemLinks.MarkSent(text, Time.unscaledTime);
        }
    }

    [HarmonyPatch(typeof(Chat), nameof(Chat.Update))]
    private static class ItemLinkChatInputPatch
    {
        // Native Chat.Update deactivates its input on Mouse0 down. Capture the
        // selection and button interaction before that happens, without changing
        // the game's update, key handling, channel, or send implementation.
        private static void Prefix(Chat __instance)
        {
            if (ItemLinksEnabled) CaptureItemLinkChatInput(__instance);
        }
    }

    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
    private static class ItemLinkChatCursorPatch
    {
        // The dedicated-server camera method has no cursor logic. Skip patching
        // it instead of reporting the expected empty body as a compatibility issue.
        private static bool Prepare() => !IsDedicatedServer;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var nativeVisibility = typeof(TextInput).GetMethod(nameof(TextInput.IsVisible), Type.EmptyTypes);
            int at = code.FindIndex(instruction => instruction.Calls(nativeVisibility));
            if (at < 0 || code.FindLastIndex(instruction => instruction.Calls(nativeVisibility)) != at)
            {
                // A foreign camera replacement may already own capture. Keep it intact.
                Log.LogWarning("Chat tooltip cursor support could not locate the camera's text-input check; leaving mouse capture unchanged.");
                return code;
            }
            // Extend only this camera decision. Do not change TextInput.IsVisible globally,
            // override the user's capture toggle, or unlock after native code has relocked.
            code.Insert(at + 1, new CodeInstruction(OpCodes.Call,
                typeof(InventorySlotsPlugin).GetMethod(nameof(KeepItemLinkChatCursorFree), BindingFlags.Static | BindingFlags.NonPublic)));
            return code;
        }
    }

    // This overload is reached after native distance/permission/filter checks.
    // Do not patch OnNewChatMessage before its asynchronous permission callback.
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(PlatformUserID), typeof(string), typeof(Talker.Type), typeof(bool))]
    private static class ItemLinkChatScopePatch
    {
        private static void Prefix(Terminal __instance, PlatformUserID user, string text, Talker.Type type, out ItemLinkChatContext? __state)
        {
            __state = _itemLinkChatContext;
            _itemLinkChatContext = null;
            if (__instance is not Chat || !ItemLinksEnabled || text.Length > 4096 ||
                !ZNet.TryGetPlayerByPlatformUserID(user, out ZNet.PlayerInfo info) ||
                info.m_characterID == ZDOID.None || info.m_characterID.UserID == 0) return;
            _itemLinkChatContext = new ItemLinkChatContext
            {
                Sender = info.m_characterID.UserID,
                Body = type == Talker.Type.Shout ? text.ToUpper() : type == Talker.Type.Whisper ? text.ToLowerInvariant() : text
            };
        }
        private static Exception? Finalizer(Exception? __exception, ItemLinkChatContext? __state)
        {
            _itemLinkChatContext = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(string))]
    private static class ItemLinkChatMarkupPatch
    {
        private static void Prefix(Terminal __instance, ref string text)
        {
            ItemLinkChatContext? context = _itemLinkChatContext;
            if (__instance is not Chat || context == null || context.Consumed || !ItemLinksEnabled ||
                !text.EndsWith(context.Body + "</color>", StringComparison.Ordinal)) return;
            context.Consumed = true;
            string body = FormatItemLinkBody(context.Sender, context.Body);
            text = text.Substring(0, text.Length - context.Body.Length - "</color>".Length) + body + "</color>";
        }
    }
}
