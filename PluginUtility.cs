using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static string NormalizeSlotId(string? id)
    {
        return InventorySlotsConfigCore.NormalizeSlotId(id);
    }

    private static string NormalizeGroupId(string? id)
    {
        return InventorySlotsConfigCore.NormalizeGroupId(id);
    }

    private static string NormalizeResourceToken(string? token)
    {
        return InventorySlotsConfigCore.NormalizeResourceToken(token);
    }

    private static string CleanPrefabName(string name)
    {
        return InventorySlotsConfigCore.CleanPrefabName(name);
    }

    private static string StripLocalizationToken(string value)
    {
        return InventorySlotsConfigCore.StripLocalizationToken(value);
    }

    private static string GetItemPrefabName(ItemDrop.ItemData item) =>
        CleanPrefabName(item.m_dropPrefab != null ? item.m_dropPrefab.name : "");

    private static string StripRichText(string? text) =>
        JewelcraftingTooltipCore.StripRichText(text);

    private static string LocalizeUi(string token, string fallback)
    {
        string localized = Localization.instance != null ? Localization.instance.Localize(token) : token;
        return string.IsNullOrWhiteSpace(localized) || localized == token ? fallback : localized;
    }

    private static string JoinShortcutDisplayTexts(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        return string.IsNullOrWhiteSpace(second) ? first : $"{first}/{second}";
    }

    private static string GetPlayerId(Player player)
    {
        PlayerProfile? profile = Game.instance?.GetPlayerProfile();
        return profile != null ? profile.GetPlayerID().ToString() : player.GetPlayerID().ToString();
    }

    private static string ConfigDirectoryPath => Path.Combine(Paths.ConfigPath, ConfigDirectoryName);
    private static string YamlFilePath => Path.Combine(ConfigDirectoryPath, YamlFileName);
    private static string ResourceMapFilePath => Path.Combine(ConfigDirectoryPath, ResourceMapFileName);
    private static string ClientStateFilePath => Path.Combine(ConfigDirectoryPath, ClientStateFileName);

    private static bool IsUnityNull(UnityEngine.Object? obj)
    {
        return obj == null;
    }

    private static int GetUnityObjectId(UnityEngine.Object? obj) =>
        obj != null && !IsUnityNull(obj) ? obj.GetInstanceID() : 0;

    private static string SafeReadBool(System.Func<bool> read)
    {
        try
        {
            return read() ? "true" : "false";
        }
        catch
        {
            return "<error>";
        }
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

    private static ConfigEntry<T> OrderedConfigEntry<T>(string group, string name, T value, ConfigDescription description, int order, bool synchronizedSetting = true)
    {
        return ConfigEntry(group, name, value, WithConfigurationManagerOrder(description, order), synchronizedSetting);
    }

    private static ConfigEntry<ControllerHotkeyAction> OrderedControllerHotkeyConfigEntry(string name, ControllerHotkeyAction value, string description, int order)
    {
        ConfigDescription configDescription = new(
            $"{description} Use the dropdown, Presets, or Capture in Configuration Manager.",
            null,
            new object[] { new ConfigurationManagerAttributes { Order = order, CustomDrawer = DrawControllerHotkeyConfig } });
        return ConfigEntry(ControllerInputConfigSection, name, value, configDescription, synchronizedSetting: false);
    }

    private static ConfigEntry<T> OrderedConfigEntry<T>(string group, string name, T value, string description, int order, bool synchronizedSetting = true)
    {
        return OrderedConfigEntry(group, name, value, new ConfigDescription(description), order, synchronizedSetting);
    }

    private static ConfigDescription WithConfigurationManagerOrder(ConfigDescription description, int order)
    {
        object[] tags = description.Tags.Concat(new object[] { new ConfigurationManagerAttributes { Order = order } }).ToArray();
        return new ConfigDescription(description.Description, description.AcceptableValues, tags);
    }

    private static ConfigEntry<T> ConfigEntry<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
    {
        return ConfigEntry(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }
}
