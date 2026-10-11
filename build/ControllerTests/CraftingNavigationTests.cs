#if INVENTORY_SLOTS
using System;
using System.Linq;
using InventorySlots;
using UnityEngine;
using UnityEngine.EventSystems;
using Plugin = InventorySlots.InventorySlotsPlugin;

internal static partial class Program
{
    private static bool NavigateRecipe(params string[] actions)
    {
        ZInput.Down.Clear();
        ZInput.Held.Clear();
        // Native InventoryGui may run before this frame's ZInput.Update marker.
        // Navigation must not wait for a callback that this hook does not have.
        Time.frameCount++;
        ZInput.Press(actions);
        return Prefix("InventoryGuiCraftingRecipeGamepadPatch", InventoryGui.instance);
    }

    private static void CheckCraftingNavigation()
    {
        foreach (string source in new[] { "JoyDPad", "JoyLStick" })
        {
            Plugin.TestCraftingReset(true, 2, 0, 3, 1);
            Check(!NavigateRecipe(source + "Down") && Plugin.TestRecipeSelected == 0, source + " follows displayed order");
            Check(!Plugin.TestSelectedIndexAllowsUpgrade && !Plugin.TestCenterRecipe,
                "Navigation preserves exact upgrade item identity and avoids hidden native centering");
            int calls = Plugin.TestRecipeSelections;
            Check(!Prefix("InventoryGuiCraftingRecipeGamepadPatch", InventoryGui.instance) && Plugin.TestRecipeSelections == calls,
                "Repeated native entry in one frame cannot move twice");
            Check(ZInput.ResetCalls.Count == 0 && ZInput.GetButton(source + "Down"), "Direction remains held for native repeat");
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == 3, "Native first index is not clamped before view conversion");
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == 1, "Native last index is not clamped before view conversion");
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == 1, "List bottom stays selected");
            NavigateRecipe(source + "Up");
            Check(Plugin.TestRecipeSelected == 3, "List up follows display order");
            NavigateRecipe(source + "Left");
            Check(Plugin.TestRecipeSelected == 3, "List ignores horizontal selection");

            // D-pad and stick aliases/diagonals must never produce two moves.
            NavigateRecipe("JoyDPadUp", "JoyLStickUp");
            Check(Plugin.TestRecipeSelected == 0, "Duplicate aliases move once");
            NavigateRecipe("JoyDPadUp", "JoyLStickDown");
            Check(Plugin.TestRecipeSelected == 0, "Opposing vertical inputs cancel");

            Plugin.TestPendingRecipeView = new[] { 8, 3, 5 };
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == 8, "Filtered-out selection recovers to first visible recipe");
            Plugin.TestPendingRecipeView = new[] { 5, 8, 3 };
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == 3, "Input refreshes changed sort/filter before moving");
            Plugin.TestPendingRecipeView = Array.Empty<int>();
            calls = Plugin.TestRecipeSelections;
            Check(!NavigateRecipe(source + "Down") && Plugin.TestRecipeSelections == calls,
                "Empty search never selects a hidden native recipe");

            int[] view = Enumerable.Range(0, 11).Select(i => 50 - i * 3).ToArray();
            Plugin.TestCraftingReset(false, view);
            NavigateRecipe(source + "Up");
            Check(Plugin.TestRecipeSelected == view[0], "Grid top clamps");
            NavigateRecipe(source + "Left");
            Check(Plugin.TestRecipeSelected == view[0], "Grid row start clamps");
            NavigateRecipe(source + "Right");
            Check(Plugin.TestRecipeSelected == view[1], "Grid right moves one column");
            NavigateRecipe(source + "Right");
            NavigateRecipe(source + "Right");
            Check(Plugin.TestRecipeSelected == view[2], "Grid row end never wraps");
            NavigateRecipe(source + "Down", source + "Right");
            Check(Plugin.TestRecipeSelected == view[5], "Grid diagonal uses one vertical move");
            NavigateRecipe(source + "Down");
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == view[10] && Plugin.TestRecipePage == 1,
                "Grid short final row uses nearest cell and reveals next page");
            NavigateRecipe(source + "Down");
            Check(Plugin.TestRecipeSelected == view[10], "Grid final row stays selected");
            NavigateRecipe(source + "Up");
            Check(Plugin.TestRecipeSelected == view[7] && Plugin.TestRecipePage == 0, "Grid up returns in current column");
            Plugin.TestRecipeColumns = 2;
            NavigateRecipe(source + "Up");
            Check(Plugin.TestRecipeSelected == view[5] && Plugin.TestRecipePage == 1, "Grid live size controls row step and page");
        }

        Plugin.TestCraftingReset(true, Enumerable.Range(0, 20).ToArray());
        Plugin.TestRecipeSelected = 13;
        NavigateRecipe("JoyDPadDown");
        Check(Plugin.TestRecipeSelected == 14 && Plugin.TestRecipePage == 1, "List reveals next row without page-size jump");
        Plugin.TestRecipePage = 5;
        Plugin.TestRecipeSelected = 0;
        NavigateRecipe("JoyDPadUp");
        Check(Plugin.TestRecipePage == 0, "Even boundary input reveals a selection hidden by manual scrolling");

        foreach (Action<InventoryGui> block in new Action<InventoryGui>[]
        {
            _ => Plugin.TestClosing = true,
            _ => Plugin.TestBlocked = true,
            _ => Plugin.TestCanShow = false,
            _ => Player.m_localPlayer.Loading = true,
            _ => Player.m_localPlayer.Teleporting = true,
            _ => Plugin.TestRulesPinned = true,
            gui => gui.IsTextPanelOpen = true,
            gui => gui.m_variantDialog = new Component(),
            gui => gui.m_splitDialog = new Component(),
            gui => gui.ActiveGroup = 1,
            gui => gui.m_inventoryGroup.IsActive = false,
            _ => { var input = new GameObject(); input.Components[typeof(TMPro.TMP_InputField)] = new TMPro.TMP_InputField(); EventSystem.current!.currentSelectedGameObject = input; }
        })
        {
            InventoryGui gui = Plugin.TestCraftingReset(true, 9, 4);
            block(gui);
            Check(!NavigateRecipe("JoyDPadDown") && Plugin.TestRecipeSelections == 0,
                "Owned but gated UI never falls back to native selection: " + block.Method.Name);
        }
        Plugin.TestCraftingReset(true, 9, 4);
        ZInput.Press("JoyDPadDown");
        Plugin.TestReserveRecipeInput();
        Check(!Prefix("InventoryGuiCraftingRecipeGamepadPatch", InventoryGui.instance) && Plugin.TestRecipeSelections == 0,
            "Reserved guide/menu input cannot navigate recipes");

        Plugin.TestCraftingReset(true, 9, 4);
        Plugin.TestEnable(false);
        NavigateRecipe("JoyDPadDown");
        Check(Plugin.TestRecipeSelected == 4, "Direct recipe navigation does not require optional controller hotkeys");
        Check(Plugin.TestRecipeOwnsDPad, "Recipe focus reserves D-pad scroll source");
        InventoryGui.instance.ActiveGroup = 1;
        Check(!Plugin.TestRecipeOwnsDPad, "Other inventory groups retain D-pad scrolling");
        InventoryGui.instance.ActiveGroup = 3;
        Plugin.TestForeignCrafting = true;
        Check(!Plugin.TestRecipeOwnsDPad && NavigateRecipe("JoyDPadDown"), "Foreign tabs retain native input and D-pad scrolling");
        Plugin.TestForeignCrafting = false;
        Plugin.TestHideRecipeGrid();
        Check(NavigateRecipe("JoyDPadDown"), "Inactive custom view leaves vanilla navigation intact");
    }
}
#endif
