using System;
using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ConfigEntry<string> _autoPickupExcludedItemsConfig = null!;

    private static void BindItemRuleConfigs()
    {
        _restockButtonMode = OrderedConfigEntry(InventoryButtonsConfigSection, "Restock Button", InventoryButtonMode.Auto,
            RuleButtonModeDescription, order: 900, synchronizedSetting: false);
        _autoPickupButtonMode = OrderedConfigEntry(InventoryButtonsConfigSection, "Auto Pickup Exclude Button", InventoryButtonMode.Auto,
            RuleButtonModeDescription, order: 870, synchronizedSetting: false);
        _trashButtonMode = OrderedConfigEntry(InventoryButtonsConfigSection, "Trash Button", InventoryButtonMode.Auto,
            TrashButtonModeDescription, order: 850, synchronizedSetting: false);
        _autoPickupExcludedItemsConfig = OrderedConfigEntry(InventoryButtonsConfigSection, "Auto Pickup Excluded Items", "",
            "Client-only prefab names excluded from automatic pickup, separated by commas, semicolons or new lines. Empty preserves normal pickup. Manual E pickup remains available. Applies to all characters using this config; does not delete items or change restock rules.",
            order: 860, synchronizedSetting: false);
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
