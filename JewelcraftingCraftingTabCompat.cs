using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool IsJewelcraftingCraftingRedesignTab(InventoryGui gui) =>
        IsJewelcraftingSocketTabActive(gui);

    private static void EnsureJewelcraftingCraftingPanelVisible(InventoryGui gui)
    {
        if (IsJewelcraftingSocketTabActive(gui) && gui.m_crafting != null && !gui.m_crafting.gameObject.activeSelf)
        {
            gui.m_crafting.gameObject.SetActive(true);
        }
    }

    private static void ApplyJewelcraftingCraftingRedesignVisibility(InventoryGui gui)
    {
        SetCraftingVanillaDetailVisible(gui, visible: false);
        _craftingVanillaDetailHidden = true;
        EnsureCraftingVanillaPanelBackgroundsHidden(gui);
        SuppressJewelcraftingCraftingSocketUiForRedesign(gui, includeSocketTab: true);
    }

    private static bool ShouldFinalizeJewelcraftingCraftingSocketSuppression(
        bool jewelcraftingSocketTab,
        bool firstApply,
        CraftingPanelUpdateReason reason,
        bool viewChanged)
    {
        return !jewelcraftingSocketTab && (firstApply || reason != CraftingPanelUpdateReason.FrameTick || viewChanged);
    }

    private static bool ShouldPreserveJewelcraftingForeignCraftingControls(InventoryGui gui) =>
        !IsVneiCraftingTabActive(gui) && (IsJewelcraftingGemcutterStationActive() || IsJewelcraftingSocketTabActive(gui));
}
