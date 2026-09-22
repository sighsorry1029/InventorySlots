using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static InventoryGui? _controllerMenuOwner;
    private static InventoryGrid? _controllerMenuGrid;
    private static Inventory? _controllerMenuInventory;
    private static Vector2i _controllerMenuCell;
    private static bool _controllerMenuFavorite;
    private static int _controllerMenuChoice;
    private static int _controllerMenuFrame = -1;
    private static InventoryGrid? _controllerMenuPressGrid;
    private static Vector2i _controllerMenuPressCell;
    private static float _controllerMenuPressTime;
    private static bool _controllerMenuStickHeld;

    internal static bool IsControllerItemMenuOpen() => _controllerMenuGrid != null;

    private static bool CanControllerFavoriteCell(InventoryGui gui, InventoryGrid grid, Vector2i cell)
    {
        Inventory inventory = grid.GetInventory();
        if (grid != gui.m_playerGrid || inventory != ((Humanoid)Player.m_localPlayer).GetInventory() || IsOutOfBounds(inventory, cell)) return false;
#if INVENTORY_SLOTS
        return CanFavoriteSlot(Player.m_localPlayer, inventory, cell);
#else
        return CanFavoriteCell(inventory, cell);
#endif
    }

    private static bool IsControllerCellFavorite(InventoryGrid grid, Vector2i cell)
    {
#if INVENTORY_SLOTS
        return IsFavoriteSlot(Player.m_localPlayer, cell);
#else
        return IsFavoriteSlot(Player.m_localPlayer, grid.GetInventory(), cell);
#endif
    }

    // Right-stick tap and right-stick chords share a button. Open only on a
    // standalone short release, never after another action, navigation or drag.
    private static bool ControllerMenuPressInterrupted() =>
        ZInput.GetButton("JoyButtonA") || ZInput.GetButton("JoyButtonB") ||
        ZInput.GetButton("JoyButtonX") || ZInput.GetButton("JoyButtonY") ||
        ZInput.GetButton("JoyLTrigger") || ZInput.GetButton("JoyRTrigger") ||
        ZInput.GetButton("JoyTabLeft") || ZInput.GetButton("JoyTabRight") ||
        ZInput.GetButton("JoyDPadLeft") || ZInput.GetButton("JoyDPadRight") ||
        ZInput.GetButton("JoyLStickLeft") || ZInput.GetButton("JoyLStickRight") || ControllerVerticalHeld();

    private static bool UpdateControllerItemMenu(InventoryGui gui)
    {
        InventoryGrid? grid = GetControllerActionGrid(gui, requireHotkeys: false);
        bool valid = grid != null && !IsInventoryButtonNavigationActive() && !IsItemRuleInputBlocked();
        if (!valid || _controllerMenuOwner != null && _controllerMenuOwner != gui)
        {
            ResetControllerItemMenu();
            HideControllerGridHelp();
            return false;
        }
        if (InventoryControllerAccess.DragObject(gui) != null)
        {
            ResetControllerItemMenu();
            UpdateControllerGridHelp(gui, grid!, holdingItem: true);
            return false;
        }

        if (IsControllerItemMenuOpen())
        {
            HideControllerGridHelp();
            if (_controllerMenuGrid != grid || _controllerMenuInventory != grid!.GetInventory() ||
                InventoryControllerAccess.Selection(grid) != _controllerMenuCell ||
                IsOutOfBounds(grid.GetInventory(), _controllerMenuCell) ||
                _controllerMenuFavorite != CanControllerFavoriteCell(gui, grid, _controllerMenuCell))
            {
                ResetControllerItemMenu();
                return false;
            }
            _controllerReservedFrame = Time.frameCount;
            if (!IsControllerInputUpdated() || _controllerMenuFrame == Time.frameCount) return true;
            _controllerMenuFrame = Time.frameCount;
            if (ZInput.GetButtonDown("JoyButtonB"))
            {
                ConsumeControllerButtonAction("JoyButtonB");
                ResetControllerItemMenu();
                return true;
            }
            // Keep native tab/group navigation available without leaving a
            // popup pointing at a grid that is no longer selected.
            if (ZInput.GetButtonDown("JoyTabLeft") || ZInput.GetButtonDown("JoyTabRight"))
            {
                ResetControllerItemMenu();
                _controllerReservedFrame = -1;
                return false;
            }
            if (ZInput.GetButtonDown("JoyButtonA"))
            {
                ConsumeControllerButtonAction("JoyButtonA");
                bool favorite = _controllerMenuFavorite && _controllerMenuChoice == 0;
                Vector2i cell = _controllerMenuCell;
                ResetControllerItemMenu();
                if (favorite) ToggleFavoriteSlot(Player.m_localPlayer, cell);
                else if (grid == gui.m_playerGrid) SortPlayerInventory(Player.m_localPlayer);
                else SortCurrentContainer(Player.m_localPlayer);
                return true;
            }
            bool up = ControllerDirectionDown("Up"), down = ControllerDirectionDown("Down");
            if (_controllerMenuFavorite && up != down) _controllerMenuChoice = down ? 1 : 0;
            ShowControllerItemMenu(gui, grid!, _controllerMenuFavorite,
                _controllerMenuFavorite && IsControllerCellFavorite(grid!, _controllerMenuCell), _controllerMenuChoice);
            return true;
        }

        UpdateControllerGridHelp(gui, grid!);
        if (!IsControllerInputUpdated() || _controllerMenuFrame == Time.frameCount) return false;
        // Do not latch a negative pre-tick result: a later entry point may see
        // this frame's refreshed button-downs, just like the existing chords.
        bool held = ZInput.GetButton("JoyRStick");
        Vector2i selected = InventoryControllerAccess.Selection(grid!);
        if (held && !_controllerMenuStickHeld && ZInput.GetButtonDown("JoyRStick"))
        {
            _controllerMenuPressGrid = grid;
            _controllerMenuPressCell = selected;
            _controllerMenuPressTime = Time.unscaledTime;
            _controllerMenuOwner = gui;
        }
        _controllerMenuStickHeld = held;
        if (ControllerMenuPressInterrupted() || _controllerMenuPressGrid != grid ||
            selected != _controllerMenuPressCell || Time.unscaledTime - _controllerMenuPressTime > 0.45f)
            _controllerMenuPressGrid = null;
        if (held || _controllerMenuPressGrid == null) return false;

        _controllerMenuPressGrid = null;
        _controllerMenuGrid = grid;
        _controllerMenuInventory = grid!.GetInventory();
        _controllerMenuCell = selected;
        _controllerMenuFavorite = CanControllerFavoriteCell(gui, grid, selected);
        _controllerMenuChoice = 0;
        _controllerMenuFrame = _controllerReservedFrame = Time.frameCount;
        HideControllerGridHelp();
        ShowControllerItemMenu(gui, grid, _controllerMenuFavorite,
            _controllerMenuFavorite && IsControllerCellFavorite(grid, selected), 0);
        return true;
    }

    private static void UpdateControllerGridHelp(InventoryGui gui, InventoryGrid grid, bool holdingItem = false)
    {
        string prefix = "$" + ModName.ToLowerInvariant() + "_controller_";
        string text = holdingItem ? "" : LocalizeUi(prefix + "actions_hint", "Tap {key}: actions")
            .Replace("{key}", GetInventoryControllerActionDisplay("JoyRStick"));
        Vector2i cell = InventoryControllerAccess.Selection(grid);
        bool player = grid == gui.m_playerGrid;
        if (player && cell.y == GetControllerInventoryButtonRows(gui) - 1 &&
            (IsControllerInventoryButtonVisible(InventorySlideButton.Restock) ||
             IsControllerInventoryButtonVisible(InventorySlideButton.Exclude) || IsControllerInventoryButtonVisible(InventorySlideButton.Trash)))
            text += (text.Length > 0 ? "  ·  " : "") + LocalizeUi(prefix + "tools_hint", "↓: buttons");
        if (!holdingItem && cell.x == grid.GetInventory().GetWidth() - 1 &&
            cell.y == (player ? GetControllerInventoryButtonRows(gui) - 1 : 0) && IsControllerSortButtonVisible(!player))
            text += "  ·  " + LocalizeUi(prefix + "sort_hint", "→: Sort");
        SetControllerGridHelp(gui, text);
    }

    internal static void ResetControllerItemMenu(InventoryGui? owner = null)
    {
        if (owner != null && _controllerMenuOwner != owner) return;
        _controllerMenuOwner = null;
        _controllerMenuGrid = _controllerMenuPressGrid = null;
        _controllerMenuInventory = null;
        _controllerMenuStickHeld = ZInput.GetButton("JoyRStick");
        HideControllerItemMenu();
    }
}
