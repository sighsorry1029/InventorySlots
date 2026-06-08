using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

internal enum CraftingPanelUpdateReason
{
    FrameTick,
    RecipeChanged,
    RecipeListChanged,
    StateChanged
}

public sealed partial class InventorySlotsPlugin
{
    private const float CraftingRequirementAvailabilityCacheSeconds = 0.25f;

    private readonly struct CraftingRequirementAvailabilityCacheEntry
    {
        public CraftingRequirementAvailabilityCacheEntry(int amount, float expiresAt)
        {
            Amount = amount;
            ExpiresAt = expiresAt;
        }

        public int Amount { get; }
        public float ExpiresAt { get; }
    }

    private sealed class CraftingRequirementRuntimeState
    {
        public readonly Dictionary<string, CraftingRequirementAvailabilityCacheEntry> AvailabilityCache = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<Piece.Requirement> VisibleRequirements = new();
        public readonly List<Piece.Requirement> VisibleRequirementCandidates = new();
        public readonly List<RectTransform?> OwnedSlots = new();
        public int AvailabilityVersion;
    }

    private sealed class CraftingRecipeRuntimeState
    {
        public readonly List<CraftingRecipeGridCell> GridCells = new();
        public readonly List<CraftingRecipeViewEntry> View = new();
        public readonly Dictionary<int, int> ViewIndexByOriginal = new();
        public readonly List<CraftingRecipeGroupPanel> GroupPanels = new();
        public readonly List<CraftingRecipeGroupButton> GroupButtons = new();
        public readonly Dictionary<Recipe, int> Variants = new();
        public readonly Dictionary<CraftingRecipePairCacheKey, string> SearchTextCache = new();
        public readonly Dictionary<string, CraftingHoverTooltipContent> HoverTooltipContentCache = new(StringComparer.Ordinal);
        public readonly List<JewelcraftingGemIconData> HoverGemIconCache = new();
        public readonly Dictionary<string, string> EnglishLocalizationCache = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> EnglishLocalizationIndex = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<CraftingRecipePairCacheKey, SortKey> SortKeyCache = new();
        public readonly Dictionary<CraftingRecipePairCacheKey, Dictionary<string, bool>> GroupMatchCache = new();
        public readonly List<CraftingRecipeGroupFilter> SelectableGroupFilterCache = new();
        public readonly Dictionary<string, bool> GroupHasRecipesCache = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CraftingGridRuntimeState
    {
        public RectTransform? RecipeGrid;
        public bool RecipeGridDirty = true;
        public int RecipePage;
        public CraftingRecipeGridStamp RecipeGridStamp;
        public string RecipeGridLayoutSignature = "";
        public int RecipeGridCellCapacity = -1;
    }

    private sealed class CraftingScrollbarRuntimeState
    {
        public RectTransform? RecipeScrollbar;
        public Scrollbar? RecipeScrollbarComponent;
        public bool UpdatingRecipeScrollbar;
        public bool RecipeScrollbarDirty = true;
        public CraftingRecipeScrollbarStamp RecipeScrollbarStamp;
    }

    private sealed class CraftingQueueRuntimeState
    {
        public bool ContinuingQueue;
        public int QueueTotal;
        public int QueueRemaining;
        public int QueueVariant;
        public int ProgressLabelCount;
        public int ProgressLabelVariant;
        public Recipe? QueueRecipe;
        public Recipe? ProgressLabelRecipe;
    }

    private sealed class CraftingUiRuntimeState
    {
        public GameObject? HoverTooltip;
        public RectTransform? HoverTooltipPanel;
        public TMP_Text? HoverTooltipTopic;
        public TMP_Text? HoverTooltipText;
        public readonly ScrollableTooltipBodyState HoverTooltipTextScroll = new();
        public RectTransform? TooltipRecipeOverlay;
        public RectTransform? TooltipExpandedPanel;
        public RectTransform? TooltipGemIconRow;
        public TMP_InputField? SearchInput;
        public RectTransform? SearchInputRect;
        public RectTransform? RecipeGridZoomHint;
        public TMP_Text? RecipeGridZoomHintText;
        public bool SearchInputDirty = true;
        public int HoveredRecipeIndex = -1;
        public CraftingSearchInputStamp SearchInputStamp;
        public CraftingRecipeGridZoomHintStamp RecipeGridZoomHintStamp;
        public string HoverTooltipSignature = "";
        public string HoverTooltipContentKey = "";
        public string HoverTooltipLayoutSignature = "";
        public string HoverTooltipVisualSignature = "";
        public string HoverGemIconSignature = "";
        public float HoverTooltipScrollOffset;
        public float HoverTooltipMaxScroll;
    }

    private sealed class CraftingRefreshRuntimeState
    {
        public bool RecipeViewDirty = true;
        public string RecipeViewSignature = "";
        public bool GroupRailDirty = true;
        public CraftingGroupRailStamp GroupRailStamp;
        public bool BottomControlsDirty = true;
        public string BottomControlsSignature = "";
        public CraftingStatusHudStamp SocketWarningStamp;
        public CraftingSortModeButtonsStamp SortModeButtonsStamp;
        public CraftingFrameFastPathStamp FrameFastPathStamp;
        public string RecipeListChangeSignature = "";
        public string SelectedRecipeChangeSignature = "";
    }

    private static class CraftingController
    {
        public static int HoveredRecipeIndex => CraftingUi.HoveredRecipeIndex;
        public static bool IsSearchInputDirty => CraftingUi.SearchInputDirty;

        public static bool NeedsModelRefresh(CraftingPanelUpdateReason reason, bool firstApply) =>
            firstApply ||
            reason != CraftingPanelUpdateReason.FrameTick ||
            HasModelRefreshWork();

        public static bool HasModelRefreshWork() =>
            CraftingRefresh.RecipeViewDirty ||
            CraftingGrid.RecipeGridDirty ||
            CraftingScrollbar.RecipeScrollbarDirty ||
            CraftingRefresh.GroupRailDirty;

        public static bool HasFrameRebuildWork() =>
            HasModelRefreshWork() ||
            CraftingRefresh.BottomControlsDirty;

        public static void MarkRecipeViewDirty()
        {
            CraftingRefresh.RecipeViewDirty = true;
            CraftingRefresh.RecipeViewSignature = "";
        }

        public static void MarkRecipeGridDirty()
        {
            CraftingGrid.RecipeGridDirty = true;
            CraftingGrid.RecipeGridStamp = default;
        }

        public static void MarkRecipeScrollbarDirty()
        {
            CraftingScrollbar.RecipeScrollbarDirty = true;
            CraftingScrollbar.RecipeScrollbarStamp = default;
        }

        public static void MarkGroupRailDirty()
        {
            CraftingRefresh.GroupRailDirty = true;
            CraftingRefresh.GroupRailStamp = default;
        }

        public static void MarkBottomControlsDirty()
        {
            CraftingRefresh.BottomControlsDirty = true;
            CraftingRefresh.BottomControlsSignature = "";
        }

        public static void MarkSearchInputDirty()
        {
            CraftingUi.SearchInputDirty = true;
            CraftingUi.SearchInputStamp = default;
        }

        public static bool CanReuseRecipeView(string signature) =>
            !CraftingRefresh.RecipeViewDirty &&
            string.Equals(CraftingRefresh.RecipeViewSignature, signature, StringComparison.Ordinal);

        public static void StoreRecipeViewSignature(string signature)
        {
            CraftingRefresh.RecipeViewSignature = signature;
            CraftingRefresh.RecipeViewDirty = false;
        }

        public static bool CanReuseRecipeGrid(CraftingRecipeGridStamp stamp) =>
            !CraftingGrid.RecipeGridDirty &&
            CraftingGrid.RecipeGridStamp.Equals(stamp);

        public static void StoreRecipeGridStamp(CraftingRecipeGridStamp stamp)
        {
            CraftingGrid.RecipeGridStamp = stamp;
            CraftingGrid.RecipeGridDirty = false;
        }

        public static bool CanReuseRecipeScrollbar(CraftingRecipeScrollbarStamp stamp) =>
            !CraftingScrollbar.RecipeScrollbarDirty &&
            CraftingScrollbar.RecipeScrollbarStamp.Equals(stamp);

        public static void StoreRecipeScrollbarStamp(CraftingRecipeScrollbarStamp stamp)
        {
            CraftingScrollbar.RecipeScrollbarStamp = stamp;
            CraftingScrollbar.RecipeScrollbarDirty = false;
        }

        public static bool CanReuseGroupRail(CraftingGroupRailStamp stamp) =>
            !CraftingRefresh.GroupRailDirty &&
            CraftingRefresh.GroupRailStamp.Equals(stamp);

        public static void StoreGroupRailStamp(CraftingGroupRailStamp stamp)
        {
            CraftingRefresh.GroupRailStamp = stamp;
            CraftingRefresh.GroupRailDirty = false;
        }

        public static bool CanReuseSearchInput(CraftingSearchInputStamp stamp) =>
            !CraftingUi.SearchInputDirty &&
            CraftingUi.SearchInputStamp.Equals(stamp);

        public static void StoreSearchInputStamp(CraftingSearchInputStamp stamp)
        {
            CraftingUi.SearchInputStamp = stamp;
            CraftingUi.SearchInputDirty = false;
        }

        public static bool NeedsBottomControlsLayout(string signature) =>
            CraftingRefresh.BottomControlsDirty ||
            !string.Equals(CraftingRefresh.BottomControlsSignature, signature, StringComparison.Ordinal);

        public static void StoreBottomControlsSignature(string signature)
        {
            CraftingRefresh.BottomControlsSignature = signature;
            CraftingRefresh.BottomControlsDirty = false;
        }

        public static bool CanReuseFrameFastPath(CraftingFrameFastPathStamp stamp) =>
            CraftingRefresh.FrameFastPathStamp.Equals(stamp);

        public static void StoreFrameFastPathStamp(CraftingFrameFastPathStamp stamp)
        {
            CraftingRefresh.FrameFastPathStamp = stamp;
        }

        public static void ResetFrameFastPathStamp()
        {
            CraftingRefresh.FrameFastPathStamp = default;
        }

        public static bool CanReuseSortModeButtons(CraftingSortModeButtonsStamp stamp) =>
            CraftingRefresh.SortModeButtonsStamp.Equals(stamp);

        public static void StoreSortModeButtonsStamp(CraftingSortModeButtonsStamp stamp)
        {
            CraftingRefresh.SortModeButtonsStamp = stamp;
        }

        public static void ResetSortModeButtonsStamp()
        {
            CraftingRefresh.SortModeButtonsStamp = default;
        }

        public static bool CanReuseSocketWarning(CraftingStatusHudStamp stamp) =>
            CraftingRefresh.SocketWarningStamp.Equals(stamp);

        public static void StoreSocketWarningStamp(CraftingStatusHudStamp stamp)
        {
            CraftingRefresh.SocketWarningStamp = stamp;
        }

        public static void ResetSocketWarningStamp()
        {
            CraftingRefresh.SocketWarningStamp = default;
        }

        public static bool TryStoreRecipeListChangeSignature(string signature)
        {
            if (string.Equals(CraftingRefresh.RecipeListChangeSignature, signature, StringComparison.Ordinal))
            {
                return false;
            }

            CraftingRefresh.RecipeListChangeSignature = signature;
            return true;
        }

        public static bool TryStoreSelectedRecipeChangeSignature(string signature)
        {
            if (string.Equals(CraftingRefresh.SelectedRecipeChangeSignature, signature, StringComparison.Ordinal))
            {
                return false;
            }

            CraftingRefresh.SelectedRecipeChangeSignature = signature;
            return true;
        }

        public static void ResetRecipeChangeSignatures()
        {
            CraftingRefresh.RecipeListChangeSignature = "";
            CraftingRefresh.SelectedRecipeChangeSignature = "";
        }

        public static void MarkRecipeViewDirtyCascade()
        {
            MarkRecipeViewDirty();
            MarkRecipeGridDirty();
            MarkRecipeScrollbarDirty();
            MarkGroupRailDirty();
            MarkBottomControlsDirty();
        }

        public static void MarkRecipeGridLayoutDirty()
        {
            MarkRecipeGridDirty();
            MarkRecipeScrollbarDirty();
            MarkBottomControlsDirty();
        }

        public static void SetHoveredRecipe(int index)
        {
            CraftingUi.HoveredRecipeIndex = index;
        }

        public static void ClearHoveredRecipe()
        {
            CraftingUi.HoveredRecipeIndex = -1;
        }

        public static bool IsHoveredRecipe(int index) =>
            CraftingUi.HoveredRecipeIndex == index;

        public static void InvalidateRecipeGridZoomHint()
        {
            CraftingUi.RecipeGridZoomHintStamp = default;
        }

        public static void ClearHoverTooltipContentKey()
        {
            CraftingUi.HoverTooltipContentKey = "";
        }
    }

    private static readonly CraftingRecipeRuntimeState CraftingRecipes = new();
    private static readonly CraftingRequirementRuntimeState CraftingRequirements = new();
    private static readonly CraftingGridRuntimeState CraftingGrid = new();
    private static readonly CraftingScrollbarRuntimeState CraftingScrollbar = new();
    private static readonly CraftingQueueRuntimeState CraftingQueue = new();
    private static readonly CraftingUiRuntimeState CraftingUi = new();
    private static readonly CraftingRefreshRuntimeState CraftingRefresh = new();
    private static readonly Dictionary<Image, bool> CraftingVanillaPanelBackgroundStates = new();
    private static readonly Dictionary<RectTransform, RectTransformSnapshot> CraftingPanelResizeProtectedSnapshots = new();
    private static readonly List<CraftingRecipeGroupFilter> CraftingRecipeGroupFilters = CreateCraftingRecipeGroupFilters();
    private static readonly HashSet<string> FavoriteCraftingRecipeKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FavoriteUpgradeItemKeys = new(StringComparer.OrdinalIgnoreCase);

    private static RectTransform? _craftingRecipeGrid { get => CraftingGrid.RecipeGrid; set => CraftingGrid.RecipeGrid = value; }
    private static RectTransform? _craftingRecipeScrollbar { get => CraftingScrollbar.RecipeScrollbar; set => CraftingScrollbar.RecipeScrollbar = value; }
    private static RectTransform? _craftingGroupRail;
    private static Scrollbar? _craftingRecipeScrollbarComponent { get => CraftingScrollbar.RecipeScrollbarComponent; set => CraftingScrollbar.RecipeScrollbarComponent = value; }
    private static TMP_InputField? _craftingCountInput;
    private static RectTransform? _craftingCountInputRect;
    private static RectTransform? _craftingCountInputViewport;
    private static RectTransform? _craftingUpgradeProgressionRect;
    private static TMP_Text? _craftingUpgradeProgressionText;
    private static RectTransform? _craftingSortModeButtonGroup;
    private static RectTransform? _craftingControlsBackground;
    private static RectTransform? _craftingSocketWarningRect;
    private static TMP_Text? _craftingSocketWarningText;
    private static Transform? _craftingRequiredStationLevelOriginalRoot;
    private static RectTransform? _craftingRequiredStationLevelHitbox;
    private static RectTransformSnapshot? _craftingPanelRootSnapshot;
    private static Vector2 _craftingPanelOriginalSizeDelta;

    private static bool _continuingCraftingQueue { get => CraftingQueue.ContinuingQueue; set => CraftingQueue.ContinuingQueue = value; }
    private static bool _updatingCraftingRecipeScrollbar { get => CraftingScrollbar.UpdatingRecipeScrollbar; set => CraftingScrollbar.UpdatingRecipeScrollbar = value; }
    private static bool _craftingRedesignApplied;
    private static bool _craftingVanillaRecipeElementsHidden;
    private static bool _craftingVanillaDetailHidden;
    private static bool _craftingVanillaRecipeScrollbarsHidden;
    private static bool _craftingVanillaPanelBackgroundsHidden;
    private static bool _craftingEnglishLocalizationIndexBuilt;

    private static int _craftingRecipePage { get => CraftingGrid.RecipePage; set => CraftingGrid.RecipePage = value; }
    private static int _craftingQueueTotal { get => CraftingQueue.QueueTotal; set => CraftingQueue.QueueTotal = value; }
    private static int _craftingQueueRemaining { get => CraftingQueue.QueueRemaining; set => CraftingQueue.QueueRemaining = value; }
    private static int _craftingQueueVariant { get => CraftingQueue.QueueVariant; set => CraftingQueue.QueueVariant = value; }
    private static int _craftingProgressLabelCount { get => CraftingQueue.ProgressLabelCount; set => CraftingQueue.ProgressLabelCount = value; }
    private static int _craftingProgressLabelVariant { get => CraftingQueue.ProgressLabelVariant; set => CraftingQueue.ProgressLabelVariant = value; }
    private static int _lastCraftingSelectedVariant = -1;
    private static int _craftingRecipeVariantVersion;
    private static int _craftingFavoritesVersion;
    private static int _craftingGroupAvailabilityVersion;
    private static int _craftingGroupAvailabilityBuiltVersion = -1;
    private static int _uiLocalizationVersion;

    private static string _loadedCraftingFavoritesPlayerId = "";
    private static string _selectedCraftingGroupId = "";
    private static string _craftingSearchQuery = "";
    private static string _craftingGroupAvailabilitySignature = "";
    private static string _craftingGroupAvailabilityContextSignature = "";
    private static string _craftingSelectableGroupFilterIdsSignature = "";
    private static string _craftingRecipeGridLayoutSignature { get => CraftingGrid.RecipeGridLayoutSignature; set => CraftingGrid.RecipeGridLayoutSignature = value; }
    private static string _craftingPanelExtensionSignature = "";
    private static string _pendingUpgradeFavoriteItemId = "";
    private static string _pendingUpgradeFavoritePrefab = "";
    private static int _craftingRecipeGridCellCapacity { get => CraftingGrid.RecipeGridCellCapacity; set => CraftingGrid.RecipeGridCellCapacity = value; }
    private static int _pendingUpgradeFavoriteQuality = -1;
    private static int _pendingUpgradeFavoriteVariant = -1;
    private static int _visibleRecycleNReclaimTabFrame = -1;
    private static int _visibleRecycleNReclaimTabGuiId = -1;
    private static Vector2i _pendingUpgradeFavoriteGridPos = new(-1, -1);
    private static bool _visibleRecycleNReclaimTabValue;

    private static Recipe? _craftingQueueRecipe { get => CraftingQueue.QueueRecipe; set => CraftingQueue.QueueRecipe = value; }
    private static Recipe? _craftingProgressLabelRecipe { get => CraftingQueue.ProgressLabelRecipe; set => CraftingQueue.ProgressLabelRecipe = value; }
    private static Recipe? _lastCraftingSelectedRecipe;
    private static object? _craftingGroupAvailabilityRecipeListRef;
    private static int _craftingGroupAvailabilityRecipeCount = -1;
}
