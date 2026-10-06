using System;
using InventorySlots;
using UnityEngine;

// Only Unity/input boundaries are simulated; policy and click-lease methods are
// extracted from ItemLinkUi.cs. Actual camera IL is checked by HarmonySmoke.
namespace UnityEngine
{
    internal static class Time { internal static int frameCount; internal static float unscaledTime; }
    internal static class Input
    {
        internal static bool Down, Held, Up;
        internal static bool GetMouseButtonDown(int button) => button == 0 && Down;
        internal static bool GetMouseButton(int button) => button == 0 && Held;
        internal static bool GetMouseButtonUp(int button) => button == 0 && Up;
    }
}
internal static class ZInput
{
    internal static bool Mouse = true, PinDown, PinHeld, PinUp, Escape, Enter;
    internal static bool IsMouseActive() => Mouse;
    internal static bool GetKey(UnityEngine.KeyCode key) => key == UnityEngine.KeyCode.Mouse2 && PinHeld;
    internal static bool GetKeyUp(UnityEngine.KeyCode key) => key == UnityEngine.KeyCode.Mouse2 && PinUp;
    internal static bool GetKeyDown(UnityEngine.KeyCode key) => key switch
    { UnityEngine.KeyCode.Mouse2 => PinDown, UnityEngine.KeyCode.Escape => Escape, _ => Enter };
}
internal sealed class Chat
{
    internal static Chat? instance;
    internal ChatInput m_input = new();
    internal Window? m_chatWindow = new();
    internal sealed class Window : UnityEngine.Transform { internal Window gameObject => this; internal bool activeInHierarchy = true; }
    internal sealed class ChatInput
    {
        internal bool isFocused = true, isActiveAndEnabled = true;
        internal string text = "draft";
        internal int selectionStringAnchorPosition = 5, selectionStringFocusPosition = 5;
        internal bool ActivationRequested;
        internal void ActivateInputField() => ActivationRequested = true;
    }
}
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
    {
        internal static bool Enabled = true, Blocked;
        private static bool ItemLinksEnabled => Enabled;
        private static bool ItemLinkUiBlocked() => Blocked;
        private static bool _itemLinkPointerLease;
        private static int _itemLinkLastFocusFrame = -10;
        private static string _itemLinkSavedDraft = "";
        private static int _itemLinkSavedAnchor, _itemLinkSavedFocus;
        internal static bool CursorPredicate(bool native = false) => KeepItemLinkChatCursorFree(native);
        internal static void Capture() => CaptureItemLinkChatInput(Chat.instance!);
    }
}
internal static class CursorChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(!InventorySlotsPlugin.CursorPredicate(), "no chat object does not request cursor");
        Chat.instance = new Chat();
        check(InventorySlotsPlugin.CursorPredicate(), "focused open chat releases cursor");
        InventorySlotsPlugin.Capture();
        Chat.instance.m_input.isFocused = false;
        check(!InventorySlotsPlugin.CursorPredicate(), "closed focus returns native capture");
        Chat.instance.m_input.isFocused = true; Chat.instance.m_input.isActiveAndEnabled = false;
        check(!InventorySlotsPlugin.CursorPredicate(), "inactive input with stale focus does not capture cursor");
        Chat.instance.m_input.isActiveAndEnabled = true;
        InventorySlotsPlugin.Enabled = false;
        check(!InventorySlotsPlugin.CursorPredicate(), "feature disabled leaves native capture");
        check(InventorySlotsPlugin.CursorPredicate(true), "native text dialog stays free even with feature disabled");
        InventorySlotsPlugin.Enabled = true;
        ZInput.Mouse = false;
        check(!InventorySlotsPlugin.CursorPredicate(), "controller-only mode keeps native capture");
        ZInput.Mouse = true;
        InventorySlotsPlugin.Blocked = true;
        check(!InventorySlotsPlugin.CursorPredicate(), "modal/search/dialog blockers stay authoritative");
        check(InventorySlotsPlugin.CursorPredicate(true), "native visible dialog remains true when item UI blocked");
        InventorySlotsPlugin.Blocked = false;
        Chat.instance.m_chatWindow!.activeInHierarchy = false;
        check(!InventorySlotsPlugin.CursorPredicate(), "hidden chat window does not release cursor");
        Chat.instance.m_chatWindow.activeInHierarchy = true;
        InventorySlotsPlugin.Capture();
        // Native Chat.Update / EventSystem can hide or deselect input before the camera.
        ++Time.frameCount; Input.Down = Input.Held = true;
        Chat.instance.m_input.isFocused = Chat.instance.m_input.isActiveAndEnabled = false;
        check(InventorySlotsPlugin.CursorPredicate(), "mouse-down after deselection keeps cursor for authorized click");
        Input.Down = false; Time.frameCount += 3;
        check(InventorySlotsPlugin.CursorPredicate(), "held click keeps cursor beyond original focused frame");
        Input.Held = false; Input.Up = true; ++Time.frameCount;
        check(InventorySlotsPlugin.CursorPredicate(), "mouse-up frame can finish tooltip button click");
        Input.Up = false; ++Time.frameCount;
        check(!InventorySlotsPlugin.CursorPredicate(), "frame after release returns native capture");
        Input.Down = Input.Held = true; Time.frameCount += 5;
        check(!InventorySlotsPlugin.CursorPredicate(), "ordinary world click cannot acquire stale chat lease");
        Input.Down = Input.Held = false;
        Chat.instance.m_chatWindow = null;
        check(!InventorySlotsPlugin.CursorPredicate(), "destroyed chat window leaves capture alone");
        Chat.instance = null;
        check(InventorySlotsPlugin.CursorPredicate(true), "native visible input survives missing chat");
    }
}
