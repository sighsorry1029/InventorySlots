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
        if (!CraftingController.CanReuseFrameFastPath(stamp))
        {
            CraftingController.StoreFrameFastPathStamp(stamp);
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
        !CraftingController.HasFrameRebuildWork() &&
        !CraftingController.IsSearchInputDirty &&
        CraftingQueue.QueueRecipe == null &&
        !CraftingQueue.ContinuingQueue;

    private static bool HandleCraftingPanelFastPathInput(InventoryGui gui, RectTransform grid)
    {
        if (HandleCraftingCountWheel())
        {
            CraftingController.MarkBottomControlsDirty();
            return true;
        }

        HandleCraftingGroupFavoriteClearShortcut();
        if (CraftingController.HasFrameRebuildWork())
        {
            return true;
        }

        bool recipeWheelHandled =
            HandleCraftingPinnedTooltipWheel() ||
            HandleCraftingHoverTooltipWheel() ||
            HandleCraftingRecipeGridZoomWheel(gui, grid) ||
            HandleCraftingRecipeGridWheel(gui, grid);
        return recipeWheelHandled || CraftingController.HasFrameRebuildWork();
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
            CraftingController.HasFrameRebuildWork() ||
            CraftingController.IsSearchInputDirty ||
            CraftingQueue.QueueRecipe != null ||
            CraftingQueue.ContinuingQueue)
        {
            ResetCraftingFrameFastPathStamp();
            return;
        }

        CraftingController.StoreFrameFastPathStamp(CreateCraftingFrameFastPathStamp(gui, adapter));
    }

    private static void ResetCraftingFrameFastPathStamp()
    {
        CraftingController.ResetFrameFastPathStamp();
    }

    private static CraftingFrameFastPathStamp CreateCraftingFrameFastPathStamp(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        RectTransform? grid = _craftingRecipeGrid;
        return new CraftingFrameFastPathStamp(
            gui.GetInstanceID(),
            gui.m_crafting != null && !IsUnityNull(gui.m_crafting) ? gui.m_crafting.GetInstanceID() : 0,
            grid != null && !IsUnityNull(grid) ? grid.GetInstanceID() : 0,
            adapter.Kind,
            GetSelectedCraftingRecipeIndexSafe(gui),
            GetCraftingRecipeViewSignature(gui, adapter),
            _craftingRecipePage,
            GetCraftingRecipeGridDimension(),
            CraftingRequirements.AvailabilityVersion,
            HasNoCraftCost(),
            GetCraftingPinnedTooltipGridSignature(),
            _craftingRecipeVariantVersion,
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
