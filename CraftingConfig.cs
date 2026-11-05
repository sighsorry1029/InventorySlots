using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ConfigEntry<int> _craftingRecipeGridSize = null!;
    private static ConfigEntry<CraftingViewMode> _craftingViewMode = null!;
    private static ConfigEntry<CraftingScrollMode> _craftingGridScrollMode = null!;
    private static ConfigEntry<CraftingScrollMode> _craftingListScrollMode = null!;
    private static ConfigEntry<KeyboardShortcut> _craftingRecipeGridZoomModifier = null!;
    private static ConfigEntry<KeyboardShortcut> _craftingClearFavoritesKey = null!;
    private static ConfigEntry<Toggle> _showCraftingRecipeGridZoomHint = null!;
    private static ConfigEntry<CraftingRecipeSortMode> _craftingRecipeSortMode = null!;
    private static ConfigEntry<Color> _craftingRecipeCraftableBackgroundColor = null!;

    private static void BindCraftingClientConfigs()
    {
        _craftingViewMode = ConfigEntry(CraftingConfigSection, "Crafting View Mode", CraftingViewMode.List,
            new ConfigDescription(
                "View selected by the Grid/List button in the crafting panel. Applies live to Craft, Upgrade, Jewelcrafting Socket, and Recycle N Reclaim. Saved locally and not synced with server.",
                null,
                new ConfigurationManagerAttributes { Browsable = false }),
            synchronizedSetting: false);
        _craftingViewMode.SettingChanged += OnCraftingViewModeChanged;
        _craftingGridScrollMode = OrderedConfigEntry(CraftingConfigSection, "Crafting Grid Scroll Mode", CraftingScrollMode.Row, "Scroll distance per wheel step in the crafting Grid view: Row moves one row; Page moves the number of rows currently visible. Applies live to InventorySlots-managed crafting tabs. Not synced with server.", order: 1000, synchronizedSetting: false);
        _craftingListScrollMode = OrderedConfigEntry(CraftingConfigSection, "Crafting List Scroll Mode", CraftingScrollMode.Page, "Scroll distance per wheel step in the crafting List view: Row moves one row; Page moves the number of rows currently visible. Item details scroll independently. Applies live to InventorySlots-managed crafting tabs. Not synced with server.", order: 990, synchronizedSetting: false);
        _craftingRecipeGridSize = ConfigEntry(
            CraftingConfigSection,
            "Crafting Recipe Grid Size",
            6,
            new ConfigDescription(
                "Recipe icon grid size inside the fixed 8x8 recipe area. Changed with the crafting grid zoom controls.",
                new AcceptableValueRange<int>(CraftingRecipeGridMinDimension, CraftingRecipeGridMaxDimension),
                new ConfigurationManagerAttributes { Browsable = false }),
            synchronizedSetting: false);
        _showCraftingRecipeGridZoomHint = OrderedConfigEntry(CraftingConfigSection, "Show Crafting Recipe Grid Zoom Hint", Toggle.On, "Show the Alt + mouse wheel hint above the crafting recipe grid. Not synced with server.", order: 980, synchronizedSetting: false);
        _craftingRecipeGridSize.SettingChanged += (_, _) =>
        {
            CraftingController.MarkRecipeGridLayoutDirty();
        };

        _craftingRecipeSortMode = ConfigEntry(
            CraftingConfigSection,
            "Crafting Recipe Sort Mode",
            CraftingRecipeSortMode.TierThenGroup,
            new ConfigDescription(
                "Sorting mode selected by the crafting station recipe grid buttons.",
                null,
                new ConfigurationManagerAttributes { Browsable = false }),
            synchronizedSetting: false);
        _craftingRecipeSortMode.SettingChanged += (_, _) =>
        {
            CraftingController.ResetSortModeButtonsStamp();
            InvalidateCraftingRecipeView();
        };

        _craftingRecipeCraftableBackgroundColor = OrderedConfigEntry(CraftingConfigSection, "Craftable Recipe Background Color", CraftingRecipeDefaultCraftableBackgroundColor, "Advanced color for recipe grid cells that can currently be crafted. Not synced with server.", order: 970, synchronizedSetting: false);
        _craftingRecipeCraftableBackgroundColor.SettingChanged += (_, _) => CraftingController.MarkRecipeGridLayoutDirty();
    }

    private static void OnCraftingViewModeChanged(object? sender, System.EventArgs args)
    {
        CraftingController.MarkRecipeGridLayoutDirty();
        ResetCraftingFrameFastPathStamp();
    }
}
