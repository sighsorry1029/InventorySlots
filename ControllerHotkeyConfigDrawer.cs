using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float ControllerHotkeyCaptureSeconds = 8f;
    private static readonly HashSet<string> ExpandedControllerHotkeyPresetDrawers = new();
    private static string _controllerHotkeyCaptureEntryKey = "";
    private static float _controllerHotkeyCaptureEndTime;

    private static readonly ControllerActionOption[] ControllerActionOptions =
    {
        new(ControllerHotkeyAction.Off, "Off"),
        new(ControllerHotkeyAction.JoyButtonA, "A"),
        new(ControllerHotkeyAction.JoyButtonB, "B"),
        new(ControllerHotkeyAction.JoyButtonX, "X"),
        new(ControllerHotkeyAction.JoyButtonY, "Y"),
        new(ControllerHotkeyAction.JoyLBumper, "LB"),
        new(ControllerHotkeyAction.JoyRBumper, "RB"),
        new(ControllerHotkeyAction.JoyLTrigger, "LT"),
        new(ControllerHotkeyAction.JoyRTrigger, "RT"),
        new(ControllerHotkeyAction.JoyBack, "Back"),
        new(ControllerHotkeyAction.JoyStart, "Start"),
        new(ControllerHotkeyAction.JoyLStick, "L Stick"),
        new(ControllerHotkeyAction.JoyRStick, "R Stick"),
        new(ControllerHotkeyAction.JoyDPadUp, "DPad Up"),
        new(ControllerHotkeyAction.JoyDPadDown, "DPad Down"),
        new(ControllerHotkeyAction.JoyDPadLeft, "DPad Left"),
        new(ControllerHotkeyAction.JoyDPadRight, "DPad Right"),
        new(ControllerHotkeyAction.JoyHotbarUse, "Hotbar Use"),
        new(ControllerHotkeyAction.JoyAltKeys, "Alt Keys"),
        new(ControllerHotkeyAction.AltPlace, "Alt Place"),
        new(ControllerHotkeyAction.JoyUse, "Use")
    };

    private static readonly Dictionary<int, ControllerHotkeyAction> UnityJoystickButtonToControllerAction = new()
    {
        [0] = ControllerHotkeyAction.JoyButtonA,
        [1] = ControllerHotkeyAction.JoyButtonB,
        [2] = ControllerHotkeyAction.JoyButtonX,
        [3] = ControllerHotkeyAction.JoyButtonY,
        [4] = ControllerHotkeyAction.JoyLBumper,
        [5] = ControllerHotkeyAction.JoyRBumper,
        [6] = ControllerHotkeyAction.JoyBack,
        [7] = ControllerHotkeyAction.JoyStart,
        [8] = ControllerHotkeyAction.JoyLStick,
        [9] = ControllerHotkeyAction.JoyRStick,
        [10] = ControllerHotkeyAction.JoyDPadLeft,
        [11] = ControllerHotkeyAction.JoyDPadRight,
        [12] = ControllerHotkeyAction.JoyDPadUp,
        [13] = ControllerHotkeyAction.JoyDPadDown,
        [14] = ControllerHotkeyAction.JoyLTrigger,
        [15] = ControllerHotkeyAction.JoyRTrigger,
        [16] = ControllerHotkeyAction.JoyButtonA,
        [17] = ControllerHotkeyAction.JoyButtonB,
        [18] = ControllerHotkeyAction.JoyButtonX,
        [19] = ControllerHotkeyAction.JoyButtonY
    };

    private static void DrawControllerHotkeyConfig(ConfigEntryBase entry)
    {
        string entryKey = GetConfigDrawerEntryKey(entry);
        ControllerHotkeyAction currentValue = GetControllerHotkeyConfigValue(entry);
        bool isCapturing = IsCapturingControllerHotkey(entryKey);

        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        string display = GetControllerHotkeyDisplayText(currentValue);
        GUILayout.Label($"Current: {(string.IsNullOrWhiteSpace(display) ? "Off" : display)}", GUILayout.ExpandWidth(true));

        if (GUILayout.Button(isCapturing ? "Stop" : "Capture", GUILayout.Width(70f)))
        {
            if (isCapturing)
            {
                StopControllerHotkeyCapture();
            }
            else
            {
                StartControllerHotkeyCapture(entryKey);
            }
        }

        if (GUILayout.Button("Clear", GUILayout.Width(48f)))
        {
            SetControllerHotkeyConfigValue(entry, ControllerHotkeyAction.Off);
            currentValue = ControllerHotkeyAction.Off;
        }

        GUILayout.EndHorizontal();

        DrawControllerHotkeyStatus(entry, currentValue, isCapturing);

        if (GUILayout.Button(ExpandedControllerHotkeyPresetDrawers.Contains(entryKey) ? "Hide Presets" : "Presets", GUILayout.Width(110f)))
        {
            if (!ExpandedControllerHotkeyPresetDrawers.Add(entryKey))
            {
                ExpandedControllerHotkeyPresetDrawers.Remove(entryKey);
            }
        }

        if (ExpandedControllerHotkeyPresetDrawers.Contains(entryKey))
        {
            DrawControllerHotkeyPresetButtons(entry, currentValue);
        }

        GUILayout.EndVertical();
    }

    private static void DrawControllerHotkeyStatus(ConfigEntryBase entry, ControllerHotkeyAction currentValue, bool isCapturing)
    {
        if (IsControllerDPadAction(currentValue) &&
            (_controllerDPadHotkeyMode?.Value ?? ControllerDPadHotkeyMode.InventoryNavigation) == ControllerDPadHotkeyMode.InventoryNavigation)
        {
            GUILayout.Label("DPad actions are ignored until Controller DPad Hotkey Mode allows hotkeys.");
        }

        if (!isCapturing)
        {
            return;
        }

        ControllerHotkeyAction captured = TryCaptureControllerHotkeyAction();
        if (captured != ControllerHotkeyAction.Off)
        {
            SetControllerHotkeyConfigValue(entry, captured);
            StopControllerHotkeyCapture();
            return;
        }

        float remaining = Mathf.Max(0f, _controllerHotkeyCaptureEndTime - Time.unscaledTime);
        GUILayout.Label($"Listening for controller input... {remaining:0.0}s");
    }

    private static void DrawControllerHotkeyPresetButtons(ConfigEntryBase entry, ControllerHotkeyAction currentValue)
    {
        const int columns = 4;
        for (int i = 0; i < ControllerActionOptions.Length; i++)
        {
            if (i % columns == 0)
            {
                GUILayout.BeginHorizontal();
            }

            ControllerActionOption option = ControllerActionOptions[i];
            string label = currentValue == option.Action ? $"* {option.Label}" : option.Label;
            if (GUILayout.Button(label, GUILayout.Width(88f)))
            {
                SetControllerHotkeyConfigValue(entry, option.Action);
            }

            if (i % columns == columns - 1 || i == ControllerActionOptions.Length - 1)
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    private static ControllerHotkeyAction TryCaptureControllerHotkeyAction()
    {
        for (int i = 0; i < ControllerActionOptions.Length; i++)
        {
            ControllerHotkeyAction action = ControllerActionOptions[i].Action;
            if (action == ControllerHotkeyAction.Off)
            {
                continue;
            }

            try
            {
                if (ZInput.GetButtonDown(GetControllerHotkeyActionName(action)))
                {
                    return action;
                }
            }
            catch
            {
                // Some semantic ZInput actions are unavailable in some game states.
            }
        }

        for (int i = 0; i <= 19; i++)
        {
            try
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + i)) &&
                    UnityJoystickButtonToControllerAction.TryGetValue(i, out ControllerHotkeyAction action))
                {
                    return action;
                }
            }
            catch
            {
                return ControllerHotkeyAction.Off;
            }
        }

        return ControllerHotkeyAction.Off;
    }

    private static ControllerHotkeyAction GetControllerHotkeyConfigValue(ConfigEntryBase entry)
    {
        return entry.BoxedValue is ControllerHotkeyAction action ? action : ControllerHotkeyAction.Off;
    }

    private static void SetControllerHotkeyConfigValue(ConfigEntryBase entry, ControllerHotkeyAction value)
    {
        if (!Equals(entry.BoxedValue, value))
        {
            entry.BoxedValue = value;
        }
    }

    private static bool IsCapturingControllerHotkey(string entryKey)
    {
        if (!string.Equals(_controllerHotkeyCaptureEntryKey, entryKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (Time.unscaledTime <= _controllerHotkeyCaptureEndTime)
        {
            return true;
        }

        StopControllerHotkeyCapture();
        return false;
    }

    private static void StartControllerHotkeyCapture(string entryKey)
    {
        _controllerHotkeyCaptureEntryKey = entryKey;
        _controllerHotkeyCaptureEndTime = Time.unscaledTime + ControllerHotkeyCaptureSeconds;
    }

    private static void StopControllerHotkeyCapture()
    {
        _controllerHotkeyCaptureEntryKey = "";
        _controllerHotkeyCaptureEndTime = 0f;
    }

    private static string GetConfigDrawerEntryKey(ConfigEntryBase entry) =>
        $"{entry.Definition.Section}\n{entry.Definition.Key}";

    private sealed class ControllerActionOption
    {
        public ControllerActionOption(ControllerHotkeyAction action, string label)
        {
            Action = action;
            Label = label;
        }

        public ControllerHotkeyAction Action { get; }
        public string Label { get; }
    }
}
