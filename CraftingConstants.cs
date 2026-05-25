using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string CraftingRecipeGridName = "InventorySlots_CraftingRecipeGrid";
    private const string CraftingPinnedTooltipNamePrefix = "InventorySlots_CraftingPinnedTooltip_";
    private const string CraftingGemIconRowName = "InventorySlots_CraftingGemIconRow";
    private const string CraftingTooltipRecipeOverlayName = "InventorySlots_CraftingTooltipRecipeOverlay";
    private const string CraftingCountInputName = "InventorySlots_CraftingCountInput";
    private const string CraftingUpgradeProgressionName = "InventorySlots_CraftingUpgradeProgression";
    private const string CraftingSearchInputName = "InventorySlots_CraftingSearchInput";
    private const string CraftingSortModeButtonGroupName = "InventorySlots_CraftingSortModeButtons";
    private const string CraftingGroupFirstSortButtonName = "InventorySlots_CraftingGroupFirstSortButton";
    private const string CraftingTierFirstSortButtonName = "InventorySlots_CraftingTierFirstSortButton";
    private const string CraftingRecipeScrollbarName = "InventorySlots_CraftingRecipeScrollbar";
    private const string CraftingControlsBackgroundName = "InventorySlots_CraftingControlsBackground";
    private const string CraftingGroupRailName = "InventorySlots_CraftingGroupRail";
    private const string CraftingGroupPanelNamePrefix = "InventorySlots_CraftingGroupPanel_";
    private const string CraftingGroupButtonNamePrefix = "InventorySlots_CraftingGroupButton_";
    private const string CraftingRecipeGridCellNamePrefix = "InventorySlots_CraftingRecipeCell_";
    private const string CraftingRecipeStyleButtonName = "InventorySlots_CraftingRecipeStyleButton";
    private const string CraftingCountWheelIconName = "InventorySlots_CraftingCountWheelIcon";
    private const string CraftingRequirementSlotNamePrefix = "InventorySlots_CraftingRequirementSlot_";
    private const string CraftingRequirementHitboxName = "InventorySlots_CraftingRequirementHitbox";
    private const string CraftingRequiredStationHitboxName = "InventorySlots_CraftingRequiredStationHitbox";
    private const string CraftingRecipeGridZoomHintName = "InventorySlots_CraftingRecipeGridZoomHint";
    private const string CraftingFavoriteBorderName = "InventorySlots_CraftingFavoriteBorder";
    private const string CraftingSelectedRecipeBorderName = "InventorySlots_CraftingSelectedRecipeBorder";
    private const string CraftingPinnedTooltipMarkerName = "InventorySlots_CraftingPinnedTooltipMarker";
    private const string CraftingSocketWarningName = "InventorySlots_CraftingSocketWarning";

    private const int CraftingRecipeGridMinDimension = 4;
    private const int CraftingRecipeGridMaxDimension = 8;
    private const int CraftingRecipeGridColumns = 8;
    private const int CraftingRecipeGridRows = 9;
    private const int CraftingRecipeIconRows = 8;
    private const int CraftingBottomControlRow = 8;
    private const int CraftingSocketWarningRow = 9;
    private const int CraftingVisibleRequirementSlots = 4;
    private const float CraftingRecipeGridCellSize = 64f;
    private const float CraftingRecipeGridCellSpace = 72f;
    private const float CraftingSocketWarningHeight = 58f;
    private const float CraftingRecipeGridZoomHintFixedSize = 16f;
    private const float CraftingRecipeGridZoomHintFixedTextIconGap = 16f;
    private const float CraftingSearchInputWidth = CraftingRecipeGridCellSpace + CraftingRecipeGridCellSize;
    private const int CraftingTooltipRecipeSlotCount = 5;
    private const int CraftingQueueMaxCount = 99;
    private static readonly Vector2 CraftingRecipeGridFixedOffset = new(0f, -100f);
    private static readonly Vector2 CraftingRecipeGridZoomHintFixedOffset = new(256f, -16f);
    private const float CraftingPanelBottomFixedExtension = 84f;
    private static readonly Vector2 CraftingRecipeScrollbarFixedOffset = new(4f, 0f);
    private static readonly Vector2 CraftingGroupRailFixedOffset = new(-66f, -96f);
    private const float CraftingGroupIconBlockFixedSize = 60f;
    private const float CraftingGroupIconFixedPadding = 3f;
    private static readonly Vector2 CraftingSearchInputFixedOffset = new(0f, 3f);
    private const float CraftingSortModeButtonGap = 4f;
    private static readonly Vector2 CraftingSortModeButtonsFixedOffset = new(0f, 4f);
    private static readonly Vector2 CraftingBottomControlsFixedOffset = new(0f, 8f);
    private static readonly Vector2 CraftingCraftButtonFixedOffset = new(0f, -4f);
    private static readonly Vector2 CraftingCountInputFixedOffset = new(0f, -4f);
}
