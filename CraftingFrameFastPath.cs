using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool TryRunCraftingPanelFrameFastPath(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        if (!CanRunCraftingPanelFrameFastPath())
        {
            return false;
        }

        CraftingFrameFastPathStamp stamp = CreateCraftingFrameFastPathStamp(gui, adapter);
        if (!_craftingFrameFastPathStamp.Equals(stamp))
        {
            _craftingFrameFastPathStamp = stamp;
            return false;
        }

        RectTransform? grid = _craftingRecipeGrid;
        if (grid == null || IsUnityNull(grid))
        {
            return false;
        }

        if (HandleCraftingPanelFastPathInput(gui, grid))
        {
            return false;
        }

        RefreshCraftingPanelDynamicFrameUi(gui, grid, adapter);
        return true;
    }

    private static bool CanRunCraftingPanelFrameFastPath() =>
        _craftingRedesignApplied &&
        _craftingRecipeGrid != null &&
        !IsUnityNull(_craftingRecipeGrid) &&
        !HasCraftingPanelRebuildWork() &&
        !CraftingUi.SearchInputDirty &&
        _craftingQueueRecipe == null &&
        !_continuingCraftingQueue;

    private static bool HasCraftingPanelRebuildWork() =>
        _craftingRecipeViewDirty ||
        _craftingRecipeGridDirty ||
        _craftingRecipeScrollbarDirty ||
        _craftingGroupRailDirty ||
        _craftingBottomControlsDirty;

    private static bool HandleCraftingPanelFastPathInput(InventoryGui gui, RectTransform grid)
    {
        if (HandleCraftingCountWheel())
        {
            MarkCraftingBottomControlsDirty();
            return true;
        }

        HandleCraftingGroupFavoriteClearShortcut();
        if (HasCraftingPanelRebuildWork())
        {
            return true;
        }

        bool recipeWheelHandled =
            HandleCraftingPinnedTooltipWheel() ||
            HandleCraftingHoverTooltipWheel() ||
            HandleCraftingRecipeGridZoomWheel(gui, grid) ||
            HandleCraftingRecipeGridWheel(gui, grid);
        return recipeWheelHandled || HasCraftingPanelRebuildWork();
    }

    private static void RefreshCraftingPanelDynamicFrameUi(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter)
    {
        SuppressCraftingTabAdapterFrameResidue(gui, adapter);
        LayoutCraftingTabAdapterBottomControls(gui, grid, adapter);
        UpdateCraftingRecipeGridZoomHint(gui, grid);
        UpdateCraftingTooltipRecipeOverlay(gui);
        if (HasActiveCraftingPinnedTooltip())
        {
            RepairCraftingPinnedTooltipTextVisibility();
        }
    }

    private static void StoreCraftingFrameFastPathSignature(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        if (!_craftingRedesignApplied ||
            _craftingRecipeGrid == null ||
            IsUnityNull(_craftingRecipeGrid) ||
            HasCraftingPanelRebuildWork() ||
            CraftingUi.SearchInputDirty ||
            _craftingQueueRecipe != null ||
            _continuingCraftingQueue)
        {
            ResetCraftingFrameFastPathStamp();
            return;
        }

        _craftingFrameFastPathStamp = CreateCraftingFrameFastPathStamp(gui, adapter);
    }

    private static void ResetCraftingFrameFastPathStamp()
    {
        _craftingFrameFastPathStamp = default;
    }

    private static CraftingFrameFastPathStamp CreateCraftingFrameFastPathStamp(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        RectTransform? grid = _craftingRecipeGrid;
        int selectedIndex = GetSelectedCraftingRecipeIndexSafe(gui);
        return new CraftingFrameFastPathStamp(
            gui.GetInstanceID(),
            gui.m_crafting != null && !IsUnityNull(gui.m_crafting) ? gui.m_crafting.GetInstanceID() : 0,
            grid != null && !IsUnityNull(grid) ? grid.GetInstanceID() : 0,
            adapter.Kind,
            SafeReadBool(() => gui.InCraftTab()),
            SafeReadBool(() => gui.InUpradeTab()),
            GetCraftingRecipeViewSignature(gui),
            selectedIndex,
            _craftingRecipePage,
            GetCraftingRecipeGridDimension(),
            CraftingRequirements.AvailabilityVersion,
            HasNoCraftCost(),
            GetCraftingPinnedTooltipGridSignature(),
            _craftingRecipeVariantVersion,
            _craftingFavoritesVersion,
            _selectedCraftingGroupId,
            _craftingSearchQuery,
            CraftingController.HoveredRecipeIndex,
            Screen.width,
            Screen.height);
    }

    private static bool HasActiveCraftingPinnedTooltip()
    {
        for (int i = 0; i < PinnedTooltips.Crafting.Panels.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
            if (panel != null && !IsUnityNull(panel) && panel.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }
}
