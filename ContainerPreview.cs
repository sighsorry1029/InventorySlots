using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

// Adapted from ContentsWithin by redseiko and MSchmoecker under GPL-3.0.
// InventorySlots owns the preview lifecycle here so it can remain read-only and grid-safe.
public sealed partial class InventorySlotsPlugin
{
    private const int ContainerPreviewHiddenFrames = 10;
    private const float ContainerPreviewFailureLogInterval = 5f;

    private enum ContainerPreviewPhase
    {
        Hidden,
        PreviewVisible,
        RestorePending,
        RealInventoryVisible
    }

    private static readonly Dictionary<GameObject, bool> ContainerPreviewHiddenObjects = new();
    private static bool _containerPreviewRealGuiVisible;
    private static bool _containerPreviewFrameOwned;
    private static bool _containerPreviewActive;
    private static bool _containerPreviewUiCaptured;
    private static bool _containerPreviewStandaloneWarningLogged;
    private static Container? _containerPreviewTarget;
    private static Inventory? _containerPreviewInventory;
    private static Inventory? _containerPreviewRenderedInventory;
    private static int _containerPreviewRenderedWidth = -1;
    private static int _containerPreviewRenderedHeight = -1;
    private static float _containerPreviewCloseAt;
    private static float _containerPreviewLastFailureAt = float.NegativeInfinity;
    private static string _containerPreviewLastFailure = "";

    internal static void OnRealInventoryGuiShown()
    {
        _containerPreviewRealGuiVisible = true;
        RestoreContainerPreviewUi();
        EndContainerPreview(InventoryGui.instance, keepRealGuiVisible: true);
    }

    internal static void BeforeRealInventoryGuiShown()
    {
        // Keep the regular panels hidden while the preview animator closes, then
        // restore them before the real inventory initializes its UI.
        RestoreContainerPreviewUi();
    }

    internal static void OnInventoryGuiHidden()
    {
        _containerPreviewRealGuiVisible = false;
        EndContainerPreview(InventoryGui.instance, keepRealGuiVisible: false);
    }

    private static void ShutdownContainerPreview()
    {
        _containerPreviewRealGuiVisible = false;
        EndContainerPreview(InventoryGui.instance, keepRealGuiVisible: false);
        RestoreContainerPreviewUi();
    }

    internal static bool ShouldBlockContainerPreviewInteraction(InventoryGui? gui)
    {
        return gui != null &&
               !IsUnityNull(gui) &&
               gui == InventoryGui.instance &&
               !_containerPreviewRealGuiVisible &&
               (_containerPreviewActive || _containerPreviewFrameOwned);
    }

    private static ContainerPreviewPhase GetContainerPreviewPhase()
    {
        if (_containerPreviewActive ||
            _containerPreviewFrameOwned ||
            _containerPreviewTarget != null ||
            _containerPreviewInventory != null ||
            _containerPreviewRenderedInventory != null)
        {
            return ContainerPreviewPhase.PreviewVisible;
        }

        if (_containerPreviewRealGuiVisible)
        {
            return ContainerPreviewPhase.RealInventoryVisible;
        }

        return _containerPreviewUiCaptured
            ? ContainerPreviewPhase.RestorePending
            : ContainerPreviewPhase.Hidden;
    }

    internal static void BeforeContainerPreviewGuiUpdate(InventoryGui gui)
    {
        _containerPreviewFrameOwned = false;
        if (IsDedicatedServer || gui == null || IsUnityNull(gui))
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        bool inputBlocked = player == null || IsUnityNull(player) || ShouldBlockGlobalHotkeys(player);

        if (_containerPreviewRealGuiVisible)
        {
            return;
        }

        if (!CanRunContainerPreview(player, inputBlocked))
        {
            EndContainerPreview(gui, keepRealGuiVisible: false);
            return;
        }

        float now = Time.unscaledTime;
        if (TryGetContainerPreviewTarget(player!, out Container? container, out Inventory? inventory))
        {
            _containerPreviewTarget = container;
            _containerPreviewInventory = inventory;
            _containerPreviewCloseAt = now + Mathf.Max(0f, _containerPreviewCloseDelay.Value);
        }
        else if (GetContainerPreviewPhase() == ContainerPreviewPhase.RestorePending)
        {
            return;
        }
        else if (!_containerPreviewActive ||
                 _containerPreviewInventory == null ||
                 now > _containerPreviewCloseAt ||
                 !IsContainerPreviewInventoryValid(_containerPreviewInventory, out _, out _))
        {
            EndContainerPreview(gui, keepRealGuiVisible: false);
            return;
        }

        _containerPreviewFrameOwned = _containerPreviewInventory != null;
        if (!_containerPreviewFrameOwned)
        {
            return;
        }

        if (gui.m_animator != null)
        {
            gui.m_animator.SetBool("visible", false);
        }

        CaptureAndHideContainerPreviewUi(gui);
    }

    internal static void AfterContainerPreviewGuiUpdate(InventoryGui gui)
    {
        if (!_containerPreviewFrameOwned || _containerPreviewRealGuiVisible)
        {
            return;
        }

        RenderContainerPreview(gui);
    }

    private static bool CanRunContainerPreview(Player? player, bool inputBlocked)
    {
        if (_containerPreviewCloseDelay.Value <= 0f ||
            inputBlocked ||
            player == null ||
            IsUnityNull(player) ||
            player!.m_isLoading ||
            player.IsDead() ||
            ((Character)player).InCutscene() ||
            player.IsTeleporting())
        {
            return false;
        }

        if (!HasPlugin(ContentsWithinGuid))
        {
            return true;
        }

        if (!_containerPreviewStandaloneWarningLogged)
        {
            _containerPreviewStandaloneWarningLogged = true;
            Log.LogWarning("InventorySlots container preview is disabled because standalone ContentsWithin is installed.");
        }

        return false;
    }

    private static bool TryGetContainerPreviewTarget(Player player, out Container? container, out Inventory? inventory)
    {
        container = GetHoveredContainer(player);
        inventory = null;
        if (container == null ||
            IsUnityNull(container) ||
            !HasContainerPlayerAccess(player, container, flashGuardStone: false))
        {
            return false;
        }

        inventory = container.GetInventory();
        if (inventory == null || !IsContainerPreviewInventoryValid(inventory, out _, out _))
        {
            inventory = null;
            return false;
        }

        return true;
    }

    private static bool IsContainerPreviewInventoryValid(Inventory inventory, out int width, out int height)
    {
        width = inventory?.GetWidth() ?? 0;
        height = inventory?.GetHeight() ?? 0;
        if (inventory == null || width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
        {
            return false;
        }

        foreach (ItemData item in inventory.GetAllItems())
        {
            if (item == null ||
                item.m_gridPos.x < 0 ||
                item.m_gridPos.y < 0 ||
                item.m_gridPos.x >= width ||
                item.m_gridPos.y >= height)
            {
                return false;
            }
        }

        return true;
    }

    private static void RenderContainerPreview(InventoryGui gui)
    {
        Inventory? inventory = _containerPreviewInventory;
        if (inventory == null ||
            !IsContainerPreviewInventoryValid(inventory, out int width, out int height) ||
            gui.m_containerGrid == null ||
            gui.m_container == null)
        {
            EndContainerPreview(gui, keepRealGuiVisible: false);
            return;
        }

        try
        {
            CaptureAndHideContainerPreviewUi(gui);
            gui.m_animator.SetBool("visible", true);
            gui.m_hiddenFrames = ContainerPreviewHiddenFrames;
            gui.m_container.gameObject.SetActive(true);

            InventoryGrid grid = gui.m_containerGrid;
            grid.m_inventory = inventory;
            grid.m_selected.x = Mathf.Clamp(grid.m_selected.x, 0, width - 1);
            grid.m_selected.y = Mathf.Clamp(grid.m_selected.y, 0, height - 1);
            int expectedElementCount = width * height;
            if (grid.m_elements == null || grid.m_elements.Count != expectedElementCount)
            {
                grid.m_width = -1;
                grid.m_height = -1;
            }

            grid.UpdateGui(null, null);

            bool targetChanged = _containerPreviewRenderedInventory != inventory ||
                                 _containerPreviewRenderedWidth != width ||
                                 _containerPreviewRenderedHeight != height;
            if (targetChanged)
            {
                grid.ResetView();
                _containerPreviewRenderedInventory = inventory;
                _containerPreviewRenderedWidth = width;
                _containerPreviewRenderedHeight = height;
            }

            string inventoryName = inventory.GetName();
            gui.m_containerName.text = Localization.instance != null
                ? Localization.instance.Localize(inventoryName)
                : inventoryName;
            gui.m_containerWeight.text = Mathf.CeilToInt(inventory.GetTotalWeight()).ToString();
            _containerPreviewActive = true;
        }
        catch (Exception exception)
        {
            LogContainerPreviewFailure(exception);
            EndContainerPreview(gui, keepRealGuiVisible: false);
        }
    }

    private static void CaptureAndHideContainerPreviewUi(InventoryGui gui)
    {
        if (!_containerPreviewUiCaptured)
        {
            ContainerPreviewHiddenObjects.Clear();
            _containerPreviewUiCaptured = true;
            RestoreContainerUiState();
            HideInventoryPinnedTooltips();
            HideInventoryOwnedHoverTooltips();
            ClearInventoryHoverTooltipSources();
        }

        CaptureAndHideContainerPreviewObject(gui.m_player != null ? gui.m_player.gameObject : null);
        CaptureAndHideContainerPreviewObject(gui.m_crafting != null ? gui.m_crafting.gameObject : null);
        CaptureAndHideContainerPreviewObject(gui.m_info != null ? gui.m_info.gameObject : null);
        CaptureAndHideContainerPreviewObject(gui.m_infoPanel != null ? gui.m_infoPanel.gameObject : null);
        CaptureAndHideContainerPreviewObject(gui.m_takeAllButton != null ? gui.m_takeAllButton.gameObject : null);
        CaptureAndHideContainerPreviewObject(gui.m_stackAllButton != null ? gui.m_stackAllButton.gameObject : null);
        CaptureAndHideContainerPreviewObject(gui.m_inventoryRoot?.Find("Crafting")?.gameObject);
        CaptureAndHideContainerPreviewObject(gui.m_inventoryRoot?.Find("RightPanel")?.gameObject);
        HideInventoryActionPanels();
    }

    private static void CaptureAndHideContainerPreviewObject(GameObject? target)
    {
        if (target == null || IsUnityNull(target))
        {
            return;
        }

        if (!ContainerPreviewHiddenObjects.ContainsKey(target))
        {
            ContainerPreviewHiddenObjects.Add(target, target.activeSelf);
        }

        if (target.activeSelf)
        {
            target.SetActive(false);
        }
    }

    private static void EndContainerPreview(InventoryGui? gui, bool keepRealGuiVisible)
    {
        if (GetContainerPreviewPhase() != ContainerPreviewPhase.PreviewVisible)
        {
            return;
        }

        _containerPreviewFrameOwned = false;
        _containerPreviewActive = false;
        _containerPreviewTarget = null;
        _containerPreviewInventory = null;
        _containerPreviewRenderedInventory = null;
        _containerPreviewRenderedWidth = -1;
        _containerPreviewRenderedHeight = -1;
        _containerPreviewCloseAt = 0f;

        if (gui == null || IsUnityNull(gui))
        {
            return;
        }

        if (!keepRealGuiVisible && gui.m_animator != null)
        {
            gui.m_animator.SetBool("visible", false);
        }

        bool keepContainerPanel = keepRealGuiVisible && gui.m_currentContainer != null;
        if (!keepContainerPanel && gui.m_container != null)
        {
            gui.m_container.gameObject.SetActive(false);
        }
    }

    private static void RestoreContainerPreviewUi()
    {
        if (!_containerPreviewUiCaptured)
        {
            return;
        }

        foreach (KeyValuePair<GameObject, bool> state in ContainerPreviewHiddenObjects)
        {
            if (state.Key != null && !IsUnityNull(state.Key))
            {
                state.Key.SetActive(state.Value);
            }
        }

        ContainerPreviewHiddenObjects.Clear();
        _containerPreviewUiCaptured = false;
    }

    private static void LogContainerPreviewFailure(Exception exception)
    {
        string message = $"{exception.GetType().Name}: {exception.Message}";
        float now = Time.unscaledTime;
        if (string.Equals(message, _containerPreviewLastFailure, StringComparison.Ordinal) &&
            now - _containerPreviewLastFailureAt < ContainerPreviewFailureLogInterval)
        {
            return;
        }

        _containerPreviewLastFailure = message;
        _containerPreviewLastFailureAt = now;
        Log.LogWarning($"Container preview was closed after a UI update failed: {message}");
    }
}

[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class InventoryGuiContainerPreviewUpdatePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(InventoryGui __instance)
    {
        InventorySlotsPlugin.BeforeContainerPreviewGuiUpdate(__instance);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(InventoryGui __instance)
    {
        InventorySlotsPlugin.AfterContainerPreviewGuiUpdate(__instance);
    }
}
