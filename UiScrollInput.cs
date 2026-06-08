using System;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum UiScrollInputMode
    {
        Discrete,
        Continuous
    }

    private static float _nextGamepadUiScrollStepTime;

    private static float GetUiScrollDelta(UiScrollInputMode mode, bool allowGamepad = true)
    {
        float mouseDelta = Input.mouseScrollDelta.y * GetMouseUiScrollMultiplier();
        if (Mathf.Abs(mouseDelta) >= 0.01f)
        {
            return mouseDelta;
        }

        if (!allowGamepad || !IsGamepadUiScrollActive(out float gamepadDelta))
        {
            return 0f;
        }

        if (mode == UiScrollInputMode.Continuous)
        {
            return gamepadDelta * GetGamepadUiScrollSensitivity() * Time.unscaledDeltaTime;
        }

        float now = Time.unscaledTime;
        if (now < _nextGamepadUiScrollStepTime)
        {
            return 0f;
        }

        _nextGamepadUiScrollStepTime = now + GetGamepadUiScrollRepeatDelay();
        return Mathf.Sign(gamepadDelta);
    }

    private static bool IsUiScrollTargetActive(RectTransform? rect, bool allowGamepad = true)
    {
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        if (RectContainsScreenPoint(rect, GetUiMousePosition()))
        {
            return true;
        }

        return allowGamepad && IsGamepadUiScrollActive();
    }

    private static Vector2 GetUiMousePosition()
    {
        return Input.mousePosition;
    }

    private static bool IsGamepadUiScrollActive() =>
        IsGamepadUiScrollActive(out _);

    private static bool IsGamepadUiScrollActive(out float delta)
    {
        delta = 0f;
        if (_enableGamepadUiScroll == null || _enableGamepadUiScroll.Value.IsOff() || !IsGamepadActiveSafe())
        {
            _nextGamepadUiScrollStepTime = 0f;
            return false;
        }

        delta = GetGamepadUiScrollRawDelta();
        if (Mathf.Abs(delta) < GetGamepadUiScrollDeadzone())
        {
            _nextGamepadUiScrollStepTime = 0f;
            delta = 0f;
            return false;
        }

        delta = Mathf.Clamp(delta, -1f, 1f);
        return true;
    }

    private static bool IsGamepadActiveSafe()
    {
        try
        {
            return ZInput.IsGamepadActive();
        }
        catch
        {
            return false;
        }
    }

    private static float GetGamepadUiScrollRawDelta()
    {
        return (_gamepadUiScrollSource?.Value ?? GamepadUiScrollSource.RightStickY) switch
        {
            GamepadUiScrollSource.DPadVertical => GetGamepadDPadVertical(),
            GamepadUiScrollSource.RightStickYOrDPadVertical => GetDominantGamepadScrollDelta(GetGamepadRightStickY(), GetGamepadDPadVertical()),
            _ => GetGamepadRightStickY()
        };
    }

    private static float GetDominantGamepadScrollDelta(float first, float second) =>
        Mathf.Abs(first) >= Mathf.Abs(second) ? first : second;

    private static float GetGamepadRightStickY()
    {
        try
        {
            return ZInput.GetJoyRightStickY(true);
        }
        catch
        {
            return 0f;
        }
    }

    private static float GetGamepadDPadVertical()
    {
        try
        {
            if (ZInput.GetButton("JoyDPadUp"))
            {
                return 1f;
            }

            if (ZInput.GetButton("JoyDPadDown"))
            {
                return -1f;
            }
        }
        catch
        {
            return 0f;
        }

        return 0f;
    }

    private static float GetMouseUiScrollMultiplier() =>
        Mathf.Clamp(_mouseUiScrollMultiplier?.Value ?? 1f, 0.1f, 5f);

    private static float GetGamepadUiScrollSensitivity() =>
        Mathf.Clamp(_gamepadUiScrollSensitivity?.Value ?? 6f, 0.5f, 20f);

    private static float GetGamepadUiScrollRepeatDelay() =>
        Mathf.Clamp(_gamepadUiScrollRepeatDelay?.Value ?? 0.18f, 0.05f, 0.75f);

    private static float GetGamepadUiScrollDeadzone() =>
        Mathf.Clamp(_gamepadUiScrollDeadzone?.Value ?? 0.35f, 0.05f, 0.95f);
}
