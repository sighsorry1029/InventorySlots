using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum UiScrollInputMode
    {
        Discrete,
        Continuous
    }

    private static float _nextGamepadUiScrollStepTime;
    private static int _mouseUiScrollConsumedFrame = -1;

    private static float GetUiScrollDelta(UiScrollInputMode mode, bool allowGamepad = true)
    {
        float mouseDelta = Input.mouseScrollDelta.y * GetMouseUiScrollMultiplier();
        if (Mathf.Abs(mouseDelta) >= 0.01f)
        {
            return _mouseUiScrollConsumedFrame == Time.frameCount ? 0f : mouseDelta;
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

    private static bool HasUnconsumedUiScrollInput()
    {
        float mouseDelta = Input.mouseScrollDelta.y * GetMouseUiScrollMultiplier();
        if (Mathf.Abs(mouseDelta) >= 0.01f && _mouseUiScrollConsumedFrame != Time.frameCount)
        {
            return true;
        }

        return IsGamepadUiScrollActive();
    }

    private static void ConsumeMouseUiScrollForCurrentFrame()
    {
        if (Mathf.Abs(Input.mouseScrollDelta.y) >= 0.01f)
        {
            _mouseUiScrollConsumedFrame = Time.frameCount;
        }
    }

    private static bool IsMouseUiScrollConsumedForCurrentFrame() =>
        _mouseUiScrollConsumedFrame == Time.frameCount;

    internal static bool TryHandleCraftingPointerScroll(ScrollRect scrollRect, PointerEventData eventData)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (!_craftingRedesignApplied ||
            gui == null ||
            gui.m_crafting == null ||
            scrollRect == null ||
            IsUnityNull(scrollRect) ||
            eventData == null ||
            Mathf.Abs(eventData.scrollDelta.y) < 0.01f ||
            !scrollRect.transform.IsChildOf(gui.m_crafting))
        {
            return false;
        }

        Vector2 pointer = eventData.position;
        if (IsPointerOverActiveCraftingPinnedTooltip(pointer))
        {
            return false;
        }

        float wheel = eventData.scrollDelta.y * GetMouseUiScrollMultiplier();
        UpdateCraftingTooltipRecipeOverlay(gui);

        if (!HasCraftingHoverTooltipWheelOwner(pointer))
        {
            return false;
        }

        return IsMouseUiScrollConsumedForCurrentFrame() || TryScrollCraftingHoverTooltip(wheel);
    }

    private static bool IsPointerOverActiveCraftingPinnedTooltip(Vector2 pointer)
    {
        for (int i = 0; i < PinnedTooltips.Crafting.Panels.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
            if (panel != null &&
                !IsUnityNull(panel) &&
                panel.gameObject.activeInHierarchy &&
                RectContainsScreenPoint(panel, pointer))
            {
                return true;
            }
        }

        return false;
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
