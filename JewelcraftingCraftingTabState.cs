using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum JewelcraftingSocketTabState
    {
        Unavailable,
        PrimaryCraftingTab,
        Closed,
        Selected
    }

    private static bool IsJewelcraftingSocketTabActive(InventoryGui? gui)
    {
        return GetJewelcraftingSocketTabState(gui) == JewelcraftingSocketTabState.Selected;
    }

    private static JewelcraftingSocketTabState GetJewelcraftingSocketTabState(InventoryGui? gui)
    {
        if (!HasJewelcraftingActive ||
            IsDedicatedServer ||
            gui == null ||
            !TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api) ||
            api == null)
        {
            return JewelcraftingSocketTabState.Unavailable;
        }

        if (IsCraftingPrimaryTabSelected(gui))
        {
            return JewelcraftingSocketTabState.PrimaryCraftingTab;
        }

        if (api.IsSocketTabOpen())
        {
            return JewelcraftingSocketTabState.Selected;
        }

        Button? button = GetJewelcraftingSocketTabButton(api);
        if (button == null || IsUnityNull(button) || !button.gameObject.activeInHierarchy)
        {
            return JewelcraftingSocketTabState.Closed;
        }

        return !button.interactable
            ? JewelcraftingSocketTabState.Selected
            : JewelcraftingSocketTabState.Closed;
    }

    private static Button? GetJewelcraftingSocketTabButton(JewelcraftingCraftingSocketUiApi api)
    {
        Transform? tab = api.GetSocketTab();
        if (tab == null || IsUnityNull(tab))
        {
            return null;
        }

        return tab.GetComponent<Button>();
    }

    internal static void NormalizeJewelcraftingSocketTabForPrimaryCraftingTab(InventoryGui? gui)
    {
        if (!HasJewelcraftingActive ||
            IsDedicatedServer ||
            gui == null ||
            !IsJewelcraftingGemcutterStationActive() ||
            !IsCraftingPrimaryTabSelected(gui) ||
            !TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api) ||
            api == null)
        {
            return;
        }

        Button? button = GetJewelcraftingSocketTabButton(api);
        if (button != null && !IsUnityNull(button) && !button.interactable)
        {
            button.interactable = true;
        }
    }

    private static bool IsCraftingPrimaryTabSelected(InventoryGui? gui) =>
        IsCraftingCraftTabSelected(gui) || IsCraftingUpgradeTabSelected(gui);

    private static bool IsCraftingCraftTabSelected(InventoryGui? gui) =>
        gui?.m_tabCraft != null &&
        !IsUnityNull(gui.m_tabCraft) &&
        !gui.m_tabCraft.interactable;

    private static bool IsCraftingUpgradeTabSelected(InventoryGui? gui) =>
        gui?.m_tabUpgrade != null &&
        !IsUnityNull(gui.m_tabUpgrade) &&
        !gui.m_tabUpgrade.interactable;

    private static bool IsJewelcraftingSocketTabButton(Button button)
    {
        if (!HasJewelcraftingActive ||
            IsDedicatedServer ||
            button == null ||
            IsUnityNull(button) ||
            !TryGetJewelcraftingCraftingSocketUiApi(out JewelcraftingCraftingSocketUiApi? api) ||
            api == null)
        {
            return false;
        }

        Button? socketButton = GetJewelcraftingSocketTabButton(api);
        return socketButton != null && !IsUnityNull(socketButton) && button == socketButton;
    }

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
