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
        _craftingRecipeGridSize = ConfigEntry(
            ClientConfigSection,
            "Crafting Recipe Grid Size",
            6,
            new ConfigDescription(
                "Recipe icon grid size inside the fixed 8x8 recipe area. Changed with the crafting grid zoom controls.",
                new AcceptableValueRange<int>(CraftingRecipeGridMinDimension, CraftingRecipeGridMaxDimension),
                new ConfigurationManagerAttributes { Browsable = false }),
            synchronizedSetting: false);
        _craftingRecipeGridZoomModifier = OrderedConfigEntry(ClientKeysConfigSection, "Crafting Recipe Grid Zoom Modifier", new KeyboardShortcut(KeyCode.LeftAlt), new ConfigDescription("Hold this key while using the mouse wheel over the crafting recipe grid to zoom between 8x8 and 4x4. Plain mouse wheel changes pages. Alt accepts both LeftAlt and RightAlt.", new AcceptableShortcuts()), order: 748, synchronizedSetting: false);
        _craftingClearFavoritesKey = OrderedConfigEntry(ClientKeysConfigSection, "Clear Crafting Favorites Key", new KeyboardShortcut(KeyCode.Mouse2), new ConfigDescription("Key used while hovering the favorite group icon to clear crafting or upgrade favorites for the current crafting tab.", new AcceptableShortcuts()), order: 740, synchronizedSetting: false);
        _showCraftingRecipeGridZoomHint = OrderedConfigEntry(ClientUiConfigSection, "Show Crafting Recipe Grid Zoom Hint", Toggle.On, "Show the Alt + mouse wheel hint above the crafting recipe grid. Not synced with server.", order: 880, synchronizedSetting: false);
        _craftingRecipeGridSize.SettingChanged += (_, _) =>
        {
            CraftingController.MarkRecipeGridLayoutDirty();
            CraftingController.InvalidateRecipeGridZoomHint();
        };
        _craftingRecipeGridZoomModifier.SettingChanged += (_, _) => CraftingController.InvalidateRecipeGridZoomHint();
        _craftingClearFavoritesKey.SettingChanged += (_, _) => CraftingController.MarkGroupRailDirty();
        _showCraftingRecipeGridZoomHint.SettingChanged += (_, _) => CraftingController.InvalidateRecipeGridZoomHint();

        _craftingRecipeSortMode = ConfigEntry(
            ClientConfigSection,
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

        _craftingRecipeCraftableBackgroundColor = OrderedConfigEntry(ClientConfigSection, "Craftable Recipe Background Color", CraftingRecipeDefaultCraftableBackgroundColor, "Advanced color for recipe grid cells that can currently be crafted. Not synced with server.", order: 780, synchronizedSetting: false);
        _craftingRecipeCraftableBackgroundColor.SettingChanged += (_, _) => CraftingController.MarkRecipeGridLayoutDirty();
    }
}
