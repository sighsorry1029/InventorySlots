using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if INVENTORY_SLOTS
using ButtonPlugin = InventorySlots.InventorySlotsPlugin;
namespace InventorySlots;
#else
using ButtonPlugin = InventoryActions.InventoryActionsPlugin;
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    private static InventoryGui? _controllerButtonLayoutOwner;
    private static int _controllerButtonLayoutRows;
    private static readonly Button?[] ControllerInventoryButtons = new Button?[3];
    private static readonly Button?[] ControllerSortButtons = new Button?[2];
    private static RectTransform? _controllerButtonOutline;
    private static TMP_Text? _controllerButtonHelp;
    private static TMP_Text? _controllerGridHelp;
    private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> ControllerGridElements =
        AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");
    private static readonly Action<InventoryGui, UIGroupHandler, bool> SetControllerInventoryGroup =
        AccessTools.MethodDelegate<Action<InventoryGui, UIGroupHandler, bool>>(
            AccessTools.DeclaredMethod(typeof(InventoryGui), "SetActiveGroup", new[] { typeof(UIGroupHandler), typeof(bool) }));

    private static void SetControllerButtonLayoutOwner(InventoryGui gui)
    {
        if (_controllerButtonLayoutOwner == gui) return;
        ResetInventoryButtonNavigation();
        Array.Clear(ControllerInventoryButtons, 0, ControllerInventoryButtons.Length);
        Array.Clear(ControllerSortButtons, 0, ControllerSortButtons.Length);
        if (_controllerButtonOutline != null) Object.Destroy(_controllerButtonOutline.gameObject);
        if (_controllerButtonHelp != null) Object.Destroy(_controllerButtonHelp.gameObject);
        if (_controllerGridHelp != null) Object.Destroy(_controllerGridHelp.gameObject);
        _controllerButtonOutline = null;
        _controllerButtonHelp = null;
        _controllerGridHelp = null;
        _controllerButtonLayoutOwner = gui;
        _controllerButtonLayoutRows = 0;
    }

    private static void RegisterControllerInventoryButton(InventoryGui gui, InventorySlideButton kind, Button button)
    {
        SetControllerButtonLayoutOwner(gui);
        ControllerInventoryButtons[(int)kind] = button;
        // The shared dispatcher owns focus navigation and submit. Mouse clicks
        // keep using this Button's original listener and protection checks.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static void RegisterControllerSortButton(InventoryGui gui, bool container, Button? button)
    {
        SetControllerButtonLayoutOwner(gui);
        ControllerSortButtons[container ? 1 : 0] = button;
        if (button != null) button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static bool IsControllerSortButtonVisible(bool container)
    {
        Button? button = ControllerSortButtons[container ? 1 : 0];
        return button != null && button.gameObject.activeInHierarchy;
    }

    private static void ActivateControllerSortButton(InventoryGui gui, bool container)
    {
        Button? button = ControllerSortButtons[container ? 1 : 0];
        if (InventoryControllerAccess.DragObject(gui) == null && IsControllerSortButtonVisible(container) &&
            button!.IsInteractable()) button.onClick.Invoke();
    }

    private static void SetControllerInventoryButtonRows(InventoryGui gui, int rows)
    {
        SetControllerButtonLayoutOwner(gui);
        _controllerButtonLayoutRows = rows;
    }

    private static int GetControllerInventoryButtonRows(InventoryGui gui) =>
        _controllerButtonLayoutOwner == gui ? _controllerButtonLayoutRows : 0;

    private static bool IsControllerInventoryButtonVisible(InventorySlideButton kind)
    {
        Button? button = ControllerInventoryButtons[(int)kind];
        bool enabled = kind == InventorySlideButton.Trash ? IsInventoryTrashButtonEnabled() : IsItemRuleButtonEnabled(kind == InventorySlideButton.Restock);
        return enabled && button != null && button.gameObject.activeInHierarchy;
    }

    private static bool CanActivateControllerInventoryButton(InventorySlideButton kind) =>
        IsControllerInventoryButtonVisible(kind) && ControllerInventoryButtons[(int)kind]!.IsInteractable();

    private static bool HasControllerContainer(InventoryGui gui) => gui.IsContainerOpen() && gui.ContainerGrid != null &&
        gui.ContainerGrid.gameObject.activeInHierarchy && gui.ContainerGrid.GetInventory() != null;

    private static GameObject? GetControllerTrashDialog()
    {
#if INVENTORY_SLOTS
        return _inventoryTrashConfirmDialog;
#else
        return Runtime.TrashConfirmDialog;
#endif
    }

    private static void SelectControllerTrashConfirmation(bool accept)
    {
        SplitDialog? dialog = GetControllerTrashDialog()?.GetComponent<SplitDialog>();
        if (dialog != null) (accept ? TrashSplitOkButton(dialog) : TrashSplitCancelButton(dialog)).Select();
    }

    private static bool IsControllerTrashAcceptOnRight()
    {
        SplitDialog? dialog = GetControllerTrashDialog()?.GetComponent<SplitDialog>();
        return dialog != null && TrashSplitOkButton(dialog).transform.position.x > TrashSplitCancelButton(dialog).transform.position.x;
    }

    private static void FocusControllerInventoryButtonRow(InventoryGui gui)
    {
        SetControllerInventoryGroup(gui, gui.m_playerGrid.m_uiGroup, false);
        // Keep a valid native selection: UpdateGui indexes this coordinate even
        // when custom navigation has reserved the grid's gamepad input.
        RestoreControllerPlayerCell(gui, _controllerButtonReturnCell);
    }

    private static Vector2i FindControllerPlayerCell(InventoryGui gui, Vector2i desired)
    {
        InventoryGrid grid = gui.m_playerGrid;
        Inventory? inventory = grid.GetInventory();
        if (inventory == null) return desired;
        int width = inventory.GetWidth();
        List<InventoryElement> elements = ControllerGridElements(grid);
        int rows = Math.Min(GetControllerInventoryButtonRows(gui), inventory.GetHeight());
        int x = Mathf.Clamp(desired.x, 0, width - 1);
        // Recovery rows may contain only a few visible occupied cells. Never
        // restore selection to a hidden/locked cell or a separate equipment row.
        for (int y = Mathf.Clamp(desired.y, 0, Math.Max(0, rows - 1)); y >= 0; y--)
            for (int distance = 0; distance < width; distance++)
            {
                int left = x - distance, right = x + distance;
                if (Visible(left, y)) return new Vector2i(left, y);
                if (distance > 0 && Visible(right, y)) return new Vector2i(right, y);
            }
        return new Vector2i(x, 0);

        bool Visible(int column, int row)
        {
            int index = row * width + column;
            return column >= 0 && column < width && elements != null && index < elements.Count &&
                elements[index] != null && elements[index].gameObject.activeInHierarchy;
        }
    }

    private static void RestoreControllerPlayerCell(InventoryGui gui, Vector2i cell) =>
        gui.m_playerGrid.SetGamepadSelection(FindControllerPlayerCell(gui, cell));

    private static void FocusControllerInventoryGrid(InventoryGui gui, Vector2i cell, bool toContainer)
    {
        InventoryGrid grid = toContainer ? gui.ContainerGrid : gui.m_playerGrid;
        if (toContainer)
        {
            int width = grid.GetInventory().GetWidth();
            int offset = Mathf.CeilToInt((gui.m_playerGrid.GetInventory().GetWidth() - width) * 0.5f);
            grid.SetGamepadSelection(new Vector2i(Mathf.Clamp(cell.x - offset, 0, width - 1), 0));
        }
        else RestoreControllerPlayerCell(gui, cell);
        SetControllerInventoryGroup(gui, grid.m_uiGroup, false);
        // UIGroup activation completes later in the frame; the native grid will
        // keep these coordinates. Select the cell now where already available.
        RectTransform? selected = grid.GetGamepadSelectedElement();
        if (selected != null) selected.GetComponent<Selectable>()?.Select();
    }

    private static void RestoreControllerSortCell(InventoryGui gui, bool container, Vector2i cell)
    {
        InventoryGrid? grid = container ? gui.ContainerGrid : gui.m_playerGrid;
        Inventory? inventory = grid != null ? grid.GetInventory() : null;
        if (inventory == null || inventory.GetWidth() <= 0 || inventory.GetHeight() <= 0) return;
        if (!container) RestoreControllerPlayerCell(gui, cell);
        else grid!.SetGamepadSelection(new Vector2i(Mathf.Clamp(cell.x, 0, inventory.GetWidth() - 1),
            Mathf.Clamp(cell.y, 0, inventory.GetHeight() - 1)));
    }

    private static void FocusControllerSortButton(InventoryGui gui, bool container, Vector2i cell)
    {
        InventoryGrid grid = container ? gui.ContainerGrid : gui.m_playerGrid;
        SetControllerInventoryGroup(gui, grid.m_uiGroup, false);
        RestoreControllerSortCell(gui, container, cell);
    }

    private static void FocusControllerSortGrid(InventoryGui gui, bool container, Vector2i cell)
    {
        FocusControllerSortButton(gui, container, cell);
        InventoryGrid grid = container ? gui.ContainerGrid : gui.m_playerGrid;
        RectTransform? selected = grid.GetGamepadSelectedElement();
        if (selected != null) selected.GetComponent<Selectable>()?.Select();
    }

    private static void ShowControllerButtonFocus(InventorySlideButton? kind)
    {
        Button? button = kind.HasValue && IsControllerInventoryButtonVisible(kind.Value)
            ? ControllerInventoryButtons[(int)kind.Value] : null;
        if (button == null)
        {
            ShowControllerFocusedButton(null, string.Empty);
            return;
        }
        bool dragging = _controllerButtonLayoutOwner != null && InventoryControllerAccess.DragObject(_controllerButtonLayoutOwner) != null;
        string action = ControllerButtonText(dragging ? "register" : "open", dragging ? "Register" : "Open");
        if (kind == InventorySlideButton.Trash) action = ControllerButtonText("open", "Open");
        string prefix = "$" + ModName.ToLowerInvariant();
        string name = kind == InventorySlideButton.Restock ? LocalizeUi(prefix + "_rules_restock_title", "Restock targets") :
            kind == InventorySlideButton.Exclude ? LocalizeUi(prefix + "_rules_exclude_title", "Auto pickup exclusions") :
            ControllerButtonText("trash", "Trash");
        ShowControllerFocusedButton(button, name + "  ·  " + ControllerButtonHelpText(action));
    }

    private static void ShowControllerSortButtonFocus(bool container) =>
        ShowControllerFocusedButton(IsControllerSortButtonVisible(container) ? ControllerSortButtons[container ? 1 : 0] : null,
            ControllerButtonHelpText(ControllerButtonText("sort", "Sort")));

    private static string ControllerButtonText(string key, string fallback) =>
        LocalizeUi("$" + ModName.ToLowerInvariant() + "_controller_" + key, fallback);

    private static string ControllerButtonHelpText(string action) =>
        "[" + GetInventoryControllerActionDisplay("JoyButtonA") + "] " + action + "    [" +
        GetInventoryControllerActionDisplay("JoyButtonB") + "] " + ControllerButtonText("back", "Back");

    private static void ShowControllerFocusedButton(Button? button, string help)
    {
        if (button == null)
        {
            if (_controllerButtonOutline != null) _controllerButtonOutline.gameObject.SetActive(false);
            if (_controllerButtonHelp != null) _controllerButtonHelp.gameObject.SetActive(false);
            // A stale Unity selection could submit the previous button again
            // after custom navigation returns control to the grid or mouse.
            EventSystem? events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null)
                foreach (Button? previous in ControllerInventoryButtons)
                    if (previous != null && events.currentSelectedGameObject == previous.gameObject)
                    {
                        events.SetSelectedGameObject(null);
                        break;
                    }
            if (events != null && events.currentSelectedGameObject != null)
                foreach (Button? previous in ControllerSortButtons)
                    if (previous != null && events.currentSelectedGameObject == previous.gameObject)
                    {
                        events.SetSelectedGameObject(null);
                        break;
                    }
            return;
        }
        HideControllerGridHelp();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != button.gameObject)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        if (_controllerButtonOutline == null)
        {
            _controllerButtonOutline = new GameObject(ModName + "_ControllerButtonFocus", typeof(RectTransform)).GetComponent<RectTransform>();
            for (int edge = 0; edge < 4; edge++)
            {
                Image image = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(_controllerButtonOutline, false);
                image.color = new Color(1f, 0.8f, 0.36f, 1f);
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                bool horizontal = edge < 2;
                rect.anchorMin = horizontal ? new Vector2(0, edge) : new Vector2(edge - 2, 0);
                rect.anchorMax = horizontal ? new Vector2(1, edge) : new Vector2(edge - 2, 1);
                rect.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
                rect.anchoredPosition = Vector2.zero;
            }
        }
        _controllerButtonOutline.SetParent(button.transform, false);
        _controllerButtonOutline.anchorMin = Vector2.zero;
        _controllerButtonOutline.anchorMax = Vector2.one;
        _controllerButtonOutline.offsetMin = new Vector2(2, 2);
        _controllerButtonOutline.offsetMax = new Vector2(-2, -2);
        _controllerButtonOutline.gameObject.SetActive(true);
        if (_controllerButtonHelp == null && _controllerButtonLayoutOwner != null)
        {
            _controllerButtonHelp = CreateControllerHelp(_controllerButtonLayoutOwner, "ControllerButtonHelp");
            if (_controllerButtonHelp != null)
            {
                // Stay outside the button's slide mask, but follow the real
                // button instead of the player's fixed-size panel rectangle.
                _controllerButtonHelp.transform.SetParent(_controllerButtonLayoutOwner.m_player.parent, false);
                _controllerButtonHelp.gameObject.AddComponent<ControllerButtonHelpAnchor>();
            }
        }
        if (_controllerButtonHelp != null)
        {
            _controllerButtonHelp.text = help;
            _controllerButtonHelp.gameObject.SetActive(true);
            _controllerButtonHelp.GetComponent<ControllerButtonHelpAnchor>().Follow((RectTransform)button.transform);
        }
    }

    // The normal-grid hint has its own label: clearing external-button focus
    // must not erase a hint installed by the dispatcher earlier this frame.
    internal static void SetControllerGridHelp(InventoryGui gui, string text)
    {
        SetControllerButtonLayoutOwner(gui);
        if (IsInventoryButtonNavigationActive() || string.IsNullOrEmpty(text))
        {
            HideControllerGridHelp();
            return;
        }
        if (_controllerGridHelp == null) _controllerGridHelp = CreateControllerHelp(gui, "ControllerGridHelp");
        if (_controllerGridHelp == null) return;
        _controllerGridHelp.text = text;
        _controllerGridHelp.gameObject.SetActive(true);
    }

    internal static void HideControllerGridHelp()
    {
        if (_controllerGridHelp != null) _controllerGridHelp.gameObject.SetActive(false);
    }

    private static TMP_Text? CreateControllerHelp(InventoryGui gui, string name)
    {
        // Use the game's existing font even when TMP has no default font.
        TMP_Text? template = GetInventoryButtonCaptionTexts<TMP_Text>(gui.m_takeAllButton).FirstOrDefault();
        if (template == null) return null;
        TMP_Text label = Object.Instantiate(template, gui.m_player, false);
        label.name = ModName + "_" + name;
        label.raycastTarget = false;
        label.fontSize = 15;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.color = new Color(1f, 0.94f, 0.8f, 1f);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(500, 25);
        rect.anchoredPosition = new Vector2(-12, 2);
        return label;
    }

    internal static RectTransform? GetControllerInventoryButtonElement(InventoryGui gui)
    {
        if (_controllerButtonOwner != gui || !IsInventoryButtonNavigationActive() || IsItemRuleInputBlocked() ||
            GetControllerTrashDialog() != null) return null;
        if (_controllerSortFocus.HasValue)
            return IsControllerSortButtonVisible(_controllerSortFocus.Value)
                ? ControllerSortButtons[_controllerSortFocus.Value ? 1 : 0]!.transform as RectTransform : null;
        return _controllerButtonFocus.HasValue && IsControllerInventoryButtonVisible(_controllerButtonFocus.Value)
            ? ControllerInventoryButtons[(int)_controllerButtonFocus.Value]!.transform as RectTransform : null;
    }

    internal static void HideControllerButtonGridSelection(InventoryGrid grid)
    {
        if (!IsInventoryButtonNavigationActive()) return;
        InventoryGrid focusedGrid = _controllerSortFocus == true ? _controllerButtonOwner!.ContainerGrid : _controllerButtonOwner!.m_playerGrid;
        if (focusedGrid != grid) return;
        RectTransform? selected = grid.GetGamepadSelectedElement();
        InventoryElement? element = selected != null ? selected.GetComponent<InventoryElement>() : null;
        if (element != null && element.m_selected != null) element.m_selected.SetActive(false);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
internal static class InventoryControllerButtonGridSelectionPatch
{
    private static void Postfix(InventoryGrid __instance) => ButtonPlugin.HideControllerButtonGridSelection(__instance);
}

[HarmonyPatch(typeof(InventoryGui), "GetSelectedGamepadElement")]
internal static class InventoryControllerSelectedButtonPatch
{
    private static void Postfix(InventoryGui __instance, ref RectTransform __result)
    {
        RectTransform? button = ButtonPlugin.GetControllerInventoryButtonElement(__instance);
        if (button != null) __result = button;
    }
}

[HarmonyPatch(typeof(InventoryGui), "Hide")]
internal static class InventoryControllerButtonHidePatch
{
    private static void Postfix(InventoryGui __instance)
    {
        ButtonPlugin.ResetControllerItemMenu(__instance);
        ButtonPlugin.HideControllerGridHelp();
        ButtonPlugin.ResetInventoryButtonNavigation(__instance);
    }
}
