using System;
using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    // Only the adapter contract is stubbed. The navigation state machine is
    // compiled directly from InventoryButtonNavigation.cs.
    private enum InventorySlideButton { Trash, Restock, Exclude }
    [Flags]
    public enum TestButtonMask { None = 0, Restock = 1, Exclude = 2, Trash = 4, All = 7 }
    public static TestButtonMask TestVisibleButtons, TestInteractableButtons;
    public static int TestButtonRows, TestButtonRowFocuses, TestGridFocuses, TestCellRestores;
    public static bool TestContainerOpen, TestLastGridWasContainer, TestPinRulesOnOpen;
    public static Vector2i TestLastGridReturnCell;
    public static string? TestShownButton;
    public static int TestTrashOpens, TestTrashCancels, TestTrashConfirms;
    public static bool TestTrashAcceptSelected;
    public static bool TestTrashAcceptOnRight;
    public static string? TestFocusedButton => _controllerButtonFocus?.ToString();
    public static bool TestTrashDialogOpen => GetControllerTrashDialog()?.activeInHierarchy == true;
    public static bool TestButtonIsFocused(string kind) => IsControllerInventoryButtonFocused(Enum.Parse<InventorySlideButton>(kind));

    private static void TestResetButtonAdapter()
    {
        TestVisibleButtons = TestInteractableButtons = TestButtonMask.All;
        TestButtonRows = TestButtonRowFocuses = TestGridFocuses = TestCellRestores = 0;
        TestContainerOpen = TestLastGridWasContainer = TestPinRulesOnOpen = false;
        TestShownButton = null;
        TestLastGridReturnCell = default;
        TestTrashOpens = TestTrashCancels = TestTrashConfirms = 0;
        TestTrashAcceptSelected = false;
        TestTrashAcceptOnRight = true;
    }

    private static TestButtonMask ButtonMask(InventorySlideButton kind) => kind switch
    {
        InventorySlideButton.Restock => TestButtonMask.Restock,
        InventorySlideButton.Exclude => TestButtonMask.Exclude,
        _ => TestButtonMask.Trash
    };
    private static bool IsControllerInventoryButtonVisible(InventorySlideButton kind) =>
        (TestVisibleButtons & ButtonMask(kind)) != 0;
    private static bool CanActivateControllerInventoryButton(InventorySlideButton kind) =>
        IsControllerInventoryButtonVisible(kind) && (TestInteractableButtons & ButtonMask(kind)) != 0;
    private static int GetControllerInventoryButtonRows(InventoryGui gui) => TestButtonRows;
    private static bool HasControllerContainer(InventoryGui gui) => TestContainerOpen;
    private static void ShowControllerButtonFocus(InventorySlideButton? kind) => TestShownButton = kind?.ToString();
    private static void FocusControllerInventoryButtonRow(InventoryGui gui)
    {
        TestButtonRowFocuses++;
        gui.m_playerGrid.m_uiGroup.IsActive = true;
        gui.ContainerGrid.m_uiGroup.IsActive = false;
        RestoreControllerPlayerCell(gui, _controllerButtonReturnCell);
    }
    private static void RestoreControllerPlayerCell(InventoryGui gui, Vector2i cell)
    {
        TestCellRestores++;
        gui.m_playerGrid.m_selected = cell;
    }
    private static void FocusControllerInventoryGrid(InventoryGui gui, Vector2i cell, bool toContainer)
    {
        TestGridFocuses++;
        TestLastGridWasContainer = toContainer;
        TestLastGridReturnCell = cell;
        gui.m_playerGrid.m_uiGroup.IsActive = !toContainer;
        gui.ContainerGrid.m_uiGroup.IsActive = toContainer;
        if (toContainer) gui.ContainerGrid.m_selected = new Vector2i(cell.x, 0);
        else gui.m_playerGrid.m_selected = cell;
    }
    private static GameObject? GetControllerTrashDialog()
    {
#if INVENTORY_SLOTS
        return _inventoryTrashConfirmDialog;
#else
        return Runtime.TrashConfirmDialog;
#endif
    }
    private static void SelectControllerTrashConfirmation(bool accept) => TestTrashAcceptSelected = accept;
    private static bool IsControllerTrashAcceptOnRight() => TestTrashAcceptOnRight;
    private static void TryClickInventoryTrashPanel()
    {
        TestTrashOpens++;
        TestSetTrashDialog();
    }
    private static void CloseInventoryTrashConfirmDialog()
    {
        TestTrashCancels++;
        TestClearTrashDialog();
    }
    private static void ConfirmInventoryTrashDelete()
    {
        TestTrashConfirms++;
        TestClearTrashDialog();
    }
    private static void TestClearTrashDialog()
    {
        GetControllerTrashDialog()?.SetActive(false);
#if INVENTORY_SLOTS
        _inventoryTrashConfirmDialog = null;
#else
        Runtime.TrashConfirmDialog = null;
#endif
    }
    public static void TestCloseRules()
    {
        TestRulesPinned = false;
        TestRulesClosedFrame = Time.frameCount;
    }
}
