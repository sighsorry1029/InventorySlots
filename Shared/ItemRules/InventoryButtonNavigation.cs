using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    // This is a UI focus row, not an extra inventory row or an item destination.
    private static InventoryGui? _controllerButtonOwner;
    private static InventorySlideButton? _controllerButtonFocus;
    // Sort is outside the inventory, not part of the three sliding buttons.
    // false selects player sort; true selects container sort.
    private static bool? _controllerSortFocus;
    private static Vector2i _controllerButtonReturnCell;
    private static int _controllerButtonFrame = -1;
    private static bool _controllerButtonWaitForVerticalRelease;
    private static GameObject? _controllerButtonTrashDialog;
    private static bool _controllerButtonTrashAccept;
    private static readonly InventorySlideButton[] ControllerButtonOrder =
        { InventorySlideButton.Restock, InventorySlideButton.Exclude, InventorySlideButton.Trash };

    internal static bool IsInventoryButtonNavigationActive() => (_controllerButtonFocus.HasValue || _controllerSortFocus.HasValue) &&
        _controllerButtonOwner != null && InventoryGui.IsVisible() && IsInventoryControllerActive() && !IsControllerItemMenuOpen();

    private static bool IsControllerInventoryButtonFocused(InventorySlideButton kind) =>
        IsInventoryButtonNavigationActive() && !IsItemRuleInputBlocked() && _controllerButtonFocus == kind;

    private static bool ControllerDirectionDown(string direction) =>
        ZInput.GetButtonDown("JoyDPad" + direction) || ZInput.GetButtonDown("JoyLStick" + direction);

    private static bool ControllerVerticalHeld() => ZInput.GetButton("JoyDPadUp") || ZInput.GetButton("JoyDPadDown") ||
        ZInput.GetButton("JoyLStickUp") || ZInput.GetButton("JoyLStickDown");

    private static void ConsumeControllerButtonAction(string action)
    {
        ZInput.ResetButtonStatus(action);
        ZInput.ResetButtonStatus("Inventory");
        ZInput.ResetButtonStatus("Use");
        ZInput.ResetButtonStatus("JoyUse");
    }

    private static InventorySlideButton? FindControllerButton(int column, int width)
    {
        int count = 0;
        foreach (InventorySlideButton kind in ControllerButtonOrder)
            if (IsControllerInventoryButtonVisible(kind)) count++;
        if (count == 0) return null;
        int target = Mathf.Clamp(column - (width - count), 0, count - 1);
        foreach (InventorySlideButton kind in ControllerButtonOrder)
            if (IsControllerInventoryButtonVisible(kind) && target-- == 0) return kind;
        return null;
    }

    private static void MoveControllerButton(int direction)
    {
        int index = System.Array.IndexOf(ControllerButtonOrder, _controllerButtonFocus!.Value);
        for (int i = index + direction; i >= 0 && i < ControllerButtonOrder.Length; i += direction)
            if (IsControllerInventoryButtonVisible(ControllerButtonOrder[i]))
            {
                _controllerButtonFocus = ControllerButtonOrder[i];
                return;
            }
    }

    private static bool UpdateInventoryButtonNavigation(InventoryGui gui)
    {
        if (!IsInventoryControllerActive() || !InventoryGui.IsVisible() || IsInventoryPanelClosing(gui) ||
            Player.m_localPlayer == null || FavoriteMemoryAccess.IsLoading(Player.m_localPlayer) ||
            Player.m_localPlayer.IsTeleporting() || !CanShowItemRules(gui))
        {
            ResetInventoryButtonNavigation();
            return false;
        }
        if (_controllerButtonOwner != null && _controllerButtonOwner != gui) ResetInventoryButtonNavigation();
        if (IsControllerItemMenuOpen())
        {
            ResetInventoryButtonNavigation();
            return false;
        }
        if (IsItemRuleInputBlocked() || ShouldBlockGlobalHotkeys(Player.m_localPlayer))
        {
            ShowControllerButtonFocus(null);
            return false; // Editors and external modal UI own input until closed.
        }
        if ((_controllerButtonFocus.HasValue || _controllerSortFocus.HasValue) && _controllerButtonFrame == Time.frameCount)
        {
            _controllerReservedFrame = Time.frameCount;
            return true;
        }
        if (_controllerButtonFocus.HasValue && _controllerButtonTrashDialog != null &&
            _controllerButtonTrashDialog == GetControllerTrashDialog() && _controllerButtonTrashDialog.activeInHierarchy)
            return UpdateControllerButtonTrashConfirmation();
        _controllerButtonTrashDialog = null;
        InventoryGrid? grid = GetControllerActionGrid(gui, requireHotkeys: false);
        if (grid == null)
        {
            ResetInventoryButtonNavigation();
            return false;
        }
        if (_controllerSortFocus.HasValue) return UpdateControllerSortButtonNavigation(gui, grid);
        if (_controllerButtonFocus.HasValue)
        {
            // Native shoulder navigation can still leave this row for crafting.
            if (IsControllerInputUpdated() && (ZInput.GetButtonDown("JoyTabLeft") || ZInput.GetButtonDown("JoyTabRight")))
            {
                ResetInventoryButtonNavigation();
                // An earlier pre-tick UI poll may have reserved this frame.
                // Let native shoulder navigation process the refreshed input.
                _controllerReservedFrame = -1;
                return false;
            }
            if (!IsControllerInventoryButtonVisible(_controllerButtonFocus.Value))
                _controllerButtonFocus = FindControllerButton(gui.m_playerGrid.GetInventory().GetWidth() - 1,
                    gui.m_playerGrid.GetInventory().GetWidth());
            if (!_controllerButtonFocus.HasValue)
            {
                ResetInventoryButtonNavigation();
                return false;
            }
            _controllerReservedFrame = Time.frameCount;
            ShowControllerButtonFocus(_controllerButtonFocus);
            if (!IsControllerInputUpdated()) return true;
            if (!ControllerVerticalHeld()) _controllerButtonWaitForVerticalRelease = false;

            // Keep the existing configurable Y/B shortcuts available on the row.
            bool modifier = InventoryControllerEnabled && _inventoryActionModifier != null &&
                _inventoryActionModifier.Value != InventoryControllerModifier.Off && ZInput.GetButton(_inventoryActionModifier.Value.ToString());
            bool restock = modifier && ZInput.GetButtonDown("JoyButtonY");
            bool exclude = modifier && ZInput.GetButtonDown("JoyButtonB");
            if (restock || exclude)
            {
                _controllerButtonFrame = Time.frameCount;
                ConsumeControllerButtonAction(restock ? "JoyButtonY" : "JoyButtonB");
                OpenControllerItemRules(restock);
                ShowControllerButtonFocus(null);
                return true;
            }
            if (ZInput.GetButtonDown("JoyButtonB"))
            {
                ConsumeControllerButtonAction("JoyButtonB");
                LeaveControllerButtonRow(gui, toContainer: false);
                return true;
            }
            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                _controllerButtonFrame = Time.frameCount;
                ConsumeControllerButtonAction("JoyButtonA");
                if (_controllerButtonFocus == InventorySlideButton.Trash)
                {
                    // The existing entry point checks ownership, favorites,
                    // equipped/hotbar protection and the server permission.
                    if (CanActivateControllerInventoryButton(InventorySlideButton.Trash))
                    {
                        TryClickInventoryTrashPanel();
                        _controllerButtonTrashDialog = GetControllerTrashDialog();
                        _controllerButtonTrashAccept = false;
                        if (_controllerButtonTrashDialog != null) SelectControllerTrashConfirmation(false);
                    }
                }
                else OpenControllerItemRules(_controllerButtonFocus == InventorySlideButton.Restock);
                ShowControllerButtonFocus(null);
                return true;
            }
            bool up = ControllerDirectionDown("Up"), down = ControllerDirectionDown("Down");
            if (!_controllerButtonWaitForVerticalRelease && up != down)
            {
                _controllerButtonFrame = Time.frameCount;
                if (up || HasControllerContainer(gui)) LeaveControllerButtonRow(gui, toContainer: down);
                return true;
            }
            bool left = ControllerDirectionDown("Left"), right = ControllerDirectionDown("Right");
            if (left != right)
            {
                _controllerButtonFrame = Time.frameCount;
                MoveControllerButton(left ? -1 : 1);
                ShowControllerButtonFocus(_controllerButtonFocus);
            }
            return true;
        }

        if (!IsControllerInputUpdated() || _controllerButtonFrame == Time.frameCount) return false;
        Vector2i cell = InventoryControllerAccess.Selection(grid);
        int rows = GetControllerInventoryButtonRows(gui);
        bool downFromGrid = ControllerDirectionDown("Down"), upFromGrid = ControllerDirectionDown("Up");
        bool rightFromGrid = ControllerDirectionDown("Right"), leftFromGrid = ControllerDirectionDown("Left");
        // Native grid navigation may move onto the last row between our entry
        // points, or onto the last column beside Sort. That same input must not
        // also enter either external button afterwards.
        if (downFromGrid || upFromGrid || rightFromGrid || leftFromGrid) _controllerButtonFrame = Time.frameCount;
        if (rightFromGrid && !leftFromGrid && !downFromGrid && !upFromGrid &&
            cell.x == grid.GetInventory().GetWidth() - 1)
        {
            bool container = grid == gui.ContainerGrid;
            bool adjacentRow = container ? HasControllerContainer(gui) && cell.y == 0 : rows > 0 && cell.y == rows - 1;
            if (adjacentRow && IsControllerSortButtonVisible(container))
            {
                _controllerButtonOwner = gui;
                _controllerButtonReturnCell = cell;
                _controllerSortFocus = container;
                _controllerButtonFrame = _controllerReservedFrame = Time.frameCount;
                FocusControllerSortButton(gui, container, cell);
                ShowControllerSortButtonFocus(container);
                return true;
            }
        }
        bool fromPlayer = grid == gui.m_playerGrid && cell.y == rows - 1 && downFromGrid;
        bool fromContainer = grid == gui.ContainerGrid && cell.y == 0 && upFromGrid;
        if (rows <= 0 || (!fromPlayer && !fromContainer)) return false;
        int width = gui.m_playerGrid.GetInventory().GetWidth();
        int column = fromPlayer ? cell.x : cell.x + Mathf.CeilToInt((width - grid.GetInventory().GetWidth()) * 0.5f);
        InventorySlideButton? target = FindControllerButton(column, width);
        if (!target.HasValue) return false;
        _controllerButtonOwner = gui;
        _controllerButtonReturnCell = new Vector2i(Mathf.Clamp(column, 0, width - 1), rows - 1);
        _controllerButtonFocus = target;
        _controllerButtonWaitForVerticalRelease = true;
        _controllerButtonFrame = _controllerReservedFrame = Time.frameCount;
        FocusControllerInventoryButtonRow(gui);
        ShowControllerButtonFocus(target);
        return true;
    }

    private static bool UpdateControllerSortButtonNavigation(InventoryGui gui, InventoryGrid grid)
    {
        bool container = _controllerSortFocus!.Value;
        if (grid != (container ? gui.ContainerGrid : gui.m_playerGrid) ||
            container && !HasControllerContainer(gui) || !IsControllerSortButtonVisible(container))
        {
            ResetInventoryButtonNavigation();
            return false;
        }
        if (IsControllerInputUpdated() && (ZInput.GetButtonDown("JoyTabLeft") || ZInput.GetButtonDown("JoyTabRight")))
        {
            ResetInventoryButtonNavigation();
            _controllerReservedFrame = -1;
            return false;
        }
        _controllerReservedFrame = Time.frameCount;
        ShowControllerSortButtonFocus(container);
        if (!IsControllerInputUpdated()) return true;

        // Preserve the configurable rules shortcuts while an external button
        // owns focus, including registration of a held player item.
        bool modifier = InventoryControllerEnabled && _inventoryActionModifier != null &&
            _inventoryActionModifier.Value != InventoryControllerModifier.Off && ZInput.GetButton(_inventoryActionModifier.Value.ToString());
        bool restock = modifier && ZInput.GetButtonDown("JoyButtonY");
        bool exclude = modifier && ZInput.GetButtonDown("JoyButtonB");
        if (restock || exclude)
        {
            _controllerButtonFrame = Time.frameCount;
            ConsumeControllerButtonAction(restock ? "JoyButtonY" : "JoyButtonB");
            OpenControllerItemRules(restock);
            ShowControllerButtonFocus(null);
            return true;
        }
        if (ZInput.GetButtonDown("JoyButtonB"))
        {
            ConsumeControllerButtonAction("JoyButtonB");
            LeaveControllerSortButton(gui, container);
            return true;
        }
        if (ZInput.GetButtonDown("JoyButtonA") || modifier && ZInput.GetButtonDown("JoyButtonX"))
        {
            _controllerButtonFrame = Time.frameCount;
            ConsumeControllerButtonAction(ZInput.GetButtonDown("JoyButtonA") ? "JoyButtonA" : "JoyButtonX");
            // The adapter invokes the registered Button listener only after
            // checking drag state and interactability, preserving sort policy.
            ActivateControllerSortButton(gui, container);
            return true;
        }
        bool left = ControllerDirectionDown("Left"), right = ControllerDirectionDown("Right");
        bool up = ControllerDirectionDown("Up"), down = ControllerDirectionDown("Down");
        if (left && !right || up != down) LeaveControllerSortButton(gui, container);
        return true;
    }

    private static void LeaveControllerSortButton(InventoryGui gui, bool container)
    {
        Vector2i cell = _controllerButtonReturnCell;
        ResetInventoryButtonNavigation();
        FocusControllerSortGrid(gui, container, cell);
        _controllerButtonFrame = _controllerReservedFrame = Time.frameCount;
    }

    private static bool UpdateControllerButtonTrashConfirmation()
    {
        _controllerReservedFrame = Time.frameCount;
        ShowControllerButtonFocus(null);
        if (!IsControllerInputUpdated() || _controllerButtonFrame == Time.frameCount) return true;
        if (ZInput.GetButtonDown("JoyButtonB"))
        {
            _controllerButtonFrame = Time.frameCount;
            ConsumeControllerButtonAction("JoyButtonB");
            CloseInventoryTrashConfirmDialog();
        }
        else if (ZInput.GetButtonDown("JoyButtonA"))
        {
            _controllerButtonFrame = Time.frameCount;
            ConsumeControllerButtonAction("JoyButtonA");
            if (_controllerButtonTrashAccept) ConfirmInventoryTrashDelete();
            else CloseInventoryTrashConfirmDialog();
        }
        else
        {
            bool left = ControllerDirectionDown("Left"), right = ControllerDirectionDown("Right");
            if (left != right)
            {
                _controllerButtonFrame = Time.frameCount;
                _controllerButtonTrashAccept = right == IsControllerTrashAcceptOnRight();
                SelectControllerTrashConfirmation(_controllerButtonTrashAccept);
            }
        }
        return true;
    }

    private static void LeaveControllerButtonRow(InventoryGui gui, bool toContainer)
    {
        Vector2i cell = _controllerButtonReturnCell;
        ResetInventoryButtonNavigation();
        FocusControllerInventoryGrid(gui, cell, toContainer);
        _controllerButtonFrame = _controllerReservedFrame = Time.frameCount;
    }

    internal static void ResetInventoryButtonNavigation(InventoryGui? owner = null)
    {
        if (owner != null && _controllerButtonOwner != owner) return;
        if (_controllerButtonOwner != null)
        {
            if (_controllerSortFocus.HasValue)
                RestoreControllerSortCell(_controllerButtonOwner, _controllerSortFocus.Value, _controllerButtonReturnCell);
            else if (_controllerButtonOwner.m_playerGrid != null)
                RestoreControllerPlayerCell(_controllerButtonOwner, _controllerButtonReturnCell);
        }
        _controllerButtonOwner = null;
        _controllerButtonFocus = null;
        _controllerSortFocus = null;
        _controllerButtonTrashDialog = null;
        _controllerButtonWaitForVerticalRelease = false;
        ShowControllerButtonFocus(null);
    }
}
