using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ServerSync;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private static readonly ConfigSync ConfigSync = new(ModGUID)
    {
        DisplayName = ModName,
        CurrentVersion = ModVersion,
        MinimumRequiredVersion = ModVersion,
        ModRequired = true
    };

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    private static ConfigEntry<Toggle> _enableInventoryTrashPanel = null!;
    private static ConfigEntry<float> _areaQuickStackRange = null!;
    private static ConfigEntry<float> _areaRestockRange = null!;
    private static ConfigEntry<Color> _favoriteBorderColor = null!;
    private static ConfigEntry<Toggle> _containerActionSuccessFx = null!;
    private static ConfigEntry<KeyboardShortcut> _favoriteModifierKey = null!;
    private static ConfigEntry<KeyboardShortcut> _containerRestockKey = null!;
    private static ConfigEntry<string> _sortButtonPositionOffset = null!;
    private static ConfigEntry<string> _trashButtonPositionOffset = null!;
    private static ConfigEntry<string> _restockTargetStackLimitsConfig = null!;
    private static readonly Color FavoriteBorderDefaultColor = new(0.1f, 0.55f, 1f, 0.95f);
    private static readonly char[] ButtonPositionOffsetSeparators = { ' ', '\t', '\r', '\n', ':', '=', ',', ';', '(', ')', '[', ']' };
    private static readonly Dictionary<string, ButtonPositionOffsetEditorState> ButtonPositionOffsetEditorStates = new(StringComparer.Ordinal);

    private static void BindConfigs()
    {
        _serverConfigLocked = ConfigEntry(GeneralConfigSection, "Lock Configuration", Toggle.On, "When enabled, only server admins can modify this mod's synced configuration.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

        _enableInventoryTrashPanel = ConfigEntry(GeneralConfigSection, "Enable Inventory Trash Panel", Toggle.On, "When enabled, shows a trash panel below the player inventory. Dropping a held player-inventory item on it opens a confirmation dialog before deleting the held amount.");
        _areaQuickStackRange = ConfigEntry(GeneralConfigSection, "Area Quick Stack Range", 10f, new ConfigDescription("Range in meters for hover Area Quick Stack. Set to 0 to disable area quick stack. The opened-container Place stacks button only uses the current container.", new AcceptableValueRange<float>(0f, 50f)));
        _areaRestockRange = ConfigEntry(GeneralConfigSection, "Area Take Stacks Range", 10f, new ConfigDescription("Range in meters for hover Area Take Stacks. Set to 0 to disable area take stacks. The opened-container Take stacks button only uses the current container.", new AcceptableValueRange<float>(0f, 50f)));

        _favoriteModifierKey = ConfigEntry(ClientConfigSection, "Favorite Modifier Key", new KeyboardShortcut(KeyCode.LeftAlt), new ConfigDescription(
            "Hold this and left-click a player inventory cell to toggle that favorite slot. Alt accepts both LeftAlt and RightAlt.",
            new AcceptableShortcuts(),
            new ConfigurationManagerAttributes { Order = 900 }),
            synchronizedSetting: false);
        _containerRestockKey = ConfigEntry(ClientConfigSection, "Container Restock Key", new KeyboardShortcut(KeyCode.E, KeyCode.LeftAlt), new ConfigDescription(
            "Hold this while hovering a container to take stacks into favorite slots from that container and nearby containers. Alt accepts both LeftAlt and RightAlt.",
            new AcceptableShortcuts(),
            new ConfigurationManagerAttributes { Order = 890 }),
            synchronizedSetting: false);
        _favoriteBorderColor = ConfigEntry(ClientConfigSection, "Favorite Border Color", FavoriteBorderDefaultColor, new ConfigDescription(
            "Color for favorite slot borders. Uses the same RRGGBBAA color format as InventorySlots color configs. Not synced with server.",
            null,
            new ConfigurationManagerAttributes { Order = 880 }),
            synchronizedSetting: false);
        _favoriteBorderColor.SettingChanged += (_, _) => RefreshFavoriteBorders();

        _containerActionSuccessFx = ConfigEntry(
            ClientConfigSection,
            "Container Action Success FX",
            Toggle.On,
            new ConfigDescription(
                "Enables transient chest-unlock success effects for hover hold area quick stack/restock. Shows VFX at up to 10 changed containers and plays the SFX once at the interacted container. Nearby players who also enable this setting see the current action, but effects are not saved or replayed for later arrivals. Opened-container buttons do not spawn effects. Not synced with server.",
                null,
                new ConfigurationManagerAttributes { Order = 870 }),
            synchronizedSetting: false);
        _sortButtonPositionOffset = ConfigEntry(
            ClientConfigSection,
            "Sort Button Position",
            "x: 0 y: 0",
            new ConfigDescription(
                "Client-only position offset for the player inventory sort button. Format: x: 0 y: 0. Positive x moves right; positive y moves up.",
                null,
                new ConfigurationManagerAttributes
                {
                    Order = 840,
                    CustomDrawer = DrawButtonPositionOffsetConfig
                }),
            synchronizedSetting: false);
        _trashButtonPositionOffset = ConfigEntry(
            ClientConfigSection,
            "Trash Button Position",
            "x: 0 y: 0",
            new ConfigDescription(
                "Client-only position offset for the inventory trash button. Format: x: 0 y: 0. Positive x moves right; positive y moves up.",
                null,
                new ConfigurationManagerAttributes
                {
                    Order = 830,
                    CustomDrawer = DrawButtonPositionOffsetConfig
                }),
            synchronizedSetting: false);

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
    }

    private static ConfigEntry<T> ConfigEntry<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
    {
        return ConfigEntry(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private static ConfigEntry<T> ConfigEntry<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription = new(
            $"{description.Description} [{(synchronizedSetting ? "Synced with Server" : "Not Synced with Server")}]",
            description.AcceptableValues,
            description.Tags);
        ConfigEntry<T> configEntry = _instance.Config.Bind(group, name, value, extendedDescription);
        ConfigSync.AddConfigEntry(configEntry).SynchronizedConfig = synchronizedSetting;
        return configEntry;
    }

    private static Vector2 GetSortButtonPositionOffset() => GetConfiguredButtonPositionOffset(_sortButtonPositionOffset);

    private static Vector2 GetTrashButtonPositionOffset() => GetConfiguredButtonPositionOffset(_trashButtonPositionOffset);

    private static Vector2 GetConfiguredButtonPositionOffset(ConfigEntry<string>? entry)
    {
        if (entry == null)
        {
            return Vector2.zero;
        }

        if (TryParseButtonPositionOffset(entry.Value, out Vector2 offset))
        {
            return offset;
        }

        return Vector2.zero;
    }

    private static bool TryParseButtonPositionOffset(string? raw, out Vector2 offset)
    {
        offset = Vector2.zero;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        string normalized = raw!.ToLowerInvariant().Replace('x', ' ').Replace('y', ' ');
        string[] values = normalized.Split(ButtonPositionOffsetSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 2 ||
            !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        {
            return false;
        }

        offset = new Vector2(x, y);
        return true;
    }

    private static void DrawButtonPositionOffsetConfig(ConfigEntryBase entry)
    {
        string key = $"{entry.Definition.Section}/{entry.Definition.Key}";
        if (!ButtonPositionOffsetEditorStates.TryGetValue(key, out ButtonPositionOffsetEditorState state))
        {
            state = new ButtonPositionOffsetEditorState();
            ButtonPositionOffsetEditorStates[key] = state;
        }

        string currentValue = entry.BoxedValue as string ?? "";
        if (!string.Equals(currentValue, state.LastEntryValue, StringComparison.Ordinal))
        {
            if (TryParseButtonPositionOffset(currentValue, out Vector2 offset))
            {
                state.X = FormatButtonPositionOffsetValue(offset.x);
                state.Y = FormatButtonPositionOffsetValue(offset.y);
            }

            state.LastEntryValue = currentValue;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("x", GUILayout.Width(12f));
        string nextX = FilterSignedFloatText(GUILayout.TextField(state.X, GUILayout.Width(70f)));
        GUILayout.Label("y", GUILayout.Width(12f));
        string nextY = FilterSignedFloatText(GUILayout.TextField(state.Y, GUILayout.Width(70f)));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (string.Equals(nextX, state.X, StringComparison.Ordinal) &&
            string.Equals(nextY, state.Y, StringComparison.Ordinal))
        {
            return;
        }

        state.X = nextX;
        state.Y = nextY;
        if (!TryParseButtonPositionOffsetValue(state.X, out float x) ||
            !TryParseButtonPositionOffsetValue(state.Y, out float y))
        {
            return;
        }

        string nextValue = $"x: {FormatButtonPositionOffsetValue(x)} y: {FormatButtonPositionOffsetValue(y)}";
        state.LastEntryValue = nextValue;
        if (!string.Equals(currentValue, nextValue, StringComparison.Ordinal))
        {
            entry.BoxedValue = nextValue;
        }
    }

    private static bool TryParseButtonPositionOffsetValue(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string FormatButtonPositionOffsetValue(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FilterSignedFloatText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return new string(value.Where(c => char.IsDigit(c) || c == '-' || c == '+' || c == '.').ToArray());
    }

    private sealed class ButtonPositionOffsetEditorState
    {
        public string LastEntryValue { get; set; } = "";
        public string X { get; set; } = "0";
        public string Y { get; set; } = "0";
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

    private static bool IsUnityNull(UnityEngine.Object? obj)
    {
        return obj == null;
    }

    private static bool IsOutOfBounds(Inventory inventory, Vector2i pos)
    {
        return inventory == null || pos.x < 0 || pos.y < 0 || pos.x >= inventory.GetWidth() || pos.y >= inventory.GetHeight();
    }

    private static Inventory? GetPlayerInventory(Player? player)
    {
        return player != null ? ((Humanoid)player).GetInventory() : null;
    }

    private static bool IsPlayerInventory(Player? player, Inventory? inventory)
    {
        return player != null && inventory != null && inventory == GetPlayerInventory(player);
    }

    private static bool IsPlayerActionCell(Inventory inventory, Vector2i pos, bool includeHotbar)
    {
        InventoryCellKind kind = GetInventoryCellKind(inventory, pos);
        return InventoryActionCellPolicyCore.CanUseContainerActionSource(kind, includeHotbar);
    }

    private static bool CanFavoriteCell(Inventory inventory, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(inventory, pos);
        return InventoryActionCellPolicyCore.CanFavoriteSlot(kind);
    }

    private static bool CanUseFavoriteRestockTargetCell(Inventory inventory, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(inventory, pos);
        return InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(kind);
    }

    private static bool CanTrashCell(Inventory inventory, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(inventory, pos);
        return InventoryActionCellPolicyCore.CanTrashSlot(kind);
    }

    private static InventoryCellKind GetInventoryCellKind(Inventory inventory, Vector2i pos)
    {
        if (IsOutOfBounds(inventory, pos) || pos.y >= Math.Min(VanillaPlayerRows, inventory.GetHeight()))
        {
            return InventoryCellKind.Outside;
        }

        return pos.y == 0 ? InventoryCellKind.Hotbar : InventoryCellKind.RegularUnlocked;
    }

    private static bool IsRegularActionItem(Player player, Inventory inventory, ItemData item, bool includeHotbar)
    {
        return item?.m_shared != null && IsPlayerActionCell(inventory, item.m_gridPos, includeHotbar);
    }

    private static bool HasNoCustomData(ItemData item)
    {
        return item.m_customData == null || item.m_customData.Count == 0;
    }

    private static bool CanUseContainerActionStacking(ItemData item)
    {
        return item?.m_shared != null && HasNoCustomData(item);
    }

    private static string GetPlayerId(Player player)
    {
        PlayerProfile? profile = Game.instance?.GetPlayerProfile();
        return profile != null ? profile.GetPlayerID().ToString() : player.GetPlayerID().ToString();
    }

    private static string GetFavoriteFilePath(string playerId)
    {
        string safeId = new(playerId.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safeId))
        {
            safeId = "unknown";
        }

        return Path.Combine(Paths.ConfigPath, $"{ModName}.Favorites.{safeId}.txt");
    }

    private static string LocalizeUi(string token, string fallback)
    {
        if (Localization.instance == null || string.IsNullOrWhiteSpace(token))
        {
            return fallback;
        }

        string localized = Localization.instance.Localize(token);
        return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, token, StringComparison.Ordinal) ? fallback : localized;
    }

    private static string GetLocalizedItemName(ItemData item)
    {
        string name = item?.m_shared?.m_name ?? "";
        return Localization.instance != null ? Localization.instance.Localize(name) : name;
    }

    private static void ShowContainerActionResult(Player player, string actionToken, string actionFallback, int moved)
    {
        if (player == null)
        {
            return;
        }

        string action = LocalizeUi(actionToken, actionFallback);
        string format = LocalizeUi("$inventoryactions_action_result_format", "{action}: {count}");
        string message = format
            .Replace("{action}", action)
            .Replace("{count}", moved.ToString());
        player.Message(MessageHud.MessageType.Center, message, 0, null);
    }

    private static bool ShouldBlockGlobalHotkeys(Player? player = null)
    {
        if (player != null && (player.m_isLoading || ((Character)player).InCutscene()))
        {
            return true;
        }

        if (Chat.instance != null && !IsUnityNull(Chat.instance) && Chat.instance.HasFocus())
        {
            return true;
        }

        if (global::Console.IsVisible() ||
            TextInput.IsVisible() ||
            Menu.IsVisible() ||
            Minimap.IsOpen() ||
            Minimap.InTextInput() ||
            StoreGui.IsVisible() ||
            GameCamera.InFreeFly())
        {
            return true;
        }

        if (TextViewer.instance != null && !IsUnityNull(TextViewer.instance) && TextViewer.instance.IsVisible())
        {
            return true;
        }

        return ZNet.instance != null && !IsUnityNull(ZNet.instance) && ZNet.instance.InPasswordDialog();
    }

    private static bool IsShortcutHeldAllowingAltPair(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None || !AreShortcutModifiersHeldAllowingAltPair(shortcut))
        {
            return false;
        }

        return IsShortcutMainKeyHeldAllowingAltPair(shortcut);
    }

    private static bool AreShortcutModifiersHeldAllowingAltPair(KeyboardShortcut shortcut)
    {
        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (!IsShortcutModifierHeldAllowingAltPair(modifier))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsShortcutModifierHeldAllowingAltPair(KeyCode key)
    {
        if (key is KeyCode.LeftAlt or KeyCode.RightAlt)
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        return Input.GetKey(key);
    }

    private static bool IsShortcutMainKeyHeldAllowingAltPair(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey is KeyCode.LeftAlt or KeyCode.RightAlt)
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        return Input.GetKey(shortcut.MainKey);
    }

    private static string GetShortcutDisplayText(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None)
        {
            return "";
        }

        IEnumerable<string> parts = shortcut.Modifiers
            .Select(GetShortcutKeyDisplayText)
            .Concat(new[] { GetShortcutKeyDisplayText(shortcut.MainKey) })
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join("+", parts);
    }

    private static string GetShortcutKeyDisplayText(KeyCode key)
    {
        string text = key.ToString();
        if (text.StartsWith("Alpha", StringComparison.Ordinal))
        {
            return text.Substring("Alpha".Length);
        }

        return key switch
        {
            KeyCode.None => "",
            KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
            KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
            KeyCode.LeftShift or KeyCode.RightShift => "Shift",
            KeyCode.Mouse0 => "M1",
            KeyCode.Mouse1 => "M2",
            KeyCode.Mouse2 => "M3",
            KeyCode.Mouse3 => "M4",
            KeyCode.Mouse4 => "M5",
            KeyCode.Mouse5 => "M6",
            KeyCode.Mouse6 => "M7",
            KeyCode.Space => "Spc",
            KeyCode.Escape => "Esc",
            KeyCode.Return => "Enter",
            _ => text
        };
    }
}

internal static class ToggleExtensions
{
    public static bool IsOn(this InventoryActionsPlugin.Toggle value) => value == InventoryActionsPlugin.Toggle.On;
}
