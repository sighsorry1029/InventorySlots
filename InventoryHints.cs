using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float FeatureGuideMaxContentWidth = 500f;
    private const float FeatureGuideMinimumSideContentWidth = 320f;
    private const float FeatureGuideFallbackContentHeight = 176f;
    private const float FeatureGuideGap = 12f;
    private const float FeatureGuideHorizontalPadding = 10f;
    private const float FeatureGuideVerticalPadding = 8f;
    private const float FeatureGuideToggleSize = 24f;
    private const float FeatureGuideToggleInset = 4f;
    private const float FeatureGuideToggleGap = 4f;
    private const float HotbarSwitchHintIconSize = 14f;
    private const float HotbarSwitchHintIconGap = 4f;
    private const float HotbarSwitchHintHorizontalPadding = 4f;
    private const float HotbarSwitchHintMinimumTextWidth = 72f;
    private const float HotbarSwitchHintMinimumWidth =
        HotbarSwitchHintIconSize +
        HotbarSwitchHintIconGap +
        HotbarSwitchHintMinimumTextWidth +
        HotbarSwitchHintHorizontalPadding * 2f;
    private const float HotbarSwitchHintMaximumWidth = 280f;
    private const float HotbarSwitchHintHeight = 38f;
    private const float HotbarSwitchHintGap = 8f;
    private const string HotbarSwitchHintRootName = "InventorySlots_HotbarSwitchHudHint";
    private const string HotbarSwitchHintIconName = "DirectionIcon";
    private const string HotbarSwitchHintTextName = "Label";

    private static readonly Color FeatureGuideBackgroundColor = new(0.055f, 0.035f, 0.025f, 0.64f);
    private static readonly Color FeatureGuideToggleColor = new(1f, 0.663f, 0.302f, 0.92f);
    private static readonly Vector3[] HotbarElementWorldCorners = new Vector3[4];

    private static void UpdateInventorySideHints(InventoryGrid playerGrid, Vector3 origin, float elementSpace, int totalRegularRows)
    {
        if (playerGrid?.m_gridRoot == null || !InventoryGui.IsVisible())
        {
            HideInventorySideHints();
            return;
        }

        UpdateInventoryWheelHint(playerGrid, origin, elementSpace, totalRegularRows);
    }

    private static void UpdateHotbarSwitchHud()
    {
        GameObject? hudRoot = Hud.instance != null ? Hud.instance.m_rootObject : null;
        if (hudRoot == null)
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
            SetHintActive(TooltipUi.FeatureGuideHudHint, false);
            UpdateFeatureGuideToggleInputLayer();
            return;
        }

        Transform? hotKeyBarTransform = hudRoot!.transform.Find("HotKeyBar");
        RectTransform parent = hudRoot.GetComponent<RectTransform>();
        if (parent == null)
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
            SetHintActive(TooltipUi.FeatureGuideHudHint, false);
            UpdateFeatureGuideToggleInputLayer();
            return;
        }

        float elementSpace = hotKeyBarTransform != null ? GetHudElementSpace() : 70f;
        Vector3 hotbarOrigin = hotKeyBarTransform != null ? hotKeyBarTransform.localPosition : Vector3.zero;
        string keyText = GetHotbarSwitchKeyDisplayText();
        bool switchVisible = _showHotbarSwitchHint != null && _showHotbarSwitchHint.Value.IsOn() &&
                             !string.IsNullOrWhiteSpace(keyText);
        Vector3 switchPosition = ResolveHotbarSwitchHintPosition(
            parent,
            hotKeyBarTransform,
            hotbarOrigin,
            elementSpace);
        if (switchVisible)
        {
            TooltipUi.HotbarSwitchHudHint = EnsureHotbarSwitchHud(parent);
            if (TooltipUi.HotbarSwitchHudHint != null && TooltipUi.HotbarSwitchHudHintText != null)
            {
                SetHintActive(TooltipUi.HotbarSwitchHudHint, true);
                string hintText = LocalizeUi("$inventoryslots_hotbar_switch_hint", "Switch [{key}]")
                    .Replace("{key}", keyText);
                LayoutHotbarSwitchHint(
                    TooltipUi.HotbarSwitchHudHint,
                    TooltipUi.HotbarSwitchHudHintText,
                    hintText);
                SetLocalPositionIfChanged(TooltipUi.HotbarSwitchHudHint, switchPosition);
            }
        }
        else
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
        }

        UpdateFeatureGuideHud(parent, hotbarOrigin, elementSpace, switchPosition, switchVisible);
    }

    private static RectTransform EnsureHotbarSwitchHud(RectTransform parent)
    {
        RectTransform? root = TooltipUi.HotbarSwitchHudHint;
        if (root == null ||
            IsUnityNull(root) ||
            root.parent != parent ||
            !string.Equals(root.name, HotbarSwitchHintRootName, StringComparison.Ordinal))
        {
            if (root != null && !IsUnityNull(root))
            {
                SetHintActive(root, false);
            }

            root = null;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (!string.Equals(child.name, HotbarSwitchHintRootName, StringComparison.Ordinal))
            {
                continue;
            }

            RectTransform? candidate = child.GetComponent<RectTransform>();
            if (root == null && candidate != null)
            {
                root = candidate;
                continue;
            }

            if (candidate == root)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }

        bool refreshSiblingOrder = root == null || !root.gameObject.activeSelf;
        if (root == null)
        {
            GameObject rootObject = new(HotbarSwitchHintRootName, typeof(RectTransform));
            rootObject.SetActive(false);
            root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
        }

        TMP_Text? legacyRootText = root.GetComponent<TMP_Text>();
        if (legacyRootText != null)
        {
            legacyRootText.enabled = false;
            legacyRootText.raycastTarget = false;
            UnityEngine.Object.Destroy(legacyRootText);
        }

        TMP_Text? text = null;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!string.Equals(child.name, HotbarSwitchHintTextName, StringComparison.Ordinal))
            {
                continue;
            }

            TMP_Text? candidate = child.GetComponent<TMP_Text>();
            if (text == null && candidate != null)
            {
                text = candidate;
                continue;
            }

            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }

        if (text == null)
        {
            CreateTextRect(HotbarSwitchHintTextName, root, out text);
        }

        if (!text.gameObject.activeSelf)
        {
            text.gameObject.SetActive(true);
        }

        if (text.font == null || IsUnityNull(text.font))
        {
            ApplyDefaultFontAsset(text);
        }

        text.raycastTarget = false;
        if (refreshSiblingOrder)
        {
            root.SetAsLastSibling();
        }

        TooltipUi.HotbarSwitchHudHint = root;
        TooltipUi.HotbarSwitchHudHintText = text;
        return root;
    }

    private static Vector3 ResolveHotbarSwitchHintPosition(
        RectTransform parent,
        Transform? hotKeyBarTransform,
        Vector3 hotbarOrigin,
        float elementSpace)
    {
        float hotbarRight = hotbarOrigin.x + InventoryWidth * elementSpace;
        HotkeyBar? hotbar = hotKeyBarTransform != null
            ? hotKeyBarTransform.GetComponent<HotkeyBar>()
            : null;
        if (hotbar?.m_elements != null && hotbar.m_elements.Count >= InventoryWidth)
        {
            HotkeyBar.ElementData? lastElement = hotbar.m_elements[InventoryWidth - 1];
            GameObject? lastElementObject = lastElement?.m_go;
            RectTransform? lastElementRect = lastElementObject != null
                ? lastElementObject.transform as RectTransform
                : null;
            if (lastElementRect != null)
            {
                lastElementRect.GetWorldCorners(HotbarElementWorldCorners);
                float measuredRight = float.NegativeInfinity;
                for (int i = 0; i < HotbarElementWorldCorners.Length; i++)
                {
                    float localX = parent
                        .InverseTransformPoint(HotbarElementWorldCorners[i])
                        .x;
                    measuredRight = Mathf.Max(measuredRight, localX);
                }

                if (IsFiniteUiCoordinate(measuredRight))
                {
                    hotbarRight = measuredRight;
                }
            }
        }

        return new Vector3(
            Mathf.Round(hotbarRight + HotbarSwitchHintGap + HotbarSwitchHintOffset.x),
            Mathf.Round(hotbarOrigin.y + HotbarSwitchHintOffset.y),
            hotbarOrigin.z);
    }

    private static bool IsFiniteUiCoordinate(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static void SetLocalPositionIfChanged(RectTransform rect, Vector3 position)
    {
        if ((rect.localPosition - position).sqrMagnitude > 0.001f)
        {
            rect.localPosition = position;
        }
    }

    private static void LayoutHotbarSwitchHint(
        RectTransform root,
        TMP_Text text,
        string value)
    {
        Vector2 baseSize = GetHotbarSwitchHintBaseSize();
        Image icon = EnsureHotbarSwitchHintIcon(root, out bool iconChanged);
        int fontId = text.font != null && !IsUnityNull(text.font)
            ? text.font.GetInstanceID()
            : 0;
        int rootId = root.GetInstanceID();
        int textId = text.GetInstanceID();
        bool layoutChanged =
            iconChanged ||
            !string.Equals(TooltipUi.HotbarSwitchHudLayoutText, value, StringComparison.Ordinal) ||
            !string.Equals(text.text, value, StringComparison.Ordinal) ||
            !text.enabled ||
            TooltipUi.HotbarSwitchHudLayoutFontId != fontId ||
            TooltipUi.HotbarSwitchHudLayoutRootId != rootId ||
            TooltipUi.HotbarSwitchHudLayoutTextId != textId ||
            !IsUsableUiMeasurement(root.sizeDelta.x) ||
            !IsUsableUiMeasurement(root.sizeDelta.y);
        if (!layoutChanged)
        {
            return;
        }

        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;

        if (!string.Equals(text.text, value, StringComparison.Ordinal))
        {
            text.text = value;
        }

        text.fontSize = HotbarSwitchHintFontSize;
        text.lineSpacing = 0f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = HotbarSwitchHintColor;
        text.margin = Vector4.zero;
        text.enableAutoSizing = false;
        text.enabled = true;

        Vector2 preferred = text.GetPreferredValues(value ?? "");
        float naturalTextWidth = IsUsableUiMeasurement(preferred.x)
            ? Mathf.Ceil(preferred.x) + 1f
            : baseSize.x - HotbarSwitchHintIconSize - HotbarSwitchHintIconGap;
        float desiredWidth =
            naturalTextWidth +
            HotbarSwitchHintIconSize +
            HotbarSwitchHintIconGap +
            HotbarSwitchHintHorizontalPadding * 2f;
        float resolvedWidth = Mathf.Ceil(Mathf.Clamp(
            desiredWidth,
            baseSize.x,
            HotbarSwitchHintMaximumWidth));
        float renderedTextWidth = Mathf.Min(
            naturalTextWidth,
            Mathf.Max(1f, resolvedWidth -
            HotbarSwitchHintHorizontalPadding * 2f -
            HotbarSwitchHintIconSize -
            HotbarSwitchHintIconGap));
        float groupWidth =
            HotbarSwitchHintIconSize +
            HotbarSwitchHintIconGap +
            renderedTextWidth;
        float groupLeft = Mathf.Round((resolvedWidth - groupWidth) * 0.5f);

        root.sizeDelta = new Vector2(resolvedWidth, baseSize.y);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(
            groupLeft + HotbarSwitchHintIconSize + HotbarSwitchHintIconGap,
            0f);
        textRect.offsetMax = new Vector2(-groupLeft, 0f);
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;

        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(
            groupLeft + HotbarSwitchHintIconSize * 0.5f,
            0f);
        iconRect.sizeDelta = new Vector2(
            HotbarSwitchHintIconSize,
            HotbarSwitchHintIconSize);
        iconRect.localScale = Vector3.one;
        iconRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
        icon.color = HotbarSwitchHintColor;
        text.ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: true);

        TooltipUi.HotbarSwitchHudLayoutText = value ?? "";
        TooltipUi.HotbarSwitchHudLayoutFontId = fontId;
        TooltipUi.HotbarSwitchHudLayoutRootId = rootId;
        TooltipUi.HotbarSwitchHudLayoutTextId = textId;
    }

    private static Vector2 GetHotbarSwitchHintBaseSize() =>
        new(HotbarSwitchHintMinimumWidth, HotbarSwitchHintHeight);

    private static Image EnsureHotbarSwitchHintIcon(RectTransform root, out bool changed)
    {
        changed = false;
        Image? icon = null;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!string.Equals(child.name, HotbarSwitchHintIconName, StringComparison.Ordinal))
            {
                continue;
            }

            Image? candidate = child.GetComponent<Image>();
            if (icon == null && candidate != null)
            {
                icon = candidate;
                continue;
            }

            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
            changed = true;
        }

        if (icon == null)
        {
            GameObject iconObject = new(HotbarSwitchHintIconName, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root, false);
            icon = iconObject.GetComponent<Image>();
            changed = true;
        }

        if (!icon.gameObject.activeSelf)
        {
            icon.gameObject.SetActive(true);
            changed = true;
        }

        Sprite triangle = GetTriangleUiSprite();
        if (icon.sprite != triangle)
        {
            icon.sprite = triangle;
            changed = true;
        }

        if (!icon.preserveAspect)
        {
            icon.preserveAspect = true;
            changed = true;
        }

        if (icon.raycastTarget)
        {
            icon.raycastTarget = false;
            changed = true;
        }

        return icon;
    }

    private static void UpdateFeatureGuideHud(
        RectTransform parent,
        Vector3 hotbarOrigin,
        float elementSpace,
        Vector3 switchPosition,
        bool switchVisible)
    {
        bool visible = _showFeatureGuide != null && _showFeatureGuide.Value.IsOn();
        if (!visible)
        {
            SetHintActive(TooltipUi.FeatureGuideHudHint, false);
            UpdateFeatureGuideToggleInputLayer();
            return;
        }

        EnsureFeatureGuideHud(parent);
        if (TooltipUi.FeatureGuideHudHint == null ||
            TooltipUi.FeatureGuideHudTitleText == null ||
            TooltipUi.FeatureGuideHudHintText == null)
        {
            return;
        }

        TMP_Text titleText = TooltipUi.FeatureGuideHudTitleText;
        TMP_Text bodyText = TooltipUi.FeatureGuideHudHintText;
        ConfigureFeatureGuideText(titleText, TextWrappingModes.NoWrap);
        ConfigureFeatureGuideText(bodyText, TextWrappingModes.Normal);
        RefreshFeatureGuideText(titleText, bodyText);
        RefreshFeatureGuideMeasurementCache(titleText, bodyText);

        bool collapsed = IsFeatureGuideCollapsed();
        Vector2 switchSize = switchVisible && TooltipUi.HotbarSwitchHudHint != null
            ? TooltipUi.HotbarSwitchHudHint.sizeDelta
            : GetHotbarSwitchHintBaseSize();
        float switchWidth = switchSize.x;
        float switchHeight = switchSize.y;
        float precedingRight = switchVisible
            ? switchPosition.x + switchWidth
            : switchPosition.x - HotbarSwitchHintGap;
        float outerLeft = precedingRight + FeatureGuideGap;

        float desiredTextWidth = Mathf.Clamp(
            TooltipUi.FeatureGuideNaturalWidth,
            FeatureGuideMinimumSideContentWidth,
            FeatureGuideMaxContentWidth);
        float minimumHeaderOuterWidth =
            FeatureGuideHorizontalPadding +
            TooltipUi.FeatureGuideTitleWidth +
            FeatureGuideToggleGap +
            FeatureGuideToggleSize +
            FeatureGuideToggleInset;
        float desiredExpandedOuterWidth = Mathf.Max(
            desiredTextWidth + FeatureGuideHorizontalPadding * 2f,
            minimumHeaderOuterWidth);
        float expandedOuterWidth = desiredExpandedOuterWidth;
        bool useFallbackPlacement = false;

        Rect parentRect = parent.rect;
        if (parentRect.width > 0f && parentRect.height > 0f)
        {
            float availableRightWidth = parentRect.xMax - FeatureGuideGap - outerLeft;
            float minimumSideOuterWidth = Mathf.Min(
                desiredExpandedOuterWidth,
                FeatureGuideMinimumSideContentWidth + FeatureGuideHorizontalPadding * 2f);
            if (availableRightWidth >= minimumSideOuterWidth)
            {
                expandedOuterWidth = Mathf.Min(desiredExpandedOuterWidth, availableRightWidth);
            }
            else
            {
                useFallbackPlacement = true;
                float maximumOuterWidth = Mathf.Max(1f, parentRect.width - FeatureGuideGap * 2f);
                expandedOuterWidth = Mathf.Min(desiredExpandedOuterWidth, maximumOuterWidth);
                float minimumOuterLeft = parentRect.xMin + FeatureGuideGap;
                float maximumOuterLeft = parentRect.xMax - FeatureGuideGap - expandedOuterWidth;
                outerLeft = maximumOuterLeft >= minimumOuterLeft
                    ? Mathf.Clamp(
                        hotbarOrigin.x - FeatureGuideHorizontalPadding,
                        minimumOuterLeft,
                        maximumOuterLeft)
                    : minimumOuterLeft;
            }
        }

        float expandedBodyWidth = Mathf.Max(
            1f,
            expandedOuterWidth - FeatureGuideHorizontalPadding * 2f);
        float measuredExpandedBodyHeight = GetFeatureGuideExpandedBodyHeight(
            bodyText,
            expandedBodyWidth);
        float headerHeight = Mathf.Max(
            TooltipUi.FeatureGuideTitleHeight,
            FeatureGuideToggleSize);
        float expandedBodyHeight = measuredExpandedBodyHeight;
        if (parentRect.width > 0f && parentRect.height > 0f)
        {
            float maximumBodyHeight = Mathf.Max(
                1f,
                parentRect.height -
                FeatureGuideGap * 2f -
                headerHeight -
                FeatureGuideVerticalPadding * 2f);
            expandedBodyHeight = Mathf.Min(expandedBodyHeight, maximumBodyHeight);
        }

        bodyText.overflowMode = expandedBodyHeight + 0.1f < measuredExpandedBodyHeight
            ? TextOverflowModes.Ellipsis
            : TextOverflowModes.Overflow;
        float expandedOuterHeight =
            headerHeight +
            expandedBodyHeight +
            FeatureGuideVerticalPadding * 2f;

        float outerTop = switchPosition.y + switchHeight * 0.5f + FeatureGuideVerticalPadding;
        if (parentRect.width > 0f && parentRect.height > 0f)
        {
            if (useFallbackPlacement)
            {
                float belowHotbar = hotbarOrigin.y - elementSpace * 0.5f - FeatureGuideGap;
                float aboveHotbar = hotbarOrigin.y + elementSpace * 0.5f + FeatureGuideGap + expandedOuterHeight;
                bool fitsBelow = belowHotbar - expandedOuterHeight >= parentRect.yMin + FeatureGuideGap;
                bool fitsAbove = aboveHotbar <= parentRect.yMax - FeatureGuideGap;
                outerTop = fitsBelow || !fitsAbove ? belowHotbar : aboveHotbar;
            }

            float minimumOuterTop = parentRect.yMin + FeatureGuideGap + expandedOuterHeight;
            float maximumOuterTop = parentRect.yMax - FeatureGuideGap;
            outerTop = maximumOuterTop >= minimumOuterTop
                ? Mathf.Clamp(outerTop, minimumOuterTop, maximumOuterTop)
                : maximumOuterTop;
        }

        float collapsedOuterWidth = Mathf.Min(
            expandedOuterWidth,
            Mathf.Max(1f, minimumHeaderOuterWidth));
        float collapsedOuterHeight = headerHeight + FeatureGuideVerticalPadding * 2f;
        float actualOuterWidth = collapsed ? collapsedOuterWidth : expandedOuterWidth;
        float actualOuterHeight = collapsed ? collapsedOuterHeight : expandedOuterHeight;
        float availableTitleWidth = Mathf.Max(
            1f,
            actualOuterWidth -
            FeatureGuideHorizontalPadding -
            FeatureGuideToggleGap -
            FeatureGuideToggleSize -
            FeatureGuideToggleInset);
        float renderedTitleWidth = Mathf.Min(
            TooltipUi.FeatureGuideTitleWidth,
            availableTitleWidth);

        TooltipUi.FeatureGuideHudHint.anchorMin = new Vector2(0.5f, 0.5f);
        TooltipUi.FeatureGuideHudHint.anchorMax = new Vector2(0.5f, 0.5f);
        TooltipUi.FeatureGuideHudHint.pivot = new Vector2(0f, 1f);
        TooltipUi.FeatureGuideHudHint.localPosition = new Vector3(
            outerLeft,
            outerTop,
            switchPosition.z);
        TooltipUi.FeatureGuideHudHint.sizeDelta = new Vector2(
            actualOuterWidth,
            actualOuterHeight);

        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(
            FeatureGuideHorizontalPadding,
            -FeatureGuideVerticalPadding -
            (headerHeight - TooltipUi.FeatureGuideTitleHeight) * 0.5f);
        titleRect.sizeDelta = new Vector2(
            renderedTitleWidth,
            TooltipUi.FeatureGuideTitleHeight);
        titleText.overflowMode = renderedTitleWidth + 0.1f < TooltipUi.FeatureGuideTitleWidth
            ? TextOverflowModes.Ellipsis
            : TextOverflowModes.Overflow;

        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(0f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = new Vector2(
            FeatureGuideHorizontalPadding,
            -FeatureGuideVerticalPadding - headerHeight);
        bodyRect.sizeDelta = new Vector2(expandedBodyWidth, expandedBodyHeight);
        bodyText.gameObject.SetActive(!collapsed);

        LayoutFeatureGuideToggle(collapsed, headerHeight, actualOuterWidth);
        SetHintActive(TooltipUi.FeatureGuideHudHint, true);
        UpdateFeatureGuideToggleInputLayer();
    }

    private static void ConfigureFeatureGuideText(TMP_Text text, TextWrappingModes wrappingMode)
    {
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = wrappingMode;
        text.enableAutoSizing = false;
        text.fontSize = 12f;
        text.lineSpacing = -3f;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = new Color(0.78f, 0.88f, 0.94f, 0.92f);
    }

    private static void RefreshFeatureGuideText(TMP_Text title, TMP_Text body)
    {
        if (string.IsNullOrEmpty(TooltipUi.FeatureGuideExpandedText) ||
            Time.unscaledTime >= TooltipUi.NextFeatureGuideTextRefreshTime)
        {
            TooltipUi.NextFeatureGuideTextRefreshTime = Time.unscaledTime + 0.25f;
            string guide = LocalizeUi(
                "$inventoryslots_feature_guide",
                "<b>InventorySlots quick guide</b>\n<color=#FFA94D>[{tooltipKey}]</color> over an inventory or chest item, or a crafting recipe: Pin tooltip\n<color=#FFA94D>[{favoriteKey}]</color> over an inventory slot or crafting recipe: Toggle favorite\nWhile looking at a chest, <color=#FFA94D>[Hold {useKey}]</color>: Store matching items nearby (favorite slots excluded)\nWhile looking at a chest, <color=#FFA94D>[Hold {restockKey}]</color>: Refill items in favorite slots from nearby chests\nPer-item restock targets: <color=#FFA94D>F1 → InventorySlots → 3 - Restock</color>\nCustom slots and rules: <color=#FFA94D>config/InventorySlots/InventorySlots.yml</color>\nHide this guide now: <color=#FFA94D>F1 → InventorySlots → Show Feature Guide → Off</color>");
            TooltipUi.FeatureGuideExpandedText = guide
                .Replace("{tooltipKey}", GetPinnedTooltipKeyDisplayText())
                .Replace("{favoriteKey}", GetFavoriteKeyHintDisplayText())
                .Replace("{useKey}", GetContainerQuickStackKeyDisplayText())
                .Replace("{restockKey}", GetContainerRestockKeyDisplayText());
        }

        string titleValue = GetFeatureGuideTitle(TooltipUi.FeatureGuideExpandedText);
        string bodyValue = GetFeatureGuideBody(TooltipUi.FeatureGuideExpandedText);
        if (!string.Equals(title.text, titleValue, StringComparison.Ordinal))
        {
            title.text = titleValue;
        }
        if (!string.Equals(body.text, bodyValue, StringComparison.Ordinal))
        {
            body.text = bodyValue;
        }
    }

    private static string GetFeatureGuideTitle(string expandedText)
    {
        int lineBreak = expandedText.IndexOf('\n');
        return lineBreak >= 0 ? expandedText.Substring(0, lineBreak) : expandedText;
    }

    private static string GetFeatureGuideBody(string expandedText)
    {
        int lineBreak = expandedText.IndexOf('\n');
        return lineBreak >= 0 && lineBreak + 1 < expandedText.Length
            ? expandedText.Substring(lineBreak + 1)
            : "";
    }

    private static bool IsFeatureGuideCollapsed()
    {
        EnsureClientStateLoaded();
        return InventoryClient.ClientState.Inventory.FeatureGuideCollapsed;
    }

    private static void ToggleFeatureGuideCollapsed()
    {
        EnsureClientStateLoaded();
        InventorySlotsClientInventoryState inventory = InventoryClient.ClientState.Inventory;
        inventory.FeatureGuideCollapsed = !inventory.FeatureGuideCollapsed;
        SaveClientState();
    }

    private static void TryToggleFeatureGuideCollapsed()
    {
        if (CanInteractWithFeatureGuideToggle())
        {
            ToggleFeatureGuideCollapsed();
        }
    }

    private static void InvalidateFeatureGuideTextAndMeasurements()
    {
        TooltipUi.FeatureGuideExpandedText = "";
        TooltipUi.FeatureGuideMeasuredText = "";
        TooltipUi.FeatureGuideMeasuredFontId = 0;
        TooltipUi.FeatureGuideWrappedWidth = -1f;
        TooltipUi.NextFeatureGuideTextRefreshTime = 0f;
    }

    private static void RefreshFeatureGuideMeasurementCache(TMP_Text title, TMP_Text body)
    {
        string expandedText = TooltipUi.FeatureGuideExpandedText;
        int fontId = body.font != null && !IsUnityNull(body.font)
            ? body.font.GetInstanceID()
            : 0;
        if (string.Equals(TooltipUi.FeatureGuideMeasuredText, expandedText, StringComparison.Ordinal) &&
            TooltipUi.FeatureGuideMeasuredFontId == fontId)
        {
            return;
        }

        Vector2 expandedNatural = MeasureFeatureGuideNaturalText(
            body,
            GetFeatureGuideBody(expandedText),
            new Vector2(FeatureGuideMaxContentWidth, FeatureGuideFallbackContentHeight));
        Vector2 titleNatural = MeasureFeatureGuideNaturalText(
            title,
            GetFeatureGuideTitle(expandedText),
            new Vector2(180f, 18f));

        TooltipUi.FeatureGuideMeasuredText = expandedText;
        TooltipUi.FeatureGuideMeasuredFontId = fontId;
        TooltipUi.FeatureGuideNaturalWidth = Mathf.Min(
            FeatureGuideMaxContentWidth,
            expandedNatural.x);
        TooltipUi.FeatureGuideTitleWidth = titleNatural.x;
        TooltipUi.FeatureGuideTitleHeight = titleNatural.y;
        TooltipUi.FeatureGuideWrappedWidth = -1f;
    }

    private static float GetFeatureGuideExpandedBodyHeight(TMP_Text text, float contentWidth)
    {
        if (Mathf.Abs(TooltipUi.FeatureGuideWrappedWidth - contentWidth) > 0.1f)
        {
            Vector2 preferred = MeasureFeatureGuideText(
                text,
                GetFeatureGuideBody(TooltipUi.FeatureGuideExpandedText),
                contentWidth,
                new Vector2(contentWidth, FeatureGuideFallbackContentHeight));
            TooltipUi.FeatureGuideWrappedWidth = contentWidth;
            TooltipUi.FeatureGuideWrappedHeight = preferred.y;
        }

        return TooltipUi.FeatureGuideWrappedHeight;
    }

    private static Vector2 MeasureFeatureGuideText(
        TMP_Text text,
        string value,
        float width,
        Vector2 fallback)
    {
        Vector2 preferred = text.GetPreferredValues(value ?? "", width, 0f);
        if (!IsUsableUiMeasurement(preferred.x) ||
            !IsUsableUiMeasurement(preferred.y))
        {
            return fallback;
        }

        return new Vector2(
            Mathf.Ceil(preferred.x) + 1f,
            Mathf.Ceil(preferred.y) + 1f);
    }

    private static Vector2 MeasureFeatureGuideNaturalText(
        TMP_Text text,
        string value,
        Vector2 fallback)
    {
        Vector2 preferred = text.GetPreferredValues(value ?? "");
        if (!IsUsableUiMeasurement(preferred.x) ||
            !IsUsableUiMeasurement(preferred.y))
        {
            return fallback;
        }

        return new Vector2(
            Mathf.Ceil(preferred.x) + 1f,
            Mathf.Ceil(preferred.y) + 1f);
    }

    private static bool IsUsableUiMeasurement(float value) =>
        value >= 1f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static void LayoutFeatureGuideToggle(
        bool collapsed,
        float headerHeight,
        float outerWidth)
    {
        if (TooltipUi.FeatureGuideToggle == null || TooltipUi.FeatureGuideToggleIcon == null)
        {
            return;
        }

        RectTransform toggle = TooltipUi.FeatureGuideToggle;
        toggle.anchorMin = new Vector2(0f, 1f);
        toggle.anchorMax = new Vector2(0f, 1f);
        toggle.pivot = new Vector2(0f, 1f);
        toggle.anchoredPosition = new Vector2(
            Mathf.Max(0f, outerWidth - FeatureGuideToggleInset - FeatureGuideToggleSize),
            -FeatureGuideVerticalPadding - (headerHeight - FeatureGuideToggleSize) * 0.5f);
        toggle.sizeDelta = new Vector2(FeatureGuideToggleSize, FeatureGuideToggleSize);
        toggle.localScale = Vector3.one;
        toggle.localRotation = Quaternion.identity;
        toggle.SetAsLastSibling();

        Image icon = TooltipUi.FeatureGuideToggleIcon;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(14f, 14f);
        iconRect.localScale = Vector3.one;
        iconRect.localRotation = collapsed
            ? Quaternion.Euler(0f, 0f, 180f)
            : Quaternion.identity;
        icon.color = FeatureGuideToggleColor;
    }

    private static void UpdateFeatureGuideToggleInputLayer()
    {
        Canvas? toggleCanvas = TooltipUi.FeatureGuideToggleCanvas;
        GraphicRaycaster? raycaster = TooltipUi.FeatureGuideToggleRaycaster;
        RectTransform? toggle = TooltipUi.FeatureGuideToggle;
        if (toggleCanvas == null ||
            IsUnityNull(toggleCanvas) ||
            raycaster == null ||
            IsUnityNull(raycaster) ||
            toggle == null ||
            IsUnityNull(toggle))
        {
            return;
        }

        bool inventoryVisible = InventoryGui.IsVisible();
        bool canInteract = CanInteractWithFeatureGuideToggle();
        Canvas? inventoryCanvas = null;
        if (inventoryVisible && InventoryGui.instance != null)
        {
            inventoryCanvas = InventoryGui.instance.GetComponentInParent<Canvas>();
        }

        Canvas? sortingSource = inventoryCanvas != null && inventoryCanvas.overrideSorting
            ? inventoryCanvas
            : inventoryCanvas?.rootCanvas;
        bool elevateAboveInventory =
            inventoryVisible &&
            canInteract &&
            sortingSource != null &&
            !IsUnityNull(sortingSource);

        if (toggleCanvas.overrideSorting != elevateAboveInventory)
        {
            toggleCanvas.overrideSorting = elevateAboveInventory;
        }

        if (elevateAboveInventory)
        {
            int sortingOrder = Math.Min(short.MaxValue, sortingSource!.sortingOrder + 1);
            if (toggleCanvas.sortingLayerID != sortingSource.sortingLayerID)
            {
                toggleCanvas.sortingLayerID = sortingSource.sortingLayerID;
            }

            if (toggleCanvas.sortingOrder != sortingOrder)
            {
                toggleCanvas.sortingOrder = sortingOrder;
            }
        }

        bool enableRaycaster = inventoryVisible
            ? elevateAboveInventory
            : canInteract;
        if (raycaster.enabled != enableRaycaster)
        {
            raycaster.enabled = enableRaycaster;
        }

        Image? hitTarget = toggle.GetComponent<Image>();
        if (hitTarget != null && hitTarget.raycastTarget != enableRaycaster)
        {
            hitTarget.raycastTarget = enableRaycaster;
        }
    }

    private static bool CanInteractWithFeatureGuideToggle()
    {
        RectTransform? root = TooltipUi.FeatureGuideHudHint;
        RectTransform? toggle = TooltipUi.FeatureGuideToggle;
        if (root == null ||
            IsUnityNull(root) ||
            !root.gameObject.activeInHierarchy ||
            toggle == null ||
            IsUnityNull(toggle) ||
            !toggle.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!InventoryGui.IsVisible())
        {
            return true;
        }

        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || gui.m_dragGo != null || gui.m_dragItem != null)
        {
            return false;
        }

        return !IsActiveUiObject(gui.m_splitPanel) &&
               !IsActiveUiObject(gui.m_variantDialog) &&
               !IsActiveUiObject(gui.m_skillsDialog) &&
               !IsActiveUiObject(gui.m_textsDialog) &&
               !IsActiveUiObject(gui.m_trophiesPanel) &&
               !IsActiveUiObject(_inventoryTrashConfirmDialog);
    }

    private static bool IsActiveUiObject(Component? component) =>
        component != null &&
        !IsUnityNull(component) &&
        component.gameObject.activeInHierarchy;

    private static bool IsActiveUiObject(GameObject? gameObject) =>
        gameObject != null &&
        !IsUnityNull(gameObject) &&
        gameObject.activeInHierarchy;

    private static string GetContainerQuickStackKeyDisplayText()
    {
        string action = ZInput.IsGamepadActive() ? "JoyUse" : "Use";
        string display = GetBoundInputActionDisplayText(action);
        return string.IsNullOrWhiteSpace(display) ? "E" : display;
    }

    private static string GetBoundInputActionDisplayText(string action)
    {
        try
        {
            if (Localization.instance != null)
            {
                return Localization.instance.GetBoundKeyString(action, true);
            }
        }
        catch
        {
            // Fall through to the raw binding lookup.
        }

        try
        {
            return ZInput.instance != null ? ZInput.instance.GetBoundKeyString(action, true) : "";
        }
        catch
        {
            return "";
        }
    }

    private static void UpdateInventoryWheelHint(InventoryGrid playerGrid, Vector3 origin, float elementSpace, int totalRegularRows)
    {
        bool visible = _showInventoryWheelButton != null && _showInventoryWheelButton.Value.IsOn() &&
                       UseExpandableInventoryRows() && totalRegularRows > BaseRows && InventoryGui.IsVisible();
        if (!visible)
        {
            SetHintActive(TooltipUi.InventoryWheelHint, false);
            return;
        }

        TooltipUi.InventoryWheelHint = EnsureInventoryHintLabel(playerGrid.m_gridRoot, "InventorySlots_InventoryWheelHint", ref TooltipUi.InventoryWheelHintText);
        if (TooltipUi.InventoryWheelHint == null || TooltipUi.InventoryWheelHintText == null)
        {
            return;
        }

        float configuredSize = InventoryWheelHintSize;
        float size = configuredSize > 0f ? Mathf.Clamp(configuredSize, 8f, 64f) : Mathf.Clamp(elementSpace * 0.34f, 18f, 28f);
        float iconWidth = Mathf.Max(18f, size * 0.92f);
        float iconHeight = Mathf.Max(28f, size * 1.32f);
        TooltipUi.InventoryWheelHint.localPosition = origin + new Vector3(-iconWidth - 9f, -(BaseRows - 1) * elementSpace, 0f) + (Vector3)InventoryWheelHintOffset;
        TooltipUi.InventoryWheelHint.sizeDelta = new Vector2(iconWidth, iconHeight);
        TooltipUi.InventoryWheelHintText.text = "";
        TooltipUi.InventoryWheelHintText.fontSize = Mathf.Clamp(size * 0.55f, 12f, 18f);
        TooltipUi.InventoryWheelHintText.overflowMode = TextOverflowModes.Overflow;
        TooltipUi.InventoryWheelHintText.color = InventoryWheelHintColor;
        UpdateInventoryWheelHintIcon(TooltipUi.InventoryWheelHint, iconWidth, iconHeight, InventoryWheelHintColor);
        SetHintActive(TooltipUi.InventoryWheelHint, true);
    }

    private static void UpdateInventoryWheelHintIcon(RectTransform root, float width, float height, Color color)
    {
        RectTransform icon = EnsureHintImage(root, "MouseWheelIcon");
        icon.sizeDelta = new Vector2(width, height);
        icon.localPosition = Vector3.zero;
        icon.SetAsLastSibling();

        Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = GetMouseWheelHintSprite();
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        iconImage.color = color;

        HideHintChild(root, "KeyBkg");
        HideHintChild(root, "Up");
        HideHintChild(root, "Down");
        HideHintChild(root, "Line");
    }

    private static void HideHintChild(RectTransform root, string name)
    {
        Transform? child = root.Find(name);
        if (child != null)
        {
            child.gameObject.SetActive(false);
        }
    }

    private static Sprite GetMouseWheelHintSprite()
    {
        if (TooltipUi.MouseWheelHintSprite != null)
        {
            return TooltipUi.MouseWheelHintSprite;
        }

        const int width = 96;
        const int height = 128;
        Color[] pixels = Enumerable.Repeat(Color.clear, width * height).ToArray();
        float margin = 7f;
        float stroke = 7f;
        float centerX = width * 0.5f;
        float radius = (width - margin * 2f) * 0.5f;
        Vector2 capsuleA = new(centerX, margin + radius);
        Vector2 capsuleB = new(centerX, height - margin - radius);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new(x + 0.5f, y + 0.5f);
                float alpha = 0f;
                float capsuleDistance = Mathf.Abs(DistanceToSegment(point, capsuleA, capsuleB) - radius);
                alpha = Mathf.Max(alpha, StrokeAlpha(capsuleDistance, stroke));
                alpha = Mathf.Max(alpha, StrokeAlpha(DistanceToSegment(point, new Vector2(margin + stroke * 0.45f, height * 0.49f), new Vector2(width - margin - stroke * 0.45f, height * 0.49f)), stroke * 0.55f));
                alpha = Mathf.Max(alpha, StrokeAlpha(DistanceToSegment(point, new Vector2(centerX, height * 0.60f), new Vector2(centerX, height * 0.83f)), stroke * 0.55f));
                alpha = Mathf.Max(alpha, StrokeAlpha(DistanceToSegment(point, new Vector2(centerX, height * 0.83f), new Vector2(centerX - width * 0.15f, height * 0.72f)), stroke * 0.55f));
                alpha = Mathf.Max(alpha, StrokeAlpha(DistanceToSegment(point, new Vector2(centerX, height * 0.83f), new Vector2(centerX + width * 0.15f, height * 0.72f)), stroke * 0.55f));
                alpha = Mathf.Max(alpha, StrokeAlpha(DistanceToSegment(point, new Vector2(centerX, height * 0.61f), new Vector2(centerX - width * 0.15f, height * 0.72f)), stroke * 0.55f));
                alpha = Mathf.Max(alpha, StrokeAlpha(DistanceToSegment(point, new Vector2(centerX, height * 0.61f), new Vector2(centerX + width * 0.15f, height * 0.72f)), stroke * 0.55f));

                if (alpha > 0f)
                {
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
        }

        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        texture.name = "InventorySlots_MouseWheelHintTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        TooltipUi.MouseWheelHintSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        TooltipUi.MouseWheelHintSprite.name = "InventorySlots_MouseWheelHintSprite";
        return TooltipUi.MouseWheelHintSprite;
    }

    private static float StrokeAlpha(float distance, float strokeWidth)
    {
        float halfWidth = strokeWidth * 0.5f;
        return Mathf.Clamp01(halfWidth + 1f - distance);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = Vector2.Dot(segment, segment);
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }

    private static TMP_Text EnsureHintText(RectTransform parent, string name, string value)
    {
        Transform? existing = parent.Find(name);
        RectTransform rect = existing != null ? existing.GetComponent<RectTransform>() : null!;
        if (rect == null)
        {
            rect = CreateTextRect(name, parent);
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        TMP_Text text = rect.GetComponent<TMP_Text>();
        ApplyDefaultFontAsset(text);
        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        return text;
    }

    private static RectTransform EnsureHintImage(RectTransform parent, string name)
    {
        Transform? existing = parent.Find(name);
        RectTransform rect = existing != null ? existing.GetComponent<RectTransform>() : null!;
        if (rect == null)
        {
            rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = rect.GetComponent<Image>();
        image.sprite = GetSolidUiSprite();
        image.raycastTarget = false;
        rect.gameObject.SetActive(true);
        return rect;
    }

    private static void EnsureFeatureGuideHud(RectTransform parent)
    {
        const string rootName = "InventorySlots_FeatureGuideHudHint";
        RectTransform? root = TooltipUi.FeatureGuideHudHint;
        if (root == null ||
            IsUnityNull(root) ||
            root.parent != parent ||
            !string.Equals(root.name, rootName, StringComparison.Ordinal))
        {
            if (root != null && !IsUnityNull(root))
            {
                SetHintActive(root, false);
            }

            root = null;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (!string.Equals(child.name, rootName, StringComparison.Ordinal))
            {
                continue;
            }

            RectTransform? candidate = child.GetComponent<RectTransform>();
            if (root == null && candidate != null)
            {
                root = candidate;
                continue;
            }

            if (candidate == root)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }

        bool refreshSiblingOrder = root == null || !root.gameObject.activeSelf;
        if (root == null)
        {
            GameObject rootObject = new(rootName, typeof(RectTransform), typeof(Image));
            rootObject.SetActive(false);
            root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
        }

        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        if (refreshSiblingOrder)
        {
            root.SetAsLastSibling();
        }

        Image background = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
        background.sprite = GetSolidUiSprite();
        background.type = Image.Type.Simple;
        background.color = FeatureGuideBackgroundColor;
        background.raycastTarget = false;

        Transform? existingTitle = root.Find("Title");
        TMP_Text? title = existingTitle != null ? existingTitle.GetComponent<TMP_Text>() : null;
        if (title == null)
        {
            CreateTextRect("Title", root, out title);
        }

        Transform? existingBody = root.Find("Body");
        TMP_Text? body = existingBody != null ? existingBody.GetComponent<TMP_Text>() : null;
        if (body == null)
        {
            CreateTextRect("Body", root, out body);
        }

        ApplyDefaultFontAsset(title);
        ApplyDefaultFontAsset(body);
        title.raycastTarget = false;
        body.raycastTarget = false;

        Transform? existingToggle = root.Find("Toggle");
        RectTransform? toggle = existingToggle != null
            ? existingToggle.GetComponent<RectTransform>()
            : null;
        if (toggle == null)
        {
            GameObject toggleObject = new(
                "Toggle",
                typeof(RectTransform),
                typeof(Image));
            toggle = toggleObject.GetComponent<RectTransform>();
            toggle.SetParent(root, false);
        }

        Image hitTarget = toggle.GetComponent<Image>() ??
                          toggle.gameObject.AddComponent<Image>();
        hitTarget.sprite = GetSolidUiSprite();
        hitTarget.color = Color.clear;

        UIInputHandler input = toggle.GetComponent<UIInputHandler>() ??
                               toggle.gameObject.AddComponent<UIInputHandler>();
        int inputHandlerId = input.GetInstanceID();
        if (TooltipUi.FeatureGuideToggleInputHandlerId != inputHandlerId)
        {
            input.m_onLeftClick += _ => TryToggleFeatureGuideCollapsed();
            TooltipUi.FeatureGuideToggleInputHandlerId = inputHandlerId;
        }

        Canvas toggleCanvas = toggle.GetComponent<Canvas>() ??
                              toggle.gameObject.AddComponent<Canvas>();
        GraphicRaycaster toggleRaycaster = toggle.GetComponent<GraphicRaycaster>() ??
                                           toggle.gameObject.AddComponent<GraphicRaycaster>();

        Transform? existingIcon = toggle.Find("Icon");
        Image? toggleIcon = existingIcon != null ? existingIcon.GetComponent<Image>() : null;
        if (toggleIcon == null)
        {
            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(toggle, false);
            toggleIcon = iconObject.GetComponent<Image>();
        }

        toggleIcon.sprite = GetTriangleUiSprite();
        toggleIcon.preserveAspect = true;
        toggleIcon.raycastTarget = false;
        TooltipUi.FeatureGuideHudHint = root;
        TooltipUi.FeatureGuideHudTitleText = title;
        TooltipUi.FeatureGuideHudHintText = body;
        TooltipUi.FeatureGuideToggle = toggle;
        TooltipUi.FeatureGuideToggleIcon = toggleIcon;
        TooltipUi.FeatureGuideToggleCanvas = toggleCanvas;
        TooltipUi.FeatureGuideToggleRaycaster = toggleRaycaster;
        root.gameObject.SetActive(true);
    }

    private static Sprite GetTriangleUiSprite()
    {
        if (TooltipUi.TriangleUiSprite != null &&
            !IsUnityNull(TooltipUi.TriangleUiSprite))
        {
            return TooltipUi.TriangleUiSprite;
        }

        const int textureSize = 32;
        const int minimumY = 9;
        const int maximumY = 23;
        const int centerX = 16;
        const int maximumHalfWidth = 9;
        int height = maximumY - minimumY;
        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int y = minimumY; y <= maximumY; y++)
        {
            float progress = (y - minimumY) / (float)height;
            int halfWidth = Mathf.Max(
                1,
                Mathf.RoundToInt(maximumHalfWidth * (1f - progress)));
            for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
            {
                pixels[y * textureSize + x] = new Color32(255, 255, 255, 255);
            }
        }

        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "InventorySlots_TriangleUiTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply();

        TooltipUi.TriangleUiSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        TooltipUi.TriangleUiSprite.name = "InventorySlots_TriangleUiSprite";
        return TooltipUi.TriangleUiSprite;
    }

    private static RectTransform? EnsureInventoryHintLabel(RectTransform parent, string name, ref TMP_Text? text, bool configureCenteredLayout = true)
    {
        Transform? existing = parent.Find(name);
        RectTransform rect = existing != null ? existing.GetComponent<RectTransform>() : null!;
        bool refreshSiblingOrder = rect == null || !rect.gameObject.activeSelf;
        if (rect == null)
        {
            rect = CreateTextRect(name, parent);
        }

        if (configureCenteredLayout)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        if (refreshSiblingOrder)
        {
            rect.SetAsLastSibling();
        }

        text = rect.GetComponent<TMP_Text>();
        ApplyDefaultFontAsset(text);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.color = new Color(0.68f, 0.88f, 1f, 1f);
        text.raycastTarget = false;
        return rect;
    }

    private static void HideInventorySideHints()
    {
        SetHintActive(TooltipUi.InventoryWheelHint, false);
    }

    private static void SetHintActive(RectTransform? hint, bool active)
    {
        if (hint != null && hint.gameObject.activeSelf != active)
        {
            hint.gameObject.SetActive(active);
        }
    }

    private static string GetHotbarSwitchKeyDisplayText() =>
        JoinShortcutDisplayTexts(
            _hotbarSwitchKey != null ? _hotbarSwitchKey.Value.GetCompactDisplayText() : "",
            GetControllerHotkeyDisplayText(_controllerHotbarSwitchButton));

    private static Transform? FindKeyboardHintParent(Transform groupRoot)
    {
        Transform? keyboard = groupRoot.Find("Keyboard");
        if (keyboard != null)
        {
            return keyboard;
        }

        for (int i = 0; i < groupRoot.childCount; i++)
        {
            Transform child = groupRoot.GetChild(i);
            if (string.Equals(child.name, "Keyboard", StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return groupRoot;
    }

    private static void ApplyDefaultFontAsset(TMP_Text? text)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset? fontAsset = GetDefaultFontAsset(text);
        if (fontAsset != null)
        {
            text.font = fontAsset;
            if (TooltipUi.DefaultFontMaterial != null && !IsUnityNull(TooltipUi.DefaultFontMaterial))
            {
                text.fontSharedMaterial = TooltipUi.DefaultFontMaterial;
            }
        }
    }

    private static RectTransform CreateTextRect(string name, Transform parent, out TMP_Text text, bool active = true)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.SetActive(false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        text = go.AddComponent<TextMeshProUGUI>();
        ApplyDefaultFontAsset(text);
        go.SetActive(active);
        return rect;
    }

    private static RectTransform CreateTextRect(string name, Transform parent, bool active = true)
    {
        return CreateTextRect(name, parent, out _, active);
    }

    private static RectTransform CreateTopLeftImageChild(string name, Transform parent, Color color, bool active, bool raycastTarget = false)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.SetActive(false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.sprite = GetSolidUiSprite();
        image.color = color;
        image.raycastTarget = raycastTarget;

        go.SetActive(active);
        return rect;
    }

    private static TMP_FontAsset? GetDefaultFontAsset(TMP_Text? exclude = null)
    {
        if (TooltipUi.DefaultFontAsset != null && !IsUnityNull(TooltipUi.DefaultFontAsset))
        {
            return TooltipUi.DefaultFontAsset;
        }

        TMP_FontAsset? fontAsset = TryFindKnownUiFontAsset(exclude) ?? TryFindLoadedFontAsset();
        if (fontAsset != null)
        {
            TooltipUi.DefaultFontAsset = fontAsset;
        }

        return TooltipUi.DefaultFontAsset;
    }

    private static TMP_FontAsset? TryFindKnownUiFontAsset(TMP_Text? exclude)
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null)
        {
            return null;
        }

        return TryGetFontAsset(gui.m_tabCraft != null ? gui.m_tabCraft.GetComponentInChildren<TMP_Text>(true) : null, exclude)
               ?? TryGetFontAsset(gui.m_tabUpgrade != null ? gui.m_tabUpgrade.GetComponentInChildren<TMP_Text>(true) : null, exclude)
               ?? TryGetFontAsset(gui.m_craftingStationName, exclude)
               ?? TryGetFontAsset(gui.m_recipeName, exclude)
               ?? TryGetFontAsset(gui.m_recipeDecription, exclude)
               ?? TryGetFontAsset(gui.m_containerName, exclude)
               ?? TryGetFontAsset(gui.m_armor, exclude)
               ?? TryGetFontAsset(gui.m_weight, exclude)
               ?? TryGetFontAsset(gui.m_containerWeight, exclude)
               ?? TryGetFontAsset(gui.m_minStationLevelText, exclude)
               ?? TryGetFontAsset(gui.m_craftButton != null ? gui.m_craftButton.GetComponentInChildren<TMP_Text>(true) : null, exclude)
               ?? TryGetFontAsset(gui.m_repairButton != null ? gui.m_repairButton.GetComponentInChildren<TMP_Text>(true) : null, exclude);
    }

    private static TMP_FontAsset? TryGetFontAsset(TMP_Text? candidate, TMP_Text? exclude)
    {
        if (candidate == null || candidate == exclude)
        {
            return null;
        }

        TMP_FontAsset? fontAsset = candidate.font;
        if (fontAsset == null || IsUnityNull(fontAsset))
        {
            return null;
        }

        CaptureDefaultFontMaterials(candidate);

        return fontAsset;
    }

    private static void CaptureDefaultFontMaterials(TMP_Text source)
    {
        if (TooltipUi.DefaultFontMaterial == null || IsUnityNull(TooltipUi.DefaultFontMaterial))
        {
            Material? material = source.fontSharedMaterial;
            if (material != null && !IsUnityNull(material))
            {
                TooltipUi.DefaultFontMaterial = material;
            }
        }
    }

    private static void ApplyDefaultFontAssetToChildren(GameObject? root)
    {
        if (root == null)
        {
            return;
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            ApplyDefaultFontAsset(text);
        }
    }

    private static TMP_FontAsset? TryFindLoadedFontAsset()
    {
        foreach (TMP_FontAsset fontAsset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (fontAsset == null || IsUnityNull(fontAsset))
            {
                continue;
            }

            string name = fontAsset.name ?? "";
            if (name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (name.IndexOf("Valheim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Averia", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return fontAsset;
            }
        }

        return Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
            .FirstOrDefault(fontAsset => fontAsset != null &&
                                         !IsUnityNull(fontAsset) &&
                                         (fontAsset.name ?? "").IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) < 0);
    }

}
