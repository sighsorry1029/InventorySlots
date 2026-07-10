using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private enum PinnedTooltipContext
    {
        None,
        InventoryContainer,
        CraftingCraft,
        CraftingUpgrade
    }

    private const float PinnedTooltipScrollbarWidth = 3f;
    private const float PinnedTooltipScrollbarOutsideOffset = 2f;
    private const float PinnedTooltipScrollSensitivity = 240f;
    private const float CraftingPinnedTooltipTextScrollPadding = 0f;
    private const float PinnedTooltipScrollOverflowThreshold = 8f;
    private const float PinnedTooltipTopOffset = -12f;
    private const float PinnedTooltipBottomOffset = 60f;
    private const float PinnedTooltipInventoryStartOffset = 6f;
    private const float PinnedTooltipCraftingStartOffset = -80f;

    private readonly struct PinnedTooltipVerticalFrame
    {
        public readonly float TopY;
        public readonly float BottomY;
        public readonly float Height;

        public PinnedTooltipVerticalFrame(float topY, float bottomY)
        {
            TopY = topY;
            BottomY = bottomY;
            Height = Mathf.Max(1f, topY - bottomY);
        }
    }
    private static string GetCraftingPinnedTooltipGridSignature()
    {
        string signature = GetActivePinnedTooltipSlotCount().ToString();
        for (int i = 0; i < PinnedTooltips.Crafting.RecipeIndices.Length; i++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[i];
            int index = panel != null && !IsUnityNull(panel) && panel.gameObject.activeSelf
                ? PinnedTooltips.Crafting.RecipeIndices[i]
                : -1;
            signature = string.Concat(signature, "|", index.ToString());
        }

        return signature;
    }

    private static Vector2 GetPinnedTooltipPosition(int slot, Vector2 size, Vector2 groupOffset)
    {
        return GetPinnedTooltipPosition(null, slot, size, groupOffset);
    }

    private static Vector2 GetPinnedTooltipPosition(RectTransform? parent, int slot, Vector2 size, Vector2 groupOffset)
    {
        float step = size.x + PinnedTooltipFixedPanelGap;
        int clampedSlot = Mathf.Clamp(slot, 0, PinnedTooltipSlotCount - 1);
        float center = (PinnedTooltipSlotCount - 1) * 0.5f;
        float maxHeight = GetPinnedTooltipPanelSize().y;
        float x = groupOffset.x + (clampedSlot - center) * step;
        float y = (maxHeight - size.y) * 0.5f + groupOffset.y;
        if (parent != null && !IsUnityNull(parent))
        {
            if (TryGetPinnedTooltipVerticalFrame(parent, out PinnedTooltipVerticalFrame frame))
            {
                y = frame.TopY - size.y * 0.5f;
            }

            if (TryGetPinnedTooltipStartLocalX(parent, out float startX))
            {
                if (TooltipController.IsCraftingPinnedContext())
                {
                    int slotsFromRight = PinnedTooltipSlotCount - 1 - clampedSlot;
                    x = startX - slotsFromRight * step - size.x * 0.5f;
                }
                else
                {
                    x = startX + clampedSlot * step + size.x * 0.5f;
                }
            }
        }

        return new Vector2(x, y);
    }

    private static int GetActivePinnedTooltipSlotCount()
    {
        int count = _pinnedTooltipSlots != null ? (int)_pinnedTooltipSlots.Value : PinnedTooltipSlotCount;
        return Mathf.Clamp(count, 1, PinnedTooltipSlotCount);
    }

    private static int GetFirstActiveCraftingPinnedTooltipSlot() =>
        PinnedTooltipSlotCount - GetActivePinnedTooltipSlotCount();

    private static PinnedTooltipContext GetCurrentCraftingPinnedTooltipContext(InventoryGui gui)
    {
        if (IsJewelcraftingSocketTabActive(gui))
        {
            return PinnedTooltipContext.CraftingUpgrade;
        }

        if (gui.InUpradeTab())
        {
            return PinnedTooltipContext.CraftingUpgrade;
        }

        if (gui.InCraftTab())
        {
            return PinnedTooltipContext.CraftingCraft;
        }

        return PinnedTooltipContext.None;
    }

    private static void SetPinnedTooltipContext(PinnedTooltipContext context)
    {
        TooltipController.SetPinnedContext(context);
    }

    private static void SyncPinnedTooltipContextWithCraftingTab(InventoryGui gui)
    {
        TooltipController.SyncCraftingPinnedContext(GetCurrentCraftingPinnedTooltipContext(gui));
    }

    private static void ApplyPinnedTooltipSlotLimit()
    {
        int activeSlots = GetActivePinnedTooltipSlotCount();
        int firstCraftingSlot = GetFirstActiveCraftingPinnedTooltipSlot();
        bool inventoryChanged = false;
        for (int slot = 0; slot < PinnedTooltipSlotCount; slot++)
        {
            if (slot >= activeSlots && (IsInventoryPinnedTooltipSlotActive(slot) || PinnedTooltips.Inventory.Items[slot] != null))
            {
                HideInventoryPinnedTooltip(slot);
                inventoryChanged = true;
            }

            RectTransform? craftingPanel = PinnedTooltips.Crafting.Panels[slot];
            if (slot < firstCraftingSlot &&
                (PinnedTooltips.Crafting.RecipeIndices[slot] >= 0 ||
                craftingPanel != null && !IsUnityNull(craftingPanel) && craftingPanel.gameObject.activeSelf)
               )
            {
                HideCraftingPinnedTooltip(slot);
            }
        }

        if (inventoryChanged)
        {
            RefreshInventoryPinnedTooltipBorders();
        }

        RaiseInventoryPinnedTooltips();
    }

    private static void InvalidatePinnedTooltipUi()
    {
        ApplyPinnedTooltipSlotLimit();
        RefreshPinnedTooltipLayouts();
        RefreshPinnedTooltipBackgrounds();
        RefreshInventoryPinnedTooltipBorders();
        CraftingController.MarkRecipeGridLayoutDirty();
    }

    private static void RefreshPinnedTooltipLayouts()
    {
        for (int slot = 0; slot < PinnedTooltips.Inventory.Panels.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[slot];
            if (panel == null || IsUnityNull(panel) || !panel.gameObject.activeInHierarchy)
            {
                continue;
            }

            TMP_Text? text = PinnedTooltips.Inventory.Texts[slot] ?? FindPinnedTooltipText(panel);
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            ApplyPinnedTooltipDynamicTextLayout(panel, text, slot, InventoryPinnedTooltipFixedOffset, topReserved: 102f, bottomReserved: 18f, resetScroll: false);
        }

        for (int slot = 0; slot < PinnedTooltips.Crafting.Panels.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[slot];
            if (panel == null || IsUnityNull(panel) || !panel.gameObject.activeInHierarchy)
            {
                continue;
            }

            TMP_Text? text = PinnedTooltips.Crafting.Texts[slot] ?? FindPinnedTooltipText(panel);
            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
            float bottomReserved = cache != null && !IsUnityNull(cache) && cache.TextBottomReserved > 1f ? cache.TextBottomReserved : 92f;
            ApplyPinnedTooltipDynamicTextLayout(panel, text, slot, CraftingPinnedTooltipFixedOffset, topReserved: 102f, bottomReserved, maxTextViewportHeight: GetPinnedTooltipMaxTextViewportHeight(panel, 102f, bottomReserved), resetScroll: false);
        }
    }

    private static void RefreshPinnedTooltipBackgrounds()
    {
        foreach (RectTransform? panel in PinnedTooltips.Inventory.Panels)
        {
            if (panel != null && !IsUnityNull(panel))
            {
                ConfigurePinnedTooltipPanelBackground(panel);
            }
        }

        foreach (RectTransform? panel in PinnedTooltips.Crafting.Panels)
        {
            if (panel != null && !IsUnityNull(panel))
            {
                ConfigurePinnedTooltipPanelBackground(panel);
            }
        }
    }

    private static Vector2 GetPinnedTooltipPanelSize()
    {
        return new Vector2(Mathf.Clamp(PinnedTooltipFixedPanelSize.x, 180f, 1200f), Mathf.Clamp(PinnedTooltipFixedPanelSize.y, 180f, 1200f));
    }

    private static Vector2 GetPinnedTooltipPanelSize(RectTransform? parent)
    {
        Vector2 size = GetPinnedTooltipPanelSize();
        if (parent != null && !IsUnityNull(parent) && TryGetPinnedTooltipVerticalFrame(parent, out PinnedTooltipVerticalFrame frame))
        {
            size.y = Mathf.Clamp(frame.Height, 180f, 1200f);
        }

        return size;
    }

    private static Vector2 GetPinnedTooltipPanelSize(float height)
    {
        Vector2 maxSize = GetPinnedTooltipPanelSize();
        return new Vector2(maxSize.x, Mathf.Clamp(height, 180f, maxSize.y));
    }

    private static Vector2 GetPinnedTooltipPanelSize(RectTransform? parent, float height)
    {
        Vector2 maxSize = GetPinnedTooltipPanelSize(parent);
        return new Vector2(maxSize.x, Mathf.Clamp(height, 180f, maxSize.y));
    }

    private static Vector2 GetPinnedTooltipPanelSize(float height, float minHeight)
    {
        Vector2 maxSize = GetPinnedTooltipPanelSize();
        return new Vector2(maxSize.x, Mathf.Clamp(height, Mathf.Clamp(minHeight, 180f, maxSize.y), maxSize.y));
    }

    private static Vector2 GetPinnedTooltipPanelSize(RectTransform? parent, float height, float minHeight)
    {
        Vector2 maxSize = GetPinnedTooltipPanelSize(parent);
        return new Vector2(maxSize.x, Mathf.Clamp(height, Mathf.Clamp(minHeight, 180f, maxSize.y), maxSize.y));
    }

    private static bool TryGetPinnedTooltipVerticalFrame(RectTransform parent, out PinnedTooltipVerticalFrame frame)
    {
        frame = default;
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null ||
            IsUnityNull(gui) ||
            !TryGetDefaultEquipmentPanelBottomWorldY(gui, parent, out float equipmentBottomY) ||
            !TryGetCraftingPanelBottomWorldY(gui, parent, out float craftingBottomY))
        {
            return false;
        }

        float topY = equipmentBottomY + GetPinnedTooltipTopOffset();
        float bottomY = craftingBottomY + GetPinnedTooltipBottomOffset();
        if (float.IsNaN(topY) ||
            float.IsNaN(bottomY) ||
            float.IsInfinity(topY) ||
            float.IsInfinity(bottomY) ||
            topY <= bottomY + 180f)
        {
            return false;
        }

        frame = new PinnedTooltipVerticalFrame(topY, bottomY);
        return true;
    }

    private static bool TryGetPinnedTooltipStartLocalX(RectTransform parent, out float x)
    {
        x = 0f;
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || IsUnityNull(gui))
        {
            return false;
        }

        if (TooltipController.IsCraftingPinnedContext())
        {
            if (!TryGetRectWorldMinX(gui.m_crafting, out float craftingLeftWorldX))
            {
                return false;
            }

            x = WorldXToCenteredAnchoredX(parent, craftingLeftWorldX) + GetPinnedTooltipCraftingStartOffset();
        }
        else
        {
            if (!TryGetRectWorldMaxX(gui.m_player, out float inventoryRightWorldX))
            {
                return false;
            }

            x = WorldXToCenteredAnchoredX(parent, inventoryRightWorldX) + GetPinnedTooltipInventoryStartOffset();
        }

        return !float.IsNaN(x) && !float.IsInfinity(x);
    }

    private static float GetPinnedTooltipTopOffset() =>
        PinnedTooltipTopOffset;

    private static float GetPinnedTooltipBottomOffset() =>
        PinnedTooltipBottomOffset;

    private static float GetPinnedTooltipInventoryStartOffset() =>
        PinnedTooltipInventoryStartOffset;

    private static float GetPinnedTooltipCraftingStartOffset() =>
        PinnedTooltipCraftingStartOffset;

    private static bool TryGetDefaultEquipmentPanelBottomWorldY(InventoryGui gui, RectTransform parent, out float localY)
    {
        localY = 0f;
        InventoryGrid? playerGrid = gui.m_playerGrid;
        if (playerGrid == null ||
            IsUnityNull(playerGrid) ||
            playerGrid.m_gridRoot == null ||
            IsUnityNull(playerGrid.m_gridRoot))
        {
            return false;
        }

        int width = playerGrid.m_inventory != null ? playerGrid.m_inventory.GetWidth() : playerGrid.m_width;
        if (width <= 0)
        {
            width = InventoryWidth;
        }

        float elementSpace = Mathf.Max(1f, playerGrid.m_elementSpace);
        Vector3 origin = GetGridOrigin(playerGrid);
        Vector3 defaultTopLeft = GetSidePanelBasePosition(origin, width, elementSpace) + (Vector3)EquipmentSlotsPanelFixedOffset;
        Vector3 defaultBottomLeft = defaultTopLeft + new Vector3(0f, -CustomSlotPanelRows * elementSpace, 0f);
        float worldY = playerGrid.m_gridRoot.TransformPoint(defaultBottomLeft).y;
        localY = WorldYToCenteredAnchoredY(parent, worldY);
        return !float.IsNaN(localY) && !float.IsInfinity(localY);
    }

    private static bool TryGetCraftingPanelBottomWorldY(InventoryGui gui, RectTransform parent, out float localY)
    {
        localY = 0f;
        RectTransform? craftingPanel = gui.m_crafting;
        if (!TryGetRectWorldMinY(craftingPanel, out float worldY))
        {
            return false;
        }

        if (craftingPanel != null && !IsUnityNull(craftingPanel))
        {
            worldY = GetPinnedTooltipCraftingPanelVirtualBottomWorldY(craftingPanel, worldY);
        }

        localY = WorldYToCenteredAnchoredY(parent, worldY);
        return !float.IsNaN(localY) && !float.IsInfinity(localY);
    }

    private static float GetPinnedTooltipCraftingPanelVirtualBottomWorldY(RectTransform craftingPanel, float currentWorldBottomY)
    {
        float remainingExtension = GetRemainingCraftingPanelVirtualExtension(craftingPanel);
        if (remainingExtension <= 0.01f)
        {
            return currentWorldBottomY;
        }

        Vector3 worldOffset = craftingPanel.TransformVector(new Vector3(0f, -remainingExtension * Mathf.Clamp01(craftingPanel.pivot.y), 0f));
        float virtualWorldBottomY = currentWorldBottomY + worldOffset.y;
        return float.IsNaN(virtualWorldBottomY) || float.IsInfinity(virtualWorldBottomY)
            ? currentWorldBottomY
            : virtualWorldBottomY;
    }

    private static float GetRemainingCraftingPanelVirtualExtension(RectTransform craftingPanel)
    {
        float extension = CraftingPanelBottomFixedExtension;
        if (extension <= 0.01f)
        {
            return 0f;
        }

        if (_craftingPanelRootSnapshot == null || _craftingPanelRootSnapshot.Rect != craftingPanel)
        {
            return extension;
        }

        float appliedExtension = Mathf.Max(0f, craftingPanel.sizeDelta.y - _craftingPanelOriginalSizeDelta.y);
        return Mathf.Max(0f, extension - appliedExtension);
    }

    private static float WorldXToCenteredAnchoredX(RectTransform parent, float worldX)
    {
        Vector3 reference = parent.position;
        return parent.InverseTransformPoint(new Vector3(worldX, reference.y, reference.z)).x - GetCenteredAnchorReference(parent).x;
    }

    private static float WorldYToCenteredAnchoredY(RectTransform parent, float worldY)
    {
        Vector3 reference = parent.position;
        return parent.InverseTransformPoint(new Vector3(reference.x, worldY, reference.z)).y - GetCenteredAnchorReference(parent).y;
    }

    private static Vector2 GetCenteredAnchorReference(RectTransform parent)
    {
        Rect rect = parent.rect;
        return new Vector2(rect.xMin + rect.width * 0.5f, rect.yMin + rect.height * 0.5f);
    }

    private static bool TryGetRectWorldMinX(RectTransform? rect, out float x) =>
        TryGetRectWorldEdge(rect, axis: 0, findMax: false, out x);

    private static bool TryGetRectWorldMaxX(RectTransform? rect, out float x) =>
        TryGetRectWorldEdge(rect, axis: 0, findMax: true, out x);

    private static bool TryGetRectWorldMinY(RectTransform? rect, out float y) =>
        TryGetRectWorldEdge(rect, axis: 1, findMax: false, out y);

    private static bool TryGetRectWorldEdge(RectTransform? rect, int axis, bool findMax, out float value)
    {
        value = 0f;
        if (rect == null || IsUnityNull(rect))
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        value = axis == 0 ? corners[0].x : corners[0].y;
        for (int i = 1; i < corners.Length; i++)
        {
            float current = axis == 0 ? corners[i].x : corners[i].y;
            value = findMax ? Mathf.Max(value, current) : Mathf.Min(value, current);
        }

        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static TMP_Text? FindPinnedTooltipText(RectTransform panel)
    {
        PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        if (cache?.BodyText != null && !IsUnityNull(cache.BodyText) && cache.BodyText.transform.IsChildOf(panel))
        {
            return cache.BodyText;
        }

        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            if (text != null && !IsUnityNull(text) && text.name == "Text")
            {
                (cache ?? panel.gameObject.AddComponent<PinnedTooltipPanelUiCache>()).BodyText = text;
                return text;
            }
        }

        return null;
    }

    private static Image EnsurePinnedTooltipIcon(RectTransform panel)
    {
        Transform? existing = panel.Find("Icon");
        RectTransform iconRect = existing != null
            ? existing.GetComponent<RectTransform>()
            : new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        if (iconRect.parent != panel)
        {
            iconRect.SetParent(panel, false);
        }

        SetTopLeftRectLayoutCached(iconRect, new Vector2(16f, -16f), new Vector2(72f, 72f), "pinned-tooltip-icon");

        Image icon = iconRect.GetComponent<Image>() ?? iconRect.gameObject.AddComponent<Image>();
        icon.raycastTarget = false;
        return icon;
    }

    private static RectTransform EnsurePinnedTooltipPanel(RectTransform parent, string name, RectTransform? current, Func<RectTransform?>? findFallback = null)
    {
        RectTransform? panel = current;
        if (panel == null || IsUnityNull(panel) || panel!.parent != parent)
        {
            Transform? existing = parent.Find(name);
            panel = existing != null ? existing.GetComponent<RectTransform>() : null;
            panel ??= findFallback?.Invoke();
            panel ??= new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(parent, false);
        }

        ConfigurePinnedTooltipPanelBackground(panel);
        ConfigurePinnedTooltipPanelRaycast(panel);
        return panel;
    }

    private static void ConfigurePinnedTooltipPanelRaycast(RectTransform panel)
    {
        PinnedTooltipPanelUiCache cache = panel.GetComponent<PinnedTooltipPanelUiCache>() ?? panel.gameObject.AddComponent<PinnedTooltipPanelUiCache>();
        if (cache.Background == null || IsUnityNull(cache.Background))
        {
            cache.Background = panel.GetComponent<Image>();
        }

        if (cache.Background != null)
        {
            cache.Background.raycastTarget = false;
        }

        if (panel.Find("Icon")?.GetComponent<Image>() is { } icon)
        {
            icon.raycastTarget = false;
        }

        if (FindPinnedTooltipText(panel) is { } text)
        {
            text.raycastTarget = false;
        }
    }

    private static TMP_Text EnsurePinnedTooltipBodyText(RectTransform panel, float fontSize)
    {
        TMP_Text? text = FindPinnedTooltipText(panel);
        if (text == null || IsUnityNull(text))
        {
            CreateTextRect("Text", panel, out text);
        }

        (panel.GetComponent<PinnedTooltipPanelUiCache>() ?? panel.gameObject.AddComponent<PinnedTooltipPanelUiCache>()).BodyText = text;
        ApplyDefaultFontAsset(text);
        ApplyTooltipSourceFont(text, "Text");
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.maxVisibleCharacters = int.MaxValue;
        text.maxVisibleWords = int.MaxValue;
        text.maxVisibleLines = int.MaxValue;
        text.raycastTarget = false;
        text.enabled = true;
        text.gameObject.SetActive(true);
        EnsurePinnedTooltipTextScrollContent(panel, text);
        return text;
    }

    private static RectTransform EnsurePinnedTooltipTextScrollContent(RectTransform panel, TMP_Text text)
    {
        PinnedTooltipPanelUiCache cache = panel.GetComponent<PinnedTooltipPanelUiCache>() ?? panel.gameObject.AddComponent<PinnedTooltipPanelUiCache>();
        ScrollableTooltipBodyState state = ScrollableTooltipBody.FromPinnedCache(cache);
        bool scrollRectEnabled = state.ScrollRect != null && !IsUnityNull(state.ScrollRect) && state.ScrollRect.enabled;
        RectTransform content = ScrollableTooltipBody.Ensure(
            panel,
            text,
            state,
            GetSolidUiSprite(),
            GetPinnedTooltipScrollSensitivity(),
            scrollRectEnabled,
            inertia: true,
            handleRaycastTarget: true,
            scrollbarRaycastTarget: true);
        ScrollableTooltipBody.ApplyToPinnedCache(state, cache);

        RectTransform textRect = text.rectTransform;
        textRect.gameObject.SetActive(true);

        text.enabled = true;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;

        return content;
    }

    private static void LayoutPinnedTooltipTextScrollbar(RectTransform rect, float topReserved, float bottomReserved)
    {
        ScrollableTooltipBody.LayoutStretchScrollbar(
            rect,
            PinnedTooltipScrollbarOutsideOffset,
            PinnedTooltipScrollbarWidth,
            topReserved,
            bottomReserved);
    }

    private static void ResetPinnedTooltipTextScrollState(RectTransform panel)
    {
        PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        if (cache == null || IsUnityNull(cache))
        {
            return;
        }

        if (cache.TextContent != null && !IsUnityNull(cache.TextContent))
        {
            cache.TextContent.anchoredPosition = Vector2.zero;
        }

        if (cache.TextScrollRect != null && !IsUnityNull(cache.TextScrollRect))
        {
            cache.TextScrollRect.verticalNormalizedPosition = 1f;
            cache.TextScrollRect.velocity = Vector2.zero;
            cache.TextScrollRect.StopMovement();
        }

        if (cache.TextScrollbar != null && !IsUnityNull(cache.TextScrollbar))
        {
            SetPinnedTooltipScrollbarVisible(cache.TextScrollbar, visible: false);
        }

        cache.TextLayoutSignature = "";
        cache.TextRepairSignature = "";
    }

    private static void RepairCraftingPinnedTooltipTextVisibility()
    {
        for (int slot = 0; slot < PinnedTooltips.Crafting.Panels.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[slot];
            if (panel == null || IsUnityNull(panel) || !panel.gameObject.activeSelf)
            {
                continue;
            }

            TMP_Text? text = PinnedTooltips.Crafting.Texts[slot];
            if (text == null || IsUnityNull(text))
            {
                text = FindPinnedTooltipText(panel);
                PinnedTooltips.Crafting.Texts[slot] = text;
            }

            if (text == null || IsUnityNull(text))
            {
                continue;
            }

            bool repaired = false;
            if (!text.gameObject.activeSelf)
            {
                repaired = true;
                text.gameObject.SetActive(true);
            }

            if (!text.enabled)
            {
                repaired = true;
                text.enabled = true;
            }

            if (text.color.a <= 0.01f)
            {
                repaired = true;
                text.color = Color.white;
            }

            PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
            if (cache == null || IsUnityNull(cache))
            {
                continue;
            }

            string repairSignature = GetPinnedTooltipRepairSignature(panel, text, cache);
            if (!repaired && string.Equals(cache.TextRepairSignature, repairSignature, StringComparison.Ordinal))
            {
                continue;
            }

            RepairPinnedTooltipScrollbarVisibility(panel, text);
            cache.TextRepairSignature = repairSignature;
        }
    }

    private static bool HandleCraftingPinnedTooltipWheel()
    {
        if (!TooltipController.IsCraftingPinnedContext())
        {
            return false;
        }

        if (ShouldBlockGlobalHotkeys(Player.m_localPlayer))
        {
            return false;
        }

        bool gamepadScroll = IsGamepadUiScrollActive();
        float wheel = GetUiScrollDelta(UiScrollInputMode.Continuous);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        for (int slot = 0; slot < PinnedTooltips.Crafting.Panels.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Crafting.Panels[slot];
            if (panel == null ||
                IsUnityNull(panel) ||
                !panel.gameObject.activeInHierarchy ||
                !gamepadScroll && !RectContainsScreenPoint(panel, GetUiMousePosition()))
            {
                continue;
            }

            return TryScrollPinnedTooltipPanel(panel, slot, wheel);
        }

        return false;
    }

    private static bool HandleInventoryPinnedTooltipWheel()
    {
        if (!TooltipController.IsInventoryPinnedContext() ||
            ShouldBlockGlobalHotkeys(Player.m_localPlayer))
        {
            return false;
        }

        bool gamepadScroll = IsGamepadUiScrollActive();
        float wheel = GetUiScrollDelta(UiScrollInputMode.Continuous);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return false;
        }

        for (int slot = 0; slot < PinnedTooltips.Inventory.Panels.Length; slot++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[slot];
            if (panel == null ||
                IsUnityNull(panel) ||
                !panel.gameObject.activeInHierarchy ||
                !gamepadScroll && !RectContainsScreenPoint(panel, GetUiMousePosition()))
            {
                continue;
            }

            return TryScrollPinnedTooltipPanel(panel, slot, wheel);
        }

        return false;
    }

    private static bool TryScrollPinnedTooltipPanel(RectTransform panel, int slot, float wheel)
    {
        PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        if (cache == null ||
            IsUnityNull(cache) ||
            cache.TextScrollRect == null ||
            IsUnityNull(cache.TextScrollRect) ||
            !cache.TextScrollRect.enabled ||
            cache.TextContentHeight <= cache.TextViewportHeight + PinnedTooltipScrollOverflowThreshold)
        {
            return false;
        }

        float maxScroll = Mathf.Max(1f, cache.TextContentHeight - cache.TextViewportHeight);
        float delta = wheel * GetPinnedTooltipScrollSensitivity() / maxScroll;
        cache.TextScrollRect.verticalNormalizedPosition = Mathf.Clamp01(cache.TextScrollRect.verticalNormalizedPosition + delta);
        if (cache.TextScrollbar != null && !IsUnityNull(cache.TextScrollbar))
        {
            cache.TextScrollbar.value = cache.TextScrollRect.verticalNormalizedPosition;
        }

        return true;
    }

    private static void RepairPinnedTooltipScrollbarVisibility(RectTransform panel, TMP_Text text)
    {
        PinnedTooltipPanelUiCache? cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        if (cache == null ||
            IsUnityNull(cache) ||
            cache.TextScrollRect == null ||
            IsUnityNull(cache.TextScrollRect) ||
            cache.TextScrollbar == null ||
            IsUnityNull(cache.TextScrollbar) ||
            cache.TextViewport == null ||
            IsUnityNull(cache.TextViewport) ||
            cache.TextContent == null ||
            IsUnityNull(cache.TextContent))
        {
            return;
        }

        float textWidth = Mathf.Max(1f, text.rectTransform.rect.width);
        float preferredHeight = GetPinnedTooltipTextOnlyPreferredHeight(text, textWidth);
        float extraContentHeight = LayoutPinnedTooltipExtraScrollContent(panel, textWidth, preferredHeight);
        float contentHeight = preferredHeight + extraContentHeight + (cache.TextHasViewportCap ? CraftingPinnedTooltipTextScrollPadding : 0f);
        float viewportHeight = cache.TextViewportHeight > 1f ? cache.TextViewportHeight : cache.TextViewport.rect.height;
        if (viewportHeight < 1f)
        {
            return;
        }

        textWidth = cache.TextWidth > 1f ? cache.TextWidth : textWidth;
        contentHeight = Mathf.Max(1f, contentHeight);
        cache.TextContentHeight = contentHeight;
        bool needsScroll = contentHeight > viewportHeight + PinnedTooltipScrollOverflowThreshold;
        ApplyPinnedTooltipTextScrollGeometry(panel, text, cache, textWidth, contentHeight, viewportHeight, resetScroll: false, needsScroll);
        cache.TextScrollRect.scrollSensitivity = GetPinnedTooltipScrollSensitivity();
        SetPinnedTooltipScrollbarVisible(cache.TextScrollbar, needsScroll);
        cache.TextScrollbar.size = needsScroll ? Mathf.Clamp01(viewportHeight / contentHeight) : 1f;
        cache.TextScrollRect.verticalScrollbar = needsScroll ? cache.TextScrollbar : null;
        cache.TextScrollRect.verticalScrollbarVisibility = needsScroll ? ScrollRect.ScrollbarVisibility.Permanent : ScrollRect.ScrollbarVisibility.AutoHide;
        cache.TextScrollRect.enabled = needsScroll;
    }

    private static string GetPinnedTooltipRepairSignature(RectTransform panel, TMP_Text text, PinnedTooltipPanelUiCache cache)
    {
        Rect textRect = text.rectTransform.rect;
        float viewportHeight = cache.TextViewportHeight > 1f
            ? cache.TextViewportHeight
            : cache.TextViewport != null && !IsUnityNull(cache.TextViewport) ? cache.TextViewport.rect.height : 0f;
        return $"{panel.rect.width:0.###}|{panel.rect.height:0.###}|{textRect.width:0.###}|{textRect.height:0.###}|{viewportHeight:0.###}|{cache.TextWidth:0.###}|{cache.TextTopReserved:0.###}|{cache.TextBottomReserved:0.###}|{cache.TextHasViewportCap}|{GetPinnedTooltipScrollSensitivity():0.###}|{panel.childCount}|{text.text?.Length ?? 0}|{text.text?.GetHashCode() ?? 0}";
    }

    private static float GetPinnedTooltipScrollSensitivity() =>
        PinnedTooltipScrollSensitivity;

    private static float GetPinnedTooltipPreferredTextHeight(TMP_Text text, float textWidth) =>
        GetPinnedTooltipTextOnlyPreferredHeight(text, textWidth);

    private static float GetPinnedTooltipTextOnlyPreferredHeight(TMP_Text text, float textWidth)
    {
        string value = text.text ?? "";
        text.rectTransform.sizeDelta = new Vector2(textWidth, 1f);
        Vector2 preferred = text.GetPreferredValues(value, textWidth, 0f);
        float height = preferred.y;

        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        Bounds bounds = text.textBounds;
        if (!float.IsNaN(bounds.size.y) && !float.IsInfinity(bounds.size.y) && bounds.size.y > 0f)
        {
            height = Mathf.Max(height, bounds.size.y);
        }

        TMP_TextInfo info = text.textInfo;
        if (info != null && info.lineCount > 0 && info.lineInfo != null && info.lineInfo.Length >= info.lineCount)
        {
            TMP_LineInfo first = info.lineInfo[0];
            TMP_LineInfo last = info.lineInfo[info.lineCount - 1];
            float lineHeight = first.ascender - last.descender;
            if (!float.IsNaN(lineHeight) && !float.IsInfinity(lineHeight))
            {
                height = Mathf.Max(height, lineHeight);
            }
        }

        return float.IsNaN(height) || float.IsInfinity(height) || height < 1f
            ? 1f
            : Mathf.Max(1f, height);
    }

    private static float GetPinnedTooltipMaxTextViewportHeight(RectTransform panel, float topReserved, float bottomReserved)
    {
        RectTransform? parent = panel.parent as RectTransform;
        return Mathf.Max(120f, GetPinnedTooltipPanelSize(parent).y - topReserved - bottomReserved);
    }

    private static Vector2 ApplyPinnedTooltipDynamicTextLayout(RectTransform panel, TMP_Text text, int slot, Vector2 groupOffset, float topReserved, float bottomReserved, float maxTextViewportHeight = 0f, bool resetScroll = true)
    {
        RectTransform content = EnsurePinnedTooltipTextScrollContent(panel, text);
        PinnedTooltipPanelUiCache cache = panel.GetComponent<PinnedTooltipPanelUiCache>();
        RectTransform parent = panel.parent as RectTransform ?? panel;
        string layoutSignature = GetPinnedTooltipTextLayoutSignature(panel, text, slot, groupOffset, topReserved, bottomReserved, maxTextViewportHeight);
        if (!resetScroll &&
            panel.gameObject.activeInHierarchy &&
            string.Equals(cache.TextLayoutSignature, layoutSignature, StringComparison.Ordinal))
        {
            return panel.rect.size;
        }

        Vector2 maxSize = GetPinnedTooltipPanelSize(parent);
        float textWidth = Mathf.Max(40f, maxSize.x - 44f);
        text.enabled = true;
        text.gameObject.SetActive(true);
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = new Vector2(textWidth, 1f);
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        float preferredHeight = GetPinnedTooltipPreferredTextHeight(text, textWidth);
        bool hasTextViewportCap = maxTextViewportHeight > 1f;
        float extraContentHeight = LayoutPinnedTooltipExtraScrollContent(panel, textWidth, preferredHeight);
        float contentHeight = preferredHeight + extraContentHeight + (hasTextViewportCap ? CraftingPinnedTooltipTextScrollPadding : 0f);
        float desiredTextViewportHeight = hasTextViewportCap ? Mathf.Min(contentHeight, maxTextViewportHeight) : contentHeight;
        float desiredHeight = topReserved + bottomReserved + desiredTextViewportHeight;
        float minimumUsableHeight = topReserved + bottomReserved + 72f;
        Vector2 size = GetPinnedTooltipPanelSize(parent, desiredHeight, minimumUsableHeight);
        Vector2 position = GetPinnedTooltipPosition(parent, slot, size, groupOffset);
        SetCenteredRect(panel.parent as RectTransform ?? panel, panel, position, size);

        RectTransform scrollView = cache.TextScrollView!;
        RectTransform viewport = cache.TextViewport!;

        float availableTextHeight = Mathf.Max(1f, size.y - topReserved - bottomReserved);
        if (hasTextViewportCap)
        {
            availableTextHeight = Mathf.Min(availableTextHeight, maxTextViewportHeight);
            float cappedPanelHeight = topReserved + bottomReserved + availableTextHeight;
            if (cappedPanelHeight < size.y - 0.5f)
            {
                size = GetPinnedTooltipPanelSize(parent, cappedPanelHeight, minimumUsableHeight);
                position = GetPinnedTooltipPosition(parent, slot, size, groupOffset);
                SetCenteredRect(panel.parent as RectTransform ?? panel, panel, position, size);
            }
        }

        cache.TextWidth = textWidth;
        cache.TextContentHeight = contentHeight;
        cache.TextViewportHeight = availableTextHeight;
        cache.TextTopReserved = topReserved;
        cache.TextBottomReserved = bottomReserved;
        cache.TextHasViewportCap = hasTextViewportCap;
        cache.TextRepairSignature = "";
        bool needsScroll = contentHeight > availableTextHeight + PinnedTooltipScrollOverflowThreshold;
        ApplyPinnedTooltipTextScrollGeometry(panel, text, cache, textWidth, contentHeight, availableTextHeight, resetScroll, needsScroll);
        if (cache.TextScrollbar != null && !IsUnityNull(cache.TextScrollbar))
        {
            RectTransform scrollbarRect = (RectTransform)cache.TextScrollbar.transform;
            LayoutPinnedTooltipTextScrollbar(scrollbarRect, topReserved, bottomReserved);
            SetPinnedTooltipScrollbarVisible(cache.TextScrollbar, needsScroll);
            cache.TextScrollbar.size = needsScroll ? Mathf.Clamp01(availableTextHeight / contentHeight) : 1f;
            cache.TextScrollbar.value = resetScroll || !needsScroll
                ? 1f
                : Mathf.Clamp01(cache.TextScrollRect != null && !IsUnityNull(cache.TextScrollRect) ? cache.TextScrollRect.verticalNormalizedPosition : cache.TextScrollbar.value);
        }

        if (cache.TextScrollRect != null)
        {
            cache.TextScrollRect.scrollSensitivity = GetPinnedTooltipScrollSensitivity();
            if (resetScroll || !needsScroll)
            {
                cache.TextScrollRect.verticalNormalizedPosition = 1f;
            }
            cache.TextScrollRect.verticalScrollbar = needsScroll ? cache.TextScrollbar : null;
            cache.TextScrollRect.verticalScrollbarVisibility = needsScroll ? ScrollRect.ScrollbarVisibility.Permanent : ScrollRect.ScrollbarVisibility.AutoHide;
            cache.TextScrollRect.enabled = needsScroll;
        }

        if (cache.TextScrollView != null && cache.TextScrollView.GetComponent<Image>() is { } scrollImage)
        {
            scrollImage.raycastTarget = false;
        }

        if (cache.TextViewport != null && cache.TextViewport.GetComponent<Image>() is { } viewportImage)
        {
            viewportImage.raycastTarget = false;
        }

        cache.TextLayoutSignature = GetPinnedTooltipTextLayoutSignature(panel, text, slot, groupOffset, topReserved, bottomReserved, maxTextViewportHeight);
        return size;
    }

    private static string GetPinnedTooltipTextLayoutSignature(RectTransform panel, TMP_Text text, int slot, Vector2 groupOffset, float topReserved, float bottomReserved, float maxTextViewportHeight)
    {
        RectTransform parent = panel.parent as RectTransform ?? panel;
        Rect parentRect = parent.rect;
        string textValue = text.text ?? "";
        return string.Join(
            "|",
            slot,
            groupOffset.x.ToString("0.###"),
            groupOffset.y.ToString("0.###"),
            topReserved.ToString("0.###"),
            bottomReserved.ToString("0.###"),
            maxTextViewportHeight.ToString("0.###"),
            parent.GetInstanceID(),
            parentRect.width.ToString("0.###"),
            parentRect.height.ToString("0.###"),
            panel.childCount,
            textValue.Length,
            textValue.GetHashCode(),
            Screen.width,
            Screen.height,
            GetPinnedTooltipExtraLayoutSignature(panel),
            PinnedTooltipScrollSensitivity.ToString("0.###"));
    }

    private static string GetPinnedTooltipExtraLayoutSignature(RectTransform panel)
    {
        RectTransform? jewelcraftingRoot = FindJewelcraftingTooltipRoot(panel);
        if (jewelcraftingRoot != null && !IsUnityNull(jewelcraftingRoot) && jewelcraftingRoot.gameObject.activeSelf)
        {
            JewelcraftingTooltipLayoutCache? cache = jewelcraftingRoot.GetComponent<JewelcraftingTooltipLayoutCache>();
            return string.Join(
                ":",
                "jc",
                jewelcraftingRoot.GetInstanceID(),
                cache?.Signature ?? "",
                cache?.Visible == true ? "1" : "0",
                cache?.HasResolvedSocketGems == true ? "1" : "0",
                GetJewelcraftingNativeTooltipLayoutSignature(jewelcraftingRoot));
        }

        Transform? gemRow = panel.Find(CraftingGemIconRowName);
        if (gemRow != null && !IsUnityNull(gemRow) && gemRow.gameObject.activeSelf)
        {
            return $"gems:{gemRow.GetInstanceID()}:{gemRow.childCount}";
        }

        Transform? recipeRow = panel.Find("RecipeRow");
        if (recipeRow != null && !IsUnityNull(recipeRow) && recipeRow.gameObject.activeSelf)
        {
            return $"recipe:{recipeRow.GetInstanceID()}:{recipeRow.childCount}";
        }

        return "";
    }

    private static void ApplyPinnedTooltipTextScrollGeometry(RectTransform panel, TMP_Text text, PinnedTooltipPanelUiCache cache, float textWidth, float contentHeight, float viewportHeight, bool resetScroll, bool needsScroll)
    {
        if (cache.TextScrollView == null ||
            IsUnityNull(cache.TextScrollView) ||
            cache.TextViewport == null ||
            IsUnityNull(cache.TextViewport) ||
            cache.TextContent == null ||
            IsUnityNull(cache.TextContent))
        {
            return;
        }

        float oldNormalized = cache.TextScrollRect != null && !IsUnityNull(cache.TextScrollRect)
            ? cache.TextScrollRect.verticalNormalizedPosition
            : 1f;
        Vector2 oldContentPosition = cache.TextContent.anchoredPosition;
        RectTransform scrollView = cache.TextScrollView;
        SetTopLeftRectLayoutCached(scrollView, new Vector2(18f, -cache.TextTopReserved), new Vector2(textWidth, viewportHeight), "pinned-tooltip-scroll-view");

        RectTransform viewport = cache.TextViewport;
        SetStretchRectLayoutCached(viewport, Vector2.zero, Vector2.zero, "pinned-tooltip-viewport");

        RectTransform content = cache.TextContent;
        SetTopLeftRectLayoutCached(content, resetScroll || !needsScroll ? Vector2.zero : oldContentPosition, new Vector2(textWidth, contentHeight), "pinned-tooltip-content");

        RectTransform textRect = text.rectTransform;
        if (textRect.parent != content)
        {
            textRect.SetParent(content, false);
        }

        SetTopLeftRectLayoutCached(textRect, Vector2.zero, new Vector2(textWidth, contentHeight), "pinned-tooltip-text");
        text.gameObject.SetActive(true);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollView);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        if (cache.TextScrollRect != null && !IsUnityNull(cache.TextScrollRect))
        {
            cache.TextScrollRect.verticalNormalizedPosition = resetScroll || !needsScroll
                ? 1f
                : Mathf.Clamp01(oldNormalized);
        }
    }

    private static void SetPinnedTooltipScrollbarVisible(Scrollbar? scrollbar, bool visible)
    {
        if (scrollbar == null || IsUnityNull(scrollbar))
        {
            return;
        }

        scrollbar.gameObject.SetActive(visible);
        scrollbar.enabled = visible;

        if (scrollbar.targetGraphic != null && !IsUnityNull(scrollbar.targetGraphic))
        {
            scrollbar.targetGraphic.enabled = visible;
            scrollbar.targetGraphic.raycastTarget = visible;
        }

        foreach (Graphic graphic in scrollbar.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            if (graphic == null || IsUnityNull(graphic))
            {
                continue;
            }

            graphic.enabled = visible;
            graphic.raycastTarget = visible;
        }
    }

    private static void ConfigurePinnedTooltipPanelBackground(RectTransform panel)
    {
        if (panel == null || IsUnityNull(panel))
        {
            return;
        }

        PinnedTooltipPanelUiCache cache = panel.GetComponent<PinnedTooltipPanelUiCache>() ?? panel.gameObject.AddComponent<PinnedTooltipPanelUiCache>();
        if (cache.Background == null || IsUnityNull(cache.Background))
        {
            cache.Background = panel.GetComponent<Image>();
        }

        Image? image = cache.Background;
        if (image == null || IsUnityNull(image))
        {
            return;
        }

        Sprite sprite = GetSolidUiSprite();
        float alpha = Mathf.Clamp01(GetAdvancedConfigFloat(_pinnedTooltipBackgroundAlpha, 0.9f));
        Color desiredColor = new(0f, 0f, 0f, alpha);
        int spriteId = sprite != null && !IsUnityNull(sprite) ? sprite.GetInstanceID() : 0;
        string signature = spriteId.ToString() + "|" + alpha.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        if (cache.BackgroundSignature == signature && image.sprite == sprite && image.color == desiredColor && !image.raycastTarget)
        {
            return;
        }

        image.sprite = sprite;
        image.color = desiredColor;
        image.raycastTarget = false;
        cache.BackgroundSignature = signature;
    }

    private static void SetCenteredRect(RectTransform parent, RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        SetCenteredRectLayout(rect, anchoredPosition, size);
    }
}
