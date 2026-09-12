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

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private static ItemRuleEditor? _itemRuleEditor;
    private static int _itemRuleInputClosedFrame = -1;
    internal static bool IsItemRuleInputBlocked() =>
        (_itemRuleEditor != null && _itemRuleEditor.Pinned) || _itemRuleInputClosedFrame == Time.frameCount;

    private static void UpdateItemRuleUi(InventoryGui gui)
    {
        if (_instance == null || !_instance.isActiveAndEnabled || IsDedicatedServer || gui.m_takeAllButton == null || gui.m_playerGrid.m_gridRoot == null) return;
        if (_itemRuleEditor == null || _itemRuleEditor.Owner != gui)
        {
            DestroyItemRuleUi();
            GameObject root = new("InventoryActions_ItemRules", typeof(RectTransform));
            root.transform.SetParent(gui.transform, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _itemRuleEditor = root.AddComponent<ItemRuleEditor>();
            _itemRuleEditor.Initialize(gui);
        }
        _itemRuleEditor.PositionToolbar();
    }

    internal static void DestroyItemRuleUi(InventoryGui? owner = null)
    {
        if (_itemRuleEditor == null || (owner != null && _itemRuleEditor.Owner != owner)) return;
        ItemRuleEditor editor = _itemRuleEditor;
        _itemRuleEditor = null;
        editor.Close();
        Object.Destroy(editor.gameObject);
    }

    // One GUI-owned component owns the toolbar, draft and event listeners. It
    // rebuilds list rows only on opening or a deliberate edit, never every frame.
    private sealed class ItemRuleEditor : MonoBehaviour
    {
        private const float Gap = 8f;
        private const float RowHeight = 38f;
        internal InventoryGui Owner = null!;
        internal bool Pinned { get; private set; }
        private RectTransform _root = null!, _toolbar = null!, _popup = null!, _content = null!, _viewport = null!;
        private Button _restockButton = null!, _excludeButton = null!, _backdrop = null!;
        private Sprite _restockIcon = null!, _excludeIcon = null!;
        private Sprite? _ruleButtonSprite;
        private int _visibleRowCount = 1;
        private TMP_Text _title = null!, _scope = null!, _status = null!;
        private RectTransform _footer = null!;
        private TMP_FontAsset _font = null!;
        private readonly List<Button> _rowButtons = new();
        private readonly List<TMP_InputField> _fields = new();
        private readonly List<Sprite> _ownedIcons = new();
        private readonly Dictionary<string, ItemData?> _resolved = new(StringComparer.Ordinal);
        private readonly HashSet<ItemRuleConfigCore.Entry> _changed = new();
        private List<ItemRuleConfigCore.Entry> _entries = new();
        private ItemRuleConfigCore.Entry? _registration;
        private string _snapshot = "";
        private bool _restock = true, _editing;
        private int _registrationMax;
        private float _hoverStarted = -1f, _outsideStarted = -1f;
        private bool? _hoverMode;
        private Camera? _camera;
        private Animator? _animator;
        private static readonly int VisibleParameter = Animator.StringToHash("visible");
        private ScrollRect _scroll = null!;
        private readonly Color _text = new(1f, 0.94f, 0.8f);
        private readonly Color _gold = new(1f, 0.8f, 0.36f);

        private ConfigEntry<string> Setting => _restock ? _restockTargetStackLimitsConfig : _autoPickupExcludedItemsConfig;
        private bool Open => _popup != null && _popup.gameObject.activeSelf;
        // InventoryGui.IsVisible intentionally lags Hide by up to two frames.
        // Use the same Animator obtained by vanilla Awake through the public API.
        private bool CanShow => _instance != null && _instance.isActiveAndEnabled && Owner != null && InventoryGui.IsVisible() &&
            (_animator == null || _animator.GetBool(VisibleParameter)) && !Menu.IsVisible() && !global::Console.IsVisible();
        private string L(string key, string fallback) => LocalizeUi("$inventoryactions_rules_" + key, fallback);

        internal void Initialize(InventoryGui gui)
        {
            Owner = gui;
            _animator = gui.GetComponent<Animator>();
            _root = (RectTransform)transform;
            _camera = gui.GetComponentInParent<Canvas>()?.worldCamera;
            _font = gui.m_takeAllButton.GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
            _toolbar = Rect("Toolbar", _root, Vector2.zero, Vector2.zero);
            _restockButton = RuleButton("InventoryActions_RestockRules", true);
            _excludeButton = RuleButton("InventoryActions_AutoPickupRules", false);
            _ruleButtonSprite = _restockButton.image.sprite;
            _restockButton.gameObject.AddComponent<UIDragHandler>().m_onReleasedOn = _ => ClickTool(true);
            _excludeButton.gameObject.AddComponent<UIDragHandler>().m_onReleasedOn = _ => ClickTool(false);
            _restockIcon = CreateRuleIcon(true); _excludeIcon = CreateRuleIcon(false);
            _backdrop = Button(_root, "Outside", "", () => { if (!_editing) Close(); });
            Stretch((RectTransform)_backdrop.transform);
            _backdrop.GetComponent<Image>().color = Color.clear;
            _backdrop.gameObject.SetActive(false);
            _popup = Rect("Popup", _root, new Vector2(360f, 160f), Vector2.zero);
            Image background = _popup.gameObject.AddComponent<Image>();
            background.color = new Color(0.15f, 0.13f, 0.14f, 0.99f);
            ApplyWoodenPanelStyle(background);
            _title = Text(_popup, "Title", "", 19f);
            Frame(_title.rectTransform, 12, -10, 336, 28);
            _scope = Text(_popup, "Scope", "", 13f);
            Frame(_scope.rectTransform, 12, -39, 336, 34);
            _viewport = Rect("Viewport", _popup, new Vector2(336, 38), new Vector2(12, -78));
            _viewport.gameObject.AddComponent<Image>().color = Color.clear;
            _viewport.gameObject.AddComponent<RectMask2D>();
            _content = Rect("Content", _viewport, new Vector2(336, 38), Vector2.zero);
            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport; _scroll.content = _content;
            _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false; _scroll.scrollSensitivity = 30f;
            _status = Text(_popup, "Status", "", 12f);
            _footer = Rect("Footer", _popup, new Vector2(336, 34), Vector2.zero);
            Button cancel = Button(_footer, "Cancel", L("cancel", "Cancel"), Cancel);
            Button save = Button(_footer, "Save", L("save", "Save"), () => Save());
            Frame((RectTransform)cancel.transform, 160, 0, 80, 32);
            Frame((RectTransform)save.transform, 248, 0, 88, 32);
            _popup.gameObject.SetActive(false);
        }

        internal void PositionToolbar()
        {
            if (!CanShow) { Hide(); return; }
            InventoryGrid grid = Owner.m_playerGrid;
            float size = Mathf.Clamp(Mathf.Max(1f, grid.m_elementSpace) * 0.72f, 42f, 58f);
            Vector3 origin = GetGridOrigin(grid);
            int rows = GetDisplayedPlayerRows(grid);
            float sortSize = GetContainerSortButtonSize(Owner);
            Vector3 trashPosition = origin + new Vector3(
                GetInventorySortPanelPosition(grid, sortSize, rows).x - origin.x + (sortSize - size) * 0.5f,
                -Mathf.Max(1, rows) * Mathf.Max(1f, grid.m_elementSpace) - TrashPanelGap, 0f);
            Vector3 world = grid.m_gridRoot.TransformPoint(trashPosition - new Vector3(2 * (size + Gap) + 4f, 0f));
            _toolbar.localPosition = _root.InverseTransformPoint(world);
            _toolbar.sizeDelta = new Vector2(2 * size + Gap, size);
            Vector2 restockOffset = GetRestockRulesButtonPositionOffset();
            Vector2 excludeOffset = GetAutoPickupButtonPositionOffset();
            Frame((RectTransform)_restockButton.transform, restockOffset.x, restockOffset.y, size, size);
            Frame((RectTransform)_excludeButton.transform, size + Gap + excludeOffset.x, excludeOffset.y, size, size);
            ConfigureInventoryActionIcon(_restockButton, size, _restockIcon);
            ConfigureInventoryActionIcon(_excludeButton, size, _excludeIcon);
            bool acceptsHeldItem = CanRegisterHeldItem();
            UpdateRuleButtonVisual(_restockButton, acceptsHeldItem);
            UpdateRuleButtonVisual(_excludeButton, acceptsHeldItem);
            _toolbar.gameObject.SetActive(true);
            if (Open) PositionPopup();
            if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
        }

        private void PositionPopup()
        {
            // Follow the selected button, including its independent live offset.
            // Prefer a downward dropdown; shrink the list before screen-edge clamping.
            Rect bounds = _root.rect;
            RectTransform button = (RectTransform)(_restock ? _restockButton : _excludeButton).transform;
            Vector3 anchor = _root.InverseTransformPoint(button.TransformPoint(new Vector3(button.rect.xMax, button.rect.yMin)));
            float x = anchor.x - _popup.rect.width;
            float y = anchor.y - 6f;
            float available = y - bounds.yMin - 8f;
            float viewHeight = Mathf.Min(Mathf.Min(6, _visibleRowCount) * RowHeight, Mathf.Max(RowHeight, available - 156f));
            ResizePopupViewport(viewHeight);
            x = Mathf.Clamp(x, bounds.xMin + 8, Mathf.Max(bounds.xMin + 8, bounds.xMax - _popup.rect.width - 8));
            // Extreme offsets can leave less than one editable row and its buttons.
            // Keep those controls reachable instead of clipping Save/Cancel off-screen.
            y = Mathf.Clamp(y, bounds.yMin + _popup.rect.height + 8, Mathf.Max(bounds.yMin + _popup.rect.height + 8, bounds.yMax - 8));
            _popup.localPosition = new Vector3(x, y, 0);
        }

        private void ResizePopupViewport(float height)
        {
            if (Mathf.Abs(_viewport.rect.height - height) < 0.1f) return;
            float inner = _popup.rect.width - 24f;
            _popup.sizeDelta = new Vector2(_popup.rect.width, 156f + height);
            Frame(_viewport, 12, -78, inner, height);
            Frame(_status.rectTransform, 12, -82 - height, inner, 30);
            Frame(_footer, 12, -116 - height, inner, 34);
            Vector2 scroll = _content.anchoredPosition;
            scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, _content.rect.height - height));
            _content.anchoredPosition = scroll;
        }

        private bool CanRegisterHeldItem()
        {
            Player? player = Player.m_localPlayer;
            ItemData? item = Owner.m_dragItem;
            return !_editing && HasHeldTrashCandidate(Owner) && player != null && item?.m_shared != null && item.m_dropPrefab != null &&
                Owner.m_dragInventory == GetPlayerInventory(player) && Owner.m_dragInventory.ContainsItem(item) &&
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

        private bool Contains(RectTransform rect) => RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, _camera);
        private void Update()
        {
            if (!CanShow || Player.m_localPlayer == null || Player.m_localPlayer.m_isLoading)
            { Hide(); return; }
            if (Owner.m_splitDialog != null && Owner.m_splitDialog.IsActive)
            { Close(); return; }
            if (Open && Pinned && (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB")))
            { Close(); return; }
            if (Open && !_editing && !string.Equals(_snapshot, Setting.Value, StringComparison.Ordinal)) LoadList(_restock, Pinned);
            bool? hovered = Contains((RectTransform)_restockButton.transform) ? true : Contains((RectTransform)_excludeButton.transform) ? false : null;
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
            if (Owner.m_splitDialog != null && Owner.m_splitDialog.IsActive) return;
            if (HasHeldTrashCandidate(Owner)) { RegisterHeldItem(restock); return; }
            if (_editing) return;
            LoadList(restock, true);
        }

        private void LoadList(bool restock, bool pin)
        {
            CloseInventoryTrashConfirmDialog();
            _restock = restock; _snapshot = Setting.Value;
            _entries = ItemRuleConfigCore.Read(_snapshot, restock);
            _changed.Clear(); _resolved.Clear(); _registration = null;
            _editing = false; Pinned = pin;
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
                ? RestockTargetLimitCore.NormalizeResourceToken(e.Key) == RestockTargetLimitCore.NormalizeResourceToken(key)
                : string.Equals(ItemRuleConfigCore.PrefabKey(e.Key), key, StringComparison.OrdinalIgnoreCase));
            bool added = entry == null;
            if (entry == null)
            {
                entry = new ItemRuleConfigCore.Entry { Start = -1, Key = key, Amount = GetRestockTargetStack(item).ToString(CultureInfo.InvariantCulture) };
                _entries.Add(entry);
            }
            _resolved[entry.Key] = item;
            Owner.SetupDragItem(null, null, 0); // Registration reads identity; no inventory mutation.
            if (restock)
            {
                _registration = entry; _registrationMax = Mathf.Max(0, item.m_shared.m_maxStackSize);
                entry.Amount = GetRestockTargetStack(item).ToString(CultureInfo.InvariantCulture);
                BeginEdit(entry); Render();
                if (_fields.Count > 0) { _fields[0].Select(); _fields[0].ActivateInputField(); }
            }
            else
            {
                if (added) { BeginEdit(entry); if (!Save()) return; }
                else Render();
                _status.text = L("registered", "Registered") + ": " + GetLocalizedItemName(item);
                ScrollTo(entry.Key);
            }
        }

        private void BeginEdit(ItemRuleConfigCore.Entry? entry = null)
        {
            if (entry != null) _changed.Add(entry);
            _editing = true; Pinned = true; _backdrop.gameObject.SetActive(true);
            _footer.gameObject.SetActive(true); _status.text = L("editing", "Editing");
        }

        private bool Save()
        {
            if (!string.Equals(Setting.Value, _snapshot, StringComparison.Ordinal))
            { _status.text = L("conflict", "Config changed. Cancel to reload before saving."); return false; }
            foreach (ItemRuleConfigCore.Entry entry in _changed.Where(e => !e.Removed))
            {
                if (_restock && (!int.TryParse(entry.Amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0 ||
                    (entry == _registration && value > _registrationMax)))
                { _status.text = L("invalid", "Enter a valid non-negative quantity."); return false; }
            }
            try
            {
                if (!ItemRuleConfigStore.Save(Setting, _snapshot, ItemRuleConfigCore.Write(_snapshot, _entries, _restock)))
                { _status.text = L("conflict", "Config changed. Cancel to reload before saving."); return false; }
            }
            catch (Exception error)
            { Log.LogWarning("Could not save item rules: " + error.Message); _status.text = L("save_failed", "Could not save config."); return false; }
            string? registeredKey = _registration?.Key;
            LoadList(_restock, true);
            if (registeredKey != null) ScrollTo(registeredKey);
            _status.text = L("saved", "Saved");
            return true;
        }

        private void Cancel() => LoadList(_restock, true);

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
                    string normalized = RestockTargetLimitCore.NormalizeResourceToken(key);
                    foreach (GameObject candidate in ObjectDB.instance.m_items)
                    {
                        ItemData? data = candidate != null ? candidate.GetComponent<ItemDrop>()?.m_itemData : null;
                        if (data?.m_shared != null && GetRestockTargetLookupTokens(data).Any(k => RestockTargetLimitCore.NormalizeResourceToken(k) == normalized))
                        { value = data; break; }
                    }
                }
            }
            _resolved[key] = value;
            return value;
        }

        private void ClearRows()
        {
            if (_content == null) { _fields.Clear(); _rowButtons.Clear(); return; }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_content)) EventSystem.current.SetSelectedGameObject(null);
            foreach (TMP_InputField field in _fields)
            {
                if (field == null) continue;
                field.onValueChanged.RemoveAllListeners(); field.onSelect.RemoveAllListeners(); field.onSubmit.RemoveAllListeners();
                field.DeactivateInputField();
            }
            foreach (Button button in _rowButtons) if (button != null) button.onClick.RemoveAllListeners();
            _fields.Clear(); _rowButtons.Clear();
            for (int i = _content.childCount - 1; i >= 0; i--) { GameObject child = _content.GetChild(i).gameObject; child.SetActive(false); Object.Destroy(child); }
        }

        private void Render()
        {
            ClearRows();
            float width = Mathf.Clamp(_root.rect.width - 16f, 260f, 360f);
            float inner = width - 24f;
            List<ItemRuleConfigCore.Entry> visible = _registration != null ? new() { _registration } : _entries.Where(e => !e.Removed).ToList();
            _visibleRowCount = Mathf.Max(1, visible.Count);
            float viewHeight = Mathf.Max(1, Mathf.Min(6, visible.Count)) * RowHeight;
            _popup.sizeDelta = new Vector2(width, 78 + viewHeight + 78);
            Frame(_title.rectTransform, 12, -10, inner, 28);
            Frame(_scope.rectTransform, 12, -39, inner, 34);
            _title.text = _restock ? L("restock_title", "Restock limits") : L("exclude_title", "Auto pickup exclusions");
            _title.color = _gold;
            _scope.text = _registration != null ? L("quantity", "Target quantity") + " (0–" + _registrationMax + ")"
                : _restock ? L("restock_scope", "Alt+E · target per favorite stack; 0 disables restock") : L("exclude_scope", "Manual E pickup is still available");
            Frame(_viewport, 12, -78, inner, viewHeight);
            _content.sizeDelta = new Vector2(inner, Mathf.Max(1, visible.Count) * RowHeight);
            _content.anchoredPosition = Vector2.zero;
            Frame(_status.rectTransform, 12, -82 - viewHeight, inner, 30);
            Frame(_footer, 12, -116 - viewHeight, inner, 34);
            RectTransform cancel = (RectTransform)_footer.GetChild(0), save = (RectTransform)_footer.GetChild(1);
            Frame(cancel, inner - 176, 0, 80, 32); Frame(save, inner - 88, 0, 88, 32);
            _footer.gameObject.SetActive(_editing);
            _status.text = _editing ? L("editing", "Editing") : Pinned ? L("pinned", "Pinned") : L("preview", "Preview");
            if (visible.Count == 0)
            {
                TMP_Text empty = Text(_content, "Empty", L("empty", "No registered items"), 16);
                Frame(empty.rectTransform, 0, 0, inner, RowHeight);
            }
            for (int i = 0; i < visible.Count; i++)
            {
                ItemRuleConfigCore.Entry entry = visible[i];
                RectTransform row = Rect("Row", _content, new Vector2(inner, RowHeight), new Vector2(0, -i * RowHeight));
                row.gameObject.AddComponent<Image>().color = Color.clear;
                ItemData? item = Resolve(entry.Key);
                RectTransform icon = Rect("Icon", row, new Vector2(26, 26), new Vector2(0, -5));
                Image graphic = icon.gameObject.AddComponent<Image>(); graphic.raycastTarget = false;
                graphic.sprite = item?.GetIcon(); graphic.enabled = graphic.sprite != null; graphic.preserveAspect = true;
                TMP_Text name = Text(row, "Name", item == null ? entry.Key : GetLocalizedItemName(item), 16);
                name.overflowMode = TextOverflowModes.Ellipsis;
                Frame(name.rectTransform, 32, -2, inner - (_restock ? 130 : 104), 32);
                UITooltip tooltip = row.gameObject.AddComponent<UITooltip>(); EnsureTooltipPrefab(tooltip);
                tooltip.enabled = tooltip.m_tooltipPrefab != null;
                tooltip.m_topic = item == null ? entry.Key : GetLocalizedItemName(item); tooltip.m_text = entry.Key;
                if (_restock)
                {
                    TMP_InputField field = NumberField(row, entry);
                    Frame((RectTransform)field.transform, inner - 94, -2, _registration != null ? 94 : 58, 32);
                }
                if (_registration == null)
                {
                    Button remove = Button(row, "Remove", _restock ? "×" : L("remove", "Remove"), () =>
                    { entry.Removed = true; BeginEdit(); Render(); });
                    Frame((RectTransform)remove.transform, inner - (_restock ? 30 : 68), -2, _restock ? 30 : 68, 32);
                    _rowButtons.Add(remove);
                }
            }
            PositionPopup();
        }

        private TMP_InputField NumberField(RectTransform parent, ItemRuleConfigCore.Entry entry)
        {
            RectTransform rect = Rect("Quantity", parent, new Vector2(58, 32), Vector2.zero);
            rect.gameObject.SetActive(false);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.08f, 0.07f, 0.08f, 1);
            RectTransform textArea = Rect("TextArea", rect, Vector2.zero, Vector2.zero); Stretch(textArea);
            textArea.offsetMin = new Vector2(4, 2); textArea.offsetMax = new Vector2(-4, -2);
            textArea.gameObject.AddComponent<RectMask2D>();
            TMP_Text value = Text(textArea, "Text", entry.Amount, 17); Stretch(value.rectTransform);
            value.alignment = TextAlignmentOptions.MidlineRight;
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image; input.textViewport = textArea; input.textComponent = value;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 10; input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = entry.Amount; input.customCaretColor = true; input.caretColor = _text;
            input.onSelect.AddListener(_ => BeginEdit());
            input.onValueChanged.AddListener(text => { entry.Amount = text; BeginEdit(entry); });
            input.onSubmit.AddListener(_ => { if (_editing && !ZInput.GetKeyDown(KeyCode.Escape)) Save(); });
            _fields.Add(input);
            rect.gameObject.SetActive(true);
            return input;
        }

        internal void Close()
        {
            if (Pinned) _itemRuleInputClosedFrame = Time.frameCount;
            Pinned = false; _editing = false; _registration = null;
            _hoverMode = null; _hoverStarted = _outsideStarted = -1f;
            if (_popup == null) return;
            ClearRows(); _entries.Clear(); _changed.Clear(); _resolved.Clear();
            _popup.gameObject.SetActive(false); _backdrop.gameObject.SetActive(false);
        }

        internal void Hide()
        {
            if (Open) Close();
            _hoverMode = null; _hoverStarted = _outsideStarted = -1f;
            if (_toolbar != null) _toolbar.gameObject.SetActive(false);
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            foreach (Button button in GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            foreach (UIDragHandler handler in GetComponentsInChildren<UIDragHandler>(true)) handler.m_onReleasedOn = null;
            foreach (Sprite sprite in _ownedIcons) if (sprite != null) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
            _ownedIcons.Clear();
            if (ReferenceEquals(_itemRuleEditor, this)) _itemRuleEditor = null;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = new("InventoryActions_Rules_" + name, typeof(RectTransform));
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
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font; label.fontSize = size; label.color = _text; label.text = value;
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
        private Button Button(Transform parent, string name, string text, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, parent, new Vector2(80, 32), Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            Image? source = Owner.m_takeAllButton.GetComponent<Image>();
            image.sprite = source?.sprite; image.type = source != null ? source.type : Image.Type.Simple;
            image.color = new Color(0.42f, 0.37f, 0.32f, 1);
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action);
            if (text.Length > 0) { TMP_Text label = Text(rect, "Label", text, 16); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; }
            return button;
        }
        private Sprite CreateRuleIcon(bool restock)
        {
            // White line art on transparency, tinted with exactly the same colors as trash.
            Color[] drawing = new Color[64 * 64];
            void Draw(int x, int y, int xx, int yy) => DrawTrashLine(drawing, 64, x, y, xx, yy, 1, Color.white);
            void Arc(float from, float to)
            {
                int x = Mathf.RoundToInt(32 + 22 * Mathf.Cos(from * Mathf.Deg2Rad));
                int y = Mathf.RoundToInt(32 + 22 * Mathf.Sin(from * Mathf.Deg2Rad));
                for (float angle = from + 5; angle <= to; angle += 5)
                {
                    int nextX = Mathf.RoundToInt(32 + 22 * Mathf.Cos(angle * Mathf.Deg2Rad));
                    int nextY = Mathf.RoundToInt(32 + 22 * Mathf.Sin(angle * Mathf.Deg2Rad));
                    Draw(x, y, nextX, nextY); x = nextX; y = nextY;
                }
            }
            if (restock)
            {
                // Parcel surrounded by two return arrows; omit tiny details at HUD size.
                Arc(-70, 90); Draw(32, 54, 38, 49); Draw(32, 54, 38, 59);
                Arc(110, 270); Draw(32, 10, 26, 5); Draw(32, 10, 26, 15);
                Draw(23, 27, 32, 22); Draw(32, 22, 41, 27); Draw(41, 27, 41, 38);
                Draw(41, 38, 32, 43); Draw(32, 43, 23, 38); Draw(23, 38, 23, 27);
                Draw(23, 27, 32, 32); Draw(32, 32, 41, 27); Draw(32, 32, 32, 43);
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
            { name = restock ? "InventoryActions_RestockIcon" : "InventoryActions_ExcludeIcon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(drawing); texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100);
            _ownedIcons.Add(sprite);
            return sprite;
        }
    }
}

[HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
internal static class ItemRuleEditorInputPatch
{
    private static void Postfix(ref bool __result) => __result |= InventoryActionsPlugin.IsItemRuleInputBlocked();
}

[HarmonyPatch(typeof(InventoryGui), "OnDestroy")]
internal static class ItemRuleEditorDestroyPatch
{
    private static void Postfix(InventoryGui __instance) => InventoryActionsPlugin.DestroyItemRuleUi(__instance);
}

[HarmonyPatch(typeof(InventoryGui), "UpdateGamepad")]
internal static class ItemRuleEditorGamepadPatch
{
    private static bool Prefix() => !InventoryActionsPlugin.IsItemRuleInputBlocked();
}

[HarmonyPatch(typeof(InventoryGrid), "UpdateGamepad")]
internal static class ItemRuleEditorGridGamepadPatch
{
    private static bool Prefix() => !InventoryActionsPlugin.IsItemRuleInputBlocked();
}
