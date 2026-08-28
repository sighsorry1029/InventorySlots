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
            return;
        }

        Transform? hotKeyBarTransform = hudRoot!.transform.Find("HotKeyBar");
        RectTransform parent = hudRoot.GetComponent<RectTransform>();
        if (parent == null)
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
            SetHintActive(TooltipUi.FeatureGuideHudHint, false);
            return;
        }

        float elementSpace = hotKeyBarTransform != null ? GetHudElementSpace() : 70f;
        Vector3 hotbarOrigin = hotKeyBarTransform != null ? hotKeyBarTransform.localPosition : Vector3.zero;
        string keyText = GetHotbarSwitchKeyDisplayText();
        bool switchVisible = _showHotbarSwitchHint != null && _showHotbarSwitchHint.Value.IsOn() &&
                             !string.IsNullOrWhiteSpace(keyText);
        if (switchVisible)
        {
            TooltipUi.HotbarSwitchHudHint = EnsureInventoryHintLabel(parent, "InventorySlots_HotbarSwitchHudHint", ref TooltipUi.HotbarSwitchHudHintText);
            if (TooltipUi.HotbarSwitchHudHint != null && TooltipUi.HotbarSwitchHudHintText != null)
            {
                float size = HotbarSwitchHintSize;
                TooltipUi.HotbarSwitchHudHint.pivot = new Vector2(0f, 0.5f);
                TooltipUi.HotbarSwitchHudHint.localPosition = hotbarOrigin + new Vector3(InventoryWidth * elementSpace + 10f, 0f, 0f) + (Vector3)HotbarSwitchHintOffset;
                TooltipUi.HotbarSwitchHudHint.sizeDelta = new Vector2(Mathf.Max(size * 2.25f, 58f), Mathf.Max(size * 1.6f, 38f));
                TooltipUi.HotbarSwitchHudHintText.text = LocalizeUi("$inventoryslots_hotbar_switch_hint", "Switch\n[{key}]")
                    .Replace("{key}", keyText);
                TooltipUi.HotbarSwitchHudHintText.fontSize = HotbarSwitchHintFontSize;
                TooltipUi.HotbarSwitchHudHintText.lineSpacing = -10f;
                TooltipUi.HotbarSwitchHudHintText.overflowMode = TextOverflowModes.Overflow;
                TooltipUi.HotbarSwitchHudHintText.color = HotbarSwitchHintColor;
                SetHintActive(TooltipUi.HotbarSwitchHudHint, true);
            }
        }
        else
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
        }

        UpdateFeatureGuideHud(parent, hotbarOrigin, elementSpace, switchVisible);
    }

    private static void UpdateFeatureGuideHud(RectTransform parent, Vector3 hotbarOrigin, float elementSpace, bool switchVisible)
    {
        bool visible = _showFeatureGuide != null && _showFeatureGuide.Value.IsOn();
        if (!visible)
        {
            SetHintActive(TooltipUi.FeatureGuideHudHint, false);
            return;
        }

        TooltipUi.FeatureGuideHudHint = EnsureInventoryHintLabel(parent, "InventorySlots_FeatureGuideHudHint", ref TooltipUi.FeatureGuideHudHintText);
        if (TooltipUi.FeatureGuideHudHint == null || TooltipUi.FeatureGuideHudHintText == null)
        {
            return;
        }

        const float guideWidth = 500f;
        const float minimumGuideWidth = 320f;
        const float guideHeight = 176f;
        const float guideGap = 12f;
        float switchWidth = Mathf.Max(HotbarSwitchHintSize * 2.25f, 58f);
        float switchHeight = Mathf.Max(HotbarSwitchHintSize * 1.6f, 38f);
        Vector3 switchPosition = hotbarOrigin + new Vector3(InventoryWidth * elementSpace + 10f, 0f, 0f) + (Vector3)HotbarSwitchHintOffset;
        float guideX = switchVisible
            ? switchPosition.x + switchWidth + guideGap
            : hotbarOrigin.x + InventoryWidth * elementSpace + guideGap;
        float guideY = switchPosition.y + switchHeight * 0.5f;
        float resolvedGuideWidth = guideWidth;
        Rect parentRect = parent.rect;
        if (parentRect.width > 0f && parentRect.height > 0f)
        {
            float availableRightWidth = parentRect.xMax - guideGap - guideX;
            if (availableRightWidth >= minimumGuideWidth)
            {
                resolvedGuideWidth = Mathf.Min(guideWidth, availableRightWidth);
            }
            else
            {
                resolvedGuideWidth = Mathf.Min(guideWidth, Mathf.Max(1f, parentRect.width - guideGap * 2f));
                guideX = Mathf.Clamp(
                    hotbarOrigin.x,
                    parentRect.xMin + guideGap,
                    parentRect.xMax - resolvedGuideWidth - guideGap);
                float belowHotbar = hotbarOrigin.y - elementSpace * 0.5f - guideGap;
                float aboveHotbar = hotbarOrigin.y + elementSpace * 0.5f + guideGap + guideHeight;
                bool fitsBelow = belowHotbar - guideHeight >= parentRect.yMin + guideGap;
                bool fitsAbove = aboveHotbar <= parentRect.yMax - guideGap;
                guideY = fitsBelow || !fitsAbove ? belowHotbar : aboveHotbar;
            }

            guideY = Mathf.Clamp(
                guideY,
                parentRect.yMin + guideHeight + guideGap,
                parentRect.yMax - guideGap);
        }

        TooltipUi.FeatureGuideHudHint.anchorMin = new Vector2(0.5f, 0.5f);
        TooltipUi.FeatureGuideHudHint.anchorMax = new Vector2(0.5f, 0.5f);
        TooltipUi.FeatureGuideHudHint.pivot = new Vector2(0f, 1f);
        TooltipUi.FeatureGuideHudHint.localPosition = new Vector3(guideX, guideY, switchPosition.z);
        TooltipUi.FeatureGuideHudHint.sizeDelta = new Vector2(resolvedGuideWidth, guideHeight);

        RefreshFeatureGuideText(TooltipUi.FeatureGuideHudHintText);
        TooltipUi.FeatureGuideHudHintText.alignment = TextAlignmentOptions.TopLeft;
        TooltipUi.FeatureGuideHudHintText.textWrappingMode = TextWrappingModes.Normal;
        TooltipUi.FeatureGuideHudHintText.enableAutoSizing = false;
        TooltipUi.FeatureGuideHudHintText.fontSize = 12f;
        TooltipUi.FeatureGuideHudHintText.lineSpacing = -3f;
        TooltipUi.FeatureGuideHudHintText.overflowMode = TextOverflowModes.Overflow;
        TooltipUi.FeatureGuideHudHintText.color = new Color(0.78f, 0.88f, 0.94f, 0.92f);
        SetHintActive(TooltipUi.FeatureGuideHudHint, true);
    }

    private static void RefreshFeatureGuideText(TMP_Text text)
    {
        if (!string.IsNullOrEmpty(text.text) && Time.unscaledTime < TooltipUi.NextFeatureGuideTextRefreshTime)
        {
            return;
        }

        TooltipUi.NextFeatureGuideTextRefreshTime = Time.unscaledTime + 0.25f;
        string guide = LocalizeUi(
            "$inventoryslots_feature_guide",
            "<b>InventorySlots quick guide</b>\n<color=#FFA94D>[{tooltipKey}]</color> over an inventory or chest item, or a crafting recipe: Pin tooltip\n<color=#FFA94D>[{favoriteKey}]</color> over an inventory slot or crafting recipe: Toggle favorite\nWhile looking at a chest, <color=#FFA94D>[Hold {useKey}]</color>: Store matching items nearby (favorite slots excluded)\nWhile looking at a chest, <color=#FFA94D>[Hold {restockKey}]</color>: Refill items in favorite slots from nearby chests\nPer-item restock targets: <color=#FFA94D>F1 → InventorySlots → 3 - Restock</color>\nCustom slots and rules: <color=#FFA94D>config/InventorySlots/InventorySlots.yml</color>\nHide this guide now: <color=#FFA94D>F1 → InventorySlots → Show Feature Guide → Off</color>");
        string resolved = guide
            .Replace("{tooltipKey}", GetPinnedTooltipKeyDisplayText())
            .Replace("{favoriteKey}", GetFavoriteKeyHintDisplayText())
            .Replace("{useKey}", GetContainerQuickStackKeyDisplayText())
            .Replace("{restockKey}", GetContainerRestockKeyDisplayText());
        if (!string.Equals(text.text, resolved, StringComparison.Ordinal))
        {
            text.text = resolved;
        }
    }

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
