using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly AccessTools.FieldRef<InventoryGui, int> CraftingFrameSelectedVariant =
        AccessTools.FieldRefAccess<InventoryGui, int>("m_selectedVariant");

    private static bool TryRunCraftingPanelFrameFastPath(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        // Scroll input belongs to the full path; never replay a step after
        // a fast-path input handler dirties the layout and falls through.
        if (adapter.Kind != CraftingTabAdapterKind.Vanilla ||
            !CanRunCraftingPanelFrameFastPath() || HasUnconsumedUiScrollInput())
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
        if (grid == null || IsUnityNull(grid) || grid.parent != gui.m_crafting || !grid.gameObject.activeSelf)
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

        bool recipeWheelHandled = HandleCraftingRecipeWheelInput(gui, grid);
        return recipeWheelHandled || CraftingController.HasFrameRebuildWork();
    }

    private static void RefreshCraftingPanelDynamicFrameUi(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter)
    {
        UpdateCraftingQueueLifecycle(gui);
        SuppressCraftingTabAdapterFrameResidue(gui, adapter);
        SuppressJewelcraftingCraftingSocketUiForRedesign(gui);
        LayoutCraftingTabAdapterBottomControls(gui, grid, adapter);
        UpdateCraftingRecipeGridZoomHint(gui, grid);
        UpdateCraftingTooltipRecipeOverlay(gui);
        UpdateCraftingViewControls(gui, grid, adapter);
        if (HasActiveCraftingPinnedTooltip())
        {
            RepairCraftingPinnedTooltipTextVisibility();
        }
    }

    private static void StoreCraftingFrameFastPathSignature(InventoryGui gui, CraftingTabAdapterState adapter)
    {
        if (adapter.Kind != CraftingTabAdapterKind.Vanilla || !CanRunCraftingPanelFrameFastPath())
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
            Screen.height,
            GetCraftingRecipeGridAvailabilityHash(gui, GetCraftingRecipePageStart(), adapter),
            CraftingUi.SearchInput != null && CraftingUi.SearchInput.isFocused,
            CraftingFrameSelectedVariant(gui));
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
