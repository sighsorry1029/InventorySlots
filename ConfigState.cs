using BepInEx.Configuration;
using ServerSync;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly ConfigSync ConfigSync = new(ModGUID)
    {
        DisplayName = ModName,
        CurrentVersion = ModVersion,
        MinimumRequiredVersion = ModVersion
    };

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    private static ConfigEntry<Toggle> _progressiveRowsEnabled = null!;
    private static ConfigEntry<int> _maxExtraRows = null!;
    private static ConfigEntry<string>[] _rowUnlockItems = null!;
    private static ConfigEntry<int> _quickSlotCount = null!;
    private static ConfigEntry<Toggle> _quickSlotProgressionEnabled = null!;
    private static ConfigEntry<string>[] _quickSlotRowUnlockItems = null!;
    private static ConfigEntry<Toggle> _equipmentSlotProgressionEnabled = null!;
    private static ConfigEntry<Toggle> _deathKeepRulesEnabled = null!;
    private static ConfigEntry<float> _areaQuickStackRange = null!;
    private static ConfigEntry<float> _areaRestockRange = null!;
    private static ConfigEntry<string> _restockTargetStackLimitsConfig = null!;
    private static ConfigEntry<KeyboardShortcut> _hotbarSwitchKey = null!;
    private static ConfigEntry<KeyboardShortcut> _containerRestockKey = null!;
    private static ConfigEntry<Toggle> _showHotbarSwitchHint = null!;
    private static ConfigEntry<Toggle> _quickSlotHudFollowsPanel = null!;
    private static ConfigEntry<InventoryRowsDisplayMode> _inventoryRowsDisplayMode = null!;
    private static ConfigEntry<Toggle> _showInventoryWheelButton = null!;
    private static ConfigEntry<Toggle> _enableInventoryTrashPanel = null!;
    private static ConfigEntry<PinnedTooltipSlotMode> _pinnedTooltipSlots = null!;
    private static ConfigEntry<KeyboardShortcut> _pinnedTooltipKey = null!;
    private static ConfigEntry<float> _pinnedTooltipBackgroundAlpha = null!;
    private static ConfigEntry<CraftingHoverTooltipMode> _showCraftingHoverTooltip = null!;
    private static ConfigEntry<float> _craftingHoverTooltipBackgroundAlpha = null!;
    private static ConfigEntry<float> _inventoryContainerHoverTooltipBackgroundAlpha = null!;
    private static ConfigEntry<PlayerStatBarLengthScaling> _playerStatBarLengthScaling = null!;
    private static ConfigEntry<float> _containerHoverHoldDuration = null!;
    private static ConfigEntry<int> _containerActionSuccessFxMode = null!;
    private static ConfigEntry<float> _containerActionSuccessFxVolume = null!;
    private static ConfigEntry<CraftingRecipeSortMode> _inventorySortMode = null!;
    private static ConfigEntry<Toggle> _autoFavoriteHotbarSwitchRow = null!;
    private static ConfigEntry<float> _mouseUiScrollMultiplier = null!;
    private static ConfigEntry<Toggle> _enableGamepadUiScroll = null!;
    private static ConfigEntry<GamepadUiScrollSource> _gamepadUiScrollSource = null!;
    private static ConfigEntry<float> _gamepadUiScrollSensitivity = null!;
    private static ConfigEntry<float> _gamepadUiScrollRepeatDelay = null!;
    private static ConfigEntry<float> _gamepadUiScrollDeadzone = null!;
    private static ConfigEntry<Toggle> _enableControllerHotkeys = null!;
    private static ConfigEntry<ControllerDPadHotkeyMode> _controllerDPadHotkeyMode = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerDPadModifierButton = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerCraftingGridZoomModifierButton = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerPinnedTooltipButton = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerFavoriteModifierButton = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerClearCraftingFavoritesButton = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerHotbarSwitchButton = null!;
    private static ConfigEntry<ControllerHotkeyAction> _controllerContainerRestockButton = null!;
    private static ConfigEntry<ControllerHotkeyAction>[] _controllerQuickSlotButtons = null!;
    private static ConfigEntry<KeyboardShortcut> _favoriteModifierKey = null!;
    private static ConfigEntry<KeyboardShortcut>[] _quickSlotHotkeys = null!;
    private static ConfigEntry<string>[] _quickSlotHotkeyDisplayTexts = null!;
    private static YamlRoot _yamlConfig = new();
}
