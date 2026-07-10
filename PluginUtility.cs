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

    private static string GetPlayerId(Player player)
    {
        PlayerProfile? profile = Game.instance?.GetPlayerProfile();
        return profile != null ? profile.GetPlayerID().ToString() : player.GetPlayerID().ToString();
    }

    private static string YamlFilePath => Path.Combine(Paths.ConfigPath, YamlFileName);
    private static string ClientStateFilePath => Path.Combine(Paths.ConfigPath, ClientStateFileName);

    private static bool IsUnityNull(UnityEngine.Object? obj)
    {
        return obj == null;
    }

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
