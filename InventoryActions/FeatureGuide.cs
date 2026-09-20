using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private const string FeatureGuideRootName = "InventoryActions_FeatureGuideHudHint";
    private const float FeatureGuideMaximumContentWidth = 500f;
    private const float FeatureGuideMinimumSideContentWidth = 320f;
    private const float FeatureGuideFallbackContentHeight = 176f;
    private const float FeatureGuideGap = 12f;
    private const float FeatureGuideHorizontalPadding = 10f;
    private const float FeatureGuideVerticalPadding = 8f;
    private const float FeatureGuideToggleSize = 24f;
    private static readonly Color FeatureGuideBackgroundColor = new(0.055f, 0.035f, 0.025f, 0.64f);
    private static readonly Color FeatureGuideTextColor = new(0.78f, 0.88f, 0.94f, 0.92f);
    private static readonly Color FeatureGuideToggleColor = new(1f, 0.663f, 0.302f, 0.92f);
    private static readonly Vector3[] FeatureGuideHotbarCorners = new Vector3[4];
    private static readonly UnityAction FeatureGuideToggleAction = ToggleFeatureGuideCollapsed;

    private static RectTransform? _featureGuideRoot;
    private static TMP_Text? _featureGuideText;
    private static RectTransform? _featureGuideToggle;
    private static TMP_Text? _featureGuideToggleArrow;
    private static Button? _featureGuideToggleButton;
    private static Canvas? _featureGuideToggleCanvas;
    private static GraphicRaycaster? _featureGuideToggleRaycaster;
    private static TMP_FontAsset? _featureGuideFont;
    private static Material? _featureGuideFontMaterial;
    private static string _featureGuideExpandedText = "";
    private static string _featureGuideTitle = "";
    private static string _featureGuideMeasuredText = "";
    private static int _featureGuideMeasuredFontId;
    private static float _featureGuideMeasuredWidth = -1f;
    private static Vector2 _featureGuideMeasuredSize = new(FeatureGuideMaximumContentWidth, FeatureGuideFallbackContentHeight);
    private static float _featureGuideNaturalContentWidth = FeatureGuideMaximumContentWidth;
    private static float _featureGuideTitleWidth = 180f;
    private static float _nextFeatureGuideTextRefreshTime;
    private static float _nextFeatureGuideFontLookupTime;

    private static void UpdateFeatureGuideHud()
    {
        if (_showFeatureGuide == null || _showFeatureGuide.Value != Toggle.On)
        {
            HideFeatureGuideHud();
            return;
        }

        GameObject? hudRootObject = Hud.instance != null ? Hud.instance.m_rootObject : null;
        RectTransform? parent = hudRootObject != null ? hudRootObject.GetComponent<RectTransform>() : null;
        Transform? hotbarTransform = hudRootObject != null ? hudRootObject.transform.Find("HotKeyBar") : null;
        if (hudRootObject == null || parent == null || hotbarTransform == null ||
            !TryResolveFeatureGuideFont(hudRootObject, hotbarTransform))
        {
            HideFeatureGuideHud();
            return;
        }

        EnsureFeatureGuideHud(parent);
        if (_featureGuideRoot == null || _featureGuideText == null)
        {
            HideFeatureGuideHud();
            return;
        }

        ApplyFeatureGuideFont(_featureGuideText);
        if (_featureGuideToggleArrow != null)
        {
            ApplyFeatureGuideFont(_featureGuideToggleArrow);
        }

        RefreshFeatureGuideText();
        HotkeyBar? hotbar = hotbarTransform.GetComponent<HotkeyBar>();
        float elementSpace = hotbar != null && hotbar.m_elementSpace > 1f ? hotbar.m_elementSpace : 70f;
        Vector3 hotbarOrigin = hotbarTransform.localPosition;
        float hotbarRight = ResolveFeatureGuideHotbarRight(parent, hotbar, hotbarOrigin, elementSpace);
        LayoutFeatureGuide(parent, hotbarOrigin, hotbarRight, elementSpace);
        SetFeatureGuideActive(true);
        UpdateFeatureGuideToggleInputLayer();
    }

    private static void EnsureFeatureGuideHud(RectTransform parent)
    {
        if (_featureGuideRoot != null && !IsUnityNull(_featureGuideRoot) && _featureGuideRoot.parent != parent)
        {
            ReleaseFeatureGuideObjects();
        }

        if (_featureGuideRoot != null && !IsUnityNull(_featureGuideRoot))
        {
            return;
        }

        Transform? stale = parent.Find(FeatureGuideRootName);
        if (stale != null)
        {
            stale.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(stale.gameObject);
        }

        GameObject rootObject = new(FeatureGuideRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootObject.SetActive(false);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        root.SetAsLastSibling();
        Image background = rootObject.GetComponent<Image>();
        background.color = FeatureGuideBackgroundColor;
        background.raycastTarget = false;

        TMP_Text guideText = CreateFeatureGuideText("Text", root, TextAlignmentOptions.TopLeft);

        GameObject toggleObject = new(
            "Toggle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        toggleObject.SetActive(false);
        RectTransform toggle = toggleObject.GetComponent<RectTransform>();
        toggle.SetParent(root, false);
        Image hitTarget = toggleObject.GetComponent<Image>();
        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = false;

        Button button = toggleObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hitTarget;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(FeatureGuideToggleAction);
        Canvas toggleCanvas = toggleObject.GetComponent<Canvas>();
        toggleCanvas.overrideSorting = false;
        GraphicRaycaster raycaster = toggleObject.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        TMP_Text arrow = CreateFeatureGuideText("Arrow", toggle, TextAlignmentOptions.Center);
        // Use an ASCII chevron so the game's static font never needs a fallback glyph.
        arrow.text = ">";
        arrow.fontSize = 18f;
        arrow.lineSpacing = 0f;
        arrow.color = FeatureGuideToggleColor;
        toggleObject.SetActive(true);

        _featureGuideRoot = root;
        _featureGuideText = guideText;
        _featureGuideToggle = toggle;
        _featureGuideToggleArrow = arrow;
        _featureGuideToggleButton = button;
        _featureGuideToggleCanvas = toggleCanvas;
        _featureGuideToggleRaycaster = raycaster;
        rootObject.SetActive(true);
    }

    private static TMP_Text CreateFeatureGuideText(string name, Transform parent, TextAlignmentOptions alignment)
    {
        // Assign Valheim's font before activation so TMP never looks for the absent LiberationSans fallback.
        GameObject textObject = new(name, typeof(RectTransform));
        textObject.SetActive(false);
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        ApplyFeatureGuideFont(text);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = false;
        text.fontSize = 12f;
        text.lineSpacing = -3f;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = FeatureGuideTextColor;
        text.raycastTarget = false;
        textObject.SetActive(true);
        return text;
    }

    private static bool TryResolveFeatureGuideFont(GameObject hudRoot, Transform hotbarTransform)
    {
        if (_featureGuideFont != null && !IsUnityNull(_featureGuideFont))
        {
            return true;
        }

        if (Time.unscaledTime < _nextFeatureGuideFontLookupTime)
        {
            return false;
        }

        _nextFeatureGuideFontLookupTime = Time.unscaledTime + 0.5f;
        TMP_Text? source = FindFeatureGuideFont(hotbarTransform.gameObject) ??
                           FindFeatureGuideFont(InventoryGui.instance != null ? InventoryGui.instance.gameObject : null) ??
                           FindFeatureGuideFont(hudRoot);
        if (source == null || source.font == null || IsUnityNull(source.font))
        {
            return false;
        }

        _featureGuideFont = source.font;
        _featureGuideFontMaterial = source.fontSharedMaterial;
        InvalidateFeatureGuideMeasurement();
        return true;
    }

    private static TMP_Text? FindFeatureGuideFont(GameObject? root)
    {
        if (root == null || IsUnityNull(root))
        {
            return null;
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            if (text == null || IsUnityNull(text) || text.font == null || IsUnityNull(text.font) ||
                (_featureGuideRoot != null && !IsUnityNull(_featureGuideRoot) && text.transform.IsChildOf(_featureGuideRoot)) ||
                (text.font.name ?? "").IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            return text;
        }

        return null;
    }

    private static void ApplyFeatureGuideFont(TMP_Text text)
    {
        if (_featureGuideFont == null || IsUnityNull(_featureGuideFont))
        {
            return;
        }

        text.font = _featureGuideFont;
        if (_featureGuideFontMaterial != null && !IsUnityNull(_featureGuideFontMaterial))
        {
            text.fontSharedMaterial = _featureGuideFontMaterial;
        }
    }

    private static void RefreshFeatureGuideText()
    {
        if (_featureGuideText == null ||
            (!string.IsNullOrEmpty(_featureGuideExpandedText) && Time.unscaledTime < _nextFeatureGuideTextRefreshTime))
        {
            return;
        }

        _nextFeatureGuideTextRefreshTime = Time.unscaledTime + 0.25f;
        bool controllerHints = UseInventoryControllerHints();
        string guide = controllerHints ? GetControllerFeatureGuideText() : LocalizeUi(
            "$inventoryactions_feature_guide",
            "<b>InventoryActions quick guide</b>\n<color=#FFA94D>[{favoriteKey} + Left Click]</color> on a player inventory slot: Toggle favorite\nWhile looking at a chest, <color=#FFA94D>[Hold {useKey}]</color>: Store matching non-favorited items in it and nearby chests\nWhile looking at a chest, <color=#FFA94D>[Hold {restockKey}]</color>: Refill favorite stacks from it and nearby chests up to their targets\nDrop a held item on <color=#FFA94D>Restock targets</color>: Set its favorite-stack target\nDrop a held item on <color=#FFA94D>Auto pickup exclusions / Trash</color>: Exclude auto pickup / delete after confirmation\nHide this guide: <color=#FFA94D>F1 → InventoryActions → Show Feature Guide → Off</color>");
        string favoriteKey = _favoriteModifierKey != null ? GetShortcutDisplayText(_favoriteModifierKey.Value) : "";
        string restockKey = controllerHints ? GetFavoriteRestockControllerDisplay() : GetContainerRestockKeyDisplayText();
        string expanded = guide
            .Replace("{favoriteKey}", string.IsNullOrWhiteSpace(favoriteKey) ? "—" : favoriteKey)
            .Replace("{useKey}", GetFeatureGuideUseKeyDisplayText())
            .Replace("{restockKey}", string.IsNullOrWhiteSpace(restockKey) ? "—" : restockKey)
            .Replace("{favoriteAction}", DisplayFeatureGuideBinding(GetInventoryControllerChordDisplay("JoyButtonA")))
            .Replace("{sortAction}", DisplayFeatureGuideBinding(GetInventoryControllerChordDisplay("JoyButtonX")))
            .Replace("{rulesAction}", DisplayFeatureGuideBinding(GetInventoryControllerChordDisplay("JoyButtonY")))
            .Replace("{excludeAction}", DisplayFeatureGuideBinding(GetInventoryControllerChordDisplay("JoyButtonB")))
            .Replace("{modeAction}", GetInventoryControllerActionDisplay("JoyButtonA"))
            .Replace("{removeAction}", GetInventoryControllerActionDisplay("JoyButtonX"))
            .Replace("{closeAction}", GetInventoryControllerActionDisplay("JoyButtonB"));
        if (!string.Equals(_featureGuideExpandedText, expanded, StringComparison.Ordinal))
        {
            _featureGuideExpandedText = expanded;
            int lineBreak = expanded.IndexOf('\n');
            _featureGuideTitle = lineBreak >= 0 ? expanded.Substring(0, lineBreak) : expanded;
            Vector2 natural = _featureGuideText.GetPreferredValues(expanded);
            _featureGuideNaturalContentWidth = IsUsableFeatureGuideMeasurement(natural.x)
                ? Mathf.Clamp(Mathf.Ceil(natural.x) + 1f, FeatureGuideMinimumSideContentWidth, FeatureGuideMaximumContentWidth)
                : FeatureGuideMaximumContentWidth;
            Vector2 titleNatural = _featureGuideText.GetPreferredValues(_featureGuideTitle);
            _featureGuideTitleWidth = IsUsableFeatureGuideMeasurement(titleNatural.x)
                ? Mathf.Ceil(titleNatural.x) + 1f
                : 180f;
            InvalidateFeatureGuideMeasurement();
        }
    }

    private static string DisplayFeatureGuideBinding(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string GetControllerFeatureGuideText()
    {
        bool korean = string.Equals(Localization.instance?.GetSelectedLanguage(), "Korean", StringComparison.OrdinalIgnoreCase);
        return LocalizeUi("$inventoryactions_feature_guide_controller", korean
            ? "<b>InventoryActions 빠른 가이드</b>\n선택한 플레이어 칸에서 <color=#FFA94D>[{favoriteAction}]</color>: 즐겨찾기 · <color=#FFA94D>[{sortAction}]</color>: 선택 중인 인벤토리/상자 정렬\n<color=#FFA94D>[{rulesAction}] / [{excludeAction}]</color>: 보충 대상 / 자동 줍기 제외 열기 (집어 든 내 아이템이 있으면 등록)\n상자를 보며 <color=#FFA94D>[{useKey} 길게]</color>: 즐겨찾기 외 같은 종류를 주변 상자에 보관\n상자를 보며 <color=#FFA94D>[{restockKey} 길게]</color>: 목표 수량·빈 칸 보충 모드에 따라 즐겨찾기 보충\n규칙 편집: 방향키 위/아래 행 선택 · 왼쪽/오른쪽 수량 · {modeAction} 모드 · {removeAction} 삭제 · {closeAction} 닫기\n가이드 숨기기: <color=#FFA94D>F1 → InventoryActions → Show Feature Guide → Off</color>"
            : "<b>InventoryActions quick guide</b>\nOn the selected player slot, <color=#FFA94D>[{favoriteAction}]</color>: Favorite · <color=#FFA94D>[{sortAction}]</color>: Sort focused inventory/chest\n<color=#FFA94D>[{rulesAction}] / [{excludeAction}]</color>: Restock targets / pickup exclusions (register a picked-up player item)\nLook at a chest, <color=#FFA94D>[Hold {useKey}]</color>: Store matching non-favorite items nearby\nLook at a chest, <color=#FFA94D>[Hold {restockKey}]</color>: Refill favorites using target quantities and empty-slot modes\nRules: D-pad up/down selects rows; left/right changes quantity; {modeAction} mode; {removeAction} remove; {closeAction} close\nHide this guide: <color=#FFA94D>F1 → InventoryActions → Show Feature Guide → Off</color>");
    }

    private static string GetFeatureGuideUseKeyDisplayText()
    {
        string action = ZInput.IsGamepadActive() ? "JoyUse" : "Use";
        try
        {
            if (Localization.instance != null)
            {
                string localized = Localization.instance.GetBoundKeyString(action, true);
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    return localized;
                }
            }
        }
        catch
        {
            // Fall through to the raw binding lookup.
        }

        try
        {
            string display = ZInput.instance != null ? ZInput.instance.GetBoundKeyString(action, true) : "";
            return string.IsNullOrWhiteSpace(display) ? "E" : display;
        }
        catch
        {
            return "E";
        }
    }

    private static float ResolveFeatureGuideHotbarRight(
        RectTransform parent,
        HotkeyBar? hotbar,
        Vector3 hotbarOrigin,
        float elementSpace)
    {
        float fallback = hotbarOrigin.x + PlayerInventoryWidth * elementSpace;
        if (hotbar?.m_elements == null || hotbar.m_elements.Count < PlayerInventoryWidth)
        {
            return fallback;
        }

        RectTransform? last = hotbar.m_elements[PlayerInventoryWidth - 1]?.m_go?.transform as RectTransform;
        if (last == null)
        {
            return fallback;
        }

        last.GetWorldCorners(FeatureGuideHotbarCorners);
        float right = float.NegativeInfinity;
        foreach (Vector3 corner in FeatureGuideHotbarCorners)
        {
            right = Mathf.Max(right, parent.InverseTransformPoint(corner).x);
        }

        return float.IsNaN(right) || float.IsInfinity(right) ? fallback : right;
    }

    private static void LayoutFeatureGuide(
        RectTransform parent,
        Vector3 hotbarOrigin,
        float hotbarRight,
        float elementSpace)
    {
        if (_featureGuideRoot == null || _featureGuideText == null)
        {
            return;
        }

        bool collapsed = _featureGuideCollapsed != null && _featureGuideCollapsed.Value == Toggle.On;
        string displayText = collapsed ? _featureGuideTitle : _featureGuideExpandedText;
        if (!string.Equals(_featureGuideText.text, displayText, StringComparison.Ordinal))
        {
            _featureGuideText.text = displayText;
            InvalidateFeatureGuideMeasurement();
        }

        Rect bounds = parent.rect;
        float expandedWidth = _featureGuideNaturalContentWidth + FeatureGuideHorizontalPadding * 2f;
        float collapsedWidth = Mathf.Min(
            expandedWidth,
            _featureGuideTitleWidth + FeatureGuideHorizontalPadding * 2f + FeatureGuideToggleSize);
        float desiredWidth = collapsed ? collapsedWidth : expandedWidth;
        float left = hotbarRight + FeatureGuideGap;
        bool fallbackPlacement = false;
        if (bounds.width > 0f && bounds.height > 0f)
        {
            float availableRight = bounds.xMax - FeatureGuideGap - left;
            if (availableRight >= FeatureGuideMinimumSideContentWidth + FeatureGuideHorizontalPadding * 2f)
            {
                desiredWidth = Mathf.Min(desiredWidth, availableRight);
            }
            else
            {
                fallbackPlacement = true;
                desiredWidth = Mathf.Min(desiredWidth, Mathf.Max(1f, bounds.width - FeatureGuideGap * 2f));
                float minimumLeft = bounds.xMin + FeatureGuideGap;
                float maximumLeft = bounds.xMax - FeatureGuideGap - desiredWidth;
                left = maximumLeft >= minimumLeft
                    ? Mathf.Clamp(hotbarOrigin.x - FeatureGuideHorizontalPadding, minimumLeft, maximumLeft)
                    : minimumLeft;
            }
        }

        float contentWidth = Mathf.Max(1f, desiredWidth - FeatureGuideHorizontalPadding * 2f);
        Vector2 measured = MeasureFeatureGuideText(displayText, contentWidth);
        float desiredHeight = Mathf.Max(
            FeatureGuideToggleSize + FeatureGuideVerticalPadding * 2f,
            measured.y + FeatureGuideVerticalPadding * 2f);
        float height = bounds.height > 0f
            ? Mathf.Min(desiredHeight, Mathf.Max(1f, bounds.height - FeatureGuideGap * 2f))
            : desiredHeight;
        _featureGuideText.overflowMode = height + 0.1f < desiredHeight
            ? TextOverflowModes.Ellipsis
            : TextOverflowModes.Overflow;

        float top = hotbarOrigin.y + elementSpace * 0.5f + FeatureGuideVerticalPadding;
        if (bounds.width > 0f && bounds.height > 0f)
        {
            if (fallbackPlacement)
            {
                float below = hotbarOrigin.y - elementSpace * 0.5f - FeatureGuideGap;
                float above = hotbarOrigin.y + elementSpace * 0.5f + FeatureGuideGap + height;
                bool fitsBelow = below - height >= bounds.yMin + FeatureGuideGap;
                bool fitsAbove = above <= bounds.yMax - FeatureGuideGap;
                top = fitsBelow || !fitsAbove ? below : above;
            }

            top = Mathf.Clamp(top, bounds.yMin + FeatureGuideGap + height, bounds.yMax - FeatureGuideGap);
        }

        RectTransform root = _featureGuideRoot;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        root.localPosition = new Vector3(Mathf.Round(left), Mathf.Round(top), hotbarOrigin.z);
        root.sizeDelta = new Vector2(Mathf.Ceil(desiredWidth), Mathf.Ceil(height));

        RectTransform textRect = _featureGuideText.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(FeatureGuideHorizontalPadding, -FeatureGuideVerticalPadding);
        textRect.sizeDelta = new Vector2(contentWidth, Mathf.Max(1f, height - FeatureGuideVerticalPadding * 2f));
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;
        LayoutFeatureGuideToggle(collapsed, desiredWidth);
    }

    private static Vector2 MeasureFeatureGuideText(string value, float width)
    {
        if (_featureGuideText == null)
        {
            return new Vector2(width, FeatureGuideFallbackContentHeight);
        }

        int fontId = _featureGuideText.font != null && !IsUnityNull(_featureGuideText.font)
            ? _featureGuideText.font.GetInstanceID()
            : 0;
        if (!string.Equals(_featureGuideMeasuredText, value, StringComparison.Ordinal) ||
            _featureGuideMeasuredFontId != fontId ||
            Mathf.Abs(_featureGuideMeasuredWidth - width) > 0.1f)
        {
            Vector2 preferred = _featureGuideText.GetPreferredValues(value, width, 0f);
            _featureGuideMeasuredSize = IsUsableFeatureGuideMeasurement(preferred.y)
                ? new Vector2(width, Mathf.Ceil(preferred.y) + 1f)
                : new Vector2(width, FeatureGuideFallbackContentHeight);
            _featureGuideMeasuredText = value;
            _featureGuideMeasuredFontId = fontId;
            _featureGuideMeasuredWidth = width;
        }

        return _featureGuideMeasuredSize;
    }

    private static bool IsUsableFeatureGuideMeasurement(float value) =>
        value >= 1f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static void InvalidateFeatureGuideMeasurement()
    {
        _featureGuideMeasuredText = "";
        _featureGuideMeasuredFontId = 0;
        _featureGuideMeasuredWidth = -1f;
    }

    private static void LayoutFeatureGuideToggle(bool collapsed, float width)
    {
        if (_featureGuideToggle == null || _featureGuideToggleArrow == null)
        {
            return;
        }

        RectTransform toggle = _featureGuideToggle;
        toggle.anchorMin = toggle.anchorMax = new Vector2(0f, 1f);
        toggle.pivot = new Vector2(0f, 1f);
        toggle.anchoredPosition = new Vector2(Mathf.Max(0f, width - FeatureGuideToggleSize - 4f), -FeatureGuideVerticalPadding);
        toggle.sizeDelta = new Vector2(FeatureGuideToggleSize, FeatureGuideToggleSize);
        toggle.localScale = Vector3.one;
        toggle.localRotation = Quaternion.identity;
        toggle.SetAsLastSibling();

        RectTransform arrow = _featureGuideToggleArrow.rectTransform;
        arrow.anchorMin = Vector2.zero;
        arrow.anchorMax = Vector2.one;
        arrow.pivot = new Vector2(0.5f, 0.5f);
        arrow.offsetMin = arrow.offsetMax = Vector2.zero;
        arrow.localScale = Vector3.one;
        arrow.localRotation = collapsed
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.Euler(0f, 0f, 90f);
    }

    private static void ToggleFeatureGuideCollapsed()
    {
        if (_featureGuideCollapsed == null || !CanInteractWithFeatureGuideToggle())
        {
            return;
        }

        _featureGuideCollapsed.Value = _featureGuideCollapsed.Value == Toggle.On ? Toggle.Off : Toggle.On;
    }

    private static void UpdateFeatureGuideToggleInputLayer()
    {
        if (_featureGuideToggle == null || IsUnityNull(_featureGuideToggle) ||
            _featureGuideToggleButton == null || IsUnityNull(_featureGuideToggleButton) ||
            _featureGuideToggleCanvas == null || IsUnityNull(_featureGuideToggleCanvas) ||
            _featureGuideToggleRaycaster == null || IsUnityNull(_featureGuideToggleRaycaster))
        {
            return;
        }

        bool inventoryVisible = InventoryGui.IsVisible();
        bool canInteract = CanInteractWithFeatureGuideToggle();
        Canvas? inventoryCanvas = inventoryVisible && InventoryGui.instance != null
            ? InventoryGui.instance.GetComponentInParent<Canvas>()
            : null;
        Canvas? sortingSource = inventoryCanvas != null && inventoryCanvas.overrideSorting
            ? inventoryCanvas
            : inventoryCanvas?.rootCanvas;
        bool elevate = inventoryVisible && canInteract && sortingSource != null && !IsUnityNull(sortingSource);
        _featureGuideToggleCanvas.overrideSorting = elevate;
        if (elevate)
        {
            _featureGuideToggleCanvas.sortingLayerID = sortingSource!.sortingLayerID;
            _featureGuideToggleCanvas.sortingOrder = Math.Min(short.MaxValue, sortingSource.sortingOrder + 1);
        }

        bool enabled = inventoryVisible ? elevate : canInteract;
        _featureGuideToggleRaycaster.enabled = enabled;
        _featureGuideToggleButton.interactable = enabled;
        Image? hitTarget = _featureGuideToggle.GetComponent<Image>();
        if (hitTarget != null)
        {
            hitTarget.raycastTarget = enabled;
        }
    }

    private static bool CanInteractWithFeatureGuideToggle()
    {
        if (_featureGuideRoot == null || IsUnityNull(_featureGuideRoot) ||
            !_featureGuideRoot.gameObject.activeInHierarchy ||
            _featureGuideToggle == null || IsUnityNull(_featureGuideToggle) ||
            !_featureGuideToggle.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!InventoryGui.IsVisible())
        {
            return true;
        }

        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || gui.m_dragGo != null || gui.m_dragItem != null || IsItemRuleInputBlocked())
        {
            return false;
        }

        return (gui.m_splitDialog == null || !gui.m_splitDialog.IsActive) &&
               !IsActiveFeatureGuideDialog(gui.m_variantDialog) &&
               !IsActiveFeatureGuideDialog(gui.m_skillsDialog) &&
               !IsActiveFeatureGuideDialog(gui.m_textsDialog) &&
               !IsActiveFeatureGuideDialog(gui.m_achievementsPanel) &&
               !IsActiveFeatureGuideObject(gui.m_trophiesPanel) &&
               !IsActiveFeatureGuideObject(Runtime.TrashConfirmDialog);
    }

    private static bool IsActiveFeatureGuideDialog(Component? dialog) =>
        dialog != null && !IsUnityNull(dialog) && dialog.gameObject.activeInHierarchy;

    private static bool IsActiveFeatureGuideObject(GameObject? gameObject) =>
        gameObject != null && !IsUnityNull(gameObject) && gameObject.activeInHierarchy;

    private static void HideFeatureGuideHud()
    {
        SetFeatureGuideActive(false);
        UpdateFeatureGuideToggleInputLayer();
    }

    private static void SetFeatureGuideActive(bool active)
    {
        if (_featureGuideRoot != null && !IsUnityNull(_featureGuideRoot) &&
            _featureGuideRoot.gameObject.activeSelf != active)
        {
            _featureGuideRoot.gameObject.SetActive(active);
        }
    }

    private static void DestroyFeatureGuideHud()
    {
        ReleaseFeatureGuideObjects();
        _featureGuideFont = null;
        _featureGuideFontMaterial = null;
        _featureGuideExpandedText = "";
        _featureGuideTitle = "";
        _nextFeatureGuideTextRefreshTime = 0f;
        _nextFeatureGuideFontLookupTime = 0f;
        InvalidateFeatureGuideMeasurement();
    }

    private static void ReleaseFeatureGuideObjects()
    {
        if (_featureGuideToggleButton != null && !IsUnityNull(_featureGuideToggleButton))
        {
            _featureGuideToggleButton.onClick.RemoveListener(FeatureGuideToggleAction);
        }

        if (_featureGuideRoot != null && !IsUnityNull(_featureGuideRoot))
        {
            _featureGuideRoot.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(_featureGuideRoot.gameObject);
        }

        _featureGuideRoot = null;
        _featureGuideText = null;
        _featureGuideToggle = null;
        _featureGuideToggleArrow = null;
        _featureGuideToggleButton = null;
        _featureGuideToggleCanvas = null;
        _featureGuideToggleRaycaster = null;
    }
}
