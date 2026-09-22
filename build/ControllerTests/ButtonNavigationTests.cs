using System;
using UnityEngine;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

internal static partial class Program
{
    private static void ButtonFrame(params string[] actions)
    {
        Time.frameCount++;
        ZInput.Held.Clear();
        ZInput.Down.Clear();
        ZInput.Press(actions);
        Plugin.OnControllerInputUpdated();
        Prefix("InventoryControllerUpdatePatch", InventoryGui.instance);
    }

    private static InventoryGui PrepareButtonRow(int column = 6, int rows = 4)
    {
        InventoryGui gui = Plugin.TestReset();
        Plugin.TestButtonRows = rows;
        gui.m_playerGrid.m_selected = new Vector2i(column, rows - 1);
        return gui;
    }

    private static InventoryGui EnterButtonRow(int column = 6)
    {
        InventoryGui gui = PrepareButtonRow(column);
        ButtonFrame("JoyDPadDown");
        Check(Plugin.IsInventoryButtonNavigationActive(), "Down enters button row from final displayed row");
        return gui;
    }

    private static bool CloseAliasesConsumed => !ZInput.GetButtonDown("Inventory") && !ZInput.GetButton("Inventory") &&
        !ZInput.GetButtonDown("Use") && !ZInput.GetButton("Use") && !ZInput.GetButtonDown("JoyUse") && !ZInput.GetButton("JoyUse");

    private static void CheckOnlyButtonFocused(string? expected, string reason)
    {
        foreach (string kind in new[] { "Restock", "Exclude", "Trash" })
            Check(Plugin.TestButtonIsFocused(kind) == (kind == expected), reason + ": " + kind);
    }

    private static void CheckButtonNavigation()
    {
        for (int column = 0; column < 8; column++)
        {
            InventoryGui gui = PrepareButtonRow(column);
            ButtonFrame("JoyDPadDown");
            string expected = column < 6 ? "Restock" : column == 6 ? "Exclude" : "Trash";
            Check(Plugin.TestFocusedButton == expected && Plugin.TestShownButton == expected,
                "Final-row column selects nearest visible button " + column);
            CheckOnlyButtonFocused(expected, "Only the selected row button requests focus expansion at column " + column);
            Check(gui.m_playerGrid.m_selected == new Vector2i(column, 3) && Plugin.TestButtonRowFocuses == 1,
                "Button row keeps valid item-cell coordinates for native UI updates " + column);
            Check(!Prefix("InventoryControllerGridInputPatch") && !Prefix("InventoryControllerEventNavigationPatch"),
                "Button row reserves grid and direct UI navigation " + column);
        }

        InventoryGui current = PrepareButtonRow(6);
        Plugin.TestVisibleButtons = Plugin.TestButtonMask.Restock | Plugin.TestButtonMask.Trash;
        ButtonFrame("JoyLStickDown");
        Check(Plugin.TestFocusedButton == "Restock", "Visible-button spacing determines the nearest target");
        ButtonFrame("JoyLStickRight");
        Check(Plugin.TestFocusedButton == "Trash", "Right skips disabled Exclude");
        CheckOnlyButtonFocused("Trash", "Moving right transfers focus expansion to Trash");
        ButtonFrame("JoyDPadRight");
        Check(Plugin.TestFocusedButton == "Trash", "Right stops at the visible row boundary");
        ButtonFrame("JoyDPadLeft");
        Check(Plugin.TestFocusedButton == "Restock", "Left skips disabled Exclude");
        CheckOnlyButtonFocused("Restock", "Moving left transfers focus expansion to Restock");
        ButtonFrame("JoyLStickLeft");
        Check(Plugin.TestFocusedButton == "Restock", "Left stops at the visible row boundary");

        current = PrepareButtonRow(7);
        Plugin.TestVisibleButtons = Plugin.TestButtonMask.None;
        ButtonFrame("JoyDPadDown");
        Check(!Plugin.IsInventoryButtonNavigationActive() && !Plugin.IsInventoryControllerInputReserved() &&
            Prefix("InventoryControllerGridInputPatch") && current.m_playerGrid.m_selected == new Vector2i(7, 3),
            "No visible buttons leaves native grid input available");
        CheckOnlyButtonFocused(null, "Gamepad use without button-row entry does not focus-expand buttons");

        current = PrepareButtonRow(6);
        ZInput.Press("JoyDPadDown"); Time.frameCount++;
        Prefix("InventoryControllerEventNavigationPatch");
        Check(!Plugin.IsInventoryButtonNavigationActive() && Plugin.TestButtonRowFocuses == 0,
            "Previous-frame Down cannot enter the button row before the input tick");
        ZInput.Down.Clear(); Plugin.OnControllerInputUpdated();
        Check(!Plugin.IsInventoryButtonNavigationActive(), "Clearing a stale Down leaves grid focus unchanged");
        Time.frameCount++;
        Prefix("InventoryControllerEventNavigationPatch");
        ZInput.Down.Add("JoyDPadDown"); Plugin.OnControllerInputUpdated();
        Prefix("InventoryControllerUpdatePatch", current);
        Prefix("InventoryControllerGridInputPatch");
        Check(Plugin.IsInventoryButtonNavigationActive() && Plugin.TestButtonRowFocuses == 1,
            "Down refreshed after early navigation enters exactly once in the same frame");

        current = PrepareButtonRow(6);
        current.m_playerGrid.m_selected = new Vector2i(6, 2);
        ButtonFrame("JoyDPadDown");
        Check(!Plugin.IsInventoryButtonNavigationActive() && Prefix("InventoryControllerGridInputPatch"),
            "Second-last player row Down remains available for native cell movement");
        // Native UpdateGamepad moves to the last row before another patched
        // entry point polls the exact same input edge in this frame.
        current.m_playerGrid.m_selected = new Vector2i(6, 3);
        Prefix("InventoryControllerUpdatePatch", current);
        Prefix("InventoryControllerGridInputPatch");
        Check(!Plugin.IsInventoryButtonNavigationActive() && Plugin.TestButtonRowFocuses == 0,
            "Native arrival at the last row cannot reuse the same Down to enter buttons");
        ButtonFrame(); ButtonFrame("JoyDPadDown");
        Check(Plugin.TestFocusedButton == "Exclude" && Plugin.TestButtonRowFocuses == 1,
            "A new Down after reaching the last player row enters buttons");

        current = PrepareButtonRow(6);
        Plugin.TestContainerOpen = true;
        current.m_playerGrid.m_uiGroup.IsActive = false;
        current.ContainerGrid.m_uiGroup.IsActive = true;
        current.ContainerGrid.m_selected = new Vector2i(6, 1);
        ButtonFrame("JoyLStickUp");
        Check(!Plugin.IsInventoryButtonNavigationActive() && Prefix("InventoryControllerGridInputPatch"),
            "Container second row Up remains available for native cell movement");
        current.ContainerGrid.m_selected = new Vector2i(6, 0);
        Prefix("InventoryControllerUpdatePatch", current);
        Prefix("InventoryControllerGridInputPatch");
        Check(!Plugin.IsInventoryButtonNavigationActive() && Plugin.TestButtonRowFocuses == 0,
            "Native arrival at the container top cannot reuse the same Up to enter buttons");
        ButtonFrame(); ButtonFrame("JoyLStickUp");
        Check(Plugin.TestFocusedButton == "Exclude" && Plugin.TestButtonRowFocuses == 1,
            "A new Up after reaching the container top enters buttons");

        foreach (int row in new[] { 1, 3, 4, 5 })
        {
            current = PrepareButtonRow(6, rows: 3);
            current.m_playerGrid.Inventory!.Height = 6;
            current.m_playerGrid.m_selected = new Vector2i(6, row);
            ButtonFrame("JoyDPadDown");
            Check(!Plugin.IsInventoryButtonNavigationActive() && !Plugin.IsInventoryControllerInputReserved(),
                "Only the last displayed row is intercepted, not ordinary/special row " + row);
        }
        PrepareButtonRow(6, rows: 3);
        ButtonFrame("JoyLStickDown");
        Check(Plugin.IsInventoryButtonNavigationActive(), "Displayed row count determines the entry boundary");

        current = EnterButtonRow();
        Plugin.TestContainerOpen = true;
        ButtonFrame("JoyDPadDown");
        Check(Plugin.IsInventoryButtonNavigationActive() && Plugin.TestGridFocuses == 0,
            "Repeated held Down cannot skip the button row");
        Time.frameCount++; ZInput.Down.Clear(); Plugin.OnControllerInputUpdated();
        Check(Plugin.IsInventoryButtonNavigationActive() && Plugin.TestGridFocuses == 0,
            "Held vertical input without a new edge remains on the button row");
        ButtonFrame();
        ButtonFrame("JoyDPadDown");
        Check(!Plugin.IsInventoryButtonNavigationActive() && Plugin.TestLastGridWasContainer && Plugin.TestGridFocuses == 1 &&
            current.ContainerGrid.m_uiGroup.IsActive && Plugin.TestLastGridReturnCell == new Vector2i(6, 3),
            "Down after release requests the open container top using the return column");

        current = EnterButtonRow(5);
        ButtonFrame(); ButtonFrame("JoyLStickDown");
        Check(Plugin.TestFocusedButton == "Restock" && Plugin.TestGridFocuses == 0,
            "Down stays on buttons when no container is open");
        ButtonFrame(); ButtonFrame("JoyLStickUp");
        Check(!Plugin.IsInventoryButtonNavigationActive() && !Plugin.TestLastGridWasContainer &&
            current.m_playerGrid.m_selected == new Vector2i(5, 3), "Up restores the previous player cell");

        current = EnterButtonRow(7);
        ButtonFrame("JoyButtonB", "Inventory", "Use", "JoyUse");
        Check(!Plugin.IsInventoryButtonNavigationActive() && current.m_playerGrid.m_selected == new Vector2i(7, 3) &&
            !ZInput.GetButtonDown("JoyButtonB") && CloseAliasesConsumed && Plugin.IsInventoryControllerInputReserved(),
            "B restores the player cell and consumes inventory-close aliases");

        current = EnterButtonRow(6);
        ButtonFrame("JoyTabRight");
        Check(!Plugin.IsInventoryButtonNavigationActive() && current.m_playerGrid.m_selected == new Vector2i(6, 3) &&
            Prefix("InventoryControllerGridInputPatch") && ZInput.GetButtonDown("JoyTabRight"),
            "Shoulder navigation leaves the row and remains available to native groups");
        foreach (string shoulder in new[] { "JoyTabLeft", "JoyTabRight" })
        {
            current = EnterButtonRow(6);
            Time.frameCount++; ZInput.Down.Clear(); ZInput.Held.Clear();
            Check(!Prefix("InventoryControllerEventNavigationPatch") && Plugin.IsInventoryControllerInputReserved(),
                "Early navigation reserves active button row before shoulder update " + shoulder);
            ZInput.Press(shoulder); Plugin.OnControllerInputUpdated();
            Check(!Plugin.IsInventoryButtonNavigationActive() && !Plugin.IsInventoryControllerInputReserved() &&
                Prefix("InventoryControllerGuiInputPatch") && Prefix("InventoryControllerGridInputPatch") &&
                Prefix("InventoryControllerEventNavigationPatch") && ZInput.GetButtonDown(shoulder),
                "Late same-frame shoulder releases the early reservation for native group switching " + shoulder);
        }

        current = PrepareButtonRow();
        Plugin.TestContainerOpen = true;
        current.m_playerGrid.m_uiGroup.IsActive = false;
        current.ContainerGrid.m_uiGroup.IsActive = true;
        current.ContainerGrid.Inventory!.Width = 6;
        current.ContainerGrid.m_selected = new Vector2i(5, 0);
        ButtonFrame("JoyDPadUp");
        Check(Plugin.TestFocusedButton == "Exclude" && current.m_playerGrid.m_uiGroup.IsActive,
            "Container top Up enters the button aligned with its centered column");
        ButtonFrame(); ButtonFrame("JoyDPadUp");
        Check(current.m_playerGrid.m_selected == new Vector2i(6, 3) && !Plugin.IsInventoryButtonNavigationActive(),
            "Container entry remembers the corresponding player return cell");
        current = PrepareButtonRow();
        current.m_playerGrid.m_uiGroup.IsActive = false;
        current.ContainerGrid.m_uiGroup.IsActive = true;
        current.ContainerGrid.m_selected = new Vector2i(5, 1);
        ButtonFrame("JoyDPadUp");
        Check(!Plugin.IsInventoryButtonNavigationActive(), "Non-top container rows retain native Up navigation");

        PrepareButtonRow(5);
        Plugin.TestEnable(false); Plugin.TestSetInventoryModifier("Off");
        ButtonFrame("JoyDPadDown"); ButtonFrame("JoyButtonA");
        Check(Plugin.TestRuleOpens == 1 && Plugin.TestLastRestock,
            "Hotkeys Off preserves direct navigation and unmodified A activation");

        foreach (int column in new[] { 5, 6 })
        {
            current = PrepareButtonRow(column);
            Plugin.TestPinRulesOnOpen = true;
            GameObject heldItem = current.m_dragGo = new GameObject();
            ButtonFrame("JoyDPadDown");
            ButtonFrame("JoyButtonA", "Inventory", "Use", "JoyUse");
            Check(Plugin.TestRuleOpens == 1 && Plugin.TestLastRestock == (column == 5) &&
                ReferenceEquals(Plugin.TestLastRuleDragObject, heldItem) && ReferenceEquals(current.m_dragGo, heldItem),
                "Unmodified A routes held-item context to the selected rules button " + column);
            Check(Plugin.TestFavorites + Plugin.TestPlayerSorts + Plugin.TestContainerSorts + Plugin.TestTrashConfirms == 0 &&
                !ZInput.GetButtonDown("JoyButtonA") && CloseAliasesConsumed,
                "Rules A consumes submit/close aliases without inventory-action callbacks " + column);
            Check(Plugin.TestShownButton == null && Plugin.TestRulesPinned, "Rules editor temporarily owns the row input " + column);
            CheckOnlyButtonFocused(null, "Rules editor suspends button-row focus expansion " + column);
            ZInput.Press("JoyButtonA"); Dispatch();
            Check(Plugin.TestRuleOpens == 1, "Repeated same-frame submit cannot reopen rules " + column);
            Plugin.TestCloseRules(); ButtonFrame();
            Check(Plugin.TestShownButton == (column == 5 ? "Restock" : "Exclude") && Plugin.IsInventoryButtonNavigationActive(),
                "Closing rules restores the focused button " + column);
        }

        foreach (string face in new[] { "JoyButtonY", "JoyButtonB" })
        {
            current = EnterButtonRow(7);
            Plugin.TestPinRulesOnOpen = true;
            GameObject heldItem = current.m_dragGo = new GameObject();
            ButtonFrame("JoyRStick", face, "Inventory", "Use", "JoyUse");
            Check(Plugin.TestRuleOpens == 1 && Plugin.TestLastRestock == (face == "JoyButtonY") &&
                ReferenceEquals(Plugin.TestLastRuleDragObject, heldItem) && Plugin.TestTrashOpens == 0 &&
                ZInput.GetButton("JoyRStick") && !ZInput.GetButtonDown(face) && CloseAliasesConsumed,
                "Existing modifier chord remains available on the button row " + face);
            CheckOnlyButtonFocused(null, "Opening rules by chord suspends previous Trash focus expansion " + face);
        }

        EnterButtonRow(7); ButtonFrame("JoyButtonA");
        Check(Plugin.TestTrashOpens == 1 && Plugin.TestTrashDialogOpen && !Plugin.TestTrashAcceptSelected && Plugin.TestTrashConfirms == 0,
            "Trash A opens confirmation with Cancel selected and does not delete");
        ButtonFrame("JoyButtonA", "Inventory", "Use", "JoyUse");
        Check(!Plugin.TestTrashDialogOpen && Plugin.TestTrashCancels == 1 && Plugin.TestTrashConfirms == 0 && CloseAliasesConsumed,
            "Default confirmation A cancels without deletion");
        ButtonFrame();
        Check(Plugin.TestShownButton == "Trash", "Cancel returns focus to Trash");

        EnterButtonRow(7); ButtonFrame("JoyButtonA");
        ButtonFrame("JoyDPadRight");
        Check(Plugin.TestTrashAcceptSelected && Plugin.TestTrashConfirms == 0,
            "Right selects trash confirmation without deleting");
        ButtonFrame("JoyButtonA", "Inventory", "Use", "JoyUse");
        Check(Plugin.TestTrashConfirms == 1 && Plugin.TestTrashCancels == 0 && !Plugin.TestTrashDialogOpen && CloseAliasesConsumed,
            "A after selecting confirmation invokes deletion exactly once");
        ZInput.Press("JoyButtonA"); Dispatch();
        Check(Plugin.TestTrashConfirms == 1 && Plugin.TestTrashOpens == 1, "Repeated confirmation in one frame cannot delete or reopen twice");

        EnterButtonRow(7); Plugin.TestTrashAcceptOnRight = false;
        ButtonFrame("JoyButtonA"); ButtonFrame("JoyDPadLeft");
        Check(Plugin.TestTrashAcceptSelected && Plugin.TestTrashConfirms == 0,
            "Left selects Delete when the adapter reports Delete on the left");
        ButtonFrame("JoyButtonA", "Inventory", "Use", "JoyUse");
        Check(Plugin.TestTrashConfirms == 1 && Plugin.TestTrashCancels == 0 && !Plugin.TestTrashDialogOpen && CloseAliasesConsumed,
            "Left then A confirms a left-side Delete button exactly once");

        EnterButtonRow(7); Plugin.TestTrashAcceptOnRight = false;
        ButtonFrame("JoyButtonA"); ButtonFrame("JoyLStickLeft"); ButtonFrame("JoyLStickRight");
        Check(!Plugin.TestTrashAcceptSelected && Plugin.TestTrashConfirms == 0,
            "Right selects Cancel when the adapter reports Delete on the left");
        ButtonFrame("JoyButtonA", "Inventory", "Use", "JoyUse");
        Check(Plugin.TestTrashConfirms == 0 && Plugin.TestTrashCancels == 1 && !Plugin.TestTrashDialogOpen && CloseAliasesConsumed,
            "Right then A cancels a dialog with Delete on the left");

        EnterButtonRow(7); ButtonFrame("JoyButtonA"); ButtonFrame("JoyLStickRight");
        ButtonFrame("JoyButtonB", "Inventory", "Use", "JoyUse");
        Check(Plugin.TestTrashConfirms == 0 && Plugin.TestTrashCancels == 1 && !Plugin.TestTrashDialogOpen &&
            !ZInput.GetButtonDown("JoyButtonB") && CloseAliasesConsumed && Plugin.IsInventoryControllerInputReserved(),
            "Confirmation B cancels even with Delete selected and consumes inventory close");

        EnterButtonRow(7);
        Plugin.TestInteractableButtons &= ~Plugin.TestButtonMask.Trash;
        ButtonFrame("JoyButtonA");
        Check(Plugin.TestTrashOpens == 0 && Plugin.TestTrashConfirms == 0 && !Plugin.TestTrashDialogOpen,
            "Non-interactable Trash cannot bypass existing protection checks");

        EnterButtonRow(6);
        Plugin.TestBlocked = true;
        Dispatch();
        Check(Plugin.TestShownButton == null && NoActions,
            "External modal hides button focus even after an action in the same frame");
        ButtonFrame("JoyButtonA");
        Check(Plugin.TestRuleOpens == 0 && ZInput.GetButtonDown("JoyButtonA") && Plugin.TestShownButton == null,
            "External modal owns submit while the button row waits");
        Plugin.TestBlocked = false; ButtonFrame();
        Check(Plugin.TestShownButton == "Exclude", "Closing an external modal restores button focus");

        EnterButtonRow(7); ButtonFrame("JoyButtonA"); ButtonFrame("JoyDPadRight");
        Plugin.TestBlocked = true;
        ButtonFrame("JoyButtonA");
        Check(Plugin.TestTrashDialogOpen && Plugin.TestTrashConfirms == 0 && ZInput.GetButtonDown("JoyButtonA"),
            "External modal blocks trash confirmation before the trash input handler");
        Plugin.TestBlocked = false; ButtonFrame(); ButtonFrame("JoyButtonB");
        Check(!Plugin.TestTrashDialogOpen && Plugin.TestTrashCancels == 1 && Plugin.TestTrashConfirms == 0,
            "Trash can still cancel after the external modal closes");

        Action[] resetCauses =
        {
            () => Plugin.TestPluginActive(false),
            () => InventoryGui.Visible = false,
            () => ZInput.Exclusive = false,
            () => InventoryGui.instance.m_playerGrid.m_uiGroup.IsActive = false
        };
        for (int i = 0; i < resetCauses.Length; i++)
        {
            current = EnterButtonRow(6);
            resetCauses[i](); ButtonFrame();
            Check(Plugin.TestFocusedButton == null && Plugin.TestShownButton == null &&
                current.m_playerGrid.m_selected == new Vector2i(6, 3),
                "Inactive, hidden, mouse, or departed grid restores player selection " + i);
            CheckOnlyButtonFocused(null, "Leaving active inventory removes row focus expansion " + i);
        }

        current = EnterButtonRow(6);
        Plugin.TestVisibleButtons = Plugin.TestButtonMask.Trash;
        ButtonFrame();
        Check(Plugin.TestFocusedButton == "Trash", "Hiding the focused button chooses an available button");
        Plugin.TestVisibleButtons = Plugin.TestButtonMask.None;
        ButtonFrame();
        Check(Plugin.TestFocusedButton == null && current.m_playerGrid.m_selected == new Vector2i(6, 3),
            "Hiding every button restores a valid player cell");
    }
}
