using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static int _controllerGuideFrame = -1;

    // A direct UI control, available independently of optional inventory hotkeys.
    private static bool ShowControllerFeatureGuideToggle() => IsInventoryControllerActive() &&
        InventoryGui.instance != null && InventoryGui.IsVisible() && !IsInventoryPanelClosing(InventoryGui.instance);

    private static string GetControllerFeatureGuideToggleDisplay() =>
        GetInventoryControllerActionDisplay("JoyLTrigger") + " + " + GetInventoryControllerActionDisplay("JoyRStick");

    private static bool UpdateControllerFeatureGuide(InventoryGui gui)
    {
        if (_controllerGuideFrame == Time.frameCount) return true;
        if (!CanUseInventoryControllerUi(gui) || IsFeatureGuideHotkeyBlocked(Player.m_localPlayer) ||
            gui.m_dragGo != null || gui.m_dragItem != null ||
            !ZInput.GetButton("JoyLTrigger") || !ZInput.GetButton("JoyRStick")) return false;

        // Cancel a pending R3 tap even when the trigger was pressed second.
        // Reserve early UI polls, but act only after ZInput refreshed this frame.
        ResetControllerItemMenu();
        _controllerReservedFrame = Time.frameCount;
        if (!IsControllerInputUpdated()) return true;
        if (ZInput.GetButtonDown("JoyRStick"))
        {
            _controllerGuideFrame = Time.frameCount;
            CycleFeatureGuideState(Player.m_localPlayer, controller: true);
            ZInput.ResetButtonStatus("JoyRStick");
        }
        return true;
    }
}
