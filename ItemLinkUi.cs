using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class SharedItemPanel
    {
        internal RectTransform? Panel;
        internal TMP_Text? Text;
        internal Image? Icon;
        internal TMP_Text? Hint;
        internal Button? Close;
        internal string Key = "", Displayed = "";
        internal ItemLinkSnapshot? Snapshot;
        internal ItemLinkSnapshot? DisplayedSnapshot;
        internal int Language = -1;
        internal bool Unavailable;
    }
    private static readonly List<SharedItemPanel> SharedItemPins = new();
    private static SharedItemPanel _sharedItemHover = new();
    private static RectTransform? _itemLinkUiRoot;
    private static Canvas? _itemLinkCanvas;
    private static string _itemLinkHoverAnchorKey = "";
    private static float _itemLinkHoverTopOffset;
    private static string? _pendingItemLinkMarker;
    private static int _pendingItemLinkFrame;
    private static bool _itemLinkFocusPending;
    private static int _itemLinkCaret;
    private static bool _itemLinkPointerLease;
    private static bool _itemLinkPinLease;
    private static bool _itemLinkPinPressed;
    private static string? _pendingItemLinkPinKey;
    private static int _itemLinkPinRefocusFrame = -10;
    private static string _itemLinkPinDraft = "";
    private static int _itemLinkPinAnchor, _itemLinkPinFocus;
    private static int _itemLinkLastFocusFrame = -10;
    private static float _itemLinkHoverKeepUntil;
    private static string _itemLinkSavedDraft = "";
    private static int _itemLinkSavedAnchor, _itemLinkSavedFocus;
    private static int _pendingItemLinkAnchor, _pendingItemLinkFocus;
    private static string _pendingItemLinkDraft = "";
    private static bool ItemLinkChatFocused => Chat.instance != null && Chat.instance.m_input != null &&
        Chat.instance.m_input.isActiveAndEnabled && Chat.instance.m_input.isFocused &&
        Chat.instance.m_chatWindow != null && Chat.instance.m_chatWindow.gameObject.activeInHierarchy;

    private static bool KeepItemLinkChatCursorFree(bool nativeTextInputVisible)
    {
        if (nativeTextInputVisible) return true;
        if (!ItemLinksEnabled || !ZInput.IsMouseActive() || Chat.instance == null ||
            Chat.instance.m_chatWindow == null || !Chat.instance.m_chatWindow.gameObject.activeInHierarchy ||
            ItemLinkUiBlocked()) return false;
        UpdateItemLinkPointerLease();
        UpdateItemLinkPinInput();
        // Chat.Update disables its input on Mouse0 down. Keep the pointer through
        // release so an already-authorized tooltip close/share click can finish.
        return ItemLinkChatFocused || _itemLinkPointerLease || _itemLinkPinLease;
    }

    private static void CaptureItemLinkChatInput(Chat chat)
    {
        UpdateItemLinkPointerLease();
        UpdateItemLinkPinInput();
        if (!chat.m_input.isFocused) return;
        _itemLinkLastFocusFrame = Time.frameCount;
        _itemLinkSavedDraft = chat.m_input.text;
        _itemLinkSavedAnchor = chat.m_input.selectionStringAnchorPosition;
        _itemLinkSavedFocus = chat.m_input.selectionStringFocusPosition;
    }

    private static void UpdateItemLinkPointerLease()
    {
        // EventSystem may deselect TMP before Chat.Update runs. The previous
        // focused frame still authorizes this click through mouse release.
        if (Input.GetMouseButtonDown(0) && (ItemLinkChatFocused || _itemLinkLastFocusFrame >= Time.frameCount - 1))
            _itemLinkPointerLease = true;
        if (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)) _itemLinkPointerLease = false;
    }

    internal static bool IsItemLinkInputBlocked()
    {
        UpdateItemLinkPinInput();
        return _itemLinkPinLease;
    }

    private static void UpdateItemLinkPinInput()
    {
        bool down = ZInput.GetKeyDown(KeyCode.Mouse2);
        bool held = ZInput.GetKey(KeyCode.Mouse2);
        // FixedUpdate can see the same InputSystem press in a later render frame.
        // Latch the physical press, including rejected clicks, until release.
        if (!held && !down) _itemLinkPinPressed = false;
        bool newPress = down && !_itemLinkPinPressed;
        if (down) _itemLinkPinPressed = true;
        if (!ItemLinksEnabled || !ZInput.IsMouseActive() || Chat.instance == null ||
            Chat.instance.m_input == null || Chat.instance.m_chatWindow == null ||
            !Chat.instance.m_chatWindow.gameObject.activeInHierarchy || ItemLinkUiBlocked())
        {
            _itemLinkPinLease = false;
            _pendingItemLinkPinKey = null;
            _itemLinkPinRefocusFrame = -10;
            return;
        }
        if (!held && !ZInput.GetKeyUp(KeyCode.Mouse2)) _itemLinkPinLease = false;
        if (!newPress) return;
        // Real TMP focus (or its immediately preceding frame) is required. Visible
        // old chat history alone must never turn an ordinary attack into a UI click.
        if (!Chat.instance.m_input.isActiveAndEnabled || ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetKeyDown(KeyCode.Return) ||
            !ItemLinkChatFocused && _itemLinkLastFocusFrame < Time.frameCount - 1) return;
        string key = FindItemLinkPointerTarget();
        if (key.Length == 0) return;
        _itemLinkPinLease = true;
        _pendingItemLinkPinKey = key;
        _itemLinkPinRefocusFrame = Time.frameCount;
        var input = Chat.instance.m_input;
        _itemLinkPinDraft = input.text;
        _itemLinkPinAnchor = ItemLinkChatFocused ? input.selectionStringAnchorPosition : _itemLinkSavedAnchor;
        _itemLinkPinFocus = ItemLinkChatFocused ? input.selectionStringFocusPosition : _itemLinkSavedFocus;
    }

    private static void RestoreItemLinkPinFocus()
    {
        if (_itemLinkPinRefocusFrame < 0 || Time.frameCount <= _itemLinkPinRefocusFrame) return;
        var input = Chat.instance?.m_input;
        // Let the click finish in EventSystem first. Never reopen a submitted or
        // explicitly closed draft, or replace text edited by another UI.
        if (input == null || Time.frameCount > _itemLinkPinRefocusFrame + 3 || !input.isActiveAndEnabled ||
            input.text != _itemLinkPinDraft || ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetKeyDown(KeyCode.Return))
        {
            _itemLinkPinRefocusFrame = -10;
            return;
        }
        if (!input.isFocused) input.ActivateInputField();
        else
        {
            input.selectionStringAnchorPosition = _itemLinkPinAnchor;
            input.selectionStringFocusPosition = _itemLinkPinFocus;
            _itemLinkPinRefocusFrame = -10;
        }
    }

    private static string FindItemLinkPointerTarget()
    {
        if (_sharedItemHover.Panel != null && _sharedItemHover.Panel.gameObject.activeInHierarchy &&
            RectContainsScreenPoint(_sharedItemHover.Panel, GetUiMousePosition())) return _sharedItemHover.Key;
        return FindHoveredItemLink();
    }

    private static string ItemLinkText(string key, string fallback) => LocalizeUi("$inventoryslots_itemlink_" + key, fallback);

    private static void EnsureItemShareButton(RectTransform panel, int slot)
    {
        PinnedTooltipPanelUiCache cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        if (cache.ItemShareButton == null)
        {
            cache.ItemShareButton = CreateItemLinkButton(panel, "ShareInChat", "", () => SharePinnedInventoryItem(slot));
            // A small outlined speech bubble, drawn from existing UI primitives.
            RectTransform icon = new GameObject("ChatIcon", typeof(RectTransform)).GetComponent<RectTransform>();
            icon.SetParent(cache.ItemShareButton.transform, false);
            SetCenteredRectLayout(icon, Vector2.zero, new Vector2(20, 18));
            ItemLinkIconLine(icon, new Vector2(-8, 6), new Vector2(8, 6));
            ItemLinkIconLine(icon, new Vector2(8, 6), new Vector2(8, -4));
            ItemLinkIconLine(icon, new Vector2(8, -4), new Vector2(0, -4));
            ItemLinkIconLine(icon, new Vector2(0, -4), new Vector2(-6, -8));
            ItemLinkIconLine(icon, new Vector2(-6, -8), new Vector2(-6, -4));
            ItemLinkIconLine(icon, new Vector2(-6, -4), new Vector2(-8, -4));
            ItemLinkIconLine(icon, new Vector2(-8, -4), new Vector2(-8, 6));
            UITooltip tip = cache.ItemShareButton.gameObject.AddComponent<UITooltip>();
            EnsureTooltipPrefab(tip);
            tip.m_topic = ItemLinkText("share", "Insert into chat");
            tip.m_text = ItemLinkText("share_help", "Add this tooltip to your chat draft. Press Enter to send; your current text is kept.");
        }
        cache.ItemShareButton.gameObject.SetActive(ItemLinksEnabled);
    }

    private static void ItemLinkIconLine(RectTransform parent, Vector2 from, Vector2 to)
    {
        RectTransform rect = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Vector2 delta = to - from;
        SetCenteredRectLayout(rect, (from + to) * 0.5f, new Vector2(delta.magnitude, 1.7f));
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        Image image = rect.GetComponent<Image>();
        image.color = new Color(1f, 0.83f, 0.42f); image.raycastTarget = false;
    }

    private static Button CreateItemLinkButton(RectTransform panel, string name, string label, UnityAction action)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-12, -12);
        rect.sizeDelta = new Vector2(30, 30);
        Image image = rect.GetComponent<Image>();
        image.sprite = GetSolidUiSprite(); image.color = new Color(0.24f, 0.21f, 0.15f, 0.95f);
        Button button = rect.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        if (label.Length > 0)
        {
            CreateTextRect("Label", rect, out TMP_Text text);
            ApplyDefaultFontAsset(text);
            text.text = label; text.fontSize = 19; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        }
        return button;
    }

    private static void SharePinnedInventoryItem(int slot)
    {
        if (!ItemLinksEnabled || Chat.instance == null || Player.m_localPlayer == null || ItemLinkUiBlocked()) return;
        ItemDrop.ItemData? item = PinnedTooltips.Inventory.Items[slot];
        TMP_Text? text = PinnedTooltips.Inventory.Texts[slot];
        RectTransform? extras = PinnedTooltips.Inventory.JewelcraftingTooltipRoots[slot];
        if (item?.m_shared == null || text == null || !text.gameObject.activeInHierarchy) return;
        string name = LocalizeUi(item.m_shared.m_name, item.m_shared.m_name);
        StringBuilder body = new(text.text);
        // Jewelcrafting's separate textual rows can be preserved without sending
        // custom data or constructing modded items on the receiver.
        if (extras != null && extras.gameObject.activeInHierarchy)
            foreach (TMP_Text extra in extras.GetComponentsInChildren<TMP_Text>())
                if (!string.IsNullOrWhiteSpace(extra.text)) body.Append('\n').Append(extra.text);
        ItemLinkSnapshot snapshot = new()
        {
            Prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "",
            Variant = Mathf.Max(0, item.m_variant),
            Label = ItemLinkWire.Plain(name + " ×" + item.m_stack + " · Q" + item.m_quality, 100),
            Body = ItemLinkWire.Rich(body.ToString())
        };
        try
        {
            byte[] data = ItemLinkWire.Encode(snapshot);
            string token = ItemLinkWire.NewToken();
            string marker = ItemLinkWire.MarkerText(snapshot.Label, token);
            var input = Chat.instance.m_input;
            bool saved = _itemLinkPointerLease && _itemLinkSavedDraft == input.text;
            int at = saved ? _itemLinkSavedAnchor : input.isFocused ? input.selectionStringAnchorPosition : input.text.Length;
            int to = saved ? _itemLinkSavedFocus : input.isFocused ? input.selectionStringFocusPosition : input.text.Length;
            if (!ItemLinkWire.InsertDraft(input.text, at, to, marker, input.characterLimit, out _, out _))
            {
                ItemLinkNotice("draft_full", "There is not enough room in the chat draft for this item link.");
                return;
            }
            ItemLinks.AddOffer(token, data, Time.unscaledTime);
            _pendingItemLinkMarker = marker;
            _pendingItemLinkFrame = Time.frameCount;
            _pendingItemLinkDraft = input.text;
            _pendingItemLinkAnchor = at;
            _pendingItemLinkFocus = to;
        }
        catch (InvalidDataException)
        {
            ItemLinkNotice("too_large", "This tooltip is too large to share in chat.");
        }
    }

    private static void ItemLinkNotice(string key, string fallback) =>
        Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ItemLinkText(key, fallback));

    private static void UpdateItemLinkDraft()
    {
        Chat chat = Chat.instance;
        if (_itemLinkFocusPending && chat.m_input.isFocused)
        {
            chat.m_input.selectionStringAnchorPosition = chat.m_input.selectionStringFocusPosition = _itemLinkCaret;
            _itemLinkFocusPending = false;
        }
        if (_pendingItemLinkMarker == null || Time.frameCount <= _pendingItemLinkFrame) return;
        string marker = _pendingItemLinkMarker;
        _pendingItemLinkMarker = null;
        var input = chat.m_input;
        // If another UI edited the draft after the click, keep its new text and
        // append; never restore an old draft over the user's input.
        int at = input.text == _pendingItemLinkDraft ? _pendingItemLinkAnchor : input.text.Length;
        int to = input.text == _pendingItemLinkDraft ? _pendingItemLinkFocus : input.text.Length;
        if (!ItemLinkWire.InsertDraft(input.text, at, to, marker, input.characterLimit, out string next, out int caret))
        {
            ItemLinkNotice("draft_full", "There is not enough room in the chat draft for this item link.");
            return;
        }
        chat.m_chatWindow.gameObject.SetActive(true);
        ItemLinkChatHideTimer(chat) = 0;
        input.gameObject.SetActive(true);
        input.text = next;
        input.ActivateInputField();
        _itemLinkCaret = caret;
        _itemLinkFocusPending = true;
    }

    private static void UpdateItemLinkShareButtons(bool visible)
    {
        UpdateItemLinkShareButtons(PinnedTooltips.Inventory.Panels, visible);
    }
    private static void UpdateItemLinkShareButtons(RectTransform?[] panels, bool visible)
    {
        foreach (RectTransform? panel in panels)
        {
            if (panel == null) continue;
            Button? button = panel.GetComponent<PinnedTooltipPanelUiCache>()?.ItemShareButton;
            if (button != null && button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
        }
    }

    private static bool ItemLinkUiBlocked()
    {
        InventoryGui? gui = InventoryGui.instance;
        Player? player = Player.m_localPlayer;
        return player == null || player.m_isLoading || player.IsDead() || ((Character)player).InCutscene() ||
               Menu.IsVisible() || global::Console.IsVisible() || TextInput.IsVisible() ||
               Minimap.IsOpen() || Minimap.InTextInput() || StoreGui.IsVisible() ||
               IsCraftingSearchFocused() || IsItemRuleInputBlocked() || IsControllerItemMenuOpen() ||
               TextViewer.instance != null && TextViewer.instance.IsVisible() ||
               UnifiedPopup.WasVisibleThisFrame() || PlayerCustomizaton.IsBarberGuiVisible() ||
               Hud.IsPieceSelectionVisible() || Hud.InRadial() ||
               (_inventoryTrashConfirmDialog != null && _inventoryTrashConfirmDialog.activeInHierarchy) ||
               gui != null && (gui.IsTextPanelOpen || gui.IsSkillsPanelOpen || gui.IsTrophisPanelOpen || gui.IsAchievementsPanelOpen ||
                   gui.m_splitDialog.gameObject.activeInHierarchy || gui.m_variantDialog.gameObject.activeInHierarchy);
    }

    private static void UpdateItemLinkUi()
    {
        UpdateItemLinkPointerLease();
        UpdateItemLinkPinInput();
        bool blocked = ItemLinkUiBlocked();
        UpdateItemLinkShareButtons(!blocked);
        if (blocked)
        {
            _pendingItemLinkMarker = null;
            if (_itemLinkUiRoot != null) _itemLinkUiRoot.gameObject.SetActive(false);
            return;
        }
        UpdateItemLinkDraft();
        RestoreItemLinkPinFocus();
        bool chatInteraction = ItemLinkChatFocused || _itemLinkPinLease;
        if (!chatInteraction && SharedItemPins.Count == 0)
        {
            if (_itemLinkUiRoot != null) _itemLinkUiRoot.gameObject.SetActive(false);
            return;
        }
        InventoryGui? gui = InventoryGui.instance;
        RectTransform? parent = gui?.m_inventoryRoot?.parent as RectTransform;
        if (parent == null) return;
        if (_itemLinkUiRoot == null || _itemLinkUiRoot.parent != parent)
        {
            DestroySharedItemPanels();
            _itemLinkUiRoot = new GameObject("InventorySlots_ItemLinks", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
            _itemLinkUiRoot.SetParent(parent, false);
            _itemLinkUiRoot.anchorMin = Vector2.zero; _itemLinkUiRoot.anchorMax = Vector2.one;
            _itemLinkUiRoot.offsetMin = _itemLinkUiRoot.offsetMax = Vector2.zero;
            _itemLinkCanvas = _itemLinkUiRoot.GetComponent<Canvas>();
            _itemLinkCanvas.overrideSorting = true;
        }
        _itemLinkUiRoot.gameObject.SetActive(true);
        UpdateItemLinkCanvasOrder(gui!);
        ConsumeItemLinkPin();
        string key = chatInteraction ? FindItemLinkPointerTarget() : "";
        if (key.Length > 0) _itemLinkHoverKeepUntil = Time.unscaledTime + 0.2f;
        else if (chatInteraction && Time.unscaledTime < _itemLinkHoverKeepUntil) key = _sharedItemHover.Key;
        ItemLinkStore.Entry? entry = key.Length > 0 ? ItemLinks.Find(key, Time.unscaledTime) : null;
        if (entry != null)
        {
            RequestItemLink(entry);
            _sharedItemHover.Key = key;
            _sharedItemHover.Snapshot = entry.Snapshot;
            _sharedItemHover.Unavailable = entry.Snapshot == null &&
                (!CanUseItemLinkEntry(entry) || entry.Attempts >= 3 && Time.unscaledTime - entry.Requested >= 4);
            ShowSharedItemPanel(_sharedItemHover, entry.Label, false);
            PositionSharedItemHover(_sharedItemHover.Panel!, key);
        }
        else if (key.Length > 0)
        {
            _sharedItemHover.Key = key;
            _sharedItemHover.Snapshot = null;
            _sharedItemHover.Unavailable = true;
            ShowSharedItemPanel(_sharedItemHover, ItemLinkText("link", "Item link"), false);
            PositionSharedItemHover(_sharedItemHover.Panel!, key);
        }
        else
        {
            _itemLinkHoverAnchorKey = "";
            if (_sharedItemHover.Panel != null) _sharedItemHover.Panel.gameObject.SetActive(false);
        }

        while (SharedItemPins.Count > GetActivePinnedTooltipSlotCount()) RemoveSharedItemPin(SharedItemPins[SharedItemPins.Count - 1]);
        bool regularPins = false;
        foreach (RectTransform? panel in PinnedTooltips.Inventory.Panels) regularPins |= panel != null && panel.gameObject.activeInHierarchy;
        foreach (RectTransform? panel in PinnedTooltips.Crafting.Panels) regularPins |= panel != null && panel.gameObject.activeInHierarchy;
        float panelWidth = SharedItemPanelMaxSize().x;
        int visiblePins = Mathf.Min(SharedItemPins.Count, Mathf.Max(1, Mathf.FloorToInt(
            (_itemLinkUiRoot.rect.width - 16 + PinnedTooltipFixedPanelGap) / (panelWidth + PinnedTooltipFixedPanelGap))));
        for (int i = 0; i < SharedItemPins.Count; i++)
        {
            SharedItemPanel pin = SharedItemPins[i];
            if (regularPins || i >= visiblePins) { if (pin.Panel != null) pin.Panel.gameObject.SetActive(false); continue; }
            ShowSharedItemPanel(pin, pin.Snapshot!.Label, true);
            PositionSharedItemPin(pin.Panel!, i, visiblePins, gui!);
            pin.Close!.interactable = ItemLinkChatFocused || InventoryGui.IsVisible() || _itemLinkPointerLease;
            if ((ItemLinkChatFocused || InventoryGui.IsVisible()) && RectContainsScreenPoint(pin.Panel!, GetUiMousePosition()))
                TryScrollPinnedTooltipPanel(pin.Panel!, i, GetUiScrollDelta(UiScrollInputMode.Continuous));
        }
        if (_sharedItemHover.Panel != null && _sharedItemHover.Panel.gameObject.activeSelf && chatInteraction)
        {
            _sharedItemHover.Panel.SetAsLastSibling();
            TryScrollPinnedTooltipPanel(_sharedItemHover.Panel, 0, GetUiScrollDelta(UiScrollInputMode.Continuous));
        }
    }

    private static void ConsumeItemLinkPin()
    {
        string? key = _pendingItemLinkPinKey;
        _pendingItemLinkPinKey = null;
        ItemLinkStore.Entry? entry = key != null ? ItemLinks.Find(key, Time.unscaledTime) : null;
        if (entry?.Snapshot != null) ToggleSharedItemPin(key!, entry.Snapshot);
    }

    private static Canvas? GetItemLinkSortingCanvas(Canvas? canvas)
    {
        while (canvas != null && !canvas.overrideSorting && canvas.transform.parent != null)
        {
            Canvas? parent = canvas.transform.parent.GetComponentInParent<Canvas>();
            if (parent == null) break;
            canvas = parent;
        }
        return canvas;
    }

    private static void UpdateItemLinkCanvasOrder(InventoryGui gui)
    {
        Canvas? inventory = GetItemLinkSortingCanvas(gui.m_inventoryRoot.GetComponentInParent<Canvas>());
        Canvas? chat = GetItemLinkSortingCanvas(Chat.instance?.m_chatWindow?.GetComponentInParent<Canvas>());
        Canvas? source = inventory;
        if (chat != null && (source == null ||
            SortingLayer.GetLayerValueFromID(chat.sortingLayerID) > SortingLayer.GetLayerValueFromID(source.sortingLayerID) ||
            chat.sortingLayerID == source.sortingLayerID && chat.sortingOrder > source.sortingOrder)) source = chat;
        if (source == null || _itemLinkCanvas == null) return;
        // Own only this Canvas. Clan and other chat mods can change their live
        // order independently; native menus/dialogs still hide this UI above.
        int order = Math.Min(short.MaxValue, source.sortingOrder + 1);
        if (_itemLinkCanvas.sortingLayerID != source.sortingLayerID) _itemLinkCanvas.sortingLayerID = source.sortingLayerID;
        if (_itemLinkCanvas.sortingOrder != order) _itemLinkCanvas.sortingOrder = order;
    }

    private static string FindHoveredItemLink()
    {
        TMP_Text output = Chat.instance.m_output;
        if (output == null || !output.gameObject.activeInHierarchy) return "";
        Camera? camera = output.canvas != null && output.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? output.canvas.worldCamera : null;
        Vector2 mouse = GetUiMousePosition();
        if (!RectTransformUtility.RectangleContainsScreenPoint(output.rectTransform, mouse, camera)) return "";
        foreach (RectMask2D mask in output.GetComponentsInParent<RectMask2D>())
            if (mask.isActiveAndEnabled && !RectTransformUtility.RectangleContainsScreenPoint(mask.rectTransform, mouse, camera)) return "";
        int link = TMP_TextUtilities.FindIntersectingLink(output, mouse, camera);
        if (link < 0 || link >= output.textInfo.linkCount) return "";
        string id = output.textInfo.linkInfo[link].GetLinkID();
        return id.StartsWith("isitem:", StringComparison.Ordinal) ? id.Substring(7) : "";
    }

    private static void ToggleSharedItemPin(string key, ItemLinkSnapshot snapshot)
    {
        SharedItemPanel? existing = SharedItemPins.Find(p => p.Key == key);
        if (existing != null) { RemoveSharedItemPin(existing); return; }
        SetPinnedTooltipContext(PinnedTooltipContext.None);
        if (SharedItemPins.Count >= GetActivePinnedTooltipSlotCount()) RemoveSharedItemPin(SharedItemPins[SharedItemPins.Count - 1]);
        SharedItemPins.Insert(0, new SharedItemPanel { Key = key, Snapshot = snapshot });
    }

    private static void RemoveSharedItemPin(SharedItemPanel pin)
    {
        if (pin.Panel != null) UnityEngine.Object.Destroy(pin.Panel.gameObject);
        SharedItemPins.Remove(pin);
    }

    private static void ShowSharedItemPanel(SharedItemPanel view, string label, bool pinned)
    {
        bool created = view.Panel == null;
        if (created)
        {
            view.Panel = EnsurePinnedTooltipPanel(_itemLinkUiRoot!, "ItemLink_" + (pinned ? view.Key : "Hover"), null);
            view.Icon = EnsurePinnedTooltipIcon(view.Panel);
            view.Text = EnsurePinnedTooltipBodyText(view.Panel, 16);
            CreateTextRect("ActionHint", view.Panel, out TMP_Text hint);
            ApplyDefaultFontAsset(hint);
            hint.fontSize = 13; hint.color = new Color(0.8f, 0.8f, 0.8f); hint.raycastTarget = false;
            SetTopLeftRectLayout(hint.rectTransform, new Vector2(100, -20), new Vector2(195, 68));
            view.Hint = hint;
            if (pinned) view.Close = CreateItemLinkButton(view.Panel, "Close", "×", () => RemoveSharedItemPin(view));
        }
        view.Panel!.gameObject.SetActive(true);
        ConfigurePinnedTooltipPanelBackground(view.Panel);
        string body = view.Snapshot?.Body ?? ("<color=#FFD36A>" + ItemLinkWire.Plain(label, 120) + "</color>\n" +
            (view.Unavailable ? ItemLinkText("unavailable", "Shared tooltip unavailable. The sender may be offline, sharing may be disabled, or the link may have expired.") :
                ItemLinkText("loading", "Loading shared tooltip…")));
        if (created || view.Displayed != body || view.DisplayedSnapshot != view.Snapshot || view.Language != _uiLocalizationVersion)
        {
            view.Displayed = body; view.Language = _uiLocalizationVersion;
            view.DisplayedSnapshot = view.Snapshot;
            view.Text!.text = body;
            view.Hint!.text = pinned ? ItemLinkText("close_help", "Open chat or inventory to close") :
                "[Mouse3] " + ItemLinkText("pin_action", "Pin / unpin");
            Sprite? sprite = null;
            if (view.Snapshot != null && ObjectDB.instance != null)
            {
                ItemDrop? drop = ObjectDB.instance.GetItemPrefab(view.Snapshot.Prefab)?.GetComponent<ItemDrop>();
                Sprite[]? icons = drop?.m_itemData?.m_shared?.m_icons;
                if (icons != null && view.Snapshot.Variant < icons.Length) sprite = icons[view.Snapshot.Variant];
            }
            view.Icon!.sprite = sprite; view.Icon.gameObject.SetActive(sprite != null);
            ResetPinnedTooltipTextScrollState(view.Panel);
        }
        ApplyPinnedTooltipDynamicTextLayout(view.Panel, view.Text!, 0, Vector2.zero, 102, 18,
            resetScroll: false, sizeLimit: SharedItemPanelMaxSize());
    }

    private static Vector2 SharedItemPanelMaxSize() => new(
        Mathf.Min(GetPinnedTooltipPanelSize().x, Mathf.Max(120, _itemLinkUiRoot!.rect.width - 16)),
        Mathf.Min(GetPinnedTooltipPanelSize().y, Mathf.Max(180, _itemLinkUiRoot!.rect.height - 32)));

    private static void PositionSharedItemHover(RectTransform panel, string key)
    {
        RectTransform parent = _itemLinkUiRoot!;
        Rect screen = parent.rect;
        if (!TryGetFeatureGuidePanelBounds(Chat.instance.m_chatWindow, parent, screen, out Rect chat)) chat = screen;
        if (_itemLinkHoverAnchorKey != key)
        {
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, GetUiMousePosition(), camera, out Vector2 mouse);
            _itemLinkHoverTopOffset = mouse.y + 18 - chat.yMax;
            _itemLinkHoverAnchorKey = key;
        }
        Vector2 size = panel.rect.size;
        Vector2 point = GetItemLinkHoverPosition(screen, chat, _itemLinkHoverTopOffset, SharedItemPanelMaxSize().x, size);
        SetCenteredRectLayout(panel, ClampSharedItemPanel(parent, point, size), size);
    }

    private static Vector2 GetItemLinkHoverPosition(Rect screen, Rect chat, float topOffset, float reservedWidth, Vector2 size)
    {
        float rightSpace = screen.xMax - chat.xMax - 26;
        float leftSpace = chat.xMin - screen.xMin - 26;
        bool right = rightSpace >= reservedWidth || leftSpace < reservedWidth && rightSpace >= leftSpace;
        // Reserve the maximum width even while loading, so receiving the actual
        // snapshot never flips the panel to the other side of the chat window.
        float x = right ? chat.xMax + 18 + size.x * 0.5f : chat.xMin - 18 - size.x * 0.5f;
        return new Vector2(x, chat.yMax + topOffset - size.y * 0.5f);
    }

    private static void PositionSharedItemPin(RectTransform panel, int slot, int visiblePins, InventoryGui gui)
    {
        RectTransform parent = _itemLinkUiRoot!;
        RefreshFeatureGuideLayout(gui);
        Rect screen = parent.rect;
        Rect crafting = TryGetFeatureGuidePanelBounds(gui.m_crafting, parent, screen, out Rect bounds) ? bounds : screen;
        Vector2 size = panel.rect.size;
        // Shift the entire row as a group rather than clamping each panel onto
        // its neighbour at the left edge of a narrow viewport.
        float rowWidth = visiblePins * size.x + (visiblePins - 1) * PinnedTooltipFixedPanelGap;
        float right = Mathf.Max(crafting.xMin + PinnedTooltipCraftingStartOffset, screen.xMin + 8 + rowWidth);
        float top = screen.yMax - 160f;
        if (TryGetDefaultEquipmentPanelBottomWorldY(gui, parent, out float equipmentBottom) &&
            gui.m_playerGrid.m_gridRoot is RectTransform gridRoot)
            top = equipmentBottom - parent.InverseTransformVector(GetFeatureGuideAnimationOffset(gridRoot)).y + PinnedTooltipTopOffset;
        Vector2 point = new(right - size.x * 0.5f - slot * (size.x + PinnedTooltipFixedPanelGap),
            top - size.y * 0.5f);
        SetCenteredRectLayout(panel, ClampSharedItemPanel(parent, point, size), size);
    }

    private static Vector2 ClampSharedItemPanel(RectTransform parent, Vector2 point, Vector2 size) => new(
        Mathf.Clamp(point.x, parent.rect.xMin + size.x * 0.5f + 8, Mathf.Max(parent.rect.xMin + size.x * 0.5f + 8, parent.rect.xMax - size.x * 0.5f - 8)),
        Mathf.Clamp(point.y, parent.rect.yMin + size.y * 0.5f + 8, Mathf.Max(parent.rect.yMin + size.y * 0.5f + 8, parent.rect.yMax - size.y * 0.5f - 8)));

    private static void HideItemLinkUi()
    {
        UpdateItemLinkShareButtons(false);
        if (_itemLinkUiRoot != null) _itemLinkUiRoot.gameObject.SetActive(false);
    }
    private static void DestroySharedItemPanels()
    {
        if (_itemLinkUiRoot != null) UnityEngine.Object.Destroy(_itemLinkUiRoot.gameObject);
        _itemLinkUiRoot = null;
        _itemLinkCanvas = null;
        _itemLinkHoverAnchorKey = "";
        SharedItemPins.Clear();
        _sharedItemHover = new SharedItemPanel();
    }
    private static void DestroyItemLinkUi()
    {
        _pendingItemLinkMarker = null;
        _itemLinkFocusPending = false;
        _itemLinkPointerLease = false;
        _itemLinkPinLease = false;
        _pendingItemLinkPinKey = null;
        _itemLinkPinPressed = false;
        _itemLinkPinRefocusFrame = -10;
        _itemLinkLastFocusFrame = -10;
        _itemLinkHoverKeepUntil = 0;
        _itemLinkSavedDraft = _pendingItemLinkDraft = "";
        DestroySharedItemPanels();
    }
}
