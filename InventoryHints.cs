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
        string keyText = GetHotbarSwitchKeyDisplayText();
        bool visible = _showHotbarSwitchHint != null && _showHotbarSwitchHint.Value.IsOn() &&
                       !string.IsNullOrWhiteSpace(keyText) &&
                       hudRoot != null;
        if (!visible)
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
            return;
        }

        Transform? hotKeyBarTransform = hudRoot!.transform.Find("HotKeyBar");
        RectTransform parent = hudRoot.GetComponent<RectTransform>();
        if (parent == null)
        {
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
            return;
        }

        TooltipUi.HotbarSwitchHudHint = EnsureInventoryHintLabel(parent, "InventorySlots_HotbarSwitchHudHint", ref TooltipUi.HotbarSwitchHudHintText);
        if (TooltipUi.HotbarSwitchHudHint == null || TooltipUi.HotbarSwitchHudHintText == null)
        {
            return;
        }

        float elementSpace = hotKeyBarTransform != null ? GetHudElementSpace() : 70f;
        float size = HotbarSwitchHintSize;
        TooltipUi.HotbarSwitchHudHint.pivot = new Vector2(0f, 0.5f);
        Vector3 hotbarOrigin = hotKeyBarTransform != null ? hotKeyBarTransform.localPosition : Vector3.zero;
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

    private static RectTransform EnsureWheelHintBackground(RectTransform parent, float size)
    {
        Transform? existing = parent.Find("KeyBkg");
        RectTransform rect = existing != null ? existing.GetComponent<RectTransform>() : null!;
        if (rect == null)
        {
            Transform? template = FindVanillaKeyBackgroundTemplate();
            if (template != null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
                clone.name = "KeyBkg";
                rect = (RectTransform)clone.transform;
                foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(includeInactive: true))
                {
                    text.text = "";
                    text.raycastTarget = false;
                }

                foreach (Image image in clone.GetComponentsInChildren<Image>(includeInactive: true))
                {
                    image.raycastTarget = false;
                }
            }
            else
            {
                rect = new GameObject("KeyBkg", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                Image image = rect.GetComponent<Image>();
                image.sprite = GetSolidUiSprite();
                image.color = new Color(0f, 0f, 0f, 0.45f);
                image.raycastTarget = false;
            }
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = Vector3.zero;
        rect.sizeDelta = new Vector2(Mathf.Max(18f, size * 0.88f), Mathf.Max(24f, size * 1.18f));
        rect.gameObject.SetActive(true);
        return rect;
    }

    private static Transform? FindVanillaKeyBackgroundTemplate()
    {
        KeyHints hints = KeyHints.instance;
        if (hints == null)
        {
            return null;
        }

        foreach (GameObject? group in new[] { hints.m_inventoryHints, hints.m_inventoryWithContainerHints, hints.m_buildHints, hints.m_combatHints })
        {
            if (IsUnityNull(group))
            {
                continue;
            }

            Transform? keyboard = FindKeyboardHintParent(group!.transform);
            if (keyboard == null)
            {
                continue;
            }

            Transform? template = keyboard.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(child => child.name.StartsWith("key_bkg", StringComparison.OrdinalIgnoreCase) && child.GetComponent<Image>() != null);
            if (template != null)
            {
                return template;
            }
        }

        return null;
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

    internal static Sprite GetSolidUiSprite()
    {
        if (TooltipUi.SolidUiSprite != null)
        {
            return TooltipUi.SolidUiSprite;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        TooltipUi.SolidUiSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return TooltipUi.SolidUiSprite;
    }

    private static RectTransform? EnsureInventoryHintLabel(RectTransform parent, string name, ref TMP_Text? text, bool configureCenteredLayout = true)
    {
        Transform? existing = parent.Find(name);
        RectTransform rect = existing != null ? existing.GetComponent<RectTransform>() : null!;
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
        rect.SetAsLastSibling();

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
