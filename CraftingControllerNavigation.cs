using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static int _craftingControllerInputFrame = -1;

    private static bool OwnsCraftingRecipeGamepadInput(InventoryGui? gui) =>
        gui != null && _craftingRedesignApplied && ShouldShowCraftingPanelRedesign(gui) &&
        _craftingRecipeGrid != null && _craftingRecipeGrid.parent == gui.m_crafting &&
        _craftingRecipeGrid.gameObject.activeInHierarchy;

    // Also queried before InventoryGui.Update by scroll consumers. Reserve the
    // D-pad for recipe selection without consuming held keys or the right stick.
    private static bool IsCraftingRecipeGamepadFocused(InventoryGui? gui) =>
        OwnsCraftingRecipeGamepadInput(gui) && InventoryGui.IsVisible() &&
        !IsInventoryPanelClosing(gui!) && gui!.m_inventoryGroup != null &&
        gui.m_inventoryGroup.IsActive && gui.ActiveGroup == 3 && !ZInput.IsTouchActive();

    internal static bool HandleCraftingRecipeGamepadInput(InventoryGui gui)
    {
        if (!OwnsCraftingRecipeGamepadInput(gui)) return false;
        // Once we own the view, blocked input must not fall back to native order.
        if (!IsCraftingRecipeGamepadFocused(gui) || !CanUseInventoryControllerUi(gui) ||
            IsInventoryControllerNavigationReserved() ||
            _craftingControllerInputFrame == Time.frameCount) return true;

        int vertical = (ZInput.GetButtonDown("JoyDPadDown") || ZInput.GetButtonDown("JoyLStickDown") ? 1 : 0) -
                       (ZInput.GetButtonDown("JoyDPadUp") || ZInput.GetButtonDown("JoyLStickUp") ? 1 : 0);
        int horizontal = _craftingListViewActive ? 0 :
            (ZInput.GetButtonDown("JoyDPadRight") || ZInput.GetButtonDown("JoyLStickRight") ? 1 : 0) -
            (ZInput.GetButtonDown("JoyDPadLeft") || ZInput.GetButtonDown("JoyLStickLeft") ? 1 : 0);
        if (vertical == 0 && horizontal == 0) return true;
        _craftingControllerInputFrame = Time.frameCount;

        UpdateCraftingRecipeView(gui);
        int selected = FindCraftingRecipeViewIndex(GetSelectedCraftingRecipeIndexSafe(gui, acceptOneLevelHigher: false));
        int next = CraftingViewCore.MoveSelection(_craftingListViewActive, selected,
            CraftingRecipes.View.Count, GetCraftingRecipeGridDimension(), horizontal, vertical);
        if (next < 0) return true;
        if (next != selected)
            SetCraftingRecipeWithStoredVariant(gui, CraftingRecipes.View[next].OriginalIndex, center: false);

        // Native UpdateRecipe renders after this input hook. Only reveal here;
        // running the full redesign would process wheel/queue input twice.
        CraftingController.ClearHoveredRecipeAndRequestMouseSync();
        _craftingListRevealedSelection = int.MinValue;
        SyncCraftingRecipePageToSelected(gui);
        return true;
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipeGamepadInput")]
internal static class InventoryGuiCraftingRecipeGamepadPatch
{
    private static bool Prefix(InventoryGui __instance) =>
        !InventorySlotsPlugin.HandleCraftingRecipeGamepadInput(__instance);
}
