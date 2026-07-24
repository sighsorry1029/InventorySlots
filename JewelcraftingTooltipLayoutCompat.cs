using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal sealed class JewelcraftingSourceRowUiCache : MonoBehaviour
{
    public string ChildSignature { get; set; } = "";
    public TMP_Text? Text { get; set; }
    public Image? BorderImage { get; set; }
    public Image? GemImage { get; set; }
}

public sealed partial class InventorySlotsPlugin
{
    private const float InventoryPinnedJewelcraftingScrollGap = 12f;
    private const float InventoryPinnedJewelcraftingRowPadding = 12f;
    private const float InventoryPinnedJewelcraftingIconGap = 8f;
    private const float InventoryPinnedJewelcraftingNativeIconSize = 42f;
    private const float InventoryPinnedJewelcraftingNativeIconInset = 4f;
    private const float InventoryPinnedJewelcraftingNativeRowGap = 10f;
    private const float InventoryPinnedJewelcraftingNativeInteractGap = 8f;
    private const float InventoryPinnedJewelcraftingNativeTextPadding = 4f;
    private const float InventoryPinnedJewelcraftingNativeBottomPadding = 32f;

    private static bool UpdateJewelcraftingTooltip(
        RectTransform panel,
        ItemData item,
        ref RectTransform? cachedRoot,
        bool showInteract = false)
    {
        if (item?.m_shared == null)
        {
            HideJewelcraftingTooltipRoot(ref cachedRoot);
            return false;
        }

        ItemData tooltipItem = item;
        string signature = GetJewelcraftingTooltipUpdateSignature(tooltipItem, showInteract);

        if (cachedRoot != null && !IsUnityNull(cachedRoot) && cachedRoot.IsChildOf(panel))
        {
            JewelcraftingTooltipLayoutCache cachedLayout = GetJewelcraftingTooltipLayoutCache(cachedRoot);
            if (TryReuseJewelcraftingNativeTooltipCache(cachedRoot, cachedLayout, item, signature, out bool cachedVisible))
            {
                return cachedVisible;
            }
        }

        if (!ShouldAttemptJewelcraftingPinnedTooltip(tooltipItem))
        {
            HideJewelcraftingTooltipRoot(ref cachedRoot);
            return false;
        }

        RectTransform? root = EnsureJewelcraftingTooltipRoot(panel, ref cachedRoot);
        if (root == null)
        {
            return false;
        }

        JewelcraftingTooltipLayoutCache cache = GetJewelcraftingTooltipLayoutCache(root);
        root.gameObject.SetActive(true);
        if (TryReuseJewelcraftingNativeTooltipCache(root, cache, item, signature, out bool reusedVisible))
        {
            return reusedVisible;
        }

        return RebuildJewelcraftingNativeTooltip(root, tooltipItem, item, showInteract, signature, cache);
    }

    private static void UpdateJewelcraftingTooltipCacheItemIdentity(JewelcraftingTooltipLayoutCache cache, ItemData item)
    {
        cache.ItemPrefabName = GetItemPrefabName(item);
        cache.ItemSharedName = item.m_shared?.m_name ?? "";
        cache.ItemVariant = item.m_variant;
    }

    private static JewelcraftingTooltipLayoutCache GetJewelcraftingTooltipLayoutCache(RectTransform root) =>
        root.GetComponent<JewelcraftingTooltipLayoutCache>() ?? root.gameObject.AddComponent<JewelcraftingTooltipLayoutCache>();

    private static void RestoreCachedJewelcraftingNativeTooltip(RectTransform root, JewelcraftingTooltipLayoutCache cache)
    {
        if (!cache.Visible)
        {
            return;
        }

        RestoreJewelcraftingSourceTooltipForNativeUpdate(root);
    }

    private static bool HasVisibleNativeJewelcraftingTooltipContent(RectTransform root) =>
        HasVisibleNativeJewelcraftingSocketRows(root) ||
        HasVisibleNativeJewelcraftingInteract(root);

    private static bool HasVisibleNativeJewelcraftingInteract(RectTransform root)
    {
        Transform? interact = GetJewelcraftingSourceInteract(root);
        if (interact == null || IsUnityNull(interact) || !interact.gameObject.activeSelf)
        {
            return false;
        }

        TMP_Text? text = interact.GetComponent<TMP_Text>();
        return text != null &&
               !IsUnityNull(text) &&
               JewelcraftingTooltipCore.HasVisibleText(text.text ?? "");
    }

    private static bool HasVisibleNativeJewelcraftingSocketRows(RectTransform root)
    {
        Transform? holes = GetJewelcraftingSourceRowsRoot(root);
        if (holes == null || IsUnityNull(holes) || !holes.gameObject.activeSelf)
        {
            return false;
        }

        for (int i = 0; i < holes.childCount; i++)
        {
            if (holes.GetChild(i) is not RectTransform row || !row.gameObject.activeSelf)
            {
                continue;
            }

            JewelcraftingSourceRowUiCache rowCache = GetJewelcraftingSourceRowUiCache(row);
            TMP_Text? text = rowCache.Text;
            Image? gemImage = rowCache.GemImage;
            if (text != null &&
                !IsUnityNull(text) &&
                JewelcraftingTooltipCore.HasVisibleText(text.text ?? ""))
            {
                return true;
            }

            if (gemImage != null &&
                !IsUnityNull(gemImage) &&
                gemImage.gameObject.activeSelf &&
                gemImage.sprite != null &&
                !IsUnityNull(gemImage.sprite))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldAttemptJewelcraftingPinnedTooltip(ItemData item)
    {
        if (TryGetJewelcraftingGemSlotCount(item, out int slotCount) && slotCount > 0)
        {
            return true;
        }

        if (GetJewelcraftingSocketPrefabNamesFromCustomData(item).Count > 0)
        {
            return true;
        }

        return HasJewelcraftingPotentialCustomData(item);
    }

    private static bool IsJewelcraftingTooltipDataKey(string key) =>
        key.IndexOf("Jewelcrafting.Sockets", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.Box", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.SocketBag", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.InventoryBag", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.DropChest", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.Frame", StringComparison.OrdinalIgnoreCase) >= 0 ||
        string.Equals(key, "ProphecySeed", StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith("#ProphecySeed", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetJewelcraftingGemSlotCount(ItemData item, out int slotCount)
    {
        slotCount = 0;
        if (!TryGetJewelcraftingGemApi(out JewelcraftingGemApi? api) || api == null)
        {
            return false;
        }

        try
        {
            if (api.GetGems(item) is not IEnumerable gems)
            {
                return false;
            }

            foreach (object? _ in gems)
            {
                slotCount++;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetJewelcraftingTooltipUpdateSignature(ItemData item, bool showInteract)
    {
        return JewelcraftingTooltipCore.BuildNativeTooltipUpdateSignature(
            showInteract,
            IsJewelcraftingAdvancedTooltipPressed(),
            IsJewelcraftingProphecyTooltipPressed(),
            _uiLocalizationVersion,
            GetEquipmentSlotTooltipSignature(item),
            GetJewelcraftingOpenSocketInventorySignature(item));
    }

    private static RectTransform? EnsureJewelcraftingTooltipRoot(RectTransform panel, ref RectTransform? cachedRoot)
    {
        if (!TryGetJewelcraftingTooltipApi(out JewelcraftingTooltipApi? api) || api == null)
        {
            return null;
        }

        if (cachedRoot != null && !IsUnityNull(cachedRoot) && cachedRoot.IsChildOf(panel))
        {
            ConfigureJewelcraftingTooltipRoot(cachedRoot);
            return cachedRoot;
        }

        RectTransform? root = FindJewelcraftingTooltipRoot(panel);
        if (root == null)
        {
            GameObject? socketTooltip = api.GetSocketTooltip();
            Transform? template = socketTooltip != null && !IsUnityNull(socketTooltip)
                ? socketTooltip.transform.Find("Bkg (1)")
                : null;
            if (template == null)
            {
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, panel, false);
            clone.name = InventoryPinnedJewelcraftingTooltipRootName;
            root = clone.GetComponent<RectTransform>();
            if (root == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }
        }

        if (!root.IsChildOf(panel))
        {
            root.SetParent(panel, false);
        }

        ConfigureJewelcraftingTooltipRoot(root);
        cachedRoot = root;
        return root;
    }

    private static void ConfigureJewelcraftingTooltipRoot(RectTransform root)
    {
        StripJewelcraftingTooltipTemplate(root);
        JewelcraftingTooltipLayoutCache cache = GetJewelcraftingTooltipLayoutCache(root);
        RefreshJewelcraftingNativeComponentCache(root, cache);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, InventoryPinnedJewelcraftingReservedHeight - 10f);
        root.sizeDelta = new Vector2(-36f, InventoryPinnedJewelcraftingReservedHeight - 28f);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;

        if (root.GetComponent<Image>() is { } background)
        {
            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = false;
        }

        foreach (Graphic graphic in cache.NativeGraphics)
        {
            if (graphic == null || IsUnityNull(graphic))
            {
                continue;
            }

            graphic.raycastTarget = false;
        }

        foreach (TMP_Text text in cache.NativeTexts)
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            ApplyDefaultFontAsset(text);
            ApplyTooltipSourceFont(text, "Text");
            text.raycastTarget = false;
        }
    }

    private static void RefreshJewelcraftingNativeComponentCache(RectTransform root, JewelcraftingTooltipLayoutCache cache)
    {
        string signature = GetJewelcraftingNativeComponentSignature(root);
        if (string.Equals(cache.NativeComponentSignature, signature, StringComparison.Ordinal) &&
            !HasInvalidJewelcraftingNativeComponentCache(cache))
        {
            return;
        }

        cache.NativeGraphics = root.GetComponentsInChildren<Graphic>(includeInactive: true);
        cache.NativeTexts = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        cache.NativeLayoutGroups = root.GetComponentsInChildren<LayoutGroup>(includeInactive: true);
        cache.NativeContentSizeFitters = root.GetComponentsInChildren<ContentSizeFitter>(includeInactive: true);
        cache.NativeComponentSignature = signature;
    }

    private static bool HasInvalidJewelcraftingNativeComponentCache(JewelcraftingTooltipLayoutCache cache) =>
        cache.NativeGraphics.Any(graphic => graphic == null || IsUnityNull(graphic)) ||
        cache.NativeTexts.Any(text => text == null || IsUnityNull(text)) ||
        cache.NativeLayoutGroups.Any(group => group == null || IsUnityNull(group)) ||
        cache.NativeContentSizeFitters.Any(fitter => fitter == null || IsUnityNull(fitter));

    private static string GetJewelcraftingNativeComponentSignature(RectTransform root)
    {
        Transform? rows = root.Find("TrannyHoles");
        Transform? interact = root.Find("Transmute_Press_Interact");
        return $"{root.childCount}|rows={rows?.childCount ?? -1}|interact={interact?.childCount ?? -1}";
    }

    private static void StripJewelcraftingTooltipTemplate(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string name = child.name;
            bool keep = string.Equals(name, "TrannyHoles", StringComparison.Ordinal) ||
                        string.Equals(name, "Transmute_Press_Interact", StringComparison.Ordinal);
            if (!keep)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static RectTransform? FindJewelcraftingTooltipRoot(RectTransform panel)
    {
        Transform? direct = panel.Find(InventoryPinnedJewelcraftingTooltipRootName);
        if (direct != null && direct.GetComponent<RectTransform>() is { } directRoot)
        {
            return directRoot;
        }

        foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(includeInactive: true))
        {
            if (rect != null && !IsUnityNull(rect) && string.Equals(rect.name, InventoryPinnedJewelcraftingTooltipRootName, StringComparison.Ordinal))
            {
                return rect;
            }
        }

        return null;
    }

    private static Transform? GetJewelcraftingSourceRowsRoot(RectTransform root)
    {
        JewelcraftingTooltipLayoutCache cache = GetJewelcraftingTooltipLayoutCache(root);
        if (cache.SourceRowsRoot != null &&
            !IsUnityNull(cache.SourceRowsRoot) &&
            cache.SourceRowsRoot.parent == root)
        {
            return cache.SourceRowsRoot;
        }

        Transform? rows = root.Find("TrannyHoles");
        cache.SourceRowsRoot = rows;
        return rows;
    }

    private static Transform? GetJewelcraftingSourceInteract(RectTransform root)
    {
        JewelcraftingTooltipLayoutCache cache = GetJewelcraftingTooltipLayoutCache(root);
        if (cache.SourceInteract != null &&
            !IsUnityNull(cache.SourceInteract) &&
            cache.SourceInteract.parent == root)
        {
            return cache.SourceInteract;
        }

        Transform? interact = root.Find("Transmute_Press_Interact");
        cache.SourceInteract = interact;
        return interact;
    }

    private static float LayoutPinnedTooltipExtraScrollContent(RectTransform panel, float textWidth, float textHeight)
    {
        RectTransform? root = FindJewelcraftingTooltipRoot(panel);
        if (root == null || IsUnityNull(root) || !root.gameObject.activeSelf)
        {
            return 0f;
        }

        PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        RectTransform? content = cache != null && !IsUnityNull(cache) ? cache.TextContent : null;
        if (content == null || IsUnityNull(content))
        {
            return 0f;
        }

        if (root.parent != content)
        {
            root.SetParent(content, false);
        }

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(0f, -textHeight - InventoryPinnedJewelcraftingScrollGap);
        root.sizeDelta = new Vector2(textWidth, Mathf.Max(1f, root.sizeDelta.y));
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        root.SetAsLastSibling();

        float height = LayoutJewelcraftingNativeTooltip(root, textWidth);
        root.sizeDelta = new Vector2(textWidth, height);
        return height + InventoryPinnedJewelcraftingScrollGap;
    }

    private static bool HasNativeJewelcraftingTooltipRows(RectTransform root)
    {
        return HasVisibleNativeJewelcraftingSocketRows(root);
    }

    private static string GetJewelcraftingNativeTooltipLayoutSignature(RectTransform root)
    {
        unchecked
        {
            int hash = 17;
            int activeRows = 0;
            Transform? interactTransform = GetJewelcraftingSourceInteract(root);
            TMP_Text? interact = interactTransform != null ? interactTransform.GetComponent<TMP_Text>() : null;
            if (interactTransform != null &&
                !IsUnityNull(interactTransform) &&
                interactTransform.gameObject.activeSelf &&
                interact != null &&
                !IsUnityNull(interact))
            {
                string interactText = interact.text ?? "";
                hash = (hash * 31) + interactText.Length;
                hash = (hash * 31) + interactText.GetHashCode();
            }

            Transform? holes = GetJewelcraftingSourceRowsRoot(root);
            if (holes != null && !IsUnityNull(holes))
            {
                hash = (hash * 31) + holes.childCount;
                for (int i = 0; i < holes.childCount; i++)
                {
                    if (holes.GetChild(i) is not RectTransform row || !row.gameObject.activeSelf)
                    {
                        continue;
                    }

                    activeRows++;
                    JewelcraftingSourceRowUiCache rowCache = GetJewelcraftingSourceRowUiCache(row);
                    string text = rowCache.Text != null && !IsUnityNull(rowCache.Text)
                        ? rowCache.Text.text ?? ""
                        : "";
                    hash = (hash * 31) + row.name.GetHashCode();
                    hash = (hash * 31) + text.Length;
                    hash = (hash * 31) + text.GetHashCode();
                    hash = (hash * 31) + (rowCache.BorderImage != null && !IsUnityNull(rowCache.BorderImage) && rowCache.BorderImage.sprite != null ? rowCache.BorderImage.sprite.GetInstanceID() : 0);
                    hash = (hash * 31) + (rowCache.GemImage != null && !IsUnityNull(rowCache.GemImage) && rowCache.GemImage.sprite != null ? rowCache.GemImage.sprite.GetInstanceID() : 0);
                }
            }

            return $"{root.childCount}:{activeRows}:{hash}";
        }
    }

    private static void ConfigureJewelcraftingNativeGemIconTooltips(RectTransform root, ItemData item)
    {
        Transform? holes = GetJewelcraftingSourceRowsRoot(root);
        if (holes == null || IsUnityNull(holes))
        {
            return;
        }

        bool pinnedTooltip = IsPinnedTooltipTransform(root);
        List<JewelcraftingGemIconData> gems = pinnedTooltip ? GetJewelcraftingGemIconData(item) : new List<JewelcraftingGemIconData>();
        bool[] usedGems = new bool[gems.Count];
        for (int i = 0; i < holes.childCount; i++)
        {
            if (holes.GetChild(i) is not RectTransform row)
            {
                continue;
            }

            JewelcraftingSourceRowUiCache rowCache = GetJewelcraftingSourceRowUiCache(row);
            Image? gem = rowCache.GemImage;
            if (gem == null || IsUnityNull(gem))
            {
                continue;
            }

            RectTransform gemRect = gem.rectTransform;
            string displayName = pinnedTooltip &&
                                 gem.gameObject.activeSelf &&
                                 gem.sprite != null &&
                                 !IsUnityNull(gem.sprite)
                ? GetJewelcraftingNativeGemDisplayName(gems, usedGems, gem.sprite)
                : "";
            ConfigurePinnedGemIconTooltip(gemRect, displayName);
        }
    }

    private static string GetJewelcraftingNativeGemDisplayName(IReadOnlyList<JewelcraftingGemIconData> gems, bool[] usedGems, Sprite sprite)
    {
        int index = FindMatchingJewelcraftingGemIcon(gems, usedGems, sprite);
        if (index < 0)
        {
            return "";
        }

        usedGems[index] = true;
        return gems[index].DisplayName;
    }

    private static JewelcraftingSourceRowUiCache GetJewelcraftingSourceRowUiCache(RectTransform row)
    {
        JewelcraftingSourceRowUiCache cache = row.GetComponent<JewelcraftingSourceRowUiCache>() ?? row.gameObject.AddComponent<JewelcraftingSourceRowUiCache>();
        if (string.Equals(cache.ChildSignature, GetJewelcraftingSourceRowChildSignature(row, cache.BorderImage), StringComparison.Ordinal) &&
            cache.Text != null &&
            cache.BorderImage != null &&
            !IsUnityNull(cache.Text) &&
            !IsUnityNull(cache.BorderImage) &&
            (cache.GemImage == null || !IsUnityNull(cache.GemImage)))
        {
            return cache;
        }

        Transform? border = row.Find("Border");
        cache.Text = FindJewelcraftingSourceRowText(row, border);
        cache.BorderImage = border != null ? border.GetComponent<Image>() : null;
        Transform? gem = border != null ? border.Find("Transmute_1") : null;
        cache.GemImage = gem != null ? gem.GetComponent<Image>() : null;
        cache.ChildSignature = GetJewelcraftingSourceRowChildSignature(row, cache.BorderImage);
        return cache;
    }

    private static TMP_Text? FindJewelcraftingSourceRowText(RectTransform row, Transform? border)
    {
        TMP_Text? direct = row.GetComponent<TMP_Text>();
        if (direct != null && !IsUnityNull(direct))
        {
            return direct;
        }

        foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            if (border != null && !IsUnityNull(border) && text.transform.IsChildOf(border))
            {
                continue;
            }

            return text;
        }

        return null;
    }

    private static string GetJewelcraftingSourceRowChildSignature(RectTransform row, Image? borderImage)
    {
        int borderChildCount = borderImage != null && !IsUnityNull(borderImage)
            ? borderImage.transform.childCount
            : -1;
        return row.childCount + "|" + borderChildCount;
    }

    private static void RestoreJewelcraftingSourceTooltipForNativeUpdate(RectTransform root)
    {
        SetJewelcraftingSourceBranchNativeActive(GetJewelcraftingSourceRowsRoot(root));
        SetJewelcraftingSourceBranchNativeActive(GetJewelcraftingSourceInteract(root));
    }

    private static void SetJewelcraftingSourceBranchNativeActive(Transform? branch)
    {
        if (branch == null || IsUnityNull(branch))
        {
            return;
        }

        branch.gameObject.SetActive(true);
        if (branch.GetComponent<CanvasGroup>() is { } canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }

    private static float LayoutJewelcraftingNativeTooltip(RectTransform root, float width)
    {
        RestoreJewelcraftingSourceTooltipForNativeUpdate(root);

        float contentWidth = Mathf.Max(80f, width);
        root.sizeDelta = new Vector2(contentWidth, Mathf.Max(1f, root.sizeDelta.y));

        float y = 0f;
        Transform? interactTransform = GetJewelcraftingSourceInteract(root);
        TMP_Text? interact = interactTransform != null ? interactTransform.GetComponent<TMP_Text>() : null;
        if (interactTransform != null &&
            !IsUnityNull(interactTransform) &&
            interactTransform.gameObject.activeSelf &&
            interact != null &&
            !IsUnityNull(interact) &&
            JewelcraftingTooltipCore.HasVisibleText(interact.text ?? ""))
        {
            float interactHeight = LayoutJewelcraftingNativeText(interact, contentWidth, y, TextAlignmentOptions.Center);
            y -= interactHeight + InventoryPinnedJewelcraftingNativeInteractGap;
        }

        Transform? holesTransform = GetJewelcraftingSourceRowsRoot(root);
        RectTransform? holes = holesTransform as RectTransform;
        if (holes != null && !IsUnityNull(holes) && holes.gameObject.activeSelf)
        {
            holes.anchorMin = new Vector2(0f, 1f);
            holes.anchorMax = new Vector2(0f, 1f);
            holes.pivot = new Vector2(0f, 1f);
            holes.anchoredPosition = new Vector2(0f, y);
            holes.localScale = Vector3.one;
            holes.localRotation = Quaternion.identity;

            float rowY = 0f;
            for (int i = 0; i < holes.childCount; i++)
            {
                if (holes.GetChild(i) is not RectTransform row || !row.gameObject.activeSelf)
                {
                    continue;
                }

                float rowHeight = LayoutJewelcraftingNativeSocketRow(row, contentWidth, rowY);
                rowY -= rowHeight + InventoryPinnedJewelcraftingNativeRowGap;
            }

            float rowsHeight = Mathf.Max(0f, -rowY - InventoryPinnedJewelcraftingNativeRowGap);
            holes.sizeDelta = new Vector2(contentWidth, rowsHeight);
            y -= rowsHeight;
        }

        float height = Mathf.Max(1f, -y + InventoryPinnedJewelcraftingNativeBottomPadding);
        root.sizeDelta = new Vector2(contentWidth, height);
        return height;
    }

    private static void DisableJewelcraftingNativeLayoutDrivers(RectTransform root)
    {
        JewelcraftingTooltipLayoutCache cache = GetJewelcraftingTooltipLayoutCache(root);
        RefreshJewelcraftingNativeComponentCache(root, cache);
        foreach (LayoutGroup group in cache.NativeLayoutGroups)
        {
            if (group != null && !IsUnityNull(group))
            {
                group.enabled = false;
            }
        }

        foreach (ContentSizeFitter fitter in cache.NativeContentSizeFitters)
        {
            if (fitter != null && !IsUnityNull(fitter))
            {
                fitter.enabled = false;
            }
        }
    }

    private static float LayoutJewelcraftingNativeText(TMP_Text text, float width, float y, TextAlignmentOptions alignment)
    {
        RectTransform rect = text.rectTransform;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, 1f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        float height = GetStableJewelcraftingTooltipTextHeight(text, width, 1f);
        rect.sizeDelta = new Vector2(width, height);
        return height;
    }

    private static float LayoutJewelcraftingNativeSocketRow(RectTransform row, float width, float y)
    {
        float textX = InventoryPinnedJewelcraftingNativeIconSize + InventoryPinnedJewelcraftingIconGap;
        float textWidth = Mathf.Max(80f, width - textX - InventoryPinnedJewelcraftingRowPadding);
        JewelcraftingSourceRowUiCache cache = GetJewelcraftingSourceRowUiCache(row);
        TMP_Text? text = cache.Text;
        float textHeight = InventoryPinnedJewelcraftingNativeIconSize;
        if (text != null && !IsUnityNull(text))
        {
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            bool textIsRow = textRect == row;
            text.margin = textIsRow
                ? new Vector4(textX, 0f, InventoryPinnedJewelcraftingRowPadding, 0f)
                : Vector4.zero;
            if (!textIsRow)
            {
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                textRect.anchoredPosition = new Vector2(textX, 0f);
                textRect.sizeDelta = new Vector2(textWidth, 1f);
                textRect.localScale = Vector3.one;
                textRect.localRotation = Quaternion.identity;
            }

            textHeight = GetStableJewelcraftingTooltipTextHeight(text, textWidth, InventoryPinnedJewelcraftingNativeIconSize);
        }

        float rowHeight = Mathf.Max(InventoryPinnedJewelcraftingNativeIconSize, textHeight + InventoryPinnedJewelcraftingNativeTextPadding);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(0f, y);
        row.sizeDelta = new Vector2(width, rowHeight);
        row.localScale = Vector3.one;
        row.localRotation = Quaternion.identity;

        Image? border = cache.BorderImage;
        if (border != null && !IsUnityNull(border))
        {
            border.raycastTarget = false;
            RectTransform borderRect = border.rectTransform;
            borderRect.anchorMin = new Vector2(0f, 1f);
            borderRect.anchorMax = new Vector2(0f, 1f);
            borderRect.pivot = new Vector2(0f, 1f);
            borderRect.anchoredPosition = Vector2.zero;
            borderRect.sizeDelta = new Vector2(InventoryPinnedJewelcraftingNativeIconSize, InventoryPinnedJewelcraftingNativeIconSize);
            borderRect.localScale = Vector3.one;
            borderRect.localRotation = Quaternion.identity;
        }

        Image? gem = cache.GemImage;
        if (gem != null && !IsUnityNull(gem))
        {
            gem.raycastTarget = false;
            RectTransform gemRect = gem.rectTransform;
            gemRect.anchorMin = Vector2.zero;
            gemRect.anchorMax = Vector2.one;
            gemRect.pivot = new Vector2(0.5f, 0.5f);
            gemRect.offsetMin = new Vector2(InventoryPinnedJewelcraftingNativeIconInset, InventoryPinnedJewelcraftingNativeIconInset);
            gemRect.offsetMax = new Vector2(-InventoryPinnedJewelcraftingNativeIconInset, -InventoryPinnedJewelcraftingNativeIconInset);
            gemRect.localScale = Vector3.one;
            gemRect.localRotation = Quaternion.identity;
        }

        if (text != null && !IsUnityNull(text))
        {
            RectTransform textRect = text.rectTransform;
            textRect.sizeDelta = textRect == row
                ? new Vector2(width, rowHeight)
                : new Vector2(textWidth, rowHeight);
        }

        return rowHeight;
    }

    private static float GetStableJewelcraftingTooltipTextHeight(TMP_Text text, float width, float fallbackHeight)
    {
        string value = text.text ?? "";
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        float preferred = text.GetPreferredValues(value, width, 0f).y;
        if (!IsFinitePositive(preferred))
        {
            preferred = fallbackHeight;
        }

        float bounds = text.textBounds.size.y;
        if (IsFinitePositive(bounds) && bounds > preferred)
        {
            float saneBoundsLimit = Mathf.Max(preferred * 2f, fallbackHeight * 2f);
            if (bounds <= saneBoundsLimit)
            {
                preferred = bounds;
            }
        }

        return Mathf.Max(1f, preferred);
    }

    private static bool IsFinitePositive(float value) =>
        value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

}
