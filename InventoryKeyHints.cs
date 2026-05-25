using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static void UpdateFavoriteKeyHint(KeyHints hints)
    {
        if (hints == null)
        {
            return;
        }

        UpdateFavoriteKeyHintGroup(hints.m_inventoryHints);
        UpdateFavoriteKeyHintGroup(hints.m_inventoryWithContainerHints);
    }

    private static void UpdateFavoriteKeyHintGroup(GameObject? group)
    {
        if (IsUnityNull(group))
        {
            return;
        }

        Transform? keyboardParent = FindKeyboardHintParent(group!.transform);
        if (keyboardParent == null)
        {
            return;
        }

        RemoveMisplacedInventorySlotsKeyHints(group.transform, keyboardParent);

        bool favoriteVisible = AreFavoritesEnabled() && _favoriteModifierKey != null;
        GameObject? favoriteHint = EnsureInventorySlotsKeyHint(keyboardParent, FavoriteKeyHintName, InventoryPanels.FavoriteKeyHintObjects);
        if (favoriteHint != null)
        {
            UpdateKeyHintObject(favoriteHint, LocalizeUi("$inventoryslots_keyhint_favorite", "Favorite"), GetFavoriteKeyHintDisplayText());
            favoriteHint.transform.SetAsFirstSibling();
            favoriteHint.SetActive(favoriteVisible);
        }

        bool tooltipVisible = IsPinnedTooltipKeyConfigured();
        GameObject? tooltipHint = EnsureInventorySlotsKeyHint(keyboardParent, PinnedTooltipKeyHintName, InventoryPanels.PinnedTooltipKeyHintObjects);
        if (tooltipHint != null)
        {
            UpdateKeyHintObject(tooltipHint, LocalizeUi("$inventoryslots_keyhint_tooltip", "Tooltip"), GetPinnedTooltipKeyDisplayText());
            tooltipHint.SetActive(tooltipVisible);
            int targetIndex = favoriteHint != null && favoriteHint.activeSelf
                ? favoriteHint.transform.GetSiblingIndex() + 1
                : 0;
            tooltipHint.transform.SetSiblingIndex(Mathf.Clamp(targetIndex, 0, keyboardParent.childCount - 1));
        }
    }

    private static void RemoveMisplacedInventorySlotsKeyHints(Transform groupRoot, Transform keyboardParent)
    {
        foreach (Transform hint in groupRoot.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            bool inventorySlotsHint =
                string.Equals(hint.name, FavoriteKeyHintName, StringComparison.Ordinal) ||
                string.Equals(hint.name, PinnedTooltipKeyHintName, StringComparison.Ordinal);
            if (!inventorySlotsHint || hint.parent == keyboardParent)
            {
                continue;
            }

            hint.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(hint.gameObject);
        }
    }

    private static GameObject? EnsureInventorySlotsKeyHint(Transform parent, string hintName, Dictionary<int, GameObject> cache)
    {
        int parentId = parent.GetInstanceID();
        if (cache.TryGetValue(parentId, out GameObject? cached) && !IsUnityNull(cached) && cached!.transform.parent == parent)
        {
            return cached;
        }

        Transform? existing = parent.Find(hintName);
        if (existing != null)
        {
            cache[parentId] = existing.gameObject;
            StripTooltips(existing.gameObject);
            return existing.gameObject;
        }

        Transform? template = FindFavoriteKeyHintTemplate(parent);
        if (template == null)
        {
            return null;
        }

        GameObject hint = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        hint.name = hintName;
        StripTooltips(hint);
        cache[parentId] = hint;
        return hint;
    }

    private static void StripTooltips(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        foreach (UITooltip tooltip in root.GetComponentsInChildren<UITooltip>(includeInactive: true))
        {
            tooltip.m_topic = "";
            tooltip.m_text = "";
            tooltip.enabled = false;
        }
    }

    private static Transform? FindFavoriteKeyHintTemplate(Transform parent)
    {
        Transform? fallback = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith("InventorySlots_", StringComparison.Ordinal))
            {
                continue;
            }

            if (child.GetComponentsInChildren<TMP_Text>(includeInactive: true).Length == 0)
            {
                continue;
            }

            fallback ??= child;
            if (child.name.IndexOf("Split", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return fallback;
    }

    private static void UpdateKeyHintObject(GameObject hint, string label, string keyText)
    {
        RectTransform rect = (RectTransform)hint.transform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        SetKeyHintLabel(hint.transform, label);
        SetKeyHintKeys(hint.transform, keyText);
        LayoutElement layout = hint.GetComponent<LayoutElement>() ?? hint.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
    }

    private static void SetKeyHintLabel(Transform root, string value)
    {
        TMP_Text? label = root.Find("Text")?.GetComponent<TMP_Text>();
        label ??= root.GetComponentsInChildren<TMP_Text>(includeInactive: true).FirstOrDefault(text => !IsKeyHintText(text));
        if (label == null)
        {
            return;
        }

        ApplyDefaultFontAsset(label);
        label.text = value;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = Mathf.Min(Mathf.Max(label.fontSize, 14f), 16f);
        if (label.TryGetComponent(out LayoutElement layout))
        {
            layout.preferredWidth = 64f;
        }
    }

    private static void SetKeyHintKeys(Transform root, string keyText)
    {
        List<TMP_Text> keyTexts = root.GetComponentsInChildren<TMP_Text>(includeInactive: true)
            .Where(IsKeyHintText)
            .ToList();
        if (keyTexts.Count == 0)
        {
            return;
        }

        if (keyTexts.Count == 1)
        {
            SetKeyHintText(keyTexts[0], keyText, visible: true);
            SetKeyHintSeparators(root, visible: false);
            return;
        }

        string[] parts = keyText.Split(new[] { '+' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            SetKeyHintText(keyTexts[0], parts[0], visible: true);
            SetKeyHintText(keyTexts[1], parts[1], visible: true);
            SetKeyHintSeparators(root, visible: true);
        }
        else
        {
            SetKeyHintText(keyTexts[0], keyText, visible: true);
            SetKeyHintText(keyTexts[1], "", visible: false);
            SetKeyHintSeparators(root, visible: false);
        }

        for (int i = 2; i < keyTexts.Count; i++)
        {
            SetKeyHintText(keyTexts[i], "", visible: false);
        }
    }

    private static bool IsKeyHintText(TMP_Text text)
    {
        return text != null && string.Equals(text.name, "Key", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetKeyHintSeparators(Transform root, bool visible)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            if (IsKeyHintText(text))
            {
                continue;
            }

            string value = text.text?.Trim() ?? "";
            bool separator = string.Equals(value, "+", StringComparison.Ordinal) ||
                             string.Equals(text.name, "Plus", StringComparison.OrdinalIgnoreCase) ||
                             text.name.IndexOf("Separator", StringComparison.OrdinalIgnoreCase) >= 0;
            if (separator)
            {
                text.gameObject.SetActive(visible);
            }
        }
    }

    private static void SetKeyHintText(TMP_Text text, string value, bool visible)
    {
        ApplyDefaultFontAsset(text);
        text.text = value;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        Transform? keyRoot = text.transform.parent;
        if (keyRoot != null && keyRoot.name.StartsWith("key_bkg", StringComparison.OrdinalIgnoreCase))
        {
            keyRoot.gameObject.SetActive(visible);
        }
        else
        {
            text.gameObject.SetActive(visible);
        }
    }

    private static string GetFavoriteModifierDisplayText()
    {
        string controller = GetControllerHotkeyDisplayText(_controllerFavoriteModifierButton);
        if (_favoriteModifierKey == null)
        {
            return string.IsNullOrWhiteSpace(controller) ? "Alt" : controller;
        }

        KeyboardShortcut shortcut = _favoriteModifierKey.Value;
        string keyboard = shortcut.GetDisplayText();
        return JoinShortcutDisplayTexts(keyboard, controller);
    }

    private static string GetFavoriteKeyHintDisplayText() => $"{GetFavoriteModifierDisplayText()} + Mouse0";

    private static bool IsPinnedTooltipKeyConfigured() =>
        _pinnedTooltipKey != null && _pinnedTooltipKey.Value.MainKey != KeyCode.None ||
        IsControllerHotkeyConfigured(_controllerPinnedTooltipButton);

    private static string GetPinnedTooltipKeyDisplayText() =>
        JoinShortcutDisplayTexts(
            _pinnedTooltipKey != null ? _pinnedTooltipKey.Value.GetDisplayText() : "Mouse2",
            GetControllerHotkeyDisplayText(_controllerPinnedTooltipButton));

    private static void SetFavoriteKeyHintsActive(GameObject? activeHint)
    {
        foreach (GameObject hint in InventoryPanels.FavoriteKeyHintObjects.Values.Concat(InventoryPanels.PinnedTooltipKeyHintObjects.Values))
        {
            if (!IsUnityNull(hint))
            {
                hint.SetActive(activeHint != null && hint == activeHint);
            }
        }
    }
}
