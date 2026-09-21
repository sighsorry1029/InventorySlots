using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool _sharedContainerSettingChanged;
    private static bool _sharedContainersActive;
    private static readonly ConditionalWeakTable<Container, Piece> SharedContainerEligibility = new();
    private static readonly HashSet<Container> SharedContainerLocalViewers = new();
    private static readonly AccessTools.FieldRef<Container, bool> SharedContainerLocalInUse =
        AccessTools.FieldRefAccess<Container, bool>("m_inUse");
    private static readonly Action<Container> UpdateSharedContainerUseVisual =
        AccessTools.MethodDelegate<Action<Container>>(AccessTools.Method(typeof(Container), "UpdateUseVisual", Type.EmptyTypes));

    internal static bool IsSharedContainerEnabled(Container container) =>
        _sharedContainersActive && !UsesVanillaContainerProtocol && !HasExternalMultiUserChestActive &&
        container != null && SharedContainerEligibility.TryGetValue(container, out Piece piece) &&
        piece != null && piece.IsPlacedByPlayer() &&
        GetContainerAreaView(container)?.IsValid() == true && !IsMultiUserChestIgnored(container);

    private static void RegisterSharedContainer(Container container)
    {
        // Hierarchy/component searches belong to registration, not every UI frame.
        SharedContainerEligibility.Remove(container);
        // Player.PlacePiece calls SetCreator after Instantiate/Awake. Cache only
        // structural eligibility and read the creator flag live on both peers.
        if (IsContainerAreaEligible(container, requirePlayerPlaced: false))
            SharedContainerEligibility.Add(container, container.GetComponent<Piece>());
    }

    private static void OnSharedContainerSettingChanged(object sender, EventArgs args) =>
        _sharedContainerSettingChanged = true;

    private static void ApplySharedContainerConfigurationChange()
    {
        if (!_sharedContainerSettingChanged) return;
        _sharedContainerSettingChanged = false;
        // Apply on Unity's thread. No item has moved while a grant is pending.
        CancelContainerAreaTransfer();
        if (InventoryGui.instance != null && InventoryGui.IsVisible()) InventoryGui.instance.Hide();
        foreach (Container container in SharedContainerLocalViewers)
        {
            if (container == null) continue;
            SharedContainerLocalInUse(container) = false;
            UpdateSharedContainerUseVisual(container);
        }
        SharedContainerLocalViewers.Clear();
        // Publish the new mode only after old viewers and pending actions close.
        // A settings callback can arrive between GUI and plugin Update calls.
        _sharedContainersActive = _enableSharedContainers?.Value == Toggle.On;
    }

    private static bool TryHandleSharedContainerButton(Player? player, Action action)
    {
        if (IsReplayingSharedContainerInteraction || player == null) return false;
        InventoryGui? gui = InventoryGui.instance;
        Container? container = gui != null ? ContainerAreaGuiContainer(gui) : null;
        if (container == null || !IsSharedContainerEnabled(container)) return false;
        if (ShouldBlockContainerPreviewInteraction(gui!)) return true;
        int epoch = _sharedContainerGuiEpoch;
        if (!TryStartSharedContainerInteraction(player, container, () =>
            {
                if (gui == null || InventoryGui.instance != gui || !InventoryGui.IsVisible() ||
                    ContainerAreaGuiContainer(gui) != container || epoch != _sharedContainerGuiEpoch) return false;
                return ReplaySharedContainerInteraction(() => { action(); return true; });
            })) ShowContainerNotReady();
        return true;
    }

    internal static bool TryHandleSharedContainerRequest(Container container, long sender, long playerId, string response)
    {
        if (!IsSharedContainerEnabled(container)) return false;
        ZNetView? view = GetContainerAreaView(container);
        if (view == null || !view.IsValid() || !view.IsOwner()) return true;
        bool granted = sender != 0 && TryGetRpcSenderPlayer(sender, playerId, out Player? player) &&
                       player != null && HasContainerAreaRequesterAccess(playerId, container) &&
                       (player.transform.position - container.transform.position).sqrMagnitude <=
                       ContainerAreaAnchorDistance * ContainerAreaAnchorDistance;
        // Opening is read-only. Stack/take-all responses on our clients enter
        // the same approved handoff as buttons; none claim ownership here.
        if (granted)
        {
            try { ZDOMan.instance.ForceSendZDO(sender, view.GetZDO().m_uid); }
            catch (Exception error)
            {
                Log.LogWarning($"Shared chest open state send failed: {error.Message}");
                granted = false;
            }
        }
        view.InvokeRPC(sender, response, granted);
        return true;
    }

    internal static bool TryHandleSharedContainerTakeAllResponse(Container container, bool granted)
    {
        if (!IsSharedContainerEnabled(container) || !granted) return false;
        InventoryGui? gui = InventoryGui.instance;
        if (gui != null && ContainerAreaGuiContainer(gui) == container && InventoryGui.IsVisible())
            TryHandleSafeTakeAll(gui);
        else ShowContainerNotReady();
        return true;
    }

    internal static bool SetSharedContainerInUse(Container container, bool inUse, ref bool localInUse,
        EffectList openEffects, EffectList closeEffects)
    {
        bool shared = IsSharedContainerEnabled(container);
        if (!shared && !SharedContainerLocalViewers.Contains(container)) return false;
        if (inUse) SharedContainerLocalViewers.Add(container);
        else SharedContainerLocalViewers.Remove(container);
        ZNetView? view = GetContainerAreaView(container);
        if (view == null || view.IsOwner()) return false;
        // A viewer flag is local UI state, not permission to write the chest.
        if (localInUse == inUse) return true;
        localInUse = inUse;
        UpdateSharedContainerUseVisual(container);
        (inUse ? openEffects : closeEffects).Create(container.transform.position, container.transform.rotation);
        return true;
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
internal static class ContainerSharedViewerStatePatch
{
    private static bool Prefix(Container __instance, bool inUse, ref bool ___m_inUse,
        EffectList ___m_openEffects, EffectList ___m_closeEffects) =>
        !InventorySlotsPlugin.SetSharedContainerInUse(__instance, inUse, ref ___m_inUse, ___m_openEffects, ___m_closeEffects);
}

[HarmonyPatch(typeof(Container), "Load")]
internal static class ContainerSharedViewerLoadPatch
{
    private static void Prefix(Container __instance, ref bool ___m_inUse, out bool __state)
    {
        __state = ___m_inUse && InventorySlotsPlugin.IsSharedContainerEnabled(__instance);
        if (__state) ___m_inUse = false;
    }

    private static void Finalizer(ref bool ___m_inUse, bool __state)
    {
        if (__state) ___m_inUse = true;
    }
}

[HarmonyPatch(typeof(Container), "UpdateUseVisual")]
internal static class ContainerSharedViewerVisualPatch
{
    private static void Postfix(Container __instance, bool ___m_inUse, GameObject ___m_open, GameObject ___m_closed)
    {
        if (!___m_inUse || !InventorySlotsPlugin.IsSharedContainerEnabled(__instance)) return;
        if (___m_open != null) ___m_open.SetActive(true);
        if (___m_closed != null) ___m_closed.SetActive(false);
    }
}

[HarmonyPatch(typeof(Container), "RPC_StackResponse")]
internal static class ContainerSharedStackResponsePatch
{
    private static bool Prefix(Container __instance, bool granted)
    {
        if (!granted || !InventorySlotsPlugin.IsSharedContainerEnabled(__instance)) return true;
        // A late response while loading must never fall through to vanilla's
        // unguarded local Inventory.StackAll, even if the handoff cannot start.
        InventorySlotsPlugin.TryHandleContainerStackAll(__instance);
        return false;
    }
}
