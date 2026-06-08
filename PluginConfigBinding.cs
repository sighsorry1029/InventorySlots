using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void BindConfigs()
    {
        _serverConfigLocked = ConfigEntry("1 - General", "Lock Configuration", Toggle.On, "When enabled, only server admins can modify this mod's synced configuration.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

        _progressiveRowsEnabled = ConfigEntry(ProgressiveSlotsConfigSection, "Enable Progressive Rows", Toggle.On, "When enabled, extra inventory rows unlock by item discovery while the internal inventory height stays fixed.");
        _maxExtraRows = ConfigEntry(ProgressiveSlotsConfigSection, "Maximum Extra Rows", 5, new ConfigDescription("Maximum extra regular inventory rows that can become usable through progression. Slot coordinates stay fixed at the mod's reserved maximum.", new AcceptableValueRange<int>(0, MaxSupportedExtraRows)));
        _rowUnlockItems = new[]
        {
            ConfigEntry(ProgressiveSlotsConfigSection, "Extra Row 1 Items", "HardAntler", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks extra row 1."),
            ConfigEntry(ProgressiveSlotsConfigSection, "Extra Row 2 Items", "CryptKey", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks extra row 2."),
            ConfigEntry(ProgressiveSlotsConfigSection, "Extra Row 3 Items", "Wishbone", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks extra row 3."),
            ConfigEntry(ProgressiveSlotsConfigSection, "Extra Row 4 Items", "DragonTear", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks extra row 4."),
            ConfigEntry(ProgressiveSlotsConfigSection, "Extra Row 5 Items", "YagluthDrop", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks extra row 5.")
        };

        _quickSlotCount = ConfigEntry(ProgressiveSlotsConfigSection, "Quick Slot Count", 9, new ConfigDescription("Number of fixed quick slots reserved after equipment slots.", new AcceptableValueRange<int>(0, MaxSupportedQuickSlots)));
        _quickSlotProgressionEnabled = ConfigEntry(ProgressiveSlotsConfigSection, "Enable Quick Slot Progression", Toggle.On, "When enabled, quick slot row 1 is available at start and quick slot rows 2-3 unlock by item discovery.");
        _quickSlotRowUnlockItems = new[]
        {
            ConfigEntry(ProgressiveSlotsConfigSection, "Quick Slot Row 2 Items", "HardAntler", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks quick slot row 2."),
            ConfigEntry(ProgressiveSlotsConfigSection, "Quick Slot Row 3 Items", "CryptKey", "Comma-separated item prefab names or internal item names. Discovering any listed item unlocks quick slot row 3.")
        };
        BindInventoryStateConfigInvalidation();

        _deathKeepRulesEnabled = ConfigEntry("1 - General", "Enable Death Keep Rules", Toggle.On, "When enabled, items matching the YAML KeepOnDeath list stay in the player inventory instead of moving to the tombstone. When disabled, KeepOnDeath is ignored and death uses the normal tombstone behavior.");
        _enableInventoryTrashPanel = ConfigEntry("1 - General", "Enable Inventory Trash Panel", Toggle.On, "When enabled, shows a trash panel below the player inventory. Dropping a held player-inventory item on it opens a confirmation dialog before deleting the held amount.");

        _areaQuickStackRange = ConfigEntry("1 - General", "Area Quick Stack Range", 10f, new ConfigDescription("Range in meters for hover Area Quick Stack. Set to 0 to disable area quick stack. The opened-container Place stacks button only uses the current container.", new AcceptableValueRange<float>(0f, 50f)));
        _areaRestockRange = ConfigEntry("1 - General", "Area Take Stacks Range", 10f, new ConfigDescription("Range in meters for hover Area Take Stacks. Set to 0 to disable area take stacks. The opened-container Take stacks button only uses the current container.", new AcceptableValueRange<float>(0f, 50f)));

        _inventoryRowsDisplayMode = OrderedConfigEntry(ClientConfigSection, "Inventory Rows Display Mode", InventoryRowsDisplayMode.Expandable, "Client-only regular inventory row display mode. Fixed always shows all unlocked regular inventory rows. Expandable restores the last locally remembered visible row count and changes it with mouse wheel while the inventory is open.", order: 900, synchronizedSetting: false);
        _autoFavoriteHotbarSwitchRow = OrderedConfigEntry(ClientConfigSection, "Auto Favorite Hotbar Switch Row", Toggle.On, "When enabled, marks row 2 as favorite when the local player is loaded or spawned. Turn this Off if you want row 2 favorites to stay manually controlled. Not synced with server.", order: 890, synchronizedSetting: false);
        _inventorySortMode = OrderedConfigEntry(ClientConfigSection, "Inventory Sort Mode", CraftingRecipeSortMode.GroupThenTier, "Sorting mode used by player inventory and container sort buttons. GroupThenTier sorts predefined group first, then biome/resource tier. TierThenGroup sorts biome/resource tier first, then predefined group.", order: 880, synchronizedSetting: false);
        _containerHoverHoldDuration = OrderedConfigEntry(ClientConfigSection, "Container Hover Hold Duration", ContainerHoverHoldDurationDefault, new ConfigDescription("Seconds a container must stay hovered while holding E or the Container Restock Key before hover quick stack/restock fires. Lower values make pass-by container actions more responsive. Not synced with server.", new AcceptableValueRange<float>(ContainerHoverHoldDurationMin, ContainerHoverHoldDurationMax)), order: 830, synchronizedSetting: false);
        _containerActionSuccessFxMode = OrderedConfigEntry(
            ClientConfigSection,
            "Container Action Success FX Mode",
            4,
            new ConfigDescription(
                "Chest-unlock FX mode for hover hold area actions. 0 disables FX. 1 spawns FX at the interacted container. 2-12 spawns FX at each container whose stack changed, up to this many containers. Opened-container buttons do not spawn FX.",
                new AcceptableValueRange<int>(0, 12)),
            order: 820,
            synchronizedSetting: false);
        _containerActionSuccessFxVolume = OrderedConfigEntry(
            ClientConfigSection,
            "Container Action Success FX Volume",
            1f,
            new ConfigDescription(
                "Volume multiplier for InventorySlots container action success FX audio. 0 mutes the FX sound and 1 keeps the original prefab volume.",
                new AcceptableValueRange<float>(0f, 1f)),
            order: 810,
            synchronizedSetting: false);
        _mouseUiScrollMultiplier = OrderedConfigEntry(ClientConfigSection, "Mouse UI Scroll Multiplier", 1f, new ConfigDescription("Global multiplier for InventorySlots mouse-wheel UI scrolling. Applies to recipe pages, recipe zoom, expandable inventory rows, and InventorySlots tooltip scrolling. Not synced with server.", new AcceptableValueRange<float>(0.1f, 5f)), order: 790, synchronizedSetting: false);

        _quickSlotHudFollowsPanel = OrderedConfigEntry(ClientUiConfigSection, "Quick Slot HUD Follows Panel", Toggle.On, "When enabled, the quick slot HUD follows the quick slot inventory panel position. Turn this Off to keep the HUD at its last saved position while moving the panel separately. Not synced with server.", order: 910, synchronizedSetting: false);
        _showInventoryWheelButton = OrderedConfigEntry(ClientUiConfigSection, "Show Inventory Wheel Hint", Toggle.On, "Show the mouse wheel hint next to the player inventory when expandable inventory rows are available. Not synced with server.", order: 900, synchronizedSetting: false);
        _showHotbarSwitchHint = OrderedConfigEntry(ClientUiConfigSection, "Show Hotbar Switch Hint", Toggle.On, "Show the hotbar row switch hint next to the hotbar. Not synced with server.", order: 890, synchronizedSetting: false);
        _showCraftingHoverTooltip = OrderedConfigEntry(ClientUiConfigSection, "Show Crafting Hover Tooltip", Toggle.On, "Show InventorySlots recipe hover tooltips in the crafting station grid. Pinned crafting tooltips still work when this is Off. Not synced with server.", order: 870, synchronizedSetting: false);
        _pinnedTooltipSlots = OrderedConfigEntry(ClientUiConfigSection, "Pinned Tooltip Slots", PinnedTooltipSlotMode.Two, "Number of comparison tooltip panels available for pinning. Inventory/container panels unfold from the inventory panel edge to the right, and crafting panels unfold from the crafting panel edge to the left.", order: 860, synchronizedSetting: false);
        _pinnedTooltipBackgroundAlpha = OrderedConfigEntry(ClientUiConfigSection, "Pinned Tooltip Background Alpha", 0.9f, new ConfigDescription("Advanced alpha for pinned tooltip panel backgrounds. 0 is fully transparent and 1 is fully opaque. Not synced with server.", new AcceptableValueRange<float>(0f, 1f)), order: 850, synchronizedSetting: false);
        _craftingHoverTooltipBackgroundAlpha = OrderedConfigEntry(ClientUiConfigSection, "Crafting Hover Tooltip Background Alpha", 0.9f, new ConfigDescription("Alpha for InventorySlots crafting recipe hover tooltip backgrounds. 0 is fully transparent and 1 is fully opaque. Not synced with server.", new AcceptableValueRange<float>(0f, 1f)), order: 840, synchronizedSetting: false);
        _inventoryContainerHoverTooltipBackgroundAlpha = OrderedConfigEntry(ClientUiConfigSection, "Inventory Container Hover Tooltip Background Alpha", 0.9f, new ConfigDescription("Alpha for InventorySlots inventory and container hover tooltip backgrounds. 0 is fully transparent and 1 is fully opaque. Not synced with server.", new AcceptableValueRange<float>(0f, 1f)), order: 830, synchronizedSetting: false);
        LoadInventoryPanelPositionsFromClientState();
        _pinnedTooltipSlots.SettingChanged += (_, _) => InvalidatePinnedTooltipUi();
        _pinnedTooltipBackgroundAlpha.SettingChanged += (_, _) => RefreshPinnedTooltipBackgrounds();
        _showCraftingHoverTooltip.SettingChanged += (_, _) => OnCraftingHoverTooltipConfigChanged();
        _craftingHoverTooltipBackgroundAlpha.SettingChanged += (_, _) => RefreshCraftingHoverTooltipBackground();
        _inventoryContainerHoverTooltipBackgroundAlpha.SettingChanged += (_, _) => RefreshInventoryContainerHoverTooltipBackground();
        _enableInventoryTrashPanel.SettingChanged += (_, _) => OnInventoryTrashPanelConfigChanged();
        _autoFavoriteHotbarSwitchRow.SettingChanged += (_, _) => ApplyAutoFavoriteHotbarSwitchRowToLocalPlayer();
        _quickSlotHudFollowsPanel.SettingChanged += (_, _) => OnQuickSlotHudFollowsPanelChanged();

        BindCraftingClientConfigs();

        _restockTargetStackLimitsConfig = ConfigEntry(
            RestockConfigSection,
            "Restock Target Stack Limits",
            "",
            new ConfigDescription(
                "Client-only per-item target stack caps for Take stacks/restock into favorite slots. Keys may be prefab names, internal item names, or localized item names in the current client language, such as Stone: 10, Coins: 500. Separate entries with commas, semicolons, or new lines. Empty uses each item's normal max stack; 0 prevents restocking that item.",
                null,
                new ConfigurationManagerAttributes
                {
                    Order = 700,
                    CustomDrawer = DrawRestockTargetStackLimitsConfig
                }),
            synchronizedSetting: false);
        _restockTargetStackLimitsConfig.SettingChanged += (_, _) => RefreshRestockTargetStackLimits();
        RefreshRestockTargetStackLimits();

        _enableGamepadUiScroll = OrderedConfigEntry(ControllerInputConfigSection, "Enable Gamepad UI Scroll", Toggle.On, "Allow gamepad input to emulate InventorySlots UI scrolling while gamepad input is active. Not synced with server.", order: 600, synchronizedSetting: false);
        _gamepadUiScrollSource = OrderedConfigEntry(ControllerInputConfigSection, "Gamepad UI Scroll Source", GamepadUiScrollSource.RightStickY, "Gamepad input used for InventorySlots UI scrolling. RightStickY uses the right stick vertical axis; DPadVertical uses held up/down; RightStickYOrDPadVertical accepts either.", order: 590, synchronizedSetting: false);
        _gamepadUiScrollSensitivity = OrderedConfigEntry(ControllerInputConfigSection, "Gamepad UI Scroll Sensitivity", 6f, new ConfigDescription("Continuous gamepad UI scroll speed multiplier for tooltip panels. Discrete UI actions such as recipe pages still move one step per repeat.", new AcceptableValueRange<float>(0.5f, 20f)), order: 580, synchronizedSetting: false);
        _gamepadUiScrollRepeatDelay = OrderedConfigEntry(ControllerInputConfigSection, "Gamepad UI Scroll Repeat Delay", 0.18f, new ConfigDescription("Delay in seconds between repeated gamepad scroll steps for discrete UI actions such as recipe pages and expandable inventory rows.", new AcceptableValueRange<float>(0.05f, 0.75f)), order: 570, synchronizedSetting: false);
        _gamepadUiScrollDeadzone = OrderedConfigEntry(ControllerInputConfigSection, "Gamepad UI Scroll Deadzone", 0.35f, new ConfigDescription("Minimum gamepad stick magnitude required before InventorySlots treats it as UI scroll input.", new AcceptableValueRange<float>(0.05f, 0.95f)), order: 560, synchronizedSetting: false);
        _enableControllerHotkeys = OrderedConfigEntry(ControllerInputConfigSection, "Enable Controller Hotkeys", Toggle.On, "Allow optional controller actions below to trigger InventorySlots hotkeys. Off disables an action. Not synced with server.", order: 500, synchronizedSetting: false);
        _controllerDPadHotkeyMode = OrderedConfigEntry(ControllerInputConfigSection, "Controller DPad Hotkey Mode", ControllerDPadHotkeyMode.InventoryNavigation, "Controls how InventorySlots should treat DPad controller hotkeys. InventoryNavigation leaves DPad actions for vanilla inventory navigation. Hotkeys allows DPad action configs to fire. HotkeysWhileHoldingModifier only allows them while the Controller DPad Modifier Button is held.", order: 495, synchronizedSetting: false);
        _controllerDPadModifierButton = OrderedControllerHotkeyConfigEntry("Controller DPad Modifier Button", ControllerHotkeyAction.Off, "Controller action held when Controller DPad Hotkey Mode is HotkeysWhileHoldingModifier. Off disables the modifier gate.", order: 493);
        _controllerCraftingGridZoomModifierButton = OrderedControllerHotkeyConfigEntry("Controller Crafting Grid Zoom Modifier Button", ControllerHotkeyAction.JoyLStick, "Controller action held while using gamepad UI scroll over the crafting recipe grid to zoom instead of changing recipe pages. Off disables this controller modifier.", order: 490);
        _controllerPinnedTooltipButton = OrderedControllerHotkeyConfigEntry("Controller Pinned Tooltip Button", ControllerHotkeyAction.Off, "Controller action pressed while hovering an inventory, container, or crafting recipe item to pin or unpin its tooltip. Off disables this controller hotkey.", order: 480);
        _controllerFavoriteModifierButton = OrderedControllerHotkeyConfigEntry("Controller Favorite Modifier Button", ControllerHotkeyAction.Off, "Controller action held while activating an inventory cell or crafting recipe icon to toggle favorite. Off disables this controller modifier.", order: 470);
        _controllerClearCraftingFavoritesButton = OrderedControllerHotkeyConfigEntry("Controller Clear Crafting Favorites Button", ControllerHotkeyAction.Off, "Controller action pressed while hovering the favorite group icon to clear crafting or upgrade favorites. Off disables this controller hotkey.", order: 460);
        _controllerHotbarSwitchButton = OrderedControllerHotkeyConfigEntry("Controller Hotbar Switch Button", ControllerHotkeyAction.Off, "Controller action pressed to rotate unlocked regular inventory rows. Off disables this controller hotkey.", order: 450);
        _controllerContainerRestockButton = OrderedControllerHotkeyConfigEntry("Controller Container Restock Button", ControllerHotkeyAction.Off, "Controller action held while looking at a container to take stacks into favorite slots from that container and nearby containers. Off disables this controller hotkey.", order: 440);
        _controllerQuickSlotButtons = new[]
        {
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 1 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 1. Off disables this controller hotkey.", order: 421),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 2 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 2. Off disables this controller hotkey.", order: 420),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 3 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 3. Off disables this controller hotkey.", order: 419),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 4 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 4. Off disables this controller hotkey.", order: 418),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 5 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 5. Off disables this controller hotkey.", order: 417),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 6 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 6. Off disables this controller hotkey.", order: 416),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 7 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 7. Off disables this controller hotkey.", order: 415),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 8 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 8. Off disables this controller hotkey.", order: 414),
            OrderedControllerHotkeyConfigEntry("Controller Quick Slot 9 Button", ControllerHotkeyAction.Off, "Controller action pressed to activate quick slot 9. Off disables this controller hotkey.", order: 413)
        };

        _hotbarSwitchKey = OrderedConfigEntry(ClientKeysConfigSection, "Hotbar Switch Key", new KeyboardShortcut(KeyCode.BackQuote), new ConfigDescription("Hotkey used to rotate unlocked regular inventory rows.", new AcceptableShortcuts()), order: 780, synchronizedSetting: false);
        _containerRestockKey = OrderedConfigEntry(ClientKeysConfigSection, "Container Restock Key", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt), new ConfigDescription("Hold this while hovering a container to take stacks into favorite slots from that container and nearby containers. Alt accepts both LeftAlt and RightAlt.", new AcceptableShortcuts()), order: 770, synchronizedSetting: false);
        _favoriteModifierKey = OrderedConfigEntry(ClientKeysConfigSection, "Favorite Modifier Key", new KeyboardShortcut(KeyCode.LeftAlt), new ConfigDescription("Hold this and left-click a player inventory cell to toggle that favorite slot. Alt accepts both LeftAlt and RightAlt.", new AcceptableShortcuts()), order: 760, synchronizedSetting: false);
        _pinnedTooltipKey = OrderedConfigEntry(ClientKeysConfigSection, "Pinned Tooltip Key", new KeyboardShortcut(KeyCode.Mouse2), new ConfigDescription("Key used while hovering an inventory, container, or crafting recipe item to pin or unpin its comparison tooltip.", new AcceptableShortcuts()), order: 750, synchronizedSetting: false);
        _quickSlotHotkeys = new[]
        {
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 1 Hotkey", new KeyboardShortcut(KeyCode.Z), new ConfigDescription("Hotkey used to activate quick slot 1.", new AcceptableShortcuts()), order: 730, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 2 Hotkey", new KeyboardShortcut(KeyCode.X), new ConfigDescription("Hotkey used to activate quick slot 2.", new AcceptableShortcuts()), order: 728, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 3 Hotkey", new KeyboardShortcut(KeyCode.C), new ConfigDescription("Hotkey used to activate quick slot 3.", new AcceptableShortcuts()), order: 726, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 4 Hotkey", new KeyboardShortcut(KeyCode.Z, KeyCode.LeftAlt), new ConfigDescription("Hotkey used to activate quick slot 4.", new AcceptableShortcuts()), order: 724, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 5 Hotkey", new KeyboardShortcut(KeyCode.X, KeyCode.LeftAlt), new ConfigDescription("Hotkey used to activate quick slot 5.", new AcceptableShortcuts()), order: 722, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 6 Hotkey", new KeyboardShortcut(KeyCode.C, KeyCode.LeftAlt), new ConfigDescription("Hotkey used to activate quick slot 6.", new AcceptableShortcuts()), order: 720, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 7 Hotkey", new KeyboardShortcut(KeyCode.Alpha1, KeyCode.LeftAlt), new ConfigDescription("Hotkey used to activate quick slot 7.", new AcceptableShortcuts()), order: 718, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 8 Hotkey", new KeyboardShortcut(KeyCode.Alpha2, KeyCode.LeftAlt), new ConfigDescription("Hotkey used to activate quick slot 8.", new AcceptableShortcuts()), order: 716, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 9 Hotkey", new KeyboardShortcut(KeyCode.Alpha3, KeyCode.LeftAlt), new ConfigDescription("Hotkey used to activate quick slot 9.", new AcceptableShortcuts()), order: 714, synchronizedSetting: false)
        };
        _quickSlotHotkeyDisplayTexts = new[]
        {
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 1 Hotkey Display Text", "", "Optional text shown on quick slot 1 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 729, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 2 Hotkey Display Text", "", "Optional text shown on quick slot 2 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 727, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 3 Hotkey Display Text", "", "Optional text shown on quick slot 3 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 725, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 4 Hotkey Display Text", "", "Optional text shown on quick slot 4 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 723, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 5 Hotkey Display Text", "", "Optional text shown on quick slot 5 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 721, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 6 Hotkey Display Text", "", "Optional text shown on quick slot 6 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 719, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 7 Hotkey Display Text", "", "Optional text shown on quick slot 7 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 717, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 8 Hotkey Display Text", "", "Optional text shown on quick slot 8 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 715, synchronizedSetting: false),
            OrderedConfigEntry(ClientKeysConfigSection, "Quick Slot 9 Hotkey Display Text", "", "Optional text shown on quick slot 9 instead of the generated hotkey label. Leave empty to auto-generate, for example M4+[ or Alt+1.", order: 713, synchronizedSetting: false)
        };
    }

    private static void BindInventoryStateConfigInvalidation()
    {
        AddInventoryStateConfigInvalidation(_progressiveRowsEnabled);
        AddInventoryStateConfigInvalidation(_maxExtraRows);
        foreach (ConfigEntry<string> entry in _rowUnlockItems)
        {
            AddInventoryStateConfigInvalidation(entry);
        }

        AddSlotDefinitionConfigInvalidation(_quickSlotCount);
        AddInventoryStateConfigInvalidation(_quickSlotProgressionEnabled);
        foreach (ConfigEntry<string> entry in _quickSlotRowUnlockItems)
        {
            AddInventoryStateConfigInvalidation(entry);
        }
    }

    private static void AddInventoryStateConfigInvalidation<T>(ConfigEntry<T> entry)
    {
        entry.SettingChanged += (_, _) => RequestLocalInventoryState(InventoryStateEnsureReason.ConfigChanged, InventoryStateAuditLevel.FullIntegrity);
    }

    private static void AddSlotDefinitionConfigInvalidation<T>(ConfigEntry<T> entry)
    {
        entry.SettingChanged += (_, _) =>
        {
            RebuildSlotDefinitions();
            RequestLocalInventoryState(InventoryStateEnsureReason.ConfigChanged, InventoryStateAuditLevel.FullIntegrity);
        };
    }

    private sealed class AcceptableShortcuts : AcceptableValueBase
    {
        public AcceptableShortcuts()
            : base(typeof(KeyboardShortcut))
        {
        }

        public override object Clamp(object value) => value;
        public override bool IsValid(object value) => true;
        public override string ToDescriptionString() => $"# Acceptable values: {string.Join(", ", UnityInput.Current.SupportedKeyCodes)}";
    }

    private sealed class ConfigurationManagerAttributes
    {
        public int? Order { get; set; }
        public bool? Browsable { get; set; }
        public Action<ConfigEntryBase>? CustomDrawer { get; set; }
    }
}
