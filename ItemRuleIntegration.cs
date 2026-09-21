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
        _showRuleTooltips = OrderedConfigEntry(InventoryButtonsConfigSection, "Show Rule Tooltips", Toggle.On,
            "Show hover help for controls in the Restock targets and Auto pickup exclusions panels and restock-entry controls in F1. Applies immediately. Item information tooltips and Configuration Manager setting descriptions remain available.",
            order: 840, synchronizedSetting: false);
        _showRuleTooltips.SettingChanged += RefreshRuleTooltipVisibility;
        _autoPickupExcludedItemsConfig = OrderedConfigEntry(InventoryButtonsConfigSection, "Auto Pickup Excluded Items", "",
            "Client-only automatic pickup rules, separated by commas, semicolons or new lines. A prefab name such as Wood excludes it from automatic pickup; Wood | On is equivalent. Wood | Off keeps the item listed but allows automatic pickup. The panel checkbox controls this state. Empty preserves normal pickup. Manual E pickup remains available. Applies to all characters using this config; does not delete items or change restock rules.",
            order: 860, synchronizedSetting: false);
        _autoPickupExcludedItemsConfig.SettingChanged += RefreshAutoPickupExclusions;
        RefreshAutoPickupExclusions(null, EventArgs.Empty);
    }

    private static bool CanShowItemRules(InventoryGui gui) => !ShouldBlockContainerPreviewInteraction(gui);

    private static void ShutdownItemRules()
    {
        DestroyItemRuleUi();
        if (_autoPickupExcludedItemsConfig != null) _autoPickupExcludedItemsConfig.SettingChanged -= RefreshAutoPickupExclusions;
        if (_showRuleTooltips != null) _showRuleTooltips.SettingChanged -= RefreshRuleTooltipVisibility;
        _autoPickupExcludedItems.Clear();
    }
}
