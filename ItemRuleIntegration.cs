using System;
using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ConfigEntry<Toggle> _showRestockRulesButton = null!;
    private static ConfigEntry<Toggle> _showAutoPickupRulesButton = null!;
    private static ConfigEntry<string> _autoPickupExcludedItemsConfig = null!;

    private static void BindItemRuleConfigs()
    {
        _showRestockRulesButton = OrderedConfigEntry(ClientUiConfigSection, "Show Restock Rules Button", Toggle.On,
            "Show the restock rules icon below the player inventory. Hiding it leaves saved restock limits active and discards an open unsaved draft. Applies immediately.",
            order: 786, synchronizedSetting: false);
        _showAutoPickupRulesButton = OrderedConfigEntry(ClientUiConfigSection, "Show Auto Pickup Exclude Button", Toggle.On,
            "Show the automatic pickup exclusion icon below the player inventory. Hiding it leaves saved exclusions active and discards an open unsaved draft. Applies immediately.",
            order: 784, synchronizedSetting: false);
        _autoPickupExcludedItemsConfig = ConfigEntry(ClientConfigSection, "Auto Pickup Excluded Items", "",
            "Client-only prefab names excluded from automatic pickup, separated by commas, semicolons or new lines. Empty preserves normal pickup. Manual E pickup remains available. Applies to all characters using this config; does not delete items or change restock rules.",
            synchronizedSetting: false);
        _autoPickupExcludedItemsConfig.SettingChanged += RefreshAutoPickupExclusions;
        RefreshAutoPickupExclusions(null, EventArgs.Empty);
    }

    private static bool CanShowItemRules(InventoryGui gui) => !ShouldBlockContainerPreviewInteraction(gui);

    private static void ShutdownItemRules()
    {
        DestroyItemRuleUi();
        if (_autoPickupExcludedItemsConfig != null) _autoPickupExcludedItemsConfig.SettingChanged -= RefreshAutoPickupExclusions;
        _autoPickupExcludedItems.Clear();
    }
}
