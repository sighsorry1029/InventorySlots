using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ItemData = ItemDrop.ItemData;

#if INVENTORY_SLOTS
using RulePlugin = InventorySlots.InventorySlotsPlugin;
using RuleTokens = InventorySlots.InventorySlotsConfigCore;
namespace InventorySlots;
#else
using RulePlugin = InventoryActions.InventoryActionsPlugin;
using RuleTokens = InventoryActions.RestockTargetLimitCore;
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    private enum InventoryButtonMode { Off, Auto, On }
    private static ConfigEntry<InventoryButtonMode> _restockButtonMode = null!;
    private static ConfigEntry<InventoryButtonMode> _autoPickupButtonMode = null!;
    private static ConfigEntry<InventoryButtonMode> _trashButtonMode = null!;
    private const string RuleButtonModeDescription = "Client-only display mode. Off hides the button without disabling saved rules. Auto shows its bottom edge and slides out only on hover; holding an item alone does not expand it. An open editor keeps its button expanded. Gamepad use expands Auto buttons. On always shows the full button. Changes apply immediately.";
    private const string TrashButtonModeDescription = "Client-only display mode. Off hides the trash button. Auto shows its bottom edge and slides out only on hover; holding an item alone does not expand it. Gamepad use expands Auto buttons. On always shows the full button. The server's Enable Inventory Trash Panel setting must also be On. Changes apply immediately.";
    private static ItemRuleEditor? _itemRuleEditor;
    private static int _itemRuleInputClosedFrame = -1;
    // Keep existing button visuals under the animated player grid during Hide.
    // This does not activate hidden buttons or keep their editor/input open.
    internal static bool IsInventoryPanelClosing(InventoryGui? gui)
    {
        if (gui == null || Player.m_localPlayer == null || Player.m_localPlayer.m_isLoading) return false;
        Animator? animator = _itemRuleEditor != null && _itemRuleEditor.Owner == gui
            ? _itemRuleEditor._animator : gui.GetComponent<Animator>();
        return animator != null && !animator.GetBool("visible");
    }
    internal static bool IsItemRuleInputBlocked() =>
        (_itemRuleEditor != null && _itemRuleEditor.Pinned) || _itemRuleInputClosedFrame == Time.frameCount;

    internal static bool IsItemRuleScrollBlocked() => IsItemRuleInputBlocked() || (_itemRuleEditor != null && _itemRuleEditor.OwnsPointer);

    // Called before vanilla InventoryGui.Update reads controller input. The
    // component's Update also calls this, with a frame guard for either order.
    internal static void UpdateItemRuleControllerInput() => _itemRuleEditor?.UpdateControllerInput();

    internal static void OpenControllerItemRules(bool restock) => _itemRuleEditor?.OpenController(restock);

    private static bool IsItemRuleButtonEnabled(bool restock) =>
        (restock ? _restockButtonMode : _autoPickupButtonMode)?.Value != InventoryButtonMode.Off;

    private static bool IsInventoryTrashButtonEnabled() =>
        _enableInventoryTrashPanel?.Value == Toggle.On && _trashButtonMode?.Value != InventoryButtonMode.Off;

    private static int GetItemRuleColumnsFromRight(bool restock) =>
        (IsInventoryTrashButtonEnabled() ? 1 : 0) +
        (restock && IsItemRuleButtonEnabled(false) ? 1 : 0);

    private static void UpdateItemRuleUi(InventoryGui gui, Vector3 gridOrigin, int visibleRows)
    {
        if (gui == null || _instance == null || !_instance.isActiveAndEnabled || IsDedicatedServer || gui.m_takeAllButton == null || gui.m_playerGrid.m_gridRoot == null) return;
        if (!IsItemRuleButtonEnabled(true) && !IsItemRuleButtonEnabled(false)) { _itemRuleEditor?.Hide(); return; }
        if (_itemRuleEditor == null || _itemRuleEditor.Owner != gui)
        {
            DestroyItemRuleUi();
            GameObject root = new(ModName + "_ItemRules", typeof(RectTransform));
            // Native inventory panels and dialogs share m_inventoryRoot. A GUI
            // canvas sibling would render above that entire subtree, including Split.
            Transform parent = gui.m_inventoryRoot != null ? gui.m_inventoryRoot : gui.transform;
            root.transform.SetParent(parent, false);
            if (gui.m_splitDialog != null && gui.m_splitDialog.transform.parent == parent)
                root.transform.SetSiblingIndex(gui.m_splitDialog.transform.GetSiblingIndex());
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _itemRuleEditor = root.AddComponent<ItemRuleEditor>();
            if (!_itemRuleEditor.Initialize(gui)) { DestroyItemRuleUi(); return; }
        }
        _itemRuleEditor.PositionToolbar(gridOrigin, visibleRows);
    }

    internal static void DestroyItemRuleUi(InventoryGui? owner = null)
    {
        if (_itemRuleEditor == null || (owner != null && _itemRuleEditor.Owner != owner)) return;
        ItemRuleEditor editor = _itemRuleEditor;
        _itemRuleEditor = null;
        editor.Close();
        Object.Destroy(editor.gameObject);
    }

    private static void ConfigureInventoryActionIcon(Button button, float buttonSize, Sprite sprite)
    {
        InventoryTrashButtonMarker marker = button.GetComponent<InventoryTrashButtonMarker>() ?? button.gameObject.AddComponent<InventoryTrashButtonMarker>();
        if (!marker.TextSuppressed)
        {
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = "";
                text.enabled = false;
            }

            foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                text.text = "";
                text.enabled = false;
            }

            marker.TextSuppressed = true;
        }

        if (marker.Icon == null || IsUnityNull(marker.Icon))
        {
            Transform existing = button.transform.Find((ModName + "_TrashIcon"));
            marker.Icon = existing != null ? existing.GetComponent<Image>() : null;
            if (marker.Icon == null || IsUnityNull(marker.Icon))
            {
                GameObject iconGo = new((ModName + "_TrashIcon"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform iconRect = (RectTransform)iconGo.transform;
                iconRect.SetParent(button.transform, false);
                marker.Icon = iconGo.GetComponent<Image>();
            }
        }

        float iconSize = Mathf.Max(18f, buttonSize * 0.58f);
        RectTransform rect = (RectTransform)marker.Icon!.transform;
        Vector2 center = new(0.5f, 0.5f);
        Vector2 size = new(iconSize, iconSize);
        // Compare the actual icon so external UI changes and a replaced icon
        // are repaired without allocating a layout signature each update.
        if (rect.parent == button.transform &&
            rect.anchorMin == center && rect.anchorMax == center && rect.pivot == center &&
            rect.anchoredPosition == Vector2.zero && rect.sizeDelta == size &&
            rect.localScale == Vector3.one && rect.localRotation == Quaternion.identity &&
            marker.Icon.sprite == sprite && marker.Icon.preserveAspect && !marker.Icon.raycastTarget)
        {
            return;
        }

        if (rect.parent != button.transform)
        {
            rect.SetParent(button.transform, false);
        }

        rect.anchorMin = center;
        rect.anchorMax = center;
        rect.pivot = center;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        marker.Icon.sprite = sprite;
        marker.Icon.preserveAspect = true;
        marker.Icon.raycastTarget = false;
    }

    private static void SetInventoryActionIconVisual(Button button, bool acceptsHeldItem)
    {
        InventoryTrashButtonMarker? marker = button.GetComponent<InventoryTrashButtonMarker>();
        if (marker?.Icon != null && !IsUnityNull(marker.Icon))
        {
            Color color = acceptsHeldItem ? new Color(1f, 0.82f, 0.55f, 1f) : new Color(0.75f, 0.75f, 0.75f, 0.65f);
            if (marker.Icon.color != color)
            {
                marker.Icon.color = color;
            }
        }
    }

    private static Vector3 CalculateInventoryBottomButtonPosition(int columns, int rows, float spacing, float size, int columnsFromRight) =>
        new((Mathf.Max(0, columns - 1 - columnsFromRight) + 0.5f) * spacing - size * 0.5f,
            -Mathf.Max(1, rows) * spacing - 8f, 0f);

    // One GUI-owned component owns the toolbar and event listeners. Quantity
    // saves keep the existing rows and input focus; list changes rebuild rows.
    private sealed class ItemRuleEditor : MonoBehaviour
    {
        private const float RowHeight = 38f;
        private const float PopupWidth = 300f;
        private const float PopupChromeHeight = 116f;
        internal InventoryGui Owner = null!;
        internal bool Pinned { get; private set; }
        private RectTransform _root = null!, _toolbar = null!, _popup = null!, _content = null!, _viewport = null!;
        private Button _restockButton = null!, _excludeButton = null!, _backdrop = null!;
        private Sprite _restockIcon = null!, _excludeIcon = null!;
        private Sprite? _ruleButtonSprite;
        private int _visibleRowCount = 1;
        private TMP_Text _title = null!, _scope = null!, _status = null!;
        private TMP_FontAsset _font = null!;
        private Material _fontMaterial = null!;
        private readonly List<Button> _rowButtons = new();
        private readonly List<TMP_InputField> _fields = new();
        private readonly List<ControllerRow> _controllerRows = new();
        private int _controllerRowIndex;
        private int _controllerInputFrame = -1;
        private bool _controllerActive, _controllerStatus;
        private string _mouseScope = "";
        private readonly List<Sprite> _ownedIcons = new();
        private readonly Dictionary<string, ItemData?> _resolved = new(StringComparer.Ordinal);
        private List<ItemRuleConfigCore.Entry> _entries = new();
        private ItemRuleConfigCore.Entry? _registration;
        private string _snapshot = "";
        private bool _restock = true;
        private int _registrationMax;
        private float _hoverStarted = -1f, _outsideStarted = -1f;
        private bool? _hoverMode;
        private Camera? _camera;
        internal Animator? _animator;
        private static readonly int VisibleParameter = Animator.StringToHash("visible");
        private ScrollRect _scroll = null!;
        private readonly Color _text = new(1f, 0.94f, 0.8f);
        private readonly Color _gold = new(1f, 0.8f, 0.36f);

        private sealed class ControllerRow
        {
            internal ItemRuleConfigCore.Entry Entry = null!;
            internal Image Background = null!;
            internal TMP_InputField? Quantity;
            internal Button? Mode;
            internal int MaximumAmount = int.MaxValue;
        }

        private ConfigEntry<string> Setting => _restock ? _restockTargetStackLimitsConfig : _autoPickupExcludedItemsConfig;
        private bool Open => _popup != null && _popup.gameObject.activeSelf;
        internal bool IsPopupOpenFor(bool restock) => Open && _restock == restock;
        // InventoryGui.IsVisible intentionally lags Hide by up to two frames.
        // Use the same Animator obtained by vanilla Awake through the public API.
        private bool CanShow => _instance != null && _instance.isActiveAndEnabled && Owner != null && InventoryGui.IsVisible() &&
            (_animator == null || _animator.GetBool(VisibleParameter)) && !Menu.IsVisible() && !global::Console.IsVisible() && CanShowItemRules(Owner);
        private bool HasBlockingDialog => IsDialogActive(Owner.m_splitDialog) ||
            IsDialogActive(Owner.m_variantDialog) || IsDialogActive(Owner.m_skillsDialog) ||
            IsDialogActive(Owner.m_textsDialog) || IsDialogActive(Owner.m_achievementsPanel) ||
            (Owner.m_trophiesPanel != null && Owner.m_trophiesPanel.activeInHierarchy) ||
#if INVENTORY_SLOTS
            (_inventoryTrashConfirmDialog != null && _inventoryTrashConfirmDialog.activeInHierarchy);
#else
            (Runtime.TrashConfirmDialog != null && Runtime.TrashConfirmDialog.activeInHierarchy);
#endif
        private static bool IsDialogActive(Component? dialog) => dialog != null && dialog.gameObject.activeInHierarchy;
        private string L(string key, string fallback) => LocalizeUi("$" + ModName.ToLowerInvariant() + "_rules_" + key, fallback);

        internal bool Initialize(InventoryGui gui)
        {
            Owner = gui;
            _animator = gui.GetComponent<Animator>();
            _root = (RectTransform)transform;
            _camera = gui.GetComponentInParent<Canvas>()?.worldCamera;
            TMP_Text? fontSource = gui.m_takeAllButton.GetComponentInChildren<TMP_Text>(true);
            if (fontSource == null || fontSource.font == null)
                fontSource = gui.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.font != null);
            if (fontSource == null) return false; // Wait until the game's own font is ready.
            _font = fontSource.font;
            _fontMaterial = fontSource?.fontSharedMaterial ?? _font.material;
            // Keep the inventory buttons in the same player-grid render phase as
            // the trash button. The popup stays on m_inventoryRoot so dialogs and
            // screen-edge clamping keep their existing behavior.
            _toolbar = Rect("Toolbar", gui.m_playerGrid.m_gridRoot, Vector2.zero, Vector2.zero);
            _restockButton = RuleButton(ModName + "_RestockRules", true);
            _excludeButton = RuleButton(ModName + "_AutoPickupRules", false);
            _ruleButtonSprite = _restockButton.image.sprite;
            _restockButton.gameObject.AddComponent<UIDragHandler>().m_onReleasedOn = _ => ClickTool(true);
            _excludeButton.gameObject.AddComponent<UIDragHandler>().m_onReleasedOn = _ => ClickTool(false);
            _restockIcon = CreateRuleIcon(true); _excludeIcon = CreateRuleIcon(false);
            _backdrop = Button(_root, "Outside", "", Close);
            Stretch((RectTransform)_backdrop.transform);
            _backdrop.GetComponent<Image>().color = Color.clear;
            _backdrop.gameObject.SetActive(false);
            _popup = Rect("Popup", _root, new Vector2(PopupWidth, 160f), Vector2.zero);
            Image background = _popup.gameObject.AddComponent<Image>();
            background.color = new Color(0.15f, 0.13f, 0.14f, 0.99f);
            ApplyWoodenPanelStyle(background);
            _title = Text(_popup, "Title", "", 19f);
            Frame(_title.rectTransform, 12, -10, PopupWidth - 24, 28);
            _scope = Text(_popup, "Scope", "", 13f);
            Frame(_scope.rectTransform, 12, -39, PopupWidth - 24, 34);
            _viewport = Rect("Viewport", _popup, new Vector2(PopupWidth - 24, 38), new Vector2(12, -78));
            _viewport.gameObject.AddComponent<Image>().color = Color.clear;
            _viewport.gameObject.AddComponent<RectMask2D>();
            _content = Rect("Content", _viewport, new Vector2(PopupWidth - 24, 38), Vector2.zero);
            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport; _scroll.content = _content;
            _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false; _scroll.scrollSensitivity = 30f;
            _status = Text(_popup, "Status", "", 12f);
            _popup.gameObject.SetActive(false);
            return true;
        }

        internal void PositionToolbar(Vector3 gridOrigin, int visibleRows)
        {
            if (!CanShow) { EndInteraction(); return; }
            if (Open && (!IsItemRuleButtonEnabled(_restock) || HasBlockingDialog)) Close();
            InventoryGrid grid = Owner.m_playerGrid;
            RectTransform buttonRoot = grid.m_gridRoot;
            if (_toolbar.parent != buttonRoot) _toolbar.SetParent(buttonRoot, false);
            float size = Mathf.Clamp(Mathf.Max(1f, grid.m_elementSpace) * 0.72f, 42f, 58f);
            // The slide content uses grid-local coordinates. Do not convert via
            // world space here: that would cancel its animated offset.
            _toolbar.localPosition = Vector3.zero;
            Place(_restockButton, true);
            Place(_excludeButton, false);
            void Place(Button button, bool restock)
            {
                bool enabled = IsItemRuleButtonEnabled(restock);
                button.gameObject.SetActive(enabled);
                if (!enabled) return;
                InventorySlideButton kind = restock ? InventorySlideButton.Restock : InventorySlideButton.Exclude;
                RectTransform content = EnsureInventoryButtonSlide(Owner, gridOrigin, visibleRows, kind, _toolbar);
                if (button.transform.parent != content) button.transform.SetParent(content, false);
                Frame((RectTransform)button.transform, 0, 0, size, size);
                int columns = Mathf.Max(1, grid.m_inventory != null ? grid.m_inventory.GetWidth() : 8);
                Vector3 position = gridOrigin + CalculateInventoryBottomButtonPosition(columns, visibleRows, Mathf.Max(1f, grid.m_elementSpace), size, GetItemRuleColumnsFromRight(restock));
                button.transform.localPosition = position;
            }
            ConfigureInventoryActionIcon(_restockButton, size, _restockIcon);
            ConfigureInventoryActionIcon(_excludeButton, size, _excludeIcon);
            bool acceptsHeldItem = CanRegisterHeldItem();
            UpdateRuleButtonVisual(_restockButton, acceptsHeldItem);
            UpdateRuleButtonVisual(_excludeButton, acceptsHeldItem);
            _toolbar.gameObject.SetActive(true);
            if (Open) PositionPopup();
        }

        internal void PositionPopup()
        {
            // Follow the selected button as rows or enabled buttons change.
            // Prefer a downward dropdown; shrink the list before screen-edge clamping.
            Rect bounds = _root.rect;
            RectTransform button = (RectTransform)(_restock ? _restockButton : _excludeButton).transform;
            Vector3 anchor = _root.InverseTransformPoint(button.TransformPoint(new Vector3(button.rect.xMax, button.rect.yMin)));
            float x = anchor.x - _popup.rect.width;
            float y = anchor.y - 6f;
            float available = y - bounds.yMin - 8f;
            float viewHeight = Mathf.Min(Mathf.Min(6, _visibleRowCount) * RowHeight, Mathf.Max(RowHeight, available - PopupChromeHeight));
            ResizePopupViewport(viewHeight);
            x = Mathf.Clamp(x, bounds.xMin + 8, Mathf.Max(bounds.xMin + 8, bounds.xMax - _popup.rect.width - 8));
            // Extreme offsets can leave less than one editable row and its buttons.
            // Keep the editable row and status reachable at the screen edge.
            y = Mathf.Clamp(y, bounds.yMin + _popup.rect.height + 8, Mathf.Max(bounds.yMin + _popup.rect.height + 8, bounds.yMax - 8));
            _popup.localPosition = new Vector3(x, y, 0);
        }

        private void ResizePopupViewport(float height)
        {
            if (Mathf.Abs(_viewport.rect.height - height) < 0.1f) return;
            float inner = _popup.rect.width - 24f;
            _popup.sizeDelta = new Vector2(_popup.rect.width, PopupChromeHeight + height);
            Frame(_viewport, 12, -78, inner, height);
            Frame(_status.rectTransform, 12, -82 - height, inner, 30);
            Vector2 scroll = _content.anchoredPosition;
            scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, _content.rect.height - height));
            _content.anchoredPosition = scroll;
        }

        private bool CanRegisterHeldItem()
        {
            Player? player = Player.m_localPlayer;
            ItemData? item = Owner.m_dragItem;
            return HasHeldTrashCandidate(Owner) && player != null && item?.m_shared != null && item.m_dropPrefab != null &&
                Owner.m_dragInventory == ((Humanoid)player).GetInventory() && Owner.m_dragInventory.ContainsItem(item) &&
                (Owner.m_splitDialog == null || !Owner.m_splitDialog.IsActive);
        }

        private void UpdateRuleButtonVisual(Button button, bool acceptsHeldItem)
        {
            // The icons share trash's appearance, not its hotbar/favorite deletion guards.
            SetInventoryActionIconVisual(button, acceptsHeldItem);
            // Vanilla uses SpriteSwap. Reuse trash's idle background while leaving
            // hover/press feedback and empty-handed list access enabled.
            Image background = button.image;
            Sprite? sprite = !acceptsHeldItem && button.spriteState.disabledSprite != null
                ? button.spriteState.disabledSprite : _ruleButtonSprite;
            if (background.sprite != sprite) background.sprite = sprite;
        }

        private Button RuleButton(string name, bool restock)
        {
            Button button = EnsureActionButton(_toolbar, Owner.m_takeAllButton, name, "", () => ClickTool(restock))!;
            // Retain the same native background hierarchy as trash, with only our action.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => ClickTool(restock));
            button.interactable = true;
            // Cloning Take All also clones its controller shortcut. The rule
            // buttons must not react to JoyLStick or consume vanilla's input.
            foreach (UIGamePad shortcut in button.GetComponentsInChildren<UIGamePad>(true))
            {
                shortcut.enabled = false;
                if (shortcut.m_hint != null && shortcut.m_hint.transform.IsChildOf(button.transform)) shortcut.m_hint.SetActive(false);
            }
            UITooltip? tooltip = button.GetComponent<UITooltip>();
            if (tooltip != null) tooltip.enabled = false; // The rule popup supplies the hover UI.
            return button;
        }

        private void ApplyWoodenPanelStyle(Image target)
        {
            // Original Valheim 1.0.12's active split-dialog background: woodpanel_512x512 / litpanel.
            Image? source = Owner.m_splitDialog != null ? Owner.m_splitDialog.transform.Find("win_bkg/border (1)")?.GetComponent<Image>() : null;
            if (source == null || source.sprite == null) source = Owner.m_player != null ? Owner.m_player.Find("Bkg")?.GetComponent<Image>() : null;
            if (source == null || source.sprite == null) source = Owner.m_crafting != null ? Owner.m_crafting.Find("Bkg")?.GetComponent<Image>() : null;
            if (source == null || source.sprite == null) return;
            target.sprite = source.sprite; target.type = source.type;
            target.material = source.material; target.color = source.color;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.fillCenter = source.fillCenter; target.preserveAspect = source.preserveAspect;
        }

        internal bool OwnsPointer => Open && Contains(_popup);

        private bool Contains(RectTransform rect) => RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, _camera);

        internal void OpenController(bool restock)
        {
            ClickTool(restock);
            if (!Open || !Pinned) return;
            // The shortcut which opened the editor must not edit its first row.
            _controllerInputFrame = Time.frameCount;
            SetControllerActive(ZInput.IsExclusiveGamepadActive());
        }

        internal void UpdateControllerInput()
        {
            if (_controllerInputFrame == Time.frameCount) return;
            _controllerInputFrame = Time.frameCount;
            if (!Open || !Pinned) return;
            if (!CanShow || Player.m_localPlayer == null || FavoriteMemoryAccess.IsLoading(Player.m_localPlayer) ||
                HasBlockingDialog || !IsItemRuleButtonEnabled(_restock))
            { Close(); return; }
            SetControllerActive(ZInput.IsExclusiveGamepadActive());
            if (!_controllerActive) return;
            // We own A/Submit here. Selecting Unity Buttons at the same time
            // would allow the EventSystem to execute their click a second time.
            ClearControllerSubmitTarget();
            if (TakeControllerButton("JoyButtonB")) { Close(); return; }
            if (!string.Equals(_snapshot, Setting.Value, StringComparison.Ordinal)) LoadList(_restock, true);
            if (_controllerRows.Count == 0) return;
            bool up = IsControllerDirectionDown("JoyDPadUp", "JoyLStickUp");
            bool down = IsControllerDirectionDown("JoyDPadDown", "JoyLStickDown");
            if (up != down)
            {
                _controllerRowIndex = Mathf.Clamp(_controllerRowIndex + (down ? 1 : -1), 0, _controllerRows.Count - 1);
                RefreshControllerSelection();
                return;
            }
            ControllerRow row = _controllerRows[_controllerRowIndex];
            if (TakeControllerButton("JoyButtonX"))
            {
                RemoveEntry(row.Entry);
                return;
            }
            if (TakeControllerButton("JoyButtonA") && row.Mode != null)
            {
                RestockRuleMode previous = row.Entry.Mode;
                row.Mode.onClick.Invoke();
                RefreshControllerSelection(updateStatus: row.Entry.Mode != previous);
                return;
            }
            bool left = IsControllerDirectionDown("JoyDPadLeft", "JoyLStickLeft");
            bool right = IsControllerDirectionDown("JoyDPadRight", "JoyLStickRight");
            if (_restock && left != right && row.Quantity != null &&
                int.TryParse(row.Entry.Amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int current))
            {
                int next = (int)Math.Min(row.MaximumAmount, Math.Max(1L, (long)current + (right ? 1 : -1)));
                if (next == current) return;
                string previous = row.Entry.Amount;
                row.Entry.Amount = next.ToString(CultureInfo.InvariantCulture);
                bool saved = Save();
                if (!saved) row.Entry.Amount = previous;
                row.Quantity.SetTextWithoutNotify(row.Entry.Amount);
                RefreshControllerSelection(updateStatus: saved);
            }
        }

        private static bool TakeControllerButton(string button)
        {
            if (!ZInput.GetButtonDown(button)) return false;
            ZInput.ResetButtonStatus(button);
            // InventoryGui.Update can read a semantic alias of the same face
            // button before consulting its gamepad groups (and without Chat).
            ZInput.ResetButtonStatus("Inventory");
            ZInput.ResetButtonStatus("Use");
            ZInput.ResetButtonStatus("JoyUse");
            return true;
        }

        private static bool IsControllerDirectionDown(string dpad, string stick)
        {
            // Vanilla registers both at a 0.3-second delay / 0.1-second repeat.
            // ResetButtonStatus clears their held state and stops that repeat.
            // Pinned blocks grid navigation and we clear EventSystem selection.
            return ZInput.GetButtonDown(dpad) || ZInput.GetButtonDown(stick);
        }

        private void ClearControllerSubmitTarget()
        {
            GameObject? selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && (selected.transform.IsChildOf(Owner.transform) || selected.transform.IsChildOf(_root)))
                EventSystem.current!.SetSelectedGameObject(null);
        }

        private void SetControllerActive(bool active)
        {
            if (_controllerActive == active) return;
            _controllerActive = active;
            bool hadFocusedField = false;
            if (active)
            {
                foreach (TMP_InputField field in _fields)
                    if (field != null && field.isFocused)
                    {
                        hadFocusedField = true;
                        field.DeactivateInputField();
                    }
                ClearControllerSubmitTarget();
            }
            else if (_controllerStatus)
            {
                _status.text = "";
                _controllerStatus = false;
            }
            // Deactivation can run onEndEdit and report a failed save. Keep
            // that message instead of replacing it with the selected mode.
            RefreshControllerSelection(updateStatus: !hadFocusedField);
        }

        private void RefreshControllerSelection(bool updateStatus = true)
        {
            _controllerRowIndex = Mathf.Clamp(_controllerRowIndex, 0, Mathf.Max(0, _controllerRows.Count - 1));
            for (int i = 0; i < _controllerRows.Count; i++)
                _controllerRows[i].Background.color = _controllerActive && i == _controllerRowIndex
                    ? new Color(0.95f, 0.70f, 0.24f, 0.20f) : Color.clear;
            bool korean = string.Equals(Localization.instance?.GetSelectedLanguage(), "Korean", StringComparison.OrdinalIgnoreCase);
            _scope.text = !_controllerActive ? _mouseScope : (_restock
                ? L("pad_restock", korean ? "위/아래: 항목 · 좌/우: 수량\n{mode}: 모드 · {remove}: 삭제 · {close}: 닫기"
                    : "Up/Down: item · Left/Right: quantity\n{mode}: mode · {remove}: remove · {close}: close")
                : L("pad_exclude", korean ? "위/아래: 항목 · {remove}: 삭제 · {close}: 닫기"
                    : "Up/Down: item · {remove}: remove · {close}: close"))
                .Replace("{mode}", GetInventoryControllerActionDisplay("JoyButtonA"))
                .Replace("{remove}", GetInventoryControllerActionDisplay("JoyButtonX"))
                .Replace("{close}", GetInventoryControllerActionDisplay("JoyButtonB"));
            if (!_controllerActive || _controllerRows.Count == 0) return;
            if (updateStatus)
            {
                ControllerRow row = _controllerRows[_controllerRowIndex];
                _status.text = _restock ? GetRestockModeTitle(row.Entry.Mode) : row.Entry.Key;
                _controllerStatus = true;
            }
            float top = _controllerRowIndex * RowHeight;
            float offset = _content.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + RowHeight > offset + _viewport.rect.height) offset = top + RowHeight - _viewport.rect.height;
            _scroll.velocity = Vector2.zero;
            Vector2 position = _content.anchoredPosition;
            position.y = Mathf.Clamp(offset, 0, Mathf.Max(0, _content.rect.height - _viewport.rect.height));
            _content.anchoredPosition = position;
        }

        private void Update()
        {
            if (!CanShow || Player.m_localPlayer == null || Player.m_localPlayer.m_isLoading)
            { EndInteraction(); return; }
            if (Open && !IsItemRuleButtonEnabled(_restock)) Close();
            if (!IsItemRuleButtonEnabled(true) && !IsItemRuleButtonEnabled(false)) { Hide(); return; }
            // Hover uses screen coordinates, so native modal raycasts alone do
            // not stop it from reopening a popup behind the dialog.
            if (HasBlockingDialog)
            { Close(); return; }
            UpdateControllerInput();
            if (Open && Pinned && (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB")))
            { Close(); return; }
            if (Open && !string.Equals(_snapshot, Setting.Value, StringComparison.Ordinal) && !_fields.Any(field => field.isFocused)) LoadList(_restock, Pinned);
            bool? hovered = IsPointerOverSlide(InventorySlideButton.Restock) && IsItemRuleButtonEnabled(true) && _restockButton.gameObject.activeInHierarchy && Contains((RectTransform)_restockButton.transform) ? true
                : IsPointerOverSlide(InventorySlideButton.Exclude) && IsItemRuleButtonEnabled(false) && _excludeButton.gameObject.activeInHierarchy && Contains((RectTransform)_excludeButton.transform) ? false : null;
            if (Pinned) return;
            if (HasHeldTrashCandidate(Owner)) { if (Open) Close(); return; }
            if (hovered != _hoverMode) { _hoverMode = hovered; _hoverStarted = Time.unscaledTime; }
            if (hovered.HasValue)
            {
                _outsideStarted = -1f;
                if (Time.unscaledTime - _hoverStarted >= 0.3f && (!Open || _restock != hovered.Value)) LoadList(hovered.Value, false);
            }
            else if (Open && Contains(_popup)) _outsideStarted = -1f;
            else if (Open)
            {
                if (_outsideStarted < 0) _outsideStarted = Time.unscaledTime;
                if (Time.unscaledTime - _outsideStarted >= 0.25f) Close();
            }
        }

        private void ClickTool(bool restock)
        {
            if (!CanShow || HasBlockingDialog || !IsItemRuleButtonEnabled(restock)) return;
            if (HasHeldTrashCandidate(Owner)) { RegisterHeldItem(restock); return; }
            LoadList(restock, true);
        }

        private void LoadList(bool restock, bool pin)
        {
            if (!CanShow || HasBlockingDialog || !IsItemRuleButtonEnabled(restock)) return;
            CloseInventoryTrashConfirmDialog();
            if (_restock != restock) { _controllerRowIndex = 0; _controllerRows.Clear(); }
            _restock = restock; _snapshot = Setting.Value;
            _entries = ItemRuleConfigCore.Read(_snapshot, restock);
            _resolved.Clear(); _registration = null;
            Pinned = pin;
            _popup.gameObject.SetActive(true); _backdrop.gameObject.SetActive(pin);
            Render();
        }

        private void RegisterHeldItem(bool restock)
        {
            Player? player = Player.m_localPlayer;
            ItemData? item = Owner.m_dragItem;
            if (!CanRegisterHeldItem() || player == null || item?.m_shared == null || item.m_dropPrefab == null) return;
            string key = ItemRuleConfigCore.PrefabKey(item.m_dropPrefab.name);
            if (key.Length == 0) return;
            LoadList(restock, true);
            ItemRuleConfigCore.Entry? entry = _entries.LastOrDefault(e => restock
                ? RuleTokens.NormalizeResourceToken(e.Key) == RuleTokens.NormalizeResourceToken(key)
                : string.Equals(ItemRuleConfigCore.PrefabKey(e.Key), key, StringComparison.OrdinalIgnoreCase));
            bool added = entry == null;
            if (entry == null)
            {
                ItemRuleConfigCore.Entry? effective = null;
                if (restock)
                    foreach (string? token in GetRestockTargetLookupTokens(item))
                    {
                        string normalized = RuleTokens.NormalizeResourceToken(token);
                        effective = _entries.LastOrDefault(e => RuleTokens.NormalizeResourceToken(e.Key) == normalized);
                        if (effective != null) break;
                    }
                entry = new ItemRuleConfigCore.Entry
                {
                    Start = -1, Key = key, Mode = effective?.Mode ?? RestockRuleMode.Existing,
                    Amount = RestockTargetLimitCore.ClampAmountForEditor(effective?.Amount ?? item.m_shared.m_maxStackSize.ToString(CultureInfo.InvariantCulture), item.m_shared.m_maxStackSize)
                };
                _entries.Add(entry);
            }
            _resolved[entry.Key] = item;
            Owner.SetupDragItem(null, null, 0); // Registration reads identity; no inventory mutation.
            if (added && !Save())
            {
                _entries.Remove(entry);
                return;
            }
            if (restock)
            {
                _registration = entry; _registrationMax = Mathf.Max(1, item.m_shared.m_maxStackSize);
                Pin(); Render();
                if (_fields.Count > 0 && !ZInput.IsExclusiveGamepadActive()) { _fields[0].Select(); _fields[0].ActivateInputField(); }
            }
            else
            {
                Render();
                _status.text = L("registered", "Registered") + ": " + GetLocalizedItemName(item);
                ScrollTo(entry.Key);
            }
        }

        private void Pin()
        {
            Pinned = true; _backdrop.gameObject.SetActive(true);
        }

        private bool Save()
        {
            _controllerStatus = false;
            if (!string.Equals(Setting.Value, _snapshot, StringComparison.Ordinal))
            { _status.text = L("conflict", "Config changed. Finish editing to reload."); return false; }
            string next = ItemRuleConfigCore.Write(_snapshot, _entries, _restock);
            List<ItemRuleConfigCore.Entry> saved = ItemRuleConfigCore.Read(next, _restock);
            // Validate before touching the config: a prefab containing config
            // delimiters must not save successfully and then break span rebasing.
            if (!_entries.Where(entry => !entry.Removed).Select(entry => entry.Key).SequenceEqual(saved.Select(entry => entry.Key)))
            { _status.text = L("save_failed", "Could not save config."); return false; }
            try
            {
                if (next != _snapshot && !ItemRuleConfigStore.Save(Setting, _snapshot, next))
                { _status.text = L("conflict", "Config changed. Finish editing to reload."); return false; }
            }
            catch (Exception error)
            { Log.LogWarning("Could not save item rules: " + error.Message); _status.text = L("save_failed", "Could not save config."); return false; }
            ItemRuleConfigCore.AcceptSaved(_entries, saved);
            _snapshot = next;
            _status.text = L("saved", "Saved");
            return true;
        }

        private void ScrollTo(string key)
        {
            int index = _entries.FindLastIndex(e => e.Key == key);
            if (index >= 0) _scroll.verticalNormalizedPosition = _entries.Count <= 1 ? 1f : 1f - (float)index / (_entries.Count - 1);
        }

        private ItemData? Resolve(string key)
        {
            if (_resolved.TryGetValue(key, out ItemData? value)) return value;
            if (ObjectDB.instance != null)
            {
                GameObject? prefab = ObjectDB.instance.GetItemPrefab(ItemRuleConfigCore.PrefabKey(key));
                value = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData : null;
                if (value == null && _restock)
                {
                    string normalized = RuleTokens.NormalizeResourceToken(key);
                    foreach (GameObject candidate in ObjectDB.instance.m_items)
                    {
                        ItemData? data = candidate != null ? candidate.GetComponent<ItemDrop>()?.m_itemData : null;
                        if (data?.m_shared != null && GetRestockTargetLookupTokens(data).Any(k => RuleTokens.NormalizeResourceToken(k) == normalized))
                        { value = data; break; }
                    }
                }
            }
            _resolved[key] = value;
            return value;
        }

        private void ClearRows()
        {
            if (_content == null) { _fields.Clear(); _rowButtons.Clear(); _controllerRows.Clear(); return; }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_content)) EventSystem.current.SetSelectedGameObject(null);
            foreach (TMP_InputField field in _fields)
            {
                if (field == null) continue;
                field.onValueChanged.RemoveAllListeners(); field.onSelect.RemoveAllListeners(); field.onEndEdit.RemoveAllListeners();
                field.DeactivateInputField();
            }
            foreach (Button button in _rowButtons) if (button != null) button.onClick.RemoveAllListeners();
            _fields.Clear(); _rowButtons.Clear(); _controllerRows.Clear();
            for (int i = _content.childCount - 1; i >= 0; i--) { GameObject child = _content.GetChild(i).gameObject; child.SetActive(false); Object.Destroy(child); }
        }

        private void Render()
        {
            string? selectedKey = _controllerRowIndex >= 0 && _controllerRowIndex < _controllerRows.Count
                ? _controllerRows[_controllerRowIndex].Entry.Key : null;
            ClearRows();
            float width = Mathf.Clamp(_root.rect.width - 16f, 260f, PopupWidth);
            float inner = width - 24f;
            List<ItemRuleConfigCore.Entry> visible = _registration != null ? new() { _registration } : _entries.Where(e => !e.Removed).ToList();
            _visibleRowCount = Mathf.Max(1, visible.Count);
            float viewHeight = Mathf.Max(1, Mathf.Min(6, visible.Count)) * RowHeight;
            _popup.sizeDelta = new Vector2(width, PopupChromeHeight + viewHeight);
            Frame(_title.rectTransform, 12, -10, inner, 28);
            Frame(_scope.rectTransform, 12, -39, inner, 34);
            _title.text = _restock ? L("restock_title", "Restock targets") : L("exclude_title", "Auto pickup exclusions");
            _title.color = _gold;
            _mouseScope = _registration != null ? L("quantity", "Target quantity") + " (1–" + _registrationMax + ")"
                : _restock ? L("restock_scope", "{key} · mode and target per favorite stack").Replace("{key}", GetContainerRestockKeyDisplayText()) : L("exclude_scope", "Manual E pickup is still available");
            _scope.text = _mouseScope;
            Frame(_viewport, 12, -78, inner, viewHeight);
            _content.sizeDelta = new Vector2(inner, Mathf.Max(1, visible.Count) * RowHeight);
            _content.anchoredPosition = Vector2.zero;
            Frame(_status.rectTransform, 12, -82 - viewHeight, inner, 30);
            _status.text = "";
            if (visible.Count == 0)
            {
                TMP_Text empty = Text(_content, "Empty", L("empty", "No registered items"), 16);
                Frame(empty.rectTransform, 0, 0, inner, RowHeight);
            }
            for (int i = 0; i < visible.Count; i++)
            {
                ItemRuleConfigCore.Entry entry = visible[i];
                RectTransform row = Rect("Row", _content, new Vector2(inner, RowHeight), new Vector2(0, -i * RowHeight));
                Image rowBackground = row.gameObject.AddComponent<Image>(); rowBackground.color = Color.clear;
                ControllerRow controllerRow = new() { Entry = entry, Background = rowBackground };
                _controllerRows.Add(controllerRow);
                float quantityX = inner - (_registration != null ? 58 : 94);
                float modeX = quantityX - 38;
                float itemWidth = _restock ? modeX - 6 : inner - 74;
                // Keep the item's hover target separate from the controls: a
                // parent UITooltip can otherwise replace the mode explanation.
                RectTransform itemInfo = Rect("ItemInfo", row, new Vector2(itemWidth, RowHeight), Vector2.zero);
                itemInfo.gameObject.AddComponent<Image>().color = Color.clear;
                ItemData? item = Resolve(entry.Key);
                RectTransform icon = Rect("Icon", itemInfo, new Vector2(26, 26), new Vector2(0, -5));
                Image graphic = icon.gameObject.AddComponent<Image>(); graphic.raycastTarget = false;
                graphic.sprite = item?.GetIcon(); graphic.enabled = graphic.sprite != null; graphic.preserveAspect = true;
                TMP_Text name = Text(itemInfo, "Name", item == null ? entry.Key : GetLocalizedItemName(item), 16);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                Frame(name.rectTransform, 32, -2, itemWidth - 32, 32);
                UITooltip tooltip = itemInfo.gameObject.AddComponent<UITooltip>(); EnsureTooltipPrefab(tooltip);
                tooltip.enabled = tooltip.m_tooltipPrefab != null;
                tooltip.m_topic = item == null ? entry.Key : GetLocalizedItemName(item); tooltip.m_text = entry.Key;
                if (_restock)
                {
                    TMP_InputField field = NumberField(row, entry, item);
                    controllerRow.Quantity = field;
                    controllerRow.MaximumAmount = item?.m_shared != null ? Mathf.Max(1, item.m_shared.m_maxStackSize) : int.MaxValue;
                    Frame((RectTransform)field.transform, quantityX, -2, 58, 32);
                    Image modeIcon = null!;
                    UITooltip modeTip = null!;
                    void RefreshMode()
                    {
                        modeIcon.sprite = GetRestockModeIcon(entry.Mode);
                        // Set also refreshes the currently visible tooltip;
                        // assigning its fields only affects the next hover.
                        if (modeTip.enabled)
                            modeTip.Set(GetRestockModeTitle(entry.Mode), GetRestockModeHelp(entry.Mode));
                    }
                    Button modeButton = Button(row, "RestockMode", "", () =>
                    {
                        Pin();
                        RestockRuleMode previous = entry.Mode;
                        entry.Mode = RestockTargetLimitCore.NextMode(entry.Mode);
                        if (!Save()) entry.Mode = previous;
                        RefreshMode();
                    });
                    ApplyCraftButtonStyle(modeButton);
                    Frame((RectTransform)modeButton.transform, modeX, -2, 32, 32);
                    modeIcon = Rect("ModeIcon", modeButton.transform, new Vector2(28, 28), new Vector2(2, -2)).gameObject.AddComponent<Image>();
                    modeIcon.raycastTarget = false;
                    modeTip = modeButton.gameObject.AddComponent<UITooltip>(); EnsureTooltipPrefab(modeTip);
                    modeTip.enabled = modeTip.m_tooltipPrefab != null;
                    RefreshMode();
                    _rowButtons.Add(modeButton);
                    controllerRow.Mode = modeButton;
                }
                if (_registration == null)
                {
                    Button remove = Button(row, "Remove", _restock ? "×" : L("remove", "Remove"), () => RemoveEntry(entry));
                    Frame((RectTransform)remove.transform, inner - (_restock ? 30 : 68), -2, _restock ? 30 : 68, 32);
                    _rowButtons.Add(remove);
                }
            }
            PositionPopup();
            int rememberedIndex = selectedKey == null ? -1 : _controllerRows.FindIndex(row => row.Entry.Key == selectedKey);
            if (rememberedIndex >= 0) _controllerRowIndex = rememberedIndex;
            RefreshControllerSelection();
        }

        private void RemoveEntry(ItemRuleConfigCore.Entry entry)
        {
            Pin(); entry.Removed = true;
            if (!Save()) { entry.Removed = false; return; }
            if (ReferenceEquals(_registration, entry)) _registration = null;
            Render();
        }

        private TMP_InputField NumberField(RectTransform parent, ItemRuleConfigCore.Entry entry, ItemData? item)
        {
            int? maximumAmount = item?.m_shared != null
                ? Mathf.Max(1, item.m_shared.m_maxStackSize)
                : null;
            RectTransform rect = Rect("Quantity", parent, new Vector2(58, 32), Vector2.zero);
            rect.gameObject.SetActive(false);
            Image image = rect.gameObject.AddComponent<Image>();
            Image? slot = Owner.m_playerGrid.m_elementPrefab.GetComponent<Image>();
            if (slot != null) CopyImageStyle(slot, image);
            RectTransform textArea = Rect("TextArea", rect, Vector2.zero, Vector2.zero); Stretch(textArea);
            textArea.offsetMin = new Vector2(4, 2); textArea.offsetMax = new Vector2(-4, -2);
            textArea.gameObject.AddComponent<RectMask2D>();
            TMP_Text value = Text(textArea, "Text", entry.Amount, 17); Stretch(value.rectTransform);
            value.alignment = TextAlignmentOptions.MidlineRight;
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image; input.textViewport = textArea; input.textComponent = value;
            input.colors = Owner.m_playerGrid.m_elementPrefab.GetComponent<Button>()?.colors ?? InputColors;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 10; input.lineType = TMP_InputField.LineType.SingleLine;
            input.restoreOriginalTextOnEscape = false; // Escape closes; valid edits are already saved.
            input.text = entry.Amount; input.customCaretColor = true; input.caretColor = _text;
            input.selectionColor = new Color(0.30f, 0.48f, 0.62f, 0.55f);
            input.onSelect.AddListener(_ => Pin());
            input.onValueChanged.AddListener(text =>
            {
                Pin();
                // An empty/invalid edit buffer must never disable restock or be
                // written to config. Keep the last successfully saved quantity.
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount < 1 ||
                    (maximumAmount.HasValue && amount > maximumAmount.Value))
                { _status.text = L("invalid", "Enter a quantity from 1 to the item's maximum stack."); return; }
                string previous = entry.Amount;
                entry.Amount = text;
                if (!Save()) { entry.Amount = previous; input.SetTextWithoutNotify(previous); }
            });
            input.onEndEdit.AddListener(text =>
            {
                string normalized = maximumAmount.HasValue
                    ? RestockTargetLimitCore.ClampAmountForEditor(text, maximumAmount.Value)
                    : RestockTargetLimitCore.NormalizeAmountForEditor(text);
                if (normalized.Length == 0)
                {
                    input.SetTextWithoutNotify(entry.Amount);
                    _status.text = L("restored", "Last saved quantity restored");
                    return;
                }
                if (normalized == entry.Amount)
                {
                    input.SetTextWithoutNotify(normalized);
                    return;
                }
                string previous = entry.Amount;
                entry.Amount = normalized;
                if (Save()) input.SetTextWithoutNotify(normalized);
                else { entry.Amount = previous; input.SetTextWithoutNotify(previous); }
            });
            _fields.Add(input);
            rect.gameObject.SetActive(true);
            return input;
        }

        internal void Close()
        {
            if (Pinned) _itemRuleInputClosedFrame = Time.frameCount;
            SetControllerActive(false);
            _controllerRowIndex = 0;
            _controllerInputFrame = Time.frameCount;
            Pinned = false; _registration = null;
            _hoverMode = null; _hoverStarted = _outsideStarted = -1f;
            if (_popup == null) return;
            ClearRows(); _entries.Clear(); _resolved.Clear();
            _popup.gameObject.SetActive(false); _backdrop.gameObject.SetActive(false);
        }

        internal void Hide()
        {
            if (Open) Close();
            _hoverMode = null; _hoverStarted = _outsideStarted = -1f;
            if (_toolbar != null) _toolbar.gameObject.SetActive(false);
        }
        private void EndInteraction()
        {
            if (IsInventoryPanelClosing(Owner)) Close();
            else Hide();
        }
        private void OnDisable()
        {
            Close();
            if (_toolbar != null) _toolbar.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            Close();
            RemoveUiListeners(transform);
            if (_toolbar != null && !_toolbar.IsChildOf(transform))
            {
                RemoveUiListeners(_toolbar);
                Object.Destroy(_toolbar.gameObject);
            }
            foreach (Sprite sprite in _ownedIcons) if (sprite != null) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
            _ownedIcons.Clear();
            if (ReferenceEquals(_itemRuleEditor, this)) _itemRuleEditor = null;
        }

        private static void RemoveUiListeners(Transform owner)
        {
            foreach (Button button in owner.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            foreach (UIDragHandler handler in owner.GetComponentsInChildren<UIDragHandler>(true)) handler.m_onReleasedOn = null;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = new(ModName + "_Rules_" + name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            Frame(rect, position.x, position.y, size.x, size.y); return rect;
        }
        private static void Frame(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y);
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private TMP_Text Text(Transform parent, string name, string value, float size)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            // TMP loads its default font in Awake. Assign the game's font before
            // activation, otherwise Valheim searches for absent LiberationSans.
            rect.gameObject.SetActive(false);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font; label.fontSharedMaterial = _fontMaterial;
            label.fontSize = size; label.color = _text; label.text = value;
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            rect.gameObject.SetActive(true);
            return label;
        }
        private Button Button(Transform parent, string name, string text, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, parent, new Vector2(80, 32), Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action);
            if (text.Length > 0)
            {
                // Borrow Craft's visuals only, without its click action, controller
                // shortcut, interactability or crafting-specific components.
                Button source = ApplyCraftButtonStyle(button);
                TMP_Text label = Text(rect, "Label", text, 16); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
                TMP_Text? sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
                if (sourceLabel != null && sourceLabel.font != null)
                {
                    label.font = sourceLabel.font; label.fontSharedMaterial = sourceLabel.fontSharedMaterial;
                    // Craft's live label can be dimmed by missing ingredients;
                    // that state does not describe this enabled rule action.
                    label.fontStyle = sourceLabel.fontStyle;
                }
            }
            return button;
        }

        private Button ApplyCraftButtonStyle(Button button)
        {
            Button source = Owner.m_craftButton != null ? Owner.m_craftButton : Owner.m_takeAllButton;
            if (source.image != null) CopyImageStyle(source.image, button.image);
            button.spriteState = source.spriteState; button.colors = source.colors; button.transition = source.transition;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return source;
        }

        private static void CopyImageStyle(Image source, Image target)
        {
            target.sprite = source.sprite; target.type = source.type;
            target.material = source.material; target.color = source.color;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.fillCenter = source.fillCenter; target.preserveAspect = source.preserveAspect;
        }

        // Fallback for UI mods without the native slot Button. The normal path
        // copies the grid's actual ColorTint, including its translucent alpha.
        private static ColorBlock InputColors => new()
        {
            normalColor = new Color(0.132f, 0.132f, 0.132f, 0.502f),
            highlightedColor = new Color(0.39f, 0.40f, 0.40f, 0.502f),
            selectedColor = new Color(0.39f, 0.40f, 0.40f, 0.502f),
            pressedColor = new Color(0.132f, 0.132f, 0.132f, 0.65f),
            disabledColor = new Color(0.132f, 0.132f, 0.132f, 0.35f),
            colorMultiplier = 1f, fadeDuration = 0.08f
        };
        private Sprite CreateRuleIcon(bool restock)
        {
            // White line art on transparency, tinted with exactly the same colors as trash.
            Color[] drawing = new Color[64 * 64];
            if (restock)
            {
                DrawRestockSymbol(drawing, Color.white, includeParcel: true);
            }
            else
            {
                // Upward pickup arrow above a tray, with an exclusion slash.
                DrawTrashLine(drawing, 64, 16, 35, 16, 48, 2, Color.white);
                DrawTrashLine(drawing, 64, 16, 48, 46, 48, 2, Color.white);
                DrawTrashLine(drawing, 64, 46, 48, 46, 35, 2, Color.white);
                DrawTrashLine(drawing, 64, 28, 40, 28, 17, 2, Color.white);
                DrawTrashLine(drawing, 64, 20, 25, 28, 17, 2, Color.white);
                DrawTrashLine(drawing, 64, 28, 17, 36, 25, 2, Color.white);
                // Clear a gap around the slash so it stays legible over the arrow.
                DrawTrashLine(drawing, 64, 13, 53, 51, 15, 3, Color.clear);
                DrawTrashLine(drawing, 64, 13, 53, 51, 15, 1, Color.white);
            }
            Texture2D texture = new(64, 64, TextureFormat.RGBA32, false)
            { name = restock ? ModName + "_RestockIcon" : ModName + "_ExcludeIcon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(drawing); texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100);
            _ownedIcons.Add(sprite);
            return sprite;
        }
    }
}

#if !INVENTORY_SLOTS
[HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
internal static class ItemRuleEditorInputPatch
{
    private static void Postfix(ref bool __result) => __result |= RulePlugin.IsItemRuleInputBlocked();
}

#endif

[HarmonyPatch(typeof(InventoryGui), "OnDestroy")]
internal static class ItemRuleEditorDestroyPatch
{
    private static void Postfix(InventoryGui __instance) => RulePlugin.DestroyItemRuleUi(__instance);
}

[HarmonyPatch(typeof(InventoryGui), "UpdateGamepad")]
internal static class ItemRuleEditorGamepadPatch
{
    private static bool Prefix() => !RulePlugin.IsItemRuleInputBlocked();
}

[HarmonyPatch(typeof(InventoryGrid), "UpdateGamepad")]
internal static class ItemRuleEditorGridGamepadPatch
{
    private static bool Prefix() => !RulePlugin.IsItemRuleInputBlocked();
}

[HarmonyPatch(typeof(UIGamePad), nameof(UIGamePad.ButtonPressed))]
internal static class ItemRuleEditorButtonShortcutPatch
{
    private static bool Prefix(UIGamePad __instance, ref bool __result)
    {
        if (!RulePlugin.IsItemRuleInputBlocked() || InventoryGui.instance == null ||
            !__instance.transform.IsChildOf(InventoryGui.instance.transform)) return true;
        __result = false;
        return false;
    }
}
