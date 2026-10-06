using System;
using InventorySlots;
using UnityEngine;

namespace UnityEngine
{
    internal enum KeyCode { Mouse2, Escape, Return }
    internal struct Vector2
    {
        internal float x, y;
        internal Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    internal struct Rect
    {
        internal float xMin, yMin, width, height;
        internal float xMax => xMin + width;
        internal float yMax => yMin + height;
        internal Rect(float x, float y, float width, float height)
        { xMin = x; yMin = y; this.width = width; this.height = height; }
    }
    internal class Transform
    {
        internal Transform? parent;
        internal Canvas? OwnCanvas;
        internal T? GetComponentInParent<T>() where T : class =>
            OwnCanvas as T ?? parent?.GetComponentInParent<T>();
    }
    internal sealed class Canvas
    {
        internal bool overrideSorting;
        internal int sortingLayerID, sortingOrder;
        internal readonly Transform transform;
        internal Canvas() { transform = new Transform { OwnCanvas = this }; }
    }
    internal static class SortingLayer
    {
        // IDs are not priorities: prove order selection uses the layer's value.
        internal static int GetLayerValueFromID(int id) => id == 90 ? 1 : id == 10 ? 2 : 0;
    }
}
internal sealed class InventoryGui { internal Transform m_inventoryRoot = new(); }
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
    {
        private static bool _itemLinkPinLease;
        private static bool _itemLinkPinPressed;
        private static string? _pendingItemLinkPinKey;
        private static int _itemLinkPinRefocusFrame = -10;
        private static string _itemLinkPinDraft = "";
        private static int _itemLinkPinAnchor, _itemLinkPinFocus;
        private static Canvas? _itemLinkCanvas;
        private static string PointerTarget = "";
        private static int PinToggles;
        private static string FindItemLinkPointerTarget() => PointerTarget;
        private static void ToggleSharedItemPin(string key, ItemLinkSnapshot snapshot) => PinToggles++;

        internal static void RunUiChecks(Action<bool, string> check)
        {
            Chat.instance = new Chat();
            _itemLinkChat = Chat.instance;
            Enabled = true; Blocked = false; ZInput.Mouse = true;
            Time.frameCount = 500; Time.unscaledTime = 500;
            const string token = "abcdef0123456789";
            byte[] data = ItemLinkWire.Encode(new ItemLinkSnapshot { Label = "Hammer", Body = "Durability", Prefab = "Hammer" });
            ItemLinkStore.Entry entry = ItemLinks.Observe(7, token, "Hammer", Time.unscaledTime);
            ItemLinks.Request(entry, Time.unscaledTime);
            ItemLinks.BeginResponse(7, token, entry.Nonce, Time.unscaledTime);
            ItemLinks.Accept(7, token, entry.Nonce, data, Time.unscaledTime);
            PointerTarget = ItemLinkStore.Key(7, token);
            string markup = FormatItemLinkBody(7, ItemLinkWire.MarkerText("Hammer", token));
            check(markup == "<link=\"isitem:7:" + token + "\"><u>[Hammer]</u></link>", "Inline link inherits channel color/opacity and stays underlined");

            Chat.instance.m_input.text = "keep my draft";
            Chat.instance.m_input.selectionStringAnchorPosition = 2;
            Chat.instance.m_input.selectionStringFocusPosition = 8;
            CaptureItemLinkChatInput(Chat.instance);
            ++Time.frameCount;
            // EventSystem first: middle-click removes TMP focus before mod Update.
            Chat.instance.m_input.isFocused = false;
            Chat.instance.m_input.selectionStringAnchorPosition = Chat.instance.m_input.selectionStringFocusPosition = 0;
            ZInput.PinDown = ZInput.PinHeld = true;
            check(IsItemLinkInputBlocked(), "Gameplay focus query captures middle click after EventSystem deselection");
            check(CursorPredicate(), "Accepted middle click keeps cursor free");
            ConsumeItemLinkPin(); ConsumeItemLinkPin(); IsItemLinkInputBlocked(); ConsumeItemLinkPin();
            check(PinToggles == 1, "Multiple Update/focus polls toggle only once per press");
            ++Time.frameCount;
            IsItemLinkInputBlocked(); ConsumeItemLinkPin();
            check(PinToggles == 1, "Same InputSystem pressed edge in a later render frame does not toggle twice");
            ZInput.PinDown = false; ++Time.frameCount;
            RestoreItemLinkPinFocus();
            check(Chat.instance.m_input.ActivationRequested && Chat.instance.m_input.text == "keep my draft", "Refocus requests preserve text");
            Chat.instance.m_input.isFocused = true;
            ++Time.frameCount; RestoreItemLinkPinFocus();
            check(Chat.instance.m_input.selectionStringAnchorPosition == 2 && Chat.instance.m_input.selectionStringFocusPosition == 8,
                "Original selection restored after TMP activation");
            Chat.instance.m_input.isFocused = false;
            Time.frameCount += 3;
            check(IsItemLinkInputBlocked(), "Holding middle button cannot turn into a secondary attack");
            ZInput.PinHeld = false; ZInput.PinUp = true; ++Time.frameCount;
            check(IsItemLinkInputBlocked(), "Release frame remains reserved for UI");
            ZInput.PinUp = false; ++Time.frameCount;
            check(!IsItemLinkInputBlocked(), "Frame after release returns gameplay input");
            ZInput.PinDown = ZInput.PinHeld = true; ++Time.frameCount;
            check(!IsItemLinkInputBlocked(), "Visible old chat and stale focus cannot capture world attack");
            ZInput.PinDown = ZInput.PinHeld = false; IsItemLinkInputBlocked();

            // Mod first: capture while genuinely focused, then lose focus later.
            Chat.instance.m_input.isFocused = true;
            CaptureItemLinkChatInput(Chat.instance);
            ++Time.frameCount; ZInput.PinDown = ZInput.PinHeld = true;
            UpdateItemLinkPinInput();
            Chat.instance.m_input.isFocused = false;
            check(IsItemLinkInputBlocked(), "Mod-before-EventSystem ordering also protects gameplay");
            ConsumeItemLinkPin(); check(PinToggles == 2, "Second distinct press toggles once again");
            Chat.instance.m_input.isActiveAndEnabled = false;
            ++Time.frameCount; RestoreItemLinkPinFocus();
            check(_itemLinkPinRefocusFrame < 0, "Explicitly closed/submitted draft is not reopened");
            ZInput.PinDown = ZInput.PinHeld = false; ++Time.frameCount; IsItemLinkInputBlocked();
            Chat.instance.m_input.isActiveAndEnabled = Chat.instance.m_input.isFocused = true;
            PointerTarget = "";
            ++Time.frameCount; ZInput.PinDown = ZInput.PinHeld = true;
            check(!IsItemLinkInputBlocked(), "Middle click outside link or tooltip stays native");
            PointerTarget = ItemLinkStore.Key(7, token);
            ++Time.frameCount; Blocked = true;
            check(!IsItemLinkInputBlocked(), "Modal UI does not acquire a chat gesture");
            Blocked = false; Enabled = false; ++Time.frameCount;
            check(!IsItemLinkInputBlocked(), "Feature disabled clears gesture state");
            Enabled = true;
            check(!IsItemLinkInputBlocked(), "Re-enabling feature does not capture an already rejected press");
            ZInput.PinDown = ZInput.PinHeld = false; IsItemLinkInputBlocked();

            foreach (string reason in new[] { "Escape", "Enter", "Changed draft" })
            {
                Chat.instance.m_input.isFocused = true;
                Chat.instance.m_input.ActivationRequested = false;
                CaptureItemLinkChatInput(Chat.instance);
                ++Time.frameCount; ZInput.PinDown = ZInput.PinHeld = true;
                UpdateItemLinkPinInput(); ConsumeItemLinkPin();
                Chat.instance.m_input.isFocused = false;
                ++Time.frameCount; ZInput.PinDown = false;
                ZInput.Escape = reason == "Escape"; ZInput.Enter = reason == "Enter";
                if (reason == "Changed draft") Chat.instance.m_input.text = "newer text";
                RestoreItemLinkPinFocus();
                check(_itemLinkPinRefocusFrame < 0 && !Chat.instance.m_input.ActivationRequested,
                    reason + " cancels deferred refocus");
                ZInput.Escape = ZInput.Enter = ZInput.PinHeld = false;
                ++Time.frameCount; IsItemLinkInputBlocked();
            }

            Rect screen = new(-960, -540, 1920, 1080);
            Rect leftChat = new(-900, -200, 500, 500);
            Rect rightChat = new(400, -200, 500, 500);
            Vector2 small = new(200, 150), full = new(380, 500);
            Vector2 smallPoint = GetItemLinkHoverPosition(screen, leftChat, -120, 380, small);
            Vector2 fullPoint = GetItemLinkHoverPosition(screen, leftChat, -120, 380, full);
            check(smallPoint.x - small.x / 2 == leftChat.xMax + 18 && fullPoint.x - full.x / 2 == leftChat.xMax + 18,
                "Loading and loaded tooltip stay on the same chat edge");
            check(smallPoint.y + small.y / 2 == fullPoint.y + full.y / 2, "Height change preserves hover top anchor");
            Vector2 rightPoint = GetItemLinkHoverPosition(screen, rightChat, -120, 380, full);
            check(rightPoint.x + full.x / 2 == rightChat.xMin - 18, "Right-edge chat uses left-side space");
            Vector2 cramped = GetItemLinkHoverPosition(screen, new Rect(-800, -200, 1600, 500), -120, 380, full);
            check(cramped.x > 0, "Insufficient space chooses deterministic side before screen clamp");

            Canvas inventory = new() { overrideSorting = true, sortingOrder = 600 };
            Canvas chat = new() { overrideSorting = true, sortingOrder = 900 };
            InventoryGui gui = new() { m_inventoryRoot = inventory.transform };
            Chat.instance.m_chatWindow!.OwnCanvas = chat;
            _itemLinkCanvas = new Canvas();
            UpdateItemLinkCanvasOrder(gui);
            check(_itemLinkCanvas.sortingOrder == 901 && inventory.sortingOrder == 600 && chat.sortingOrder == 900,
                "Owned tooltip Canvas draws above chat without mutating native canvases");
            chat.sortingOrder = 599; UpdateItemLinkCanvasOrder(gui);
            check(_itemLinkCanvas.sortingOrder == 601, "Live Clan-adjusted chat order still respects inventory layer");
            chat.sortingLayerID = 10; inventory.sortingLayerID = 90; UpdateItemLinkCanvasOrder(gui);
            check(_itemLinkCanvas.sortingLayerID == 10, "Sorting layer priority wins over numeric ID/order");
            chat.sortingOrder = short.MaxValue; UpdateItemLinkCanvasOrder(gui);
            check(_itemLinkCanvas.sortingOrder == short.MaxValue, "Canvas order cannot wrap past Unity limit");
            Canvas inherited = new() { overrideSorting = false };
            inherited.transform.parent = chat.transform;
            check(GetItemLinkSortingCanvas(inherited) == chat, "Non-override child resolves its actual sorting ancestor");
            ItemLinks.Clear(); Chat.instance = null;
        }
    }
}
internal static class ItemLinkUiChecks
{
    internal static void Run(Action<bool, string> check) => InventorySlotsPlugin.RunUiChecks(check);
}
