using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static void UpdateCraftingPanelRedesign(InventoryGui gui, CraftingPanelUpdateReason reason = CraftingPanelUpdateReason.FrameTick)
    {
        PrepareCraftingTabAdapterPreflight(gui);
        CraftingTabAdapterState adapter = GetCraftingTabAdapterState(gui);

        if (reason == CraftingPanelUpdateReason.RecipeListChanged)
        {
            ClearCraftingRecipeCaches();
            InvalidateCraftingRecipeView();
            InvalidateCraftingVanillaHiddenState();
        }

        bool shouldShowRedesign = ShouldShowCraftingPanelRedesign(gui, adapter);
        if (!shouldShowRedesign)
        {
            UpdateCraftingTabAdapterSuppression(gui, shouldSuppress: false, adapter);
            if (_craftingRedesignApplied)
            {
                HideCraftingPanelRedesign();
            }

            if (adapter.IsForeign)
            {
                HideForeignCraftingRedesignResidue(gui);
            }

            return;
        }

        UpdateCraftingTabAdapterSuppression(gui, shouldSuppress: true, adapter);
        if (reason == CraftingPanelUpdateReason.FrameTick && TryRunCraftingPanelFrameFastPath(gui, adapter))
        {
            return;
        }

        bool firstApply = !_craftingRedesignApplied;
        bool modelRefresh = CraftingController.NeedsModelRefresh(reason, firstApply);

        RectTransform? grid = EnsureCraftingRecipeGrid(gui, syncCells: modelRefresh);
        if (grid == null)
        {
            return;
        }

        if (modelRefresh)
        {
            RebuildStationInputTokens();
        }

        _craftingRedesignApplied = true;

        SyncPinnedTooltipContextWithCraftingTab(gui);
        EnsureCraftingVanillaRecipeElementsHidden(gui);
        ApplyCraftingTabAdapterVisibility(gui, adapter);

        UpdateCraftingPanelHeightExtension(gui);
        ResetCraftingCountIfSelectionChanged(gui);
        HandleCraftingCountWheel();
        UpdateCraftingQueueLifecycle(gui);
        TryContinueCraftingQueue(gui);
        EnsureCraftingFavoritesLoaded(Player.m_localPlayer);
        HandleCraftingGroupFavoriteClearShortcut();
        UpdateCraftingSearchInput(gui, grid);
        UpdateCraftingSortModeButtons(gui, grid);
        if (adapter.UsesDefaultGroupRail)
        {
            UpdateCraftingGroupRail(gui, grid);
        }
        else
        {
            HideCraftingGroupRail();
        }

        bool viewChanged = UpdateCraftingRecipeView(gui);
        EnsureSelectedCraftingRecipeVisible(gui);
        PrepareCraftingTooltipScrollInput(gui);
        bool recipeWheelHandled = HandleCraftingPinnedTooltipWheel() || HandleCraftingHoverTooltipWheel() || HandleCraftingRecipeGridZoomWheel(gui, grid) || HandleCraftingRecipeGridWheel(gui, grid);
        if (!recipeWheelHandled)
        {
            SyncCraftingRecipePageToSelected(gui);
        }

        ClampCraftingRecipePage(gui);
        UpdateCraftingGridLayering(gui, grid);
        LayoutCraftingRecipeGrid(gui, grid);
        UpdateCraftingRecipeScrollbar(gui, grid);
        UpdateCraftingTooltipRecipeOverlay(gui);
        LayoutCraftingTabAdapterBottomControls(gui, grid, adapter);
        UpdateCraftingRecipeGridZoomHint(gui, grid);
        RepairCraftingPinnedTooltipTextVisibility();
        FinalizeCraftingTabAdapterFrame(gui, adapter, firstApply, reason, viewChanged);

        StoreCraftingFrameFastPathSignature(gui, adapter);
    }

    private static void UpdateCraftingPanelHeightExtension(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return;
        }

        float extension = CraftingPanelBottomFixedExtension;
        if (extension <= 0.01f)
        {
            RestoreCraftingPanelExtension(gui);
            return;
        }

        ApplyCraftingPanelExtension(gui, extension);
    }

    private static void RestoreCraftingPanelHeightExtension(InventoryGui? gui = null)
    {
        RestoreCraftingPanelExtension(gui);
    }

    private static void ApplyCraftingPanelExtension(InventoryGui gui, float extension)
    {
        RectTransform panel = gui.m_crafting;
        Vector2 originalSize = _craftingPanelRootSnapshot != null && _craftingPanelRootSnapshot.Rect == panel
            ? _craftingPanelOriginalSizeDelta
            : panel.sizeDelta;
        Vector2 targetSize = new(originalSize.x, originalSize.y + extension);
        string signature = $"{panel.GetInstanceID()}|{extension:0.###}|{panel.childCount}";
        if (_craftingPanelRootSnapshot != null &&
            _craftingPanelRootSnapshot.Rect == panel &&
            string.Equals(_craftingPanelExtensionSignature, signature, StringComparison.Ordinal) &&
            (panel.sizeDelta - targetSize).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (_craftingPanelRootSnapshot == null || _craftingPanelRootSnapshot.Rect != panel)
        {
            _craftingPanelRootSnapshot = new RectTransformSnapshot(panel);
            _craftingPanelOriginalSizeDelta = panel.sizeDelta;
            originalSize = _craftingPanelOriginalSizeDelta;
            targetSize = new Vector2(originalSize.x, originalSize.y + extension);
            CraftingPanelResizeProtectedSnapshots.Clear();
            CaptureCraftingPanelResizeProtectedSnapshots(gui);
        }
        else
        {
            CaptureCraftingPanelResizeProtectedSnapshots(gui);
        }

        Dictionary<RectTransform, Vector3> protectedWorldPositions = CaptureCraftingPanelResizeProtectedWorldPositions(gui);
        if ((panel.sizeDelta - targetSize).sqrMagnitude > 0.0001f)
        {
            panel.sizeDelta = targetSize;
        }

        foreach (KeyValuePair<RectTransform, Vector3> pair in protectedWorldPositions)
        {
            RectTransform rect = pair.Key;
            if (rect == null || IsUnityNull(rect))
            {
                continue;
            }

            rect.position = pair.Value;
        }

        _craftingPanelExtensionSignature = signature;
    }

    private static void RestoreCraftingPanelExtension(InventoryGui? gui)
    {
        if (_craftingPanelRootSnapshot != null)
        {
            if (gui == null || gui.m_crafting == null || _craftingPanelRootSnapshot.Rect == gui.m_crafting)
            {
                _craftingPanelRootSnapshot.Restore();
            }
        }

        foreach (RectTransformSnapshot snapshot in CraftingPanelResizeProtectedSnapshots.Values)
        {
            snapshot.Restore();
        }

        CraftingPanelResizeProtectedSnapshots.Clear();
        _craftingPanelRootSnapshot = null;
        _craftingPanelOriginalSizeDelta = Vector2.zero;
        _craftingPanelExtensionSignature = "";
    }

    private static void CaptureCraftingPanelResizeProtectedSnapshots(InventoryGui gui)
    {
        foreach (RectTransform rect in FindCraftingPanelResizeProtectedChildren(gui))
        {
            if (rect == null || IsUnityNull(rect) || CraftingPanelResizeProtectedSnapshots.ContainsKey(rect))
            {
                continue;
            }

            CraftingPanelResizeProtectedSnapshots[rect] = new RectTransformSnapshot(rect);
        }
    }

    private static Dictionary<RectTransform, Vector3> CaptureCraftingPanelResizeProtectedWorldPositions(InventoryGui gui)
    {
        Dictionary<RectTransform, Vector3> positions = new();
        foreach (RectTransform rect in FindCraftingPanelResizeProtectedChildren(gui))
        {
            if (rect == null || IsUnityNull(rect))
            {
                continue;
            }

            positions[rect] = rect.position;
        }

        return positions;
    }

    private static IEnumerable<RectTransform> FindCraftingPanelResizeProtectedChildren(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            yield break;
        }

        for (int i = 0; i < gui.m_crafting.childCount; i++)
        {
            if (gui.m_crafting.GetChild(i) is not RectTransform rect)
            {
                continue;
            }

            if (ShouldProtectCraftingPanelResizeChild(gui, rect))
            {
                yield return rect;
            }
        }
    }

    private static bool ShouldProtectCraftingPanelResizeChild(InventoryGui gui, RectTransform rect)
    {
        if (rect == null ||
            IsUnityNull(rect) ||
            rect == gui.m_crafting ||
            IsOwnedCraftingUiTransform(rect) ||
            IsCraftingPanelStretchBackground(rect))
        {
            return false;
        }

        return rect.anchorMin.y < 0.999f || rect.anchorMax.y < 0.999f;
    }

    private static bool IsCraftingPanelStretchBackground(RectTransform rect)
    {
        string lowerName = rect.name.ToLowerInvariant();
        if (lowerName is "bkg" or "darken" or "selected_frame")
        {
            return true;
        }

        bool stretchesVertically = rect.anchorMin.y <= 0.001f && rect.anchorMax.y >= 0.999f;
        if (!stretchesVertically)
        {
            return false;
        }

        Image? image = rect.GetComponent<Image>();
        if (image == null)
        {
            return lowerName.Contains("background") || lowerName.Contains("panel") || lowerName.Contains("frame");
        }

        return lowerName.Contains("bkg") ||
               lowerName.Contains("background") ||
               lowerName.Contains("darken") ||
               lowerName.Contains("panel") ||
               lowerName.Contains("frame");
    }

    private static void InvalidateCraftingRecipeView()
    {
        CraftingController.MarkRecipeViewDirty();
        CraftingController.MarkGroupRailDirty();
        CraftingController.MarkRecipeGridLayoutDirty();
    }

    private static void ClearCraftingRecipeCaches()
    {
        CraftingRecipes.SearchTextCache.Clear();
        CraftingRecipes.HoverTooltipContentCache.Clear();
        CraftingRecipes.SortKeyCache.Clear();
        CraftingRecipes.GroupMatchCache.Clear();
        ClearInventorySortCaches();
        ClearItemClassifierCaches();
        ClearCraftingRequirementAvailabilityCache();
        ClearCraftingGroupAvailabilityCache();
        ClearRecycleNReclaimSignatureCaches();
    }

    private static void HideCraftingPanelRedesign()
    {
        InventoryGui? gui = InventoryGui.instance;
        CraftingTabAdapterState adapter = GetCraftingTabAdapterState(gui);
        bool foreignCraftingTab = adapter.IsForeign;

        HideOwnedCraftingRedesignUi();
        UpdateMyLittleUICraftingObjectSuppression(gui, shouldSuppress: false, adapter);

        if (gui != null)
        {
            bool preserveForeignCraftingControls = foreignCraftingTab && ShouldPreserveForeignCraftingControls(gui);
            RestoreCraftingPanelHeightExtension(gui);
            RestoreCraftingVanillaState(gui, restoreRecipeUi: !foreignCraftingTab || preserveForeignCraftingControls);
            if (preserveForeignCraftingControls)
            {
                ForceRestoreForeignCraftingVanillaState(gui);
                RestoreJewelcraftingCraftingSocketUiForVanilla(gui);
            }
            else if (foreignCraftingTab)
            {
                HideCraftingRedesignBottomControls(gui);
            }
        }

        CraftingController.ClearHoveredRecipe();
        CraftingController.ResetRecipeListChangeSignature();
        CraftingRecipes.View.Clear();
        CraftingRecipes.ViewIndexByOriginal.Clear();
        InvalidateCraftingRecipeView();
        ClearCraftingQueue();
        _craftingRedesignApplied = false;
    }

    private static void HideForeignCraftingRedesignResidue(InventoryGui gui)
    {
        HideOwnedCraftingRedesignUi();
        RestoreCraftingPanelHeightExtension(gui);
        if (ShouldPreserveForeignCraftingControls(gui))
        {
            RestoreCraftingVanillaState(gui, restoreRecipeUi: true);
            ForceRestoreForeignCraftingVanillaState(gui);
            RestoreJewelcraftingCraftingSocketUiForVanilla(gui);
        }
        else
        {
            HideCraftingRedesignBottomControls(gui);
        }
    }

    private static void ForceRestoreForeignCraftingVanillaState(InventoryGui gui)
    {
        if (gui.m_crafting != null && !gui.m_crafting.gameObject.activeSelf)
        {
            gui.m_crafting.gameObject.SetActive(true);
        }

        SetCraftingVanillaRecipeElementsVisible(gui, visible: true);
        SetCraftingVanillaDetailVisible(gui, visible: true);
        SetCraftingVanillaRecipeScrollbarsVisible(gui, visible: true);
        SetCraftingVanillaPanelBackgroundsVisible(gui, visible: true);
        _craftingVanillaRecipeElementsHidden = false;
        _craftingVanillaDetailHidden = false;
        _craftingVanillaRecipeScrollbarsHidden = false;
        _craftingVanillaPanelBackgroundsHidden = false;
    }

    private static void RestoreCraftingVanillaState(InventoryGui gui, bool restoreRecipeUi)
    {
        if (_craftingVanillaPanelBackgroundsHidden || CraftingVanillaPanelBackgroundStates.Count > 0)
        {
            SetCraftingVanillaPanelBackgroundsVisible(gui, visible: true);
        }

        if (!restoreRecipeUi)
        {
            return;
        }

        if (_craftingVanillaRecipeElementsHidden)
        {
            SetCraftingVanillaRecipeElementsVisible(gui, visible: true);
            _craftingVanillaRecipeElementsHidden = false;
        }

        if (_craftingVanillaDetailHidden)
        {
            SetCraftingVanillaDetailVisible(gui, visible: true);
            _craftingVanillaDetailHidden = false;
        }

        if (_craftingVanillaRecipeScrollbarsHidden)
        {
            SetCraftingVanillaRecipeScrollbarsVisible(gui, visible: true);
            _craftingVanillaRecipeScrollbarsHidden = false;
        }
    }

    private static void HideOwnedCraftingRedesignUi()
    {
        if (_craftingRecipeGrid != null)
        {
            _craftingRecipeGrid.gameObject.SetActive(false);
        }

        HideCraftingTooltipRecipeOverlay();

        if (_craftingCountInputRect != null)
        {
            _craftingCountInputRect.gameObject.SetActive(false);
        }

        if (CraftingUi.SearchInputRect != null)
        {
            CraftingUi.SearchInputRect.gameObject.SetActive(false);
        }

        if (_craftingSortModeButtonGroup != null)
        {
            _craftingSortModeButtonGroup.gameObject.SetActive(false);
        }

        if (_craftingRecipeScrollbar != null)
        {
            _craftingRecipeScrollbar.gameObject.SetActive(false);
        }

        if (_craftingGroupRail != null)
        {
            _craftingGroupRail.gameObject.SetActive(false);
        }

        HideCraftingPinnedTooltips();

        HideRecycleNReclaimHud();
        HideOwnedCraftingRequirementSlots();

        if (_craftingControlsBackground != null)
        {
            _craftingControlsBackground.gameObject.SetActive(false);
        }

        if (CraftingUi.RecipeGridZoomHint != null)
        {
            CraftingUi.RecipeGridZoomHint.gameObject.SetActive(false);
        }

        HideCraftingSocketWarning();
    }

    internal static void PrepareCraftingQueue(InventoryGui gui)
    {
        if (CraftingQueue.ContinuingQueue)
        {
            return;
        }

        ClearCraftingQueue();
        if (!ShouldShowCraftingPanelRedesign(gui) || gui.m_selectedRecipe.Recipe == null || gui.m_selectedRecipe.ItemData != null)
        {
            return;
        }

        bool vanillaMultiCraft = IsVanillaMultiCraftModifierHeld();
        int count = vanillaMultiCraft ? Mathf.Max(1, gui.m_multiCraftAmount) : GetCraftingCount();
        CaptureCraftingProgressLabelCount(gui, count);
        if (vanillaMultiCraft)
        {
            return;
        }

        if (count <= 1)
        {
            return;
        }

        CraftingQueue.QueueRemaining = count - 1;
        CraftingQueue.QueueTotal = count;
        CraftingQueue.QueueRecipe = gui.m_selectedRecipe.Recipe;
        CraftingQueue.QueueVariant = gui.m_selectedVariant;
    }

    internal static void ValidateCraftingQueueStarted(InventoryGui gui)
    {
        if (!CraftingQueue.ContinuingQueue && CraftingQueue.QueueRemaining > 0 && gui.m_craftTimer < 0f)
        {
            ClearCraftingQueue(clearProgressLabel: false);
        }
    }

    private static void TryContinueCraftingQueue(InventoryGui gui)
    {
        if (CraftingQueue.ContinuingQueue || CraftingQueue.QueueRemaining <= 0 || gui.m_craftTimer >= 0f)
        {
            return;
        }

        if (!CanContinueCraftingQueue(gui))
        {
            ClearCraftingQueue();
            return;
        }

        CraftingQueue.QueueRemaining--;
        CraftingQueue.ContinuingQueue = true;
        int originalMultiCraftAmount = gui.m_multiCraftAmount;
        try
        {
            gui.m_multiCraftAmount = 1;
            gui.OnCraftPressed();
            gui.m_multiCrafting = false;
            if (gui.m_craftTimer < 0f)
            {
                ClearCraftingQueue();
            }
        }
        finally
        {
            gui.m_multiCraftAmount = originalMultiCraftAmount;
            CraftingQueue.ContinuingQueue = false;
        }
    }

    internal static void ClearCraftingQueue(bool clearProgressLabel = true)
    {
        CraftingQueue.QueueTotal = 0;
        CraftingQueue.QueueRemaining = 0;
        CraftingQueue.QueueRecipe = null;
        CraftingQueue.QueueVariant = 0;
        if (clearProgressLabel)
        {
            ClearCraftingProgressLabelCount();
        }
    }

    private static void UpdateCraftingQueueLifecycle(InventoryGui gui)
    {
        UpdateCraftingProgressLabelLifecycle(gui);
        if (CraftingQueue.QueueRecipe == null)
        {
            return;
        }

        Recipe? selectedRecipe = gui.m_selectedRecipe.Recipe;
        if (selectedRecipe != CraftingQueue.QueueRecipe || gui.m_selectedRecipe.ItemData != null || gui.m_selectedVariant != CraftingQueue.QueueVariant)
        {
            ClearCraftingQueue();
            return;
        }

        if (CraftingQueue.QueueRemaining <= 0 && gui.m_craftTimer < 0f)
        {
            ClearCraftingQueue();
        }
    }

    private static void CaptureCraftingProgressLabelCount(InventoryGui gui, int count)
    {
        if (count <= 1 || gui.m_selectedRecipe.Recipe == null || gui.m_selectedRecipe.ItemData != null)
        {
            ClearCraftingProgressLabelCount();
            return;
        }

        CraftingQueue.ProgressLabelCount = Mathf.Clamp(count, 1, CraftingQueueMaxCount);
        CraftingQueue.ProgressLabelRecipe = gui.m_selectedRecipe.Recipe;
        CraftingQueue.ProgressLabelVariant = gui.m_selectedVariant;
    }

    private static void ClearCraftingProgressLabelCount()
    {
        CraftingQueue.ProgressLabelCount = 0;
        CraftingQueue.ProgressLabelRecipe = null;
        CraftingQueue.ProgressLabelVariant = 0;
    }

    private static void UpdateCraftingProgressLabelLifecycle(InventoryGui gui)
    {
        if (CraftingQueue.ProgressLabelRecipe == null)
        {
            return;
        }

        bool selectedMatches = gui.m_selectedRecipe.Recipe == CraftingQueue.ProgressLabelRecipe &&
                               gui.m_selectedRecipe.ItemData == null &&
                               gui.m_selectedVariant == CraftingQueue.ProgressLabelVariant;
        bool queueCanContinue = CraftingQueue.QueueRecipe == CraftingQueue.ProgressLabelRecipe && CraftingQueue.QueueRemaining > 0;
        if (!selectedMatches || (gui.m_craftTimer < 0f && !queueCanContinue))
        {
            ClearCraftingProgressLabelCount();
        }
    }

    private static void ResetCraftingCountIfSelectionChanged(InventoryGui gui)
    {
        Recipe? selectedRecipe = gui.m_selectedRecipe.Recipe;
        int selectedVariant = gui.m_selectedVariant;
        if (selectedRecipe == _lastCraftingSelectedRecipe && selectedVariant == _lastCraftingSelectedVariant)
        {
            return;
        }

        _lastCraftingSelectedRecipe = selectedRecipe;
        _lastCraftingSelectedVariant = selectedVariant;
        if (selectedRecipe == null)
        {
            return;
        }

        ClearCraftingQueue();
        SetCraftingCount(1);
    }

    private static bool ShouldShowCraftingPanelRedesign(InventoryGui? gui)
    {
        return ShouldShowCraftingPanelRedesign(gui, GetCraftingTabAdapterState(gui));
    }

    private static bool ShouldShowCraftingPanelRedesign(InventoryGui? gui, CraftingTabAdapterState adapter)
    {
        return !IsDedicatedServer &&
               gui != null &&
               gui.m_crafting != null &&
               gui.m_crafting.gameObject.activeInHierarchy &&
               adapter.IsRedesign;
    }

    private static bool ShouldPreserveForeignCraftingControls(InventoryGui gui) =>
        ShouldPreserveJewelcraftingForeignCraftingControls(gui);

    private static bool HasActiveForeignCraftingTab(InventoryGui gui)
    {
        if (gui.m_tabCraft == null || gui.m_tabUpgrade == null)
        {
            return false;
        }

        Transform? tabRoot = gui.m_tabCraft.transform.parent;
        if (tabRoot == null)
        {
            return false;
        }

        foreach (Button button in tabRoot.GetComponentsInChildren<Button>(includeInactive: true))
        {
            if (button == null ||
                IsUnityNull(button) ||
                button == gui.m_tabCraft ||
                button == gui.m_tabUpgrade ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!button.interactable)
            {
                if (IsCraftingPrimaryTabSelected(gui) && IsJewelcraftingSocketTabButton(button))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }
}
