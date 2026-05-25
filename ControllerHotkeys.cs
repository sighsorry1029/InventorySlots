using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool IsControllerHotkeyDown(ConfigEntry<ControllerHotkeyAction>? entry) =>
        IsControllerHotkeyActive(entry, down: true, respectDPadMode: true);

    private static bool IsControllerHotkeyHeld(ConfigEntry<ControllerHotkeyAction>? entry) =>
        IsControllerHotkeyActive(entry, down: false, respectDPadMode: true);

    private static bool IsControllerHotkeyConfigured(ConfigEntry<ControllerHotkeyAction>? entry)
    {
        return entry != null &&
               entry.Value != ControllerHotkeyAction.Off &&
               IsControllerActionAllowedByDPadMode(entry.Value, forDisplay: true);
    }

    private static bool IsControllerHotkeyActive(ConfigEntry<ControllerHotkeyAction>? entry, bool down, bool respectDPadMode)
    {
        if (_enableControllerHotkeys == null ||
            _enableControllerHotkeys.Value.IsOff() ||
            entry == null ||
            entry.Value == ControllerHotkeyAction.Off ||
            !IsGamepadActiveSafe())
        {
            return false;
        }

        ControllerHotkeyAction action = entry.Value;
        if (respectDPadMode && !IsControllerActionAllowedByDPadMode(action, forDisplay: false))
        {
            return false;
        }

        string actionName = GetControllerHotkeyActionName(action);
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return false;
        }

        try
        {
            return down ? ZInput.GetButtonDown(actionName) : ZInput.GetButton(actionName);
        }
        catch
        {
            return false;
        }
    }

    private static string GetControllerHotkeyDisplayText(ConfigEntry<ControllerHotkeyAction>? entry)
    {
        return GetControllerHotkeyDisplayText(entry, respectDPadMode: true);
    }

    private static string GetControllerHotkeyDisplayText(ConfigEntry<ControllerHotkeyAction>? entry, bool respectDPadMode)
    {
        if (entry == null || entry.Value == ControllerHotkeyAction.Off)
        {
            return "";
        }

        ControllerHotkeyAction action = entry.Value;
        if (respectDPadMode && !IsControllerActionAllowedByDPadMode(action, forDisplay: true))
        {
            return "";
        }

        string display = GetControllerHotkeyDisplayText(action);
        if (respectDPadMode &&
            IsControllerDPadAction(action) &&
            (_controllerDPadHotkeyMode?.Value ?? ControllerDPadHotkeyMode.InventoryNavigation) == ControllerDPadHotkeyMode.HotkeysWhileHoldingModifier)
        {
            string modifier = GetControllerHotkeyDisplayText(_controllerDPadModifierButton, respectDPadMode: false);
            if (!string.IsNullOrWhiteSpace(modifier))
            {
                display = $"{modifier} + {display}";
            }
        }

        return display;
    }

    private static string GetControllerHotkeyDisplayText(ControllerHotkeyAction action)
    {
        return action == ControllerHotkeyAction.Off
            ? ""
            : GetControllerActionDisplayText(GetControllerHotkeyActionName(action));
    }

    private static string GetControllerActionDisplayText(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "";
        }

        try
        {
            string bound = ZInput.instance != null ? ZInput.instance.GetBoundKeyString(action, true) : "";
            if (!string.IsNullOrWhiteSpace(bound))
            {
                return bound;
            }
        }
        catch
        {
            // Fall through to the action name.
        }

        return action;
    }

    private static string GetControllerHotkeyActionName(ControllerHotkeyAction action) =>
        action == ControllerHotkeyAction.Off ? "" : action.ToString();

    private static bool IsControllerActionAllowedByDPadMode(ControllerHotkeyAction action, bool forDisplay)
    {
        if (!IsControllerDPadAction(action))
        {
            return true;
        }

        return (_controllerDPadHotkeyMode?.Value ?? ControllerDPadHotkeyMode.InventoryNavigation) switch
        {
            ControllerDPadHotkeyMode.Hotkeys => true,
            ControllerDPadHotkeyMode.HotkeysWhileHoldingModifier => forDisplay
                ? IsControllerHotkeyConfiguredIgnoringDPadMode(_controllerDPadModifierButton)
                : IsControllerHotkeyActive(_controllerDPadModifierButton, down: false, respectDPadMode: false),
            _ => false
        };
    }

    private static bool IsControllerHotkeyConfiguredIgnoringDPadMode(ConfigEntry<ControllerHotkeyAction>? entry) =>
        entry != null && entry.Value != ControllerHotkeyAction.Off;

    private static bool IsControllerDPadAction(ControllerHotkeyAction action)
    {
        return action is ControllerHotkeyAction.JoyDPadUp or
            ControllerHotkeyAction.JoyDPadDown or
            ControllerHotkeyAction.JoyDPadLeft or
            ControllerHotkeyAction.JoyDPadRight;
    }
}
