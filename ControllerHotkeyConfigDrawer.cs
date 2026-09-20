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
        if (isCapturing)
        {
            ControllerHotkeyAction captured = TryCaptureControllerHotkeyAction();
            if (captured != ControllerHotkeyAction.Off)
            {
                SetControllerHotkeyConfigValue(entry, captured);
                currentValue = captured;
                StopControllerHotkeyCapture();
                isCapturing = false;
            }
        }

        const float gap = 4f;
        float layoutWidth = GetConfigDrawerLayoutWidth(entry);
        float lineHeight = ConfigDrawerLineHeight();
        GUIStyle labelStyle = ConfigDrawerWrappedLabelStyle();
        string display = currentValue.ToString();
        foreach (ControllerActionOption option in ControllerActionOptions)
        {
            if (option.Action != currentValue) continue;
            display = option.Label;
            break;
        }

        string currentText = $"Current: {display}";
        float currentWidth = GetControllerConfigCurrentWidth(layoutWidth, gap, out _, out _);
        float headerHeight = Mathf.Max(lineHeight, labelStyle.CalcHeight(new GUIContent(currentText), currentWidth));
        string status = GetControllerHotkeyStatus(currentValue, isCapturing);
        float statusHeight = status.Length == 0 ? 0f : labelStyle.CalcHeight(new GUIContent(status), layoutWidth);
        bool expanded = ExpandedControllerHotkeyPresetDrawers.Contains(entryKey);
        int columns = Mathf.Clamp(Mathf.FloorToInt((layoutWidth + gap) / (88f + gap)), 1, 4);
        int rows = (ControllerActionOptions.Length + columns - 1) / columns;
        float height = headerHeight + gap + lineHeight;
        if (statusHeight > 0f) height += gap + statusHeight;
        if (expanded) height += gap + rows * (lineHeight + gap) - gap;

        // One elastic layout entry keeps long labels and preset buttons from
        // contributing their preferred widths to the manager's scroll view.
        Rect area = ReserveConfigDrawerArea(entry, height);
        GUI.BeginGroup(area);
        try
        {
            float width = Mathf.Max(1f, area.width);
            float actualGap = Mathf.Min(gap, width / 8f);
            currentWidth = GetControllerConfigCurrentWidth(width, actualGap, out float captureWidth, out float clearWidth);
            GUI.Label(new Rect(0f, 0f, currentWidth, headerHeight), currentText, labelStyle);
            float buttonY = Mathf.Max(0f, (headerHeight - lineHeight) / 2f);
            if (GUI.Button(new Rect(currentWidth + actualGap, buttonY, captureWidth, lineHeight), isCapturing ? "Stop" : "Capture"))
            {
                if (isCapturing) StopControllerHotkeyCapture();
                else StartControllerHotkeyCapture(entryKey);
            }

            if (GUI.Button(new Rect(width - clearWidth, buttonY, clearWidth, lineHeight), "Clear"))
            {
                SetControllerHotkeyConfigValue(entry, ControllerHotkeyAction.Off);
            }

            float y = headerHeight + gap;
            if (statusHeight > 0f)
            {
                GUI.Label(new Rect(0f, y, width, statusHeight), status, labelStyle);
                y += statusHeight + gap;
            }

            if (GUI.Button(new Rect(0f, y, Mathf.Min(110f, width), lineHeight), expanded ? "Hide Presets" : "Presets"))
            {
                if (!ExpandedControllerHotkeyPresetDrawers.Add(entryKey))
                    ExpandedControllerHotkeyPresetDrawers.Remove(entryKey);
            }

            if (expanded)
                DrawControllerHotkeyPresetButtons(entry, currentValue, width, y + lineHeight + gap, lineHeight, columns);
        }
        finally { GUI.EndGroup(); }
    }

    private static float GetControllerConfigCurrentWidth(float width, float gap, out float captureWidth, out float clearWidth)
    {
        float available = Mathf.Max(1f, width - gap * 2f);
        captureWidth = Mathf.Min(70f, available * 0.38f);
        clearWidth = Mathf.Min(48f, available * 0.27f);
        return Mathf.Max(1f, available - captureWidth - clearWidth);
    }

    private static string GetControllerHotkeyStatus(ControllerHotkeyAction currentValue, bool isCapturing)
    {
        string status = "";
        if (IsControllerDPadAction(currentValue) &&
            (_controllerDPadHotkeyMode?.Value ?? ControllerDPadHotkeyMode.InventoryNavigation) == ControllerDPadHotkeyMode.InventoryNavigation)
        {
            status = "DPad actions are ignored until Controller DPad Hotkey Mode allows hotkeys.";
        }

        if (isCapturing)
        {
            float remaining = Mathf.Max(0f, _controllerHotkeyCaptureEndTime - Time.unscaledTime);
            if (status.Length > 0) status += "\n";
            status += $"Listening for controller input... {remaining:0.0}s";
        }
        return status;
    }

    private static void DrawControllerHotkeyPresetButtons(ConfigEntryBase entry, ControllerHotkeyAction currentValue,
        float width, float y, float lineHeight, int columns)
    {
        float gap = Mathf.Min(4f, width / (columns * 2f));
        float buttonWidth = Mathf.Max(0.1f, (width - gap * (columns - 1)) / columns);
        for (int i = 0; i < ControllerActionOptions.Length; i++)
        {
            ControllerActionOption option = ControllerActionOptions[i];
            string label = currentValue == option.Action ? $"* {option.Label}" : option.Label;
            Rect rect = new((i % columns) * (buttonWidth + gap), y + (i / columns) * (lineHeight + gap), buttonWidth, lineHeight);
            if (GUI.Button(rect, new GUIContent(label, option.Label)))
            {
                SetControllerHotkeyConfigValue(entry, option.Action);
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
