using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static BaseUnityPlugin? _itemLinkClanPlugin;
    private static EventInfo? _itemLinkClanEvent;
    private static Func<string>? _itemLinkClanId;
    private static Func<string, long, bool>? _itemLinkClanCanShare;
    private static readonly Action<string, long, string, Action<string>> ClanItemLinkFormatter = FormatClanItemLinks;
    private static string _itemLinkClanScope = "";
    private static double _itemLinkClanNextBind;

    private static void UpdateClanItemLinks()
    {
        if (_itemLinkClanEvent != null && (_itemLinkClanPlugin == null || !_itemLinkClanPlugin.isActiveAndEnabled))
            UnbindClanItemLinks();
        if (_itemLinkClanEvent == null && Time.unscaledTime >= _itemLinkClanNextBind)
        {
            _itemLinkClanNextBind = Time.unscaledTime + 2;
            // Optional, lazily discovered after both plugins initialize. No Clan
            // reference or hard dependency is added to the merged InventorySlots DLL.
            if (Chainloader.PluginInfos.TryGetValue("sighsorry.Clan", out PluginInfo? info) &&
                info?.Instance != null && info.Instance.isActiveAndEnabled)
            {
                try
                {
                    Type? api = info.Instance.GetType().Assembly.GetType("Clan.ClanApi");
                    EventInfo? evt = api?.GetEvent("ClientChatFormatting", BindingFlags.Public | BindingFlags.Static);
                    MethodInfo? getClan = api?.GetProperty("ChatClanId", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                    MethodInfo? canShare = api?.GetMethod("CanShareChatWithPeer", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(long) }, null);
                    if (evt?.EventHandlerType == typeof(Action<string, long, string, Action<string>>) &&
                        getClan?.ReturnType == typeof(string) && canShare?.ReturnType == typeof(bool))
                    {
                        _itemLinkClanId = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), getClan);
                        _itemLinkClanCanShare = (Func<string, long, bool>)Delegate.CreateDelegate(typeof(Func<string, long, bool>), canShare);
                        _itemLinkClanPlugin = info.Instance;
                        _itemLinkClanEvent = evt;
                        evt.AddEventHandler(null, ClanItemLinkFormatter);
                    }
                }
                catch (Exception exception) when (exception is ArgumentException || exception is MemberAccessException || exception is TargetInvocationException)
                {
                    UnbindClanItemLinks();
                    Log.LogWarning("Clan chat tooltip integration could not bind; readable chat labels remain available.");
                }
            }
        }
        RefreshClanItemLinkScope();
    }

    private static void RefreshClanItemLinkScope()
    {
        string clanId = _itemLinkClanPlugin != null && _itemLinkClanPlugin.isActiveAndEnabled
            ? _itemLinkClanId?.Invoke() ?? "" : "";
        if (clanId == _itemLinkClanScope) return;
        ItemLinks.ClearClan();
        _itemLinkClanScope = clanId;
    }

    private static void UnbindClanItemLinks()
    {
        try { _itemLinkClanEvent?.RemoveEventHandler(null, ClanItemLinkFormatter); }
        catch (Exception exception) when (exception is MemberAccessException || exception is TargetInvocationException)
        {
            Log.LogWarning("Clan chat tooltip integration could not unsubscribe; inactive callbacks will be ignored.");
        }
        _itemLinkClanEvent = null;
        _itemLinkClanPlugin = null;
        _itemLinkClanId = null;
        _itemLinkClanCanShare = null;
        _itemLinkClanScope = "";
        ItemLinks.ClearClan();
    }

    private static bool CanShareClanItemLink(string clanId, long peerId) =>
        ItemLinksEnabled && _itemLinkClanPlugin != null && _itemLinkClanPlugin.isActiveAndEnabled &&
        _itemLinkClanCanShare?.Invoke(clanId, peerId) == true;

    private static void FormatClanItemLinks(string clanId, long senderPeerId, string body, Action<string> replace)
    {
        if (!ItemLinksEnabled || Player.m_localPlayer == null || _itemLinkChat != Chat.instance ||
            _itemLinkRegisteredRpc == null || _itemLinkRegisteredRpc != ZRoutedRpc.instance ||
            _itemLinkClanPlugin == null || !_itemLinkClanPlugin.isActiveAndEnabled || body.Length > 4096) return;
        RefreshClanItemLinkScope();
        if (clanId.Length == 0 || clanId != _itemLinkClanScope) return;
        // This callback comes from the server-accepted echo, not from typing or
        // sending a draft. Remote authors can never publish one of our offers.
        if (senderPeerId == ZNet.GetUID()) ItemLinks.MarkClanSent(body, clanId, Time.unscaledTime);
        replace(FormatItemLinkBody(senderPeerId, body, clanId));
    }
}
