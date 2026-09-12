using System;
using BepInEx.Configuration;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

internal static class ItemRuleConfigStore
{
    internal static bool Save(ConfigEntry<string> entry, string expected, string next)
    {
        if (!string.Equals(entry.Value, expected, StringComparison.Ordinal)) return false;
        ConfigFile config = entry.ConfigFile;
        bool autoSave = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        try
        {
            entry.Value = next;
            config.Save();
            return true;
        }
        catch
        {
            // Keep both ConfigEntry and its SettingChanged consumers consistent
            // after an IO failure, so the same draft can be retried.
            entry.Value = expected;
            throw;
        }
        finally { config.SaveOnConfigSet = autoSave; }
    }
}
