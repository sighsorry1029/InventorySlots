using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float CraftingHoverTooltipScrollbarWidth = 3f;
    private const float CraftingHoverTooltipScrollbarOutsideOffset = -5f;
    private const float CraftingHoverTooltipScrollSensitivity = 96f;
    private const float CraftingHoverTooltipMinBodyHeight = 80f;
    private const float CraftingHoverTooltipMaxPanelHeight = 720f;

    private static void UpdateCraftingTooltipRecipeOverlay(InventoryGui gui)
    {
        SyncCraftingRecipeHoverWithMouseIfRequested();

        if (!IsCraftingHoverTooltipEnabled() ||
            CraftingController.HoveredRecipeIndex < 0 ||
            !IsCraftingTooltipRecipeOverlayTargetValid() ||
            !TryGetCraftingRecipePair(gui, CraftingController.HoveredRecipeIndex, out InventoryGui.RecipeDataPair pair))
        {
            HideCraftingTooltipRecipeOverlay();
            return;
        }

        GameObject? tooltip = EnsureCraftingHoverTooltip(gui);
        RectTransform? tooltipRect = tooltip != null && !IsUnityNull(tooltip) ? tooltip.transform as RectTransform : null;
        RectTransform? panel = CraftingUi.HoverTooltipPanel;
        if (tooltip == null || tooltipRect == null || panel == null || IsUnityNull(panel))
        {
            HideCraftingTooltipRecipeOverlay();
            return;
        }

        const float slotSize = 42f;
        const float gap = 6f;
        const float padding = 8f;
        const float gemIconSize = 24f;
        const float gemGap = 5f;
        const float minSlotSize = 30f;
        const float minGap = 4f;
        CraftingHoverTooltipMode mode = GetCraftingHoverTooltipMode();
        bool showDetails = mode == CraftingHoverTooltipMode.Full;
        CraftingHoverTooltipContent content = GetCraftingHoverTooltipContent(pair, showDetails);
        bool veiledMasked = IsVeiledRecipeMasked(pair);
        ItemData? jewelcraftingTooltipItem = showDetails && !veiledMasked ? GetCraftingJewelcraftingTooltipItem(pair) : null;
        string gemSignature = GetCraftingHoverGemIconSignature(jewelcraftingTooltipItem);
        List<JewelcraftingGemIconData> gemIcons = GetCraftingHoverGemIcons(jewelcraftingTooltipItem, gemSignature);
        bool hasGemRow = showDetails && gemIcons.Count > 0;
        tooltip.SetActive(true);
        ApplyCraftingHoverTooltipBackground(tooltip.GetComponent<Image>());
        UpdateCraftingHoverTooltipText(content.Topic, content.Body);

        float contentWidth = CraftingTooltipRecipeSlotCount * slotSize + (CraftingTooltipRecipeSlotCount - 1) * gap;
        float overlayWidth = Mathf.Max(410f, contentWidth + padding * 2f);
        float effectiveSlotSize = slotSize;
        float effectiveGap = gap;
        float effectiveGemIconSize = gemIconSize;
        float effectiveGemGap = gemGap;

        ApplyCraftingTooltipOverlayWidth(overlayWidth, contentWidth, padding, minSlotSize, minGap, ref effectiveSlotSize, ref effectiveGap, ref effectiveGemIconSize, ref effectiveGemGap);
        if (Mathf.Abs(panel.rect.width - overlayWidth) > 0.5f)
        {
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, overlayWidth);
        }

        float height = showDetails ? effectiveSlotSize + padding * 2f + (hasGemRow ? gemGap + effectiveGemIconSize : 0f) : 0f;
        RectTransform overlay = EnsureCraftingTooltipRecipeOverlay(tooltipRect, panel);
        LayoutCraftingHoverTooltipPanel(panel, overlay, overlayWidth, height, padding, showBody: showDetails && !string.IsNullOrWhiteSpace(content.Body), showOverlay: showDetails);
        DisableCraftingTooltipRecipeOverlayBackground(overlay.GetComponent<Image>());

        panel.position = ZInput.mousePosition;
        Utils.ClampUIToScreen(panel);
        tooltip.transform.SetAsLastSibling();
        if (!showDetails)
        {
            overlay.gameObject.SetActive(false);
            HideCraftingGemIconRow(ref CraftingUi.TooltipGemIconRow);
            CraftingUi.HoverTooltipVisualSignature = "";
            return;
        }

        overlay.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        string visualSignature = GetCraftingHoverTooltipVisualSignature(
            pair,
            gemSignature,
            effectiveSlotSize,
            effectiveGap,
            effectiveGemIconSize,
            effectiveGemGap,
            hasGemRow);
        if (!string.Equals(CraftingUi.HoverTooltipVisualSignature, visualSignature, StringComparison.Ordinal) ||
            overlay.Find("RecipeRow") == null ||
            hasGemRow && overlay.Find(CraftingGemIconRowName) == null)
        {
            ConfigureCraftingTooltipRecipeRow(overlay, pair, Vector2.zero, effectiveSlotSize, effectiveGap, enableSlotTooltips: false);
            if (hasGemRow)
            {
                UpdateCraftingGemIconRow(
                    overlay,
                    gemIcons,
                    ref CraftingUi.TooltipGemIconRow,
                    new Vector2(0f, effectiveSlotSize + effectiveGemGap),
                    effectiveGemIconSize,
                    effectiveGemGap,
                    enableIconTooltips: false);
            }
            else
            {
                HideCraftingGemIconRow(ref CraftingUi.TooltipGemIconRow);
            }

            CraftingUi.HoverTooltipVisualSignature = visualSignature;
        }
    }

    private static CraftingHoverTooltipMode GetCraftingHoverTooltipMode() =>
        _showCraftingHoverTooltip != null ? _showCraftingHoverTooltip.Value : CraftingHoverTooltipMode.Full;

    private static bool IsCraftingHoverTooltipEnabled() =>
        GetCraftingHoverTooltipMode() != CraftingHoverTooltipMode.Off;

    private static void OnCraftingHoverTooltipConfigChanged()
    {
        if (!IsCraftingHoverTooltipEnabled())
        {
            HideCraftingTooltipRecipeOverlay();
            return;
        }

        RefreshCraftingHoverTooltipBackground();
    }

    private static bool IsCraftingTooltipRecipeOverlayTargetValid() =>
        IsCraftingTooltipRecipeOverlayTargetValid(GetUiMousePosition());

    private static bool IsCraftingTooltipRecipeOverlayTargetValid(Vector2 mouse)
    {
        RectTransform? grid = _craftingRecipeGrid;
        if (grid == null || IsUnityNull(grid) || !grid.gameObject.activeInHierarchy)
        {
            return false;
        }

        int viewIndex = FindCraftingRecipeViewIndex(CraftingController.HoveredRecipeIndex);
        if (viewIndex < 0)
        {
            return false;
        }

        int capacity = GetCraftingRecipeGridCapacity();
        int pageStart = _craftingRecipePage * capacity;
        int slotIndex = viewIndex - pageStart;
        if (slotIndex < 0 || slotIndex >= capacity || slotIndex >= CraftingRecipes.GridCells.Count)
        {
            return false;
        }

        CraftingRecipeGridCell cell = CraftingRecipes.GridCells[slotIndex];
        if (cell.Go == null || IsUnityNull(cell.Go) || !cell.Go.activeInHierarchy)
        {
            return false;
        }

        return RectContainsScreenPoint(cell.Rect, mouse);
    }

    private static CraftingHoverTooltipContent GetCraftingHoverTooltipContent(InventoryGui.RecipeDataPair pair, bool includeBody)
    {
        if (!includeBody)
        {
            return new CraftingHoverTooltipContent(GetCraftingRecipeDisplayName(pair), "");
        }

        string cacheKey = GetCraftingHoverTooltipContentKey(pair);
        if (!string.IsNullOrEmpty(cacheKey) &&
            CraftingRecipes.HoverTooltipContentCache.TryGetValue(cacheKey, out CraftingHoverTooltipContent cached))
        {
            return cached;
        }

        CraftingHoverTooltipContent content = new(GetCraftingRecipeDisplayName(pair), GetCraftingRecipeTooltip(pair));
        if (!string.IsNullOrEmpty(cacheKey))
        {
            CraftingRecipes.HoverTooltipContentCache[cacheKey] = content;
        }

        return content;
    }

    private static string GetCraftingHoverTooltipContentKey(InventoryGui.RecipeDataPair pair)
    {
        CraftingRecipePairCacheKey pairKey = GetCraftingRecipePairCacheKey(pair);
        Recipe? recipe = pair.Recipe;
        ItemData? item = pair.ItemData;
        int worldLevel = Game.m_worldLevel;
        int recipeAmount = recipe != null ? recipe.m_amount : 1;
        CraftingTabAdapterKind adapterKind = GetCraftingTabAdapterState(InventoryGui.instance).Kind;
        string itemRequiresSkillLevelSignature = GetItemRequiresSkillLevelCraftingTooltipSignature(pair);
        string veiledRecipeSignature = GetVeiledRecipeDisplaySignature(pair);
        if (item == null)
        {
            return $"{pairKey.RecipeId}|{pairKey.ItemKey}|adapter:{adapterKind}|craft|{recipeAmount}|{worldLevel}|irsl:{itemRequiresSkillLevelSignature}|{veiledRecipeSignature}";
        }

        string itemMode = adapterKind == CraftingTabAdapterKind.JewelcraftingSocket
            ? "socket"
            : "upgrade";
        return $"{pairKey.RecipeId}|{pairKey.ItemKey}|adapter:{adapterKind}|{itemMode}|{recipeAmount}|{worldLevel}|{GetCraftingTooltipItemSignature(item)}|irsl:{itemRequiresSkillLevelSignature}|{veiledRecipeSignature}";
    }

    private static string GetCraftingTooltipItemSignature(ItemData item)
    {
        unchecked
        {
            int customDataHash = 17;
            if (item.m_customData != null)
            {
                foreach (KeyValuePair<string, string> pair in item.m_customData)
                {
                    customDataHash = customDataHash * 31 + StringComparer.Ordinal.GetHashCode(pair.Key);
                    customDataHash = customDataHash * 31 + StringComparer.Ordinal.GetHashCode(pair.Value ?? "");
                }
            }

            int sharedId = item.m_shared != null ? item.m_shared.GetHashCode() : 0;
            return $"{sharedId}|{item.m_quality}|{item.m_variant}|{item.m_stack}|{item.m_durability:0.###}|{customDataHash}";
        }
    }

    private static GameObject? EnsureCraftingHoverTooltip(InventoryGui gui)
    {
        if (CraftingUi.HoverTooltip != null &&
            !IsUnityNull(CraftingUi.HoverTooltip) &&
            CraftingUi.HoverTooltipPanel != null &&
            !IsUnityNull(CraftingUi.HoverTooltipPanel))
        {
            return CraftingUi.HoverTooltip;
        }

        Canvas? canvas = gui.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : gui.transform;
        CraftingUi.HoverTooltip = new GameObject("InventorySlots_CraftingHoverTooltipRoot", typeof(RectTransform));
        CraftingUi.HoverTooltip.transform.SetParent(parent, false);
        CraftingUi.HoverTooltip.name = "InventorySlots_CraftingHoverTooltip";
        CraftingUi.HoverTooltip.SetActive(false);

        RectTransform panel = CraftingUi.HoverTooltip.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;

        ApplyCraftingHoverTooltipBackground(CraftingUi.HoverTooltip.AddComponent<Image>());

        CreateTextRect("Topic", panel, out TMP_Text topic);
        ApplyTooltipSourceFont(topic, "Topic");
        topic.alignment = TextAlignmentOptions.Center;
        topic.fontSize = 22f;
        topic.fontStyle = FontStyles.Bold;
        topic.color = new Color(1f, 0.82f, 0.42f, 1f);
        topic.textWrappingMode = TextWrappingModes.Normal;
        topic.overflowMode = TextOverflowModes.Overflow;
        topic.raycastTarget = false;

        CreateTextRect("Text", panel, out TMP_Text text);
        ApplyTooltipSourceFont(text, "Text");
        text.alignment = TextAlignmentOptions.TopLeft;
        text.fontSize = 18f;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        EnsureCraftingHoverTooltipTextScrollContent(panel, text);

        CraftingUi.HoverTooltipPanel = panel;
        CraftingUi.HoverTooltipTopic = topic;
        CraftingUi.HoverTooltipText = text;
        CraftingUi.HoverTooltipSignature = "";
        CraftingUi.HoverTooltipLayoutSignature = "";
        CraftingUi.HoverTooltipVisualSignature = "";
        return CraftingUi.HoverTooltip;
    }

    private static bool UpdateCraftingHoverTooltipText(string topic, string body)
    {
        string signature = topic + "\n---\n" + body;
        if (string.Equals(CraftingUi.HoverTooltipSignature, signature, StringComparison.Ordinal))
        {
            return false;
        }

        ApplyTooltipSourceFont(CraftingUi.HoverTooltipTopic, "Topic");
        ApplyTooltipSourceFont(CraftingUi.HoverTooltipText, "Text");

        if (CraftingUi.HoverTooltipTopic != null && !IsUnityNull(CraftingUi.HoverTooltipTopic))
        {
            CraftingUi.HoverTooltipTopic.text = topic;
            CraftingUi.HoverTooltipTopic.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        }

        if (CraftingUi.HoverTooltipText != null && !IsUnityNull(CraftingUi.HoverTooltipText))
        {
            CraftingUi.HoverTooltipText.text = body;
            CraftingUi.HoverTooltipScrollOffset = 0f;
            CraftingUi.HoverTooltipText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        }

        CraftingUi.HoverTooltipSignature = signature;
        CraftingUi.HoverTooltipLayoutSignature = "";
        return true;
    }

    private static void LayoutCraftingHoverTooltipPanel(RectTransform panel, RectTransform overlay, float width, float overlayHeight, float padding, bool showBody, bool showOverlay)
    {
        string signature = GetCraftingHoverTooltipLayoutSignature(width, overlayHeight, padding, showBody, showOverlay);
        if (string.Equals(CraftingUi.HoverTooltipLayoutSignature, signature, StringComparison.Ordinal))
        {
            ApplyCraftingHoverTooltipScrollPosition();
            return;
        }

        float topicGap = showBody ? 6f : 0f;
        float rowGap = showOverlay ? 8f : 0f;
        float textWidth = Mathf.Max(40f, width - padding * 2f);
        float topicHeight = 0f;
        float bodyHeight = 0f;
        float bodyPreferredHeight = 0f;

        if (CraftingUi.HoverTooltipTopic != null && !IsUnityNull(CraftingUi.HoverTooltipTopic))
        {
            TMP_Text topic = CraftingUi.HoverTooltipTopic;
            topic.gameObject.SetActive(true);
            topic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            topic.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
            topicHeight = Mathf.Max(28f, topic.preferredHeight);
        }

        if (CraftingUi.HoverTooltipText != null && !IsUnityNull(CraftingUi.HoverTooltipText))
        {
            TMP_Text body = CraftingUi.HoverTooltipText;
            EnsureCraftingHoverTooltipTextScrollContent(panel, body);
            body.gameObject.SetActive(showBody);
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            if (showBody)
            {
                body.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
                bodyPreferredHeight = GetCraftingHoverTooltipPreferredTextHeight(body, textWidth);
            }
        }

        float bodyTop = padding + topicHeight + topicGap;
        float fixedHeight = bodyTop + rowGap + (showOverlay ? overlayHeight : 0f) + padding;
        if (showBody)
        {
            float maxBodyHeight = Mathf.Max(CraftingHoverTooltipMinBodyHeight, GetCraftingHoverTooltipMaxPanelHeight() - fixedHeight);
            bodyHeight = bodyPreferredHeight > 0f ? Mathf.Min(bodyPreferredHeight, maxBodyHeight) : 0f;
        }

        float panelHeight = bodyTop + bodyHeight + rowGap + overlayHeight + padding;
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(width, panelHeight);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;

        if (CraftingUi.HoverTooltipTopic != null && !IsUnityNull(CraftingUi.HoverTooltipTopic))
        {
            LayoutCraftingHoverTextRect(CraftingUi.HoverTooltipTopic.rectTransform, padding, padding, topicHeight);
        }

        if (CraftingUi.HoverTooltipText != null && !IsUnityNull(CraftingUi.HoverTooltipText))
        {
            if (showBody)
            {
                LayoutCraftingHoverTooltipTextScroll(panel, textWidth, padding, bodyTop, bodyHeight, bodyPreferredHeight);
            }
            else
            {
                HideCraftingHoverTooltipBodyScroll();
            }
        }

        overlay.anchorMin = new Vector2(0f, 0f);
        overlay.anchorMax = new Vector2(1f, 0f);
        overlay.pivot = new Vector2(0.5f, 0f);
        overlay.anchoredPosition = new Vector2(0f, padding);
        overlay.sizeDelta = new Vector2(-padding * 2f, overlayHeight);
        overlay.localScale = Vector3.one;
        overlay.localRotation = Quaternion.identity;
        CraftingUi.HoverTooltipLayoutSignature = signature;
    }

    private static string GetCraftingHoverTooltipLayoutSignature(float width, float overlayHeight, float padding, bool showBody, bool showOverlay)
    {
        return string.Join(
            "|",
            CraftingUi.HoverTooltipSignature,
            Screen.width,
            Screen.height,
            GetCraftingHoverTooltipMaxPanelHeight().ToString("0.###"),
            width.ToString("0.###"),
            overlayHeight.ToString("0.###"),
            padding.ToString("0.###"),
            showBody,
            showOverlay);
    }

    private static void LayoutCraftingHoverTextRect(RectTransform rect, float horizontalPadding, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-horizontalPadding * 2f, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static RectTransform EnsureCraftingHoverTooltipTextScrollContent(RectTransform panel, TMP_Text text)
    {
        return ScrollableTooltipBody.Ensure(
            panel,
            text,
            CraftingUi.HoverTooltipTextScroll,
            GetSolidUiSprite(),
            CraftingHoverTooltipScrollSensitivity,
            scrollRectEnabled: false,
            inertia: false,
            handleRaycastTarget: false,
            scrollbarRaycastTarget: false);
    }

    private static void LayoutCraftingHoverTooltipTextScroll(RectTransform panel, float textWidth, float padding, float top, float viewportHeight, float contentHeight)
    {
        if (CraftingUi.HoverTooltipText == null || IsUnityNull(CraftingUi.HoverTooltipText))
        {
            return;
        }

        EnsureCraftingHoverTooltipTextScrollContent(panel, CraftingUi.HoverTooltipText);
        ScrollableTooltipBodyLayoutResult result = ScrollableTooltipBody.LayoutPixelScroll(
            CraftingUi.HoverTooltipTextScroll,
            CraftingUi.HoverTooltipText,
            textWidth,
            padding,
            top,
            viewportHeight,
            contentHeight,
            CraftingUi.HoverTooltipScrollOffset,
            CraftingHoverTooltipScrollbarOutsideOffset,
            CraftingHoverTooltipScrollbarWidth,
            enableScrollRectWhenNeeded: false);

        CraftingUi.HoverTooltipScrollOffset = result.ScrollOffset;
        CraftingUi.HoverTooltipMaxScroll = result.MaxScroll;
    }

    private static void ApplyCraftingHoverTooltipScrollPosition()
    {
        ScrollableTooltipBody.ApplyPixelScrollPosition(
            CraftingUi.HoverTooltipTextScroll,
            CraftingUi.HoverTooltipScrollOffset,
            CraftingUi.HoverTooltipMaxScroll);
    }

    private static void HideCraftingHoverTooltipBodyScroll()
    {
        CraftingUi.HoverTooltipScrollOffset = 0f;
        CraftingUi.HoverTooltipMaxScroll = 0f;
        if (CraftingUi.HoverTooltipTextScroll.Scrollbar != null && !IsUnityNull(CraftingUi.HoverTooltipTextScroll.Scrollbar))
        {
            CraftingUi.HoverTooltipTextScroll.Scrollbar.gameObject.SetActive(false);
        }

        if (CraftingUi.HoverTooltipTextScroll.ScrollView != null && !IsUnityNull(CraftingUi.HoverTooltipTextScroll.ScrollView))
        {
            CraftingUi.HoverTooltipTextScroll.ScrollView.gameObject.SetActive(false);
        }
    }

    private static bool HandleCraftingHoverTooltipWheel()
    {
        bool gamepadScroll = IsGamepadUiScrollActive();
        if (!HasCraftingHoverTooltipWheelOwner(GetUiMousePosition(), gamepadScroll))
        {
            return false;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Continuous);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        return TryScrollCraftingHoverTooltip(wheel);
    }

    private static bool TryScrollCraftingHoverTooltip(float wheel)
    {
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        ConsumeMouseUiScrollForCurrentFrame();
        CraftingUi.HoverTooltipScrollOffset = Mathf.Clamp(
            CraftingUi.HoverTooltipScrollOffset - wheel * CraftingHoverTooltipScrollSensitivity,
            0f,
            CraftingUi.HoverTooltipMaxScroll);
        ApplyCraftingHoverTooltipScrollPosition();
        return true;
    }

    private static bool HasCraftingHoverTooltipWheelOwner(Vector2 pointer, bool allowGamepad = false)
    {
        return CraftingUi.HoverTooltip != null &&
               !IsUnityNull(CraftingUi.HoverTooltip) &&
               CraftingUi.HoverTooltip.activeInHierarchy &&
               CraftingUi.HoverTooltipPanel != null &&
               !IsUnityNull(CraftingUi.HoverTooltipPanel) &&
               CraftingUi.HoverTooltipMaxScroll > 1f &&
               !IsCraftingRecipeGridZoomModifierHeld() &&
               (allowGamepad || IsCraftingTooltipRecipeOverlayTargetValid(pointer));
    }

    private static void PrepareCraftingTooltipScrollInput(InventoryGui gui)
    {
        if (HasUnconsumedUiScrollInput())
        {
            UpdateCraftingTooltipRecipeOverlay(gui);
        }
    }

    private static float GetCraftingHoverTooltipPreferredTextHeight(TMP_Text text, float textWidth)
    {
        Vector2 preferred = text.GetPreferredValues(text.text ?? "", textWidth, 0f);
        if (float.IsNaN(preferred.y) || float.IsInfinity(preferred.y) || preferred.y < 1f)
        {
            return Mathf.Max(1f, text.preferredHeight);
        }

        return Mathf.Max(1f, preferred.y);
    }

    private static float GetCraftingHoverTooltipMaxPanelHeight()
    {
        float screenHeight = Screen.height > 0 ? Screen.height : CraftingHoverTooltipMaxPanelHeight;
        return Mathf.Clamp(screenHeight * 0.62f, 260f, CraftingHoverTooltipMaxPanelHeight);
    }

    private static string GetCraftingHoverGemIconSignature(ItemData? item)
    {
        return item?.m_shared == null
            ? ""
            : string.Join("|", _uiLocalizationVersion, GetEquipmentSlotTooltipSignature(item));
    }

    private static List<JewelcraftingGemIconData> GetCraftingHoverGemIcons(ItemData? item, string signature)
    {
        if (item?.m_shared == null || string.IsNullOrEmpty(signature))
        {
            if (CraftingRecipes.HoverGemIconCache.Count > 0)
            {
                CraftingRecipes.HoverGemIconCache.Clear();
            }

            CraftingUi.HoverGemIconSignature = "";
            return CraftingRecipes.HoverGemIconCache;
        }

        if (string.Equals(CraftingUi.HoverGemIconSignature, signature, StringComparison.Ordinal))
        {
            return CraftingRecipes.HoverGemIconCache;
        }

        CraftingRecipes.HoverGemIconCache.Clear();
        CraftingRecipes.HoverGemIconCache.AddRange(GetJewelcraftingGemIconData(item));
        CraftingUi.HoverGemIconSignature = signature;
        return CraftingRecipes.HoverGemIconCache;
    }

    private static string GetCraftingHoverTooltipVisualSignature(
        InventoryGui.RecipeDataPair pair,
        string gemSignature,
        float slotSize,
        float gap,
        float gemIconSize,
        float gemGap,
        bool hasGemRow)
    {
        return string.Join(
            "|",
            GetCraftingTooltipRecipeRowSignature(pair),
            gemSignature,
            slotSize.ToString("0.###"),
            gap.ToString("0.###"),
            gemIconSize.ToString("0.###"),
            gemGap.ToString("0.###"),
            hasGemRow);
    }

    private static string GetCraftingTooltipRecipeRowSignature(InventoryGui.RecipeDataPair pair)
    {
        Recipe? recipe = pair.Recipe;
        int quality = pair.ItemData == null ? 1 : pair.ItemData.m_quality + 1;
        bool veiledMasked = IsVeiledRecipeMasked(pair);
        StringBuilder builder = new();
        builder.Append(GetCraftingHoverTooltipContentKey(pair));

        int written = 0;
        if (recipe?.m_resources != null)
        {
            for (int i = 0; i < recipe.m_resources.Length && written < CraftingTooltipRecipeSlotCount - 1; i++)
            {
                Requirement requirement = recipe.m_resources[i];
                if (requirement == null || requirement.m_resItem == null)
                {
                    continue;
                }

                int required = requirement.GetAmount(quality);
                if (required <= 0)
                {
                    continue;
                }

                int available = GetAvailableCraftingRequirementAmount(requirement);
                bool requirementKnown = !veiledMasked || IsVeiledRecipeRequirementKnown(requirement);
                builder
                    .Append('|')
                    .Append(requirementKnown ? requirement.m_resItem.name : "?")
                    .Append(':')
                    .Append(requirementKnown ? available.ToString() : "?")
                    .Append('/')
                    .Append(requirementKnown ? required.ToString() : "?");
                written++;
            }
        }

        CraftingStation? station = recipe != null ? recipe.GetRequiredStation(quality) : null;
        int requiredStationLevel = recipe != null ? recipe.GetRequiredStationLevel(quality) : 0;
        bool stationRequirementKnown = !veiledMasked || recipe == null || KnowsVeiledRecipeStationRequirement(recipe, quality);
        CraftingStation? currentStation = Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentCraftingStation() : null;
        bool stationAvailable = station == null ||
                                HasNoCraftCost() ||
                                stationRequirementKnown && currentStation != null && currentStation.m_name == station.m_name && currentStation.GetLevel() >= requiredStationLevel;
        builder
            .Append("|station:")
            .Append(station != null && stationRequirementKnown ? station.m_name : "")
            .Append(':')
            .Append(stationRequirementKnown ? requiredStationLevel.ToString() : "?")
            .Append(':')
            .Append(stationAvailable);
        return builder.ToString();
    }

    private static bool ApplyCraftingTooltipOverlayWidth(float overlayWidth, float contentWidth, float padding, float minSlotSize, float minGap, ref float effectiveSlotSize, ref float effectiveGap, ref float effectiveGemIconSize, ref float effectiveGemGap)
    {
        float minOverlayWidth = CraftingTooltipRecipeSlotCount * minSlotSize + (CraftingTooltipRecipeSlotCount - 1) * minGap + padding * 2f;
        if (overlayWidth < minOverlayWidth)
        {
            return false;
        }

        if (overlayWidth >= contentWidth + padding * 2f)
        {
            return true;
        }

        float availableWidth = Mathf.Max(0f, overlayWidth - padding * 2f);
        effectiveSlotSize = (availableWidth - (CraftingTooltipRecipeSlotCount - 1) * effectiveGap) / CraftingTooltipRecipeSlotCount;
        if (effectiveSlotSize < minSlotSize)
        {
            effectiveGap = Mathf.Max(minGap, (availableWidth - CraftingTooltipRecipeSlotCount * minSlotSize) / (CraftingTooltipRecipeSlotCount - 1));
            effectiveSlotSize = (availableWidth - (CraftingTooltipRecipeSlotCount - 1) * effectiveGap) / CraftingTooltipRecipeSlotCount;
        }

        effectiveSlotSize = Mathf.Clamp(effectiveSlotSize, minSlotSize, 42f);
        effectiveGemIconSize = Mathf.Min(24f, Mathf.Max(18f, effectiveSlotSize * 0.58f));
        effectiveGemGap = Mathf.Min(5f, Mathf.Max(3f, effectiveSlotSize * 0.12f));
        return true;
    }

    private static RectTransform EnsureCraftingTooltipRecipeOverlay(RectTransform tooltipRect, RectTransform parent)
    {
        if (CraftingUi.TooltipRecipeOverlay != null &&
            !IsUnityNull(CraftingUi.TooltipRecipeOverlay) &&
            CraftingUi.TooltipRecipeOverlay!.parent == parent)
        {
            return CraftingUi.TooltipRecipeOverlay;
        }

        Transform? existing = parent.Find(CraftingTooltipRecipeOverlayName) ?? tooltipRect.Find(CraftingTooltipRecipeOverlayName);
        RectTransform? overlay = CraftingUi.TooltipRecipeOverlay != null && !IsUnityNull(CraftingUi.TooltipRecipeOverlay) ? CraftingUi.TooltipRecipeOverlay : existing != null ? existing.GetComponent<RectTransform>() : null;
        CraftingUi.TooltipRecipeOverlay = overlay;
        if (CraftingUi.TooltipRecipeOverlay == null)
        {
            CraftingUi.TooltipRecipeOverlay = new GameObject(CraftingTooltipRecipeOverlayName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        }
        else if (CraftingUi.TooltipRecipeOverlay.GetComponent<Image>() == null)
        {
            CraftingUi.TooltipRecipeOverlay.gameObject.AddComponent<Image>();
        }

        CraftingUi.TooltipRecipeOverlay.SetParent(parent, false);
        return CraftingUi.TooltipRecipeOverlay;
    }

    private static void DisableCraftingTooltipRecipeOverlayBackground(Image? image)
    {
        if (image == null)
        {
            return;
        }

        image.enabled = false;
        image.raycastTarget = false;
    }

    private static void HideCraftingTooltipRecipeOverlay()
    {
        if (CraftingUi.HoverTooltip != null && !IsUnityNull(CraftingUi.HoverTooltip))
        {
            CraftingUi.HoverTooltip.SetActive(false);
        }

        if (CraftingUi.TooltipRecipeOverlay != null && !IsUnityNull(CraftingUi.TooltipRecipeOverlay))
        {
            CraftingUi.TooltipRecipeOverlay.gameObject.SetActive(false);
        }

        CraftingUi.HoverTooltipSignature = "";
        CraftingUi.HoverTooltipLayoutSignature = "";
        CraftingUi.HoverTooltipVisualSignature = "";
        CraftingUi.HoverGemIconSignature = "";
        CraftingRecipes.HoverGemIconCache.Clear();
        HideCraftingHoverTooltipBodyScroll();
        HideCraftingGemIconRow(ref CraftingUi.TooltipGemIconRow);
    }

    private static void ConfigureCraftingTooltipRecipeRow(RectTransform owner, InventoryGui.RecipeDataPair pair, Vector2 bottomLeft, float slotSize, float gap, bool enableSlotTooltips = true)
    {
        RectTransform row = EnsureCraftingTooltipRecipeRow(owner);
        row.anchorMin = Vector2.zero;
        row.anchorMax = Vector2.zero;
        row.pivot = Vector2.zero;
        row.anchoredPosition = bottomLeft;
        row.sizeDelta = new Vector2(CraftingTooltipRecipeSlotCount * slotSize + (CraftingTooltipRecipeSlotCount - 1) * gap, slotSize);
        row.localScale = Vector3.one;
        row.localRotation = Quaternion.identity;
        row.gameObject.SetActive(true);

        Recipe? recipe = pair.Recipe;
        int quality = pair.ItemData == null ? 1 : pair.ItemData.m_quality + 1;
        bool veiledMasked = IsVeiledRecipeMasked(pair);
        for (int i = 0; i < CraftingTooltipRecipeSlotCount - 1; i++)
        {
            int slot = i;
            RectTransform slotRect = EnsureCraftingTooltipRecipeSlot(row, slot);
            slotRect.anchoredPosition = new Vector2(slot * (slotSize + gap), 0f);
            if (!TryGetCraftingTooltipRequirement(recipe, quality, i, out Requirement requirement))
            {
                ConfigureCraftingTooltipRecipeSlot(row, slot, null, "", available: true, slotSize, "", enableSlotTooltips);
                continue;
            }

            int required = requirement.GetAmount(quality);
            int available = GetAvailableCraftingRequirementAmount(requirement);
            bool requirementKnown = !veiledMasked || IsVeiledRecipeRequirementKnown(requirement);
            string requirementName = requirementKnown ? GetRequirementDisplayName(requirement) : GetVeiledRecipeUnknownNameText();
            ConfigureCraftingTooltipRecipeSlot(
                row,
                slot,
                requirement.m_resItem.m_itemData.GetIcon(),
                requirementKnown ? $"{available}/{required}" : GetVeiledRecipeUnknownRequirementText(),
                requirementKnown && (HasNoCraftCost() || available >= required),
                slotSize,
                requirementName,
                enableSlotTooltips,
                requirementKnown ? null : Color.black,
                requirementKnown ? null : Color.white);
        }

        CraftingStation? station = recipe != null ? recipe.GetRequiredStation(quality) : null;
        int requiredStationLevel = recipe != null ? recipe.GetRequiredStationLevel(quality) : 0;
        bool stationRequirementKnown = !veiledMasked || recipe == null || KnowsVeiledRecipeStationRequirement(recipe, quality);
        CraftingStation? currentStation = Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentCraftingStation() : null;
        bool stationAvailable = station == null ||
                                HasNoCraftCost() ||
                                stationRequirementKnown && currentStation != null && currentStation.m_name == station.m_name && currentStation.GetLevel() >= requiredStationLevel;
        int stationSlot = CraftingTooltipRecipeSlotCount - 1;
        RectTransform stationSlotRect = EnsureCraftingTooltipRecipeSlot(row, stationSlot);
        stationSlotRect.anchoredPosition = new Vector2(stationSlot * (slotSize + gap), 0f);
        ConfigureCraftingTooltipRecipeSlot(
            row,
            stationSlot,
            station != null ? station.m_icon : null,
            station != null && requiredStationLevel > 0 ? stationRequirementKnown ? requiredStationLevel.ToString() : GetVeiledRecipeUnknownRequirementText() : "",
            available: stationAvailable,
            slotSize,
            station != null ? stationRequirementKnown ? GetCraftingStationDisplayName(station) : GetVeiledRecipeUnknownNameText() : "",
            enableSlotTooltips,
            stationRequirementKnown ? null : Color.black,
            stationRequirementKnown ? null : Color.white);
    }

    private static bool TryGetCraftingTooltipRequirement(Recipe? recipe, int quality, int index, out Requirement requirement)
    {
        requirement = null!;
        if (recipe?.m_resources == null)
        {
            return false;
        }

        int visibleIndex = 0;
        for (int i = 0; i < recipe.m_resources.Length; i++)
        {
            Requirement candidate = recipe.m_resources[i];
            if (candidate == null || candidate.m_resItem == null || candidate.GetAmount(quality) <= 0)
            {
                continue;
            }

            if (visibleIndex == index)
            {
                requirement = candidate;
                return true;
            }

            visibleIndex++;
            if (visibleIndex > index)
            {
                return false;
            }
        }

        return false;
    }

    private static RectTransform EnsureCraftingTooltipRecipeRow(RectTransform owner)
    {
        const string rowName = "RecipeRow";
        Transform? existing = owner.Find(rowName);
        RectTransform row = existing != null ? (RectTransform)existing : new GameObject(rowName, typeof(RectTransform)).GetComponent<RectTransform>();
        if (row.parent != owner)
        {
            row.SetParent(owner, false);
        }

        return row;
    }

    private static void ConfigureCraftingTooltipRecipeSlot(RectTransform row, int index, Sprite? icon, string amount, bool available, float slotSize, string tooltipTopic, bool enableTooltip, Color? iconColor = null, Color? amountColor = null)
    {
        RectTransform slot = EnsureCraftingTooltipRecipeSlot(row, index);
        slot.gameObject.SetActive(true);
        slot.sizeDelta = new Vector2(slotSize, slotSize);

        Image background = slot.GetComponent<Image>() ?? slot.gameObject.AddComponent<Image>();
        background.sprite = GetSolidUiSprite();
        background.color = new Color(0.12f, 0.075f, 0.045f, 0.62f);
        background.raycastTarget = enableTooltip && !string.IsNullOrWhiteSpace(tooltipTopic);
        ConfigureSimpleTooltip(slot.gameObject, tooltipTopic, enableTooltip && !string.IsNullOrWhiteSpace(tooltipTopic));

        Image iconImage = slot.Find("Icon")?.GetComponent<Image>() ?? CreateTooltipRecipeSlotImage(slot);
        iconImage.gameObject.SetActive(icon != null);
        if (icon != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(5f, -5f);
            iconRect.sizeDelta = new Vector2(slotSize - 10f, slotSize - 10f);
            iconImage.sprite = icon;
            iconImage.color = iconColor ?? (available ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.62f));
            iconImage.raycastTarget = false;
        }

        TMP_Text amountText = slot.Find("Amount")?.GetComponent<TMP_Text>() ?? CreateTooltipRecipeSlotText(slot);
        ApplyDefaultFontAsset(amountText);
        amountText.text = amount;
        amountText.gameObject.SetActive(!string.IsNullOrEmpty(amount));
        amountText.color = amountColor ?? (available ? new Color(1f, 0.84f, 0.42f, 1f) : new Color(1f, 0.32f, 0.24f, 1f));
        amountText.alignment = TextAlignmentOptions.BottomRight;
        amountText.textWrappingMode = TextWrappingModes.NoWrap;
        amountText.enableAutoSizing = true;
        amountText.fontSizeMin = 9f;
        amountText.fontSizeMax = 16f;
        RectTransform textRect = amountText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(3f, 2f);
        textRect.offsetMax = new Vector2(-3f, -2f);
    }

    private static RectTransform EnsureCraftingTooltipRecipeSlot(RectTransform row, int index)
    {
        string name = "RecipeSlot" + index;
        Transform? existing = row.Find(name);
        RectTransform slot = existing != null ? (RectTransform)existing : new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        if (slot.parent != row)
        {
            slot.SetParent(row, false);
        }

        slot.anchorMin = Vector2.zero;
        slot.anchorMax = Vector2.zero;
        slot.pivot = Vector2.zero;
        slot.localScale = Vector3.one;
        slot.localRotation = Quaternion.identity;
        return slot;
    }

    private static Image CreateTooltipRecipeSlotImage(RectTransform slot)
    {
        RectTransform icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        icon.SetParent(slot, false);
        return icon.GetComponent<Image>();
    }

    private static TMP_Text CreateTooltipRecipeSlotText(RectTransform slot)
    {
        CreateTextRect("Amount", slot, out TMP_Text amountText);
        return amountText;
    }
}
