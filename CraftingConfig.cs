using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ConfigEntry<int> _craftingRecipeGridSize = null!;
    private static ConfigEntry<KeyboardShortcut> _craftingRecipeGridZoomModifier = null!;
    private static ConfigEntry<KeyboardShortcut> _craftingClearFavoritesKey = null!;
    private static ConfigEntry<Toggle> _showCraftingRecipeGridZoomHint = null!;
    private static ConfigEntry<CraftingRecipeSortMode> _craftingRecipeSortMode = null!;
    private static ConfigEntry<Color> _craftingRecipeCraftableBackgroundColor = null!;

    private static void BindCraftingClientConfigs()
    {
        _craftingRecipeGridSize = OrderedConfigEntry(ClientConfigSection, "Crafting Recipe Grid Size", 6, new ConfigDescription("Recipe icon grid size inside the fixed 8x8 recipe area. 8 means 8x8 small icons; 4 means 4x4 large icons.", new AcceptableValueRange<int>(CraftingRecipeGridMinDimension, CraftingRecipeGridMaxDimension)), order: 860, synchronizedSetting: false);
        _craftingRecipeGridZoomModifier = ConfigEntry(ClientKeysConfigSection, "Crafting Recipe Grid Zoom Modifier", new KeyboardShortcut(KeyCode.LeftAlt), new ConfigDescription("Hold this key while using the mouse wheel over the crafting recipe grid to zoom between 8x8 and 4x4. Plain mouse wheel changes pages. Alt accepts both LeftAlt and RightAlt.", new AcceptableShortcuts()), synchronizedSetting: false);
        _craftingClearFavoritesKey = ConfigEntry(ClientKeysConfigSection, "Clear Crafting Favorites Key", new KeyboardShortcut(KeyCode.Mouse2), new ConfigDescription("Key used while hovering the favorite group icon to clear crafting or upgrade favorites for the current crafting tab.", new AcceptableShortcuts()), synchronizedSetting: false);
        _showCraftingRecipeGridZoomHint = OrderedConfigEntry(ClientUiConfigSection, "Show Crafting Recipe Grid Zoom Hint", Toggle.On, "Show the Alt + mouse wheel hint above the crafting recipe grid. Not synced with server.", order: 880, synchronizedSetting: false);
        _craftingRecipeGridSize.SettingChanged += (_, _) =>
        {
            InvalidateCraftingRecipeGridLayout();
            InvalidateCraftingRecipeGridZoomHint();
        };
        _craftingRecipeGridZoomModifier.SettingChanged += (_, _) => InvalidateCraftingRecipeGridZoomHint();
        _craftingClearFavoritesKey.SettingChanged += (_, _) => MarkCraftingGroupRailDirty();
        _showCraftingRecipeGridZoomHint.SettingChanged += (_, _) => InvalidateCraftingRecipeGridZoomHint();

        _craftingRecipeSortMode = OrderedConfigEntry(ClientConfigSection, "Crafting Recipe Sort Mode", CraftingRecipeSortMode.TierThenGroup, "Sorting mode used by crafting station recipe grids. Crafting still keeps favorites and craftable recipes first. TierThenGroup sorts biome/resource tier first, then predefined group. GroupThenTier sorts predefined group first, then biome/resource tier.", order: 870, synchronizedSetting: false);
        _craftingRecipeSortMode.SettingChanged += (_, _) =>
        {
            _craftingSortModeButtonsStamp = default;
            InvalidateCraftingRecipeView();
        };

        _craftingRecipeCraftableBackgroundColor = OrderedConfigEntry(ClientConfigSection, "Craftable Recipe Background Color", CraftingRecipeDefaultCraftableBackgroundColor, "Advanced color for recipe grid cells that can currently be crafted. Not synced with server.", order: 850, synchronizedSetting: false);
        _craftingRecipeCraftableBackgroundColor.SettingChanged += (_, _) => InvalidateCraftingRecipeGridLayout();
    }
}
