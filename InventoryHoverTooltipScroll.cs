using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float InventoryHoverTooltipWidth = 410f;
    private const float InventoryHoverTooltipPadding = 8f;
    private const float InventoryHoverTooltipTopicGap = 6f;
    private const float InventoryHoverTooltipScrollbarWidth = 3f;
    private const float InventoryHoverTooltipScrollbarOutsideOffset = 2f;
    private const float InventoryHoverTooltipScrollSensitivity = 120f;
    private const float InventoryHoverTooltipMinBodyHeight = 80f;
    private const float InventoryHoverTooltipMaxPanelHeight = 720f;
    private const float InventoryHoverTooltipScrollbarThresholdHeight = 600f;
    private const int InventoryHoverTooltipSourceCacheLimit = 64;

    private sealed class InventoryHoverTooltipRuntimeState
    {
        public GameObject? HoverTooltip;
        public RectTransform? HoverTooltipPanel;
        public TMP_Text? HoverTooltipTopic;
        public TMP_Text? HoverTooltipText;
        public readonly ScrollableTooltipBodyState HoverTooltipTextScroll = new();
        public readonly TooltipSourceCacheCore<UITooltip, InventoryHoverTooltipItemSource> HoverTooltipItems = new(InventoryHoverTooltipSourceCacheLimit);
        public string HoverTooltipSignature = "";
        public string HoverTooltipLayoutSignature = "";
        public float HoverTooltipScrollOffset;
        public float HoverTooltipMaxScroll;
        public bool HoverTooltipNeedsScroll;
        public RectTransform? HoverJewelcraftingTooltipRoot;
        public string HoverJewelcraftingTooltipSignature = "";
        public int HoverJewelcraftingTooltipLayoutRefreshFrames;
        public GameObject? SimpleNameTooltip;
        public RectTransform? SimpleNameTooltipRect;
        public TMP_Text? SimpleNameTooltipText;
        public string SimpleNameTooltipValue = "";
        public SimpleNameTooltipPlacement SimpleNameTooltipPlacement = SimpleNameTooltipPlacement.RightOfCursor;
        public readonly SimpleTooltipOwnershipCore SimpleNameTooltipOwner = new();
        public readonly object SimpleNameTooltipFallbackOwner = new();
        public object? PinnedGemNameTooltipOwner;
        public float PinnedGemNameTooltipHoldUntil = -1000f;
        public UITooltip? ContainerHoverTooltipSource;
        public UITooltip? OwnedHoverTooltipSource;
        public bool LateUpdateExceptionLogged;
    }

    private static readonly InventoryHoverTooltipRuntimeState InventoryHoverTooltipRuntime = new();
    private static GameObject? _inventoryHoverTooltip { get => InventoryHoverTooltipRuntime.HoverTooltip; set => InventoryHoverTooltipRuntime.HoverTooltip = value; }
    private static RectTransform? _inventoryHoverTooltipPanel { get => InventoryHoverTooltipRuntime.HoverTooltipPanel; set => InventoryHoverTooltipRuntime.HoverTooltipPanel = value; }
    private static TMP_Text? _inventoryHoverTooltipTopic { get => InventoryHoverTooltipRuntime.HoverTooltipTopic; set => InventoryHoverTooltipRuntime.HoverTooltipTopic = value; }
    private static TMP_Text? _inventoryHoverTooltipText { get => InventoryHoverTooltipRuntime.HoverTooltipText; set => InventoryHoverTooltipRuntime.HoverTooltipText = value; }
    private static ScrollableTooltipBodyState InventoryHoverTooltipTextScroll => InventoryHoverTooltipRuntime.HoverTooltipTextScroll;
    private static TooltipSourceCacheCore<UITooltip, InventoryHoverTooltipItemSource> InventoryHoverTooltipItems => InventoryHoverTooltipRuntime.HoverTooltipItems;
    private static string _inventoryHoverTooltipSignature { get => InventoryHoverTooltipRuntime.HoverTooltipSignature; set => InventoryHoverTooltipRuntime.HoverTooltipSignature = value; }
    private static string _inventoryHoverTooltipLayoutSignature { get => InventoryHoverTooltipRuntime.HoverTooltipLayoutSignature; set => InventoryHoverTooltipRuntime.HoverTooltipLayoutSignature = value; }
    private static float _inventoryHoverTooltipScrollOffset { get => InventoryHoverTooltipRuntime.HoverTooltipScrollOffset; set => InventoryHoverTooltipRuntime.HoverTooltipScrollOffset = value; }
    private static float _inventoryHoverTooltipMaxScroll { get => InventoryHoverTooltipRuntime.HoverTooltipMaxScroll; set => InventoryHoverTooltipRuntime.HoverTooltipMaxScroll = value; }
    private static bool _inventoryHoverTooltipNeedsScroll { get => InventoryHoverTooltipRuntime.HoverTooltipNeedsScroll; set => InventoryHoverTooltipRuntime.HoverTooltipNeedsScroll = value; }
    private static RectTransform? _inventoryHoverJewelcraftingTooltipRoot { get => InventoryHoverTooltipRuntime.HoverJewelcraftingTooltipRoot; set => InventoryHoverTooltipRuntime.HoverJewelcraftingTooltipRoot = value; }
    private static string _inventoryHoverJewelcraftingTooltipSignature { get => InventoryHoverTooltipRuntime.HoverJewelcraftingTooltipSignature; set => InventoryHoverTooltipRuntime.HoverJewelcraftingTooltipSignature = value; }
    private static int _inventoryHoverJewelcraftingTooltipLayoutRefreshFrames { get => InventoryHoverTooltipRuntime.HoverJewelcraftingTooltipLayoutRefreshFrames; set => InventoryHoverTooltipRuntime.HoverJewelcraftingTooltipLayoutRefreshFrames = value; }
    private static GameObject? _inventorySimpleNameTooltip { get => InventoryHoverTooltipRuntime.SimpleNameTooltip; set => InventoryHoverTooltipRuntime.SimpleNameTooltip = value; }
    private static RectTransform? _inventorySimpleNameTooltipRect { get => InventoryHoverTooltipRuntime.SimpleNameTooltipRect; set => InventoryHoverTooltipRuntime.SimpleNameTooltipRect = value; }
    private static TMP_Text? _inventorySimpleNameTooltipText { get => InventoryHoverTooltipRuntime.SimpleNameTooltipText; set => InventoryHoverTooltipRuntime.SimpleNameTooltipText = value; }
    private static string _inventorySimpleNameTooltipValue { get => InventoryHoverTooltipRuntime.SimpleNameTooltipValue; set => InventoryHoverTooltipRuntime.SimpleNameTooltipValue = value; }
    private static SimpleNameTooltipPlacement _inventorySimpleNameTooltipPlacement { get => InventoryHoverTooltipRuntime.SimpleNameTooltipPlacement; set => InventoryHoverTooltipRuntime.SimpleNameTooltipPlacement = value; }
    private static SimpleTooltipOwnershipCore InventorySimpleNameTooltipOwner => InventoryHoverTooltipRuntime.SimpleNameTooltipOwner;
    private static object InventorySimpleNameTooltipFallbackOwner => InventoryHoverTooltipRuntime.SimpleNameTooltipFallbackOwner;
    private static object? _pinnedGemNameTooltipOwner { get => InventoryHoverTooltipRuntime.PinnedGemNameTooltipOwner; set => InventoryHoverTooltipRuntime.PinnedGemNameTooltipOwner = value; }
    private static float _pinnedGemNameTooltipHoldUntil { get => InventoryHoverTooltipRuntime.PinnedGemNameTooltipHoldUntil; set => InventoryHoverTooltipRuntime.PinnedGemNameTooltipHoldUntil = value; }
    private static UITooltip? _inventoryContainerHoverTooltipSource { get => InventoryHoverTooltipRuntime.ContainerHoverTooltipSource; set => InventoryHoverTooltipRuntime.ContainerHoverTooltipSource = value; }
    private static UITooltip? _inventorySlotsOwnedHoverTooltipSource { get => InventoryHoverTooltipRuntime.OwnedHoverTooltipSource; set => InventoryHoverTooltipRuntime.OwnedHoverTooltipSource = value; }
    private static bool _tooltipLateUpdateExceptionLogged { get => InventoryHoverTooltipRuntime.LateUpdateExceptionLogged; set => InventoryHoverTooltipRuntime.LateUpdateExceptionLogged = value; }

    private enum SimpleNameTooltipPlacement
    {
        RightOfCursor,
        LeftOfCursor
    }

    private readonly struct InventoryHoverTooltipItemSource
    {
        public InventoryHoverTooltipItemSource(InventoryGrid? grid, ItemData item)
        {
            Grid = grid;
            Item = item;
        }

        public InventoryGrid? Grid { get; }
        public ItemData Item { get; }
    }

    internal static void EnsureInventoryContainerHoverTooltipScroll(UITooltip tooltip)
    {
        UpdateInventoryContainerHoverTooltip(tooltip, resetScroll: true, handleWheel: false);
    }

    internal static void RegisterInventoryGridItemTooltip(InventoryGrid? grid, ItemData item, UITooltip tooltip)
    {
        if (tooltip == null ||
            IsUnityNull(tooltip) ||
            item?.m_shared == null)
        {
            return;
        }

        PruneInventoryHoverTooltipSources();
        InventoryHoverTooltipItems.Set(tooltip, new InventoryHoverTooltipItemSource(grid, item));
    }

    internal static void UpdateInventoryContainerHoverTooltipScroll(UITooltip tooltip)
    {
        UpdateInventoryContainerHoverTooltip(tooltip, resetScroll: false, handleWheel: true);
    }

    internal static void BeginInventoryContainerHoverTooltipOwnership(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip) || !IsInventoryContainerTooltipSource(tooltip))
        {
            return;
        }

        if (_inventoryContainerHoverTooltipSource != tooltip)
        {
            _inventoryHoverTooltipSignature = "";
            _inventoryHoverTooltipScrollOffset = 0f;
        }

        _inventoryContainerHoverTooltipSource = tooltip;
    }

    internal static void EndInventoryContainerHoverTooltipOwnership(UITooltip tooltip)
    {
        if (tooltip == null ||
            IsUnityNull(tooltip) ||
            _inventoryContainerHoverTooltipSource == null ||
            IsUnityNull(_inventoryContainerHoverTooltipSource) ||
            _inventoryContainerHoverTooltipSource != tooltip)
        {
            return;
        }

        _inventoryContainerHoverTooltipSource = null;
        HideInventoryContainerCustomTooltip();
        ForceHideInventorySimpleNameTooltip();
        RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
    }

    internal static void UpdateInventorySlotsOwnedHoverTooltip(UITooltip tooltip, bool resetScroll, bool handleWheel)
    {
        if (!ShouldUseInventorySlotsOwnedHoverTooltip(tooltip))
        {
            return;
        }

        if (resetScroll)
        {
            BeginInventorySlotsOwnedHoverTooltip(tooltip);
        }
        else if (_inventorySlotsOwnedHoverTooltipSource == null ||
                 IsUnityNull(_inventorySlotsOwnedHoverTooltipSource) ||
                 _inventorySlotsOwnedHoverTooltipSource != tooltip)
        {
            return;
        }

        if (IsSimpleInventorySlotsNameTooltip(tooltip))
        {
            HideInventoryContainerCustomTooltip();
            HideVanillaTooltipVisual(UITooltip.m_tooltip);
            UpdateInventorySimpleNameTooltip(tooltip);
            return;
        }

        ForceHideInventorySimpleNameTooltip();
        UpdateInventoryHoverCustomTooltip(
            tooltip,
            resetScroll,
            handleWheel,
            GetInventoryHoverCustomTooltipBackgroundAlpha(tooltip),
            hideVanillaTooltip: true);
    }

    internal static bool ShouldAllowTooltipLateUpdate(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip) || !ShouldSuppressVanillaLateUpdate(tooltip))
        {
            return true;
        }

        bool hasOwnedSource =
            _inventorySlotsOwnedHoverTooltipSource != null &&
            !IsUnityNull(_inventorySlotsOwnedHoverTooltipSource) &&
            _inventorySlotsOwnedHoverTooltipSource == tooltip;
        bool isActiveOwnedSource = hasOwnedSource || UITooltip.m_current == tooltip;

        if (!isActiveOwnedSource)
        {
            return true;
        }

        UpdateInventorySlotsOwnedHoverTooltip(tooltip, resetScroll: !hasOwnedSource, handleWheel: true);
        return false;
    }

    internal static void BeginInventorySlotsOwnedHoverTooltip(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return;
        }

        if (_inventorySlotsOwnedHoverTooltipSource != tooltip)
        {
            _inventoryHoverTooltipSignature = "";
            _inventoryHoverTooltipScrollOffset = 0f;
        }

        _inventorySlotsOwnedHoverTooltipSource = tooltip;
        UITooltip.m_current = tooltip;
        if (ShouldSuppressVanillaHoverStart(tooltip) && UITooltip.m_hovered == tooltip.gameObject)
        {
            UITooltip.m_hovered = null;
        }
    }

    internal static void EndInventorySlotsOwnedHoverTooltip(UITooltip tooltip)
    {
        if (tooltip == null ||
            IsUnityNull(tooltip) ||
            _inventorySlotsOwnedHoverTooltipSource == null ||
            IsUnityNull(_inventorySlotsOwnedHoverTooltipSource) ||
            _inventorySlotsOwnedHoverTooltipSource != tooltip)
        {
            return;
        }

        _inventorySlotsOwnedHoverTooltipSource = null;
        if (UITooltip.m_current == tooltip)
        {
            UITooltip.m_current = null;
        }

        if (UITooltip.m_hovered == tooltip.gameObject)
        {
            UITooltip.m_hovered = null;
        }

        HideInventoryContainerCustomTooltip();
        ForceHideInventorySimpleNameTooltip();
        RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
    }

    internal static bool TryRecoverInventorySlotsTooltipLateUpdateException(UITooltip tooltip, Exception exception)
    {
        if (exception is not NullReferenceException ||
            tooltip == null ||
            IsUnityNull(tooltip) ||
            !IsRecoverableInventorySlotsTooltipLateUpdateSource(tooltip))
        {
            return false;
        }

        if (!_tooltipLateUpdateExceptionLogged)
        {
            _tooltipLateUpdateExceptionLogged = true;
            Log.LogWarning($"Recovered InventorySlots-owned UITooltip LateUpdate null state for {tooltip.name}. Future identical recoveries will be silent.");
        }

        if (UITooltip.m_current == tooltip)
        {
            UITooltip.m_current = null;
        }

        if (UITooltip.m_hovered == tooltip.gameObject)
        {
            UITooltip.m_hovered = null;
        }

        if (_inventorySlotsOwnedHoverTooltipSource == tooltip)
        {
            _inventorySlotsOwnedHoverTooltipSource = null;
        }

        HideInventoryContainerCustomTooltip();
        ForceHideInventorySimpleNameTooltip();
        RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
        return true;
    }

    private static bool IsRecoverableInventorySlotsTooltipLateUpdateSource(UITooltip tooltip)
    {
        if (_inventorySlotsOwnedHoverTooltipSource != null &&
            !IsUnityNull(_inventorySlotsOwnedHoverTooltipSource) &&
            _inventorySlotsOwnedHoverTooltipSource == tooltip)
        {
            return true;
        }

        return UITooltip.m_current == tooltip && ShouldUseInventorySlotsOwnedHoverTooltip(tooltip);
    }

    internal static void OnVanillaTooltipHidden()
    {
        _inventoryContainerHoverTooltipSource = null;
        _inventorySlotsOwnedHoverTooltipSource = null;
        HideInventoryContainerCustomTooltip();
        ForceHideInventorySimpleNameTooltip();
        PruneInventoryHoverTooltipSources();
        RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
    }

    private static bool ShouldSuppressInventoryContainerRowsWheel()
    {
        if (IsMouseOverContainerInventory())
        {
            return true;
        }

        if (UITooltip.m_current == null ||
            IsUnityNull(UITooltip.m_current) ||
            !IsInventoryContainerTooltipSource(UITooltip.m_current))
        {
            return false;
        }

        return _inventoryHoverTooltipNeedsScroll ||
               HasTooltipScrollRect(UITooltip.m_tooltip);
    }

    private static bool IsMouseOverContainerInventory()
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null ||
            IsUnityNull(gui) ||
            gui.m_container == null ||
            IsUnityNull(gui.m_container) ||
            !gui.m_container.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector2 mouse = GetUiMousePosition();
        InventoryGrid? containerGrid = gui.m_containerGrid;
        if (containerGrid?.m_gridRoot != null &&
            !IsUnityNull(containerGrid.m_gridRoot) &&
            containerGrid.m_gridRoot.gameObject.activeInHierarchy &&
            RectContainsScreenPoint(containerGrid.m_gridRoot, mouse))
        {
            return true;
        }

        return RectContainsScreenPoint(gui.m_container, mouse);
    }

    private static bool HasTooltipScrollRect(GameObject? tooltipObject)
    {
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return false;
        }

        ScrollRect? scrollRect = tooltipObject.GetComponentInChildren<ScrollRect>(includeInactive: false);
        if (scrollRect == null || IsUnityNull(scrollRect) || !scrollRect.gameObject.activeInHierarchy)
        {
            return false;
        }

        Scrollbar? scrollbar = scrollRect.verticalScrollbar;
        return scrollRect.enabled ||
               scrollbar != null && !IsUnityNull(scrollbar) && scrollbar.gameObject.activeInHierarchy;
    }

    private static void UpdateInventoryContainerHoverTooltip(UITooltip tooltip, bool resetScroll, bool handleWheel)
    {
        if (tooltip == null ||
            !IsInventoryContainerTooltipSource(tooltip) ||
            ShouldUseInventorySlotsOwnedHoverTooltip(tooltip))
        {
            HideInventoryContainerCustomTooltip();
            RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
            return;
        }

        ForceHideInventorySimpleNameTooltip();
        UpdateInventoryHoverCustomTooltip(
            tooltip,
            resetScroll,
            handleWheel,
            GetInventoryHoverCustomTooltipBackgroundAlpha(tooltip),
            hideVanillaTooltip: true);
    }

    private static void UpdateInventoryHoverCustomTooltip(UITooltip tooltip, bool resetScroll, bool handleWheel, float backgroundAlpha, bool hideVanillaTooltip)
    {
        GameObject? customTooltip = EnsureInventoryContainerCustomTooltip(tooltip);
        RectTransform? panel = _inventoryHoverTooltipPanel;
        if (customTooltip == null || panel == null || IsUnityNull(panel))
        {
            if (hideVanillaTooltip)
            {
                RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
            }

            return;
        }

        string topic = LocalizeTooltipText(GetInventoryContainerHoverTooltipTopic(tooltip));
        string body = LocalizeTooltipText(GetInventoryContainerHoverTooltipBody(tooltip));
        InventoryHoverTooltipItemSource? tooltipItemSource = GetInventoryContainerHoverTooltipItemSource(tooltip);
        ItemData? tooltipItem = tooltipItemSource?.Item;
        if (string.IsNullOrWhiteSpace(topic) && string.IsNullOrWhiteSpace(body))
        {
            HideInventoryContainerCustomTooltip();
            if (hideVanillaTooltip)
            {
                RestoreVanillaTooltipVisual(UITooltip.m_tooltip);
            }

            return;
        }

        bool suppressEpicLootArtifactsOnce =
            hideVanillaTooltip &&
            IsInventoryContainerTooltipSource(tooltip) &&
            (resetScroll ||
             _inventoryContainerHoverTooltipSource == null ||
             IsUnityNull(_inventoryContainerHoverTooltipSource) ||
             _inventoryContainerHoverTooltipSource != tooltip);

        if (hideVanillaTooltip)
        {
            HideVanillaTooltipVisual(UITooltip.m_tooltip);
        }

        customTooltip.SetActive(true);
        BeginInventoryContainerHoverTooltipOwnership(tooltip);
        if (suppressEpicLootArtifactsOnce)
        {
            SuppressEpicLootInventoryContainerTooltipArtifacts();
        }

        ApplyInventoryContainerCustomTooltipBackground(customTooltip.GetComponent<Image>(), backgroundAlpha);
        string itemSignature = GetInventoryHoverTooltipItemSignature(tooltipItem);
        bool textChanged = UpdateInventoryContainerCustomTooltipText(topic, body, resetScroll, itemSignature);
        bool extraChanged = UpdateInventoryHoverJewelcraftingTooltip(
            panel,
            tooltipItem,
            ShouldShowJewelcraftingInventoryInteract(tooltipItemSource?.Grid),
            itemSignature);
        LayoutInventoryContainerCustomTooltip(panel, resetScroll || textChanged || extraChanged);
        panel.position = ZInput.mousePosition;
        Utils.ClampUIToScreen(panel);
        panel.SetAsLastSibling();

        if (handleWheel)
        {
            HandleInventoryContainerHoverTooltipWheel();
        }
    }

    private static string GetInventoryContainerHoverTooltipTopic(UITooltip tooltip)
    {
        if (tooltip.TryGetComponent(out InventorySlotsTooltipDisplayData displayData) &&
            displayData.HasDisplayData &&
            !string.IsNullOrWhiteSpace(displayData.DisplayTopic))
        {
            return displayData.DisplayTopic;
        }

        if (!string.IsNullOrWhiteSpace(tooltip.m_topic))
        {
            return tooltip.m_topic;
        }

        TMP_Text? topic = FindTooltipTextByName(UITooltip.m_tooltip, "Topic");
        return topic != null && !IsUnityNull(topic) ? topic.text ?? "" : "";
    }

    private static string GetInventoryContainerHoverTooltipBody(UITooltip tooltip)
    {
        if (tooltip.TryGetComponent(out InventorySlotsTooltipDisplayData displayData) &&
            displayData.HasDisplayData)
        {
            return displayData.DisplayText;
        }

        if (!string.IsNullOrWhiteSpace(tooltip.m_text))
        {
            return tooltip.m_text;
        }

        TMP_Text? body = FindTooltipTextByName(UITooltip.m_tooltip, "Text");
        return body != null && !IsUnityNull(body) ? body.text ?? "" : "";
    }

    private static InventoryHoverTooltipItemSource? GetInventoryContainerHoverTooltipItemSource(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return null;
        }

        if (!InventoryHoverTooltipItems.TryGet(tooltip, out InventoryHoverTooltipItemSource source))
        {
            return null;
        }

        if (!IsInventoryHoverTooltipSourceValid(tooltip, source))
        {
            InventoryHoverTooltipItems.Remove(tooltip);
            return null;
        }

        return source;
    }

    private static void ClearInventoryHoverTooltipSources()
    {
        InventoryHoverTooltipItems.Clear();
    }

    private static void PruneInventoryHoverTooltipSources()
    {
        InventoryHoverTooltipItems.RemoveWhere((tooltip, source) => !IsInventoryHoverTooltipSourceValid(tooltip, source));
    }

    private static bool IsInventoryHoverTooltipSourceValid(UITooltip tooltip, InventoryHoverTooltipItemSource source)
    {
        return tooltip != null &&
               !IsUnityNull(tooltip) &&
               source.Item?.m_shared != null &&
               (source.Grid == null || !IsUnityNull(source.Grid));
    }

    private static string GetInventoryHoverTooltipItemSignature(ItemData? item) =>
        item?.m_shared == null ? "" : GetEquipmentSlotTooltipSignature(item);

    private static void ApplyInventoryHoverTooltipSourceFonts()
    {
        ApplyTooltipSourceFont(_inventoryHoverTooltipTopic, "Topic");
        ApplyTooltipSourceFont(_inventoryHoverTooltipText, "Text");
    }

    private static void ApplyTooltipSourceFont(TMP_Text? target, string sourceName)
    {
        if (target == null || IsUnityNull(target))
        {
            return;
        }

        try
        {
            TMP_Text? source = FindTooltipTextByName(UITooltip.m_tooltip, sourceName);
            if (source == null || IsUnityNull(source) || source == target)
            {
                ApplyDefaultFontAsset(target);
                return;
            }

            TMP_FontAsset? font = source.font;
            if (font == null || IsUnityNull(font))
            {
                ApplyDefaultFontAsset(target);
                return;
            }

            target.font = font;
            Material? material = source.fontSharedMaterial;
            if (material != null && !IsUnityNull(material))
            {
                target.fontSharedMaterial = material;
            }
        }
        catch (Exception)
        {
            ApplyDefaultFontAsset(target);
        }
    }

    private static bool UpdateInventoryHoverJewelcraftingTooltip(
        RectTransform panel,
        ItemData? item,
        bool showInteract,
        string itemSignature)
    {
        if (item?.m_shared == null)
        {
            return HideInventoryHoverJewelcraftingTooltip();
        }

        string signature = GetInventoryHoverJewelcraftingTooltipSignature(item, showInteract, itemSignature);
        if (_inventoryHoverJewelcraftingTooltipRoot != null &&
            !IsUnityNull(_inventoryHoverJewelcraftingTooltipRoot) &&
            _inventoryHoverJewelcraftingTooltipRoot.gameObject.activeSelf &&
            string.Equals(_inventoryHoverJewelcraftingTooltipSignature, signature, StringComparison.Ordinal))
        {
            JewelcraftingTooltipLayoutCache? cache = _inventoryHoverJewelcraftingTooltipRoot.GetComponent<JewelcraftingTooltipLayoutCache>();
            if (cache == null || !cache.Visible)
            {
                return false;
            }

            bool shouldRefresh = JewelcraftingTooltipCore.ShouldRefreshNativeTooltip(
                cache.Signature,
                signature,
                cache.Visible,
                cache.HasResolvedSocketGems,
                cache.RowlessRefreshAttempts);
            if (!shouldRefresh &&
                (!cache.HasResolvedSocketGems ||
                 HasNativeJewelcraftingTooltipRows(_inventoryHoverJewelcraftingTooltipRoot)))
            {
                return false;
            }
        }

        RectTransform? jewelcraftingRoot = _inventoryHoverJewelcraftingTooltipRoot;
        bool updated = UpdateJewelcraftingTooltip(panel, item, ref jewelcraftingRoot, showInteract, signature);
        _inventoryHoverJewelcraftingTooltipRoot = jewelcraftingRoot;
        if (updated)
        {
            _inventoryHoverJewelcraftingTooltipSignature = signature;
            ApplyInventoryHoverJewelcraftingSourceFonts();
            _inventoryHoverJewelcraftingTooltipLayoutRefreshFrames = 2;
            return true;
        }

        return HideInventoryHoverJewelcraftingTooltip();
    }

    private static string GetInventoryHoverJewelcraftingTooltipSignature(
        ItemData item,
        bool showInteract,
        string itemSignature)
    {
        return string.Join(
            "|",
            showInteract,
            IsJewelcraftingAdvancedTooltipPressed(),
            IsJewelcraftingProphecyTooltipPressed(),
            _uiLocalizationVersion,
            itemSignature,
            GetJewelcraftingOpenSocketInventorySignature(item));
    }

    private static bool HideInventoryHoverJewelcraftingTooltip()
    {
        bool changed = !string.IsNullOrEmpty(_inventoryHoverJewelcraftingTooltipSignature);
        if (_inventoryHoverJewelcraftingTooltipRoot != null && !IsUnityNull(_inventoryHoverJewelcraftingTooltipRoot))
        {
            changed |= _inventoryHoverJewelcraftingTooltipRoot.gameObject.activeSelf;
            _inventoryHoverJewelcraftingTooltipRoot.gameObject.SetActive(false);
        }

        _inventoryHoverJewelcraftingTooltipSignature = "";
        _inventoryHoverJewelcraftingTooltipLayoutRefreshFrames = 0;
        return changed;
    }

    private static void ApplyInventoryHoverJewelcraftingSourceFonts()
    {
        RectTransform? root = _inventoryHoverJewelcraftingTooltipRoot;
        if (root == null || IsUnityNull(root))
        {
            return;
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
        {
            ApplyTooltipSourceFont(text, "Text");
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            graphic.raycastTarget = false;
        }
    }

    private static float GetInventoryHoverTooltipScrollbarThresholdHeight() =>
        Mathf.Clamp(InventoryHoverTooltipScrollbarThresholdHeight, InventoryHoverTooltipMinBodyHeight, InventoryHoverTooltipMaxPanelHeight);

    private static TMP_Text? FindTooltipTextByName(GameObject? tooltipObject, string name)
    {
        if (tooltipObject == null || IsUnityNull(tooltipObject))
        {
            return null;
        }

        foreach (TMP_Text text in tooltipObject.GetComponentsInChildren<TMP_Text>(includeInactive: false))
        {
            if (text != null &&
                !IsUnityNull(text) &&
                string.Equals(text.gameObject.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return null;
    }

    private static GameObject? EnsureInventoryContainerCustomTooltip(UITooltip tooltip)
    {
        if (_inventoryHoverTooltip != null &&
            !IsUnityNull(_inventoryHoverTooltip) &&
            _inventoryHoverTooltipPanel != null &&
            !IsUnityNull(_inventoryHoverTooltipPanel))
        {
            return _inventoryHoverTooltip;
        }

        InventoryGui? gui = InventoryGui.instance;
        Canvas? canvas = gui != null ? gui.GetComponentInParent<Canvas>() : tooltip.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : tooltip.transform.root;

        _inventoryHoverTooltip = new GameObject("InventorySlots_InventoryHoverTooltip", typeof(RectTransform), typeof(Image));
        _inventoryHoverTooltip.transform.SetParent(parent, false);
        _inventoryHoverTooltip.SetActive(false);

        RectTransform panel = _inventoryHoverTooltip.GetComponent<RectTransform>();
        SetTopLeftRectLayout(panel, panel.anchoredPosition, panel.sizeDelta);
        ApplyInventoryContainerCustomTooltipBackground(_inventoryHoverTooltip.GetComponent<Image>(), GetInventoryContainerHoverTooltipBackgroundAlpha());

        CreateTextRect("Topic", panel, out TMP_Text topicText);
        topicText.alignment = TextAlignmentOptions.Center;
        topicText.fontSize = 22f;
        topicText.fontStyle = FontStyles.Bold;
        topicText.color = new Color(1f, 0.82f, 0.42f, 1f);
        topicText.textWrappingMode = TextWrappingModes.Normal;
        topicText.overflowMode = TextOverflowModes.Overflow;
        topicText.raycastTarget = false;

        CreateTextRect("Text", panel, out TMP_Text bodyText);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.fontSize = 18f;
        bodyText.color = Color.white;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.raycastTarget = false;
        EnsureInventoryHoverTooltipTextScrollContent(panel, bodyText);

        _inventoryHoverTooltipPanel = panel;
        _inventoryHoverTooltipTopic = topicText;
        _inventoryHoverTooltipText = bodyText;
        _inventoryHoverTooltipSignature = "";
        _inventoryHoverTooltipLayoutSignature = "";
        return _inventoryHoverTooltip;
    }

    private static bool UpdateInventoryContainerCustomTooltipText(string topic, string body, bool resetScroll, string extraSignature)
    {
        string signature = topic + "\n---\n" + body + "\n---\n" + extraSignature;
        if (!resetScroll && string.Equals(_inventoryHoverTooltipSignature, signature, StringComparison.Ordinal))
        {
            return false;
        }

        ApplyInventoryHoverTooltipSourceFonts();

        if (_inventoryHoverTooltipTopic != null && !IsUnityNull(_inventoryHoverTooltipTopic))
        {
            _inventoryHoverTooltipTopic.text = topic;
            _inventoryHoverTooltipTopic.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        }

        if (_inventoryHoverTooltipText != null && !IsUnityNull(_inventoryHoverTooltipText))
        {
            _inventoryHoverTooltipText.text = body;
            _inventoryHoverTooltipText.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        }

        _inventoryHoverTooltipScrollOffset = 0f;
        _inventoryHoverTooltipSignature = signature;
        _inventoryHoverTooltipLayoutSignature = "";
        return true;
    }

    private static void LayoutInventoryContainerCustomTooltip(RectTransform panel, bool force)
    {
        if (_inventoryHoverJewelcraftingTooltipLayoutRefreshFrames > 0)
        {
            force = true;
            _inventoryHoverJewelcraftingTooltipLayoutRefreshFrames--;
        }

        string layoutSignature = GetInventoryHoverTooltipLayoutSignature(panel);
        if (!force && string.Equals(_inventoryHoverTooltipLayoutSignature, layoutSignature, StringComparison.Ordinal))
        {
            ApplyInventoryHoverTooltipScrollPosition();
            return;
        }

        float width = InventoryHoverTooltipWidth;
        float padding = InventoryHoverTooltipPadding;
        float textWidth = Mathf.Max(40f, width - padding * 2f);
        float topicHeight = 0f;
        float bodyPreferredHeight = 0f;

        if (_inventoryHoverTooltipTopic != null && !IsUnityNull(_inventoryHoverTooltipTopic))
        {
            TMP_Text topic = _inventoryHoverTooltipTopic;
            topic.gameObject.SetActive(!string.IsNullOrWhiteSpace(topic.text));
            topic.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            topic.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
            topicHeight = topic.gameObject.activeSelf ? Mathf.Max(28f, topic.preferredHeight) : 0f;
        }

        if (_inventoryHoverTooltipText != null && !IsUnityNull(_inventoryHoverTooltipText))
        {
            TMP_Text body = _inventoryHoverTooltipText;
            EnsureInventoryHoverTooltipTextScrollContent(panel, body);
            body.gameObject.SetActive(!string.IsNullOrWhiteSpace(body.text));
            body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            body.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
            bodyPreferredHeight = body.gameObject.activeSelf ? GetInventoryHoverTooltipPreferredTextHeight(body, textWidth) : 0f;
        }

        float extraContentHeight = LayoutInventoryHoverTooltipExtraScrollContent(panel, textWidth, bodyPreferredHeight);
        float contentHeight = bodyPreferredHeight + extraContentHeight;
        float bodyTop = padding + topicHeight + (topicHeight > 0f && contentHeight > 0f ? InventoryHoverTooltipTopicGap : 0f);
        float screenBodyHeight = GetInventoryHoverTooltipMaxPanelHeight() - bodyTop - padding;
        float thresholdBodyHeight = GetInventoryHoverTooltipScrollbarThresholdHeight();
        float maxBodyHeight = Mathf.Max(
            InventoryHoverTooltipMinBodyHeight,
            Mathf.Min(screenBodyHeight, thresholdBodyHeight));
        float bodyHeight = contentHeight > 0f ? Mathf.Min(contentHeight, maxBodyHeight) : 0f;
        float panelHeight = bodyTop + bodyHeight + padding;

        SetTopLeftRectLayout(panel, panel.anchoredPosition, new Vector2(width, panelHeight));

        if (_inventoryHoverTooltipTopic != null && !IsUnityNull(_inventoryHoverTooltipTopic))
        {
            LayoutInventoryHoverTooltipTextRect(_inventoryHoverTooltipTopic.rectTransform, padding, padding, topicHeight);
        }

        if (_inventoryHoverTooltipText != null && !IsUnityNull(_inventoryHoverTooltipText))
        {
            LayoutInventoryHoverTooltipTextScroll(panel, textWidth, padding, bodyTop, bodyHeight, contentHeight);
        }

        _inventoryHoverTooltipLayoutSignature = GetInventoryHoverTooltipLayoutSignature(panel);
    }

    private static string GetInventoryHoverTooltipLayoutSignature(RectTransform panel)
    {
        return string.Join(
            "|",
            _inventoryHoverTooltipSignature,
            _inventoryHoverJewelcraftingTooltipSignature,
            Screen.width.ToString(),
            Screen.height.ToString(),
            panel.childCount.ToString(),
            GetInventoryHoverTooltipMaxPanelHeight().ToString("0.###"),
            GetInventoryHoverTooltipScrollbarThresholdHeight().ToString("0.###"));
    }

    private static float LayoutInventoryHoverTooltipExtraScrollContent(RectTransform panel, float textWidth, float textHeight)
    {
        RectTransform? root = _inventoryHoverJewelcraftingTooltipRoot;
        RectTransform? content = InventoryHoverTooltipTextScroll.Content;
        if (root == null ||
            IsUnityNull(root) ||
            !root.gameObject.activeSelf ||
            content == null ||
            IsUnityNull(content) ||
            !root.IsChildOf(panel))
        {
            return 0f;
        }

        if (root.parent != content)
        {
            root.SetParent(content, false);
        }

        SetTopLeftRectLayout(root, new Vector2(0f, -textHeight - InventoryPinnedJewelcraftingScrollGap), new Vector2(textWidth, Mathf.Max(1f, root.sizeDelta.y)));
        root.SetAsLastSibling();

        float height = LayoutJewelcraftingNativeTooltip(root, textWidth);
        root.sizeDelta = new Vector2(textWidth, height);
        return height > 0f ? height + InventoryPinnedJewelcraftingScrollGap : 0f;
    }

    private static RectTransform EnsureInventoryHoverTooltipTextScrollContent(RectTransform panel, TMP_Text text)
    {
        return ScrollableTooltipBody.Ensure(
            panel,
            text,
            InventoryHoverTooltipTextScroll,
            GetSolidUiSprite(),
            InventoryHoverTooltipScrollSensitivity,
            scrollRectEnabled: false,
            inertia: false,
            handleRaycastTarget: false,
            scrollbarRaycastTarget: false);
    }

    private static void LayoutInventoryHoverTooltipTextScroll(RectTransform panel, float textWidth, float padding, float top, float viewportHeight, float contentHeight)
    {
        if (_inventoryHoverTooltipText == null ||
            IsUnityNull(_inventoryHoverTooltipText))
        {
            return;
        }

        EnsureInventoryHoverTooltipTextScrollContent(panel, _inventoryHoverTooltipText);
        ScrollableTooltipBodyLayoutResult result = ScrollableTooltipBody.LayoutPixelScroll(
            InventoryHoverTooltipTextScroll,
            _inventoryHoverTooltipText,
            textWidth,
            padding,
            top,
            viewportHeight,
            contentHeight,
            _inventoryHoverTooltipScrollOffset,
            InventoryHoverTooltipScrollbarOutsideOffset,
            InventoryHoverTooltipScrollbarWidth,
            enableScrollRectWhenNeeded: false);

        _inventoryHoverTooltipScrollOffset = result.ScrollOffset;
        _inventoryHoverTooltipMaxScroll = result.MaxScroll;
        _inventoryHoverTooltipNeedsScroll = result.NeedsScroll;
    }

    private static void HandleInventoryContainerHoverTooltipWheel()
    {
        if (!_inventoryHoverTooltipNeedsScroll)
        {
            return;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Continuous);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return;
        }

        _inventoryHoverTooltipScrollOffset = Mathf.Clamp(
            _inventoryHoverTooltipScrollOffset - wheel * InventoryHoverTooltipScrollSensitivity,
            0f,
            _inventoryHoverTooltipMaxScroll);
        ApplyInventoryHoverTooltipScrollPosition();
    }

    private static void ApplyInventoryHoverTooltipScrollPosition()
    {
        ScrollableTooltipBody.ApplyPixelScrollPosition(
            InventoryHoverTooltipTextScroll,
            _inventoryHoverTooltipScrollOffset,
            _inventoryHoverTooltipMaxScroll);
    }

    private static void HideInventoryContainerCustomTooltipVisual()
    {
        if (_inventoryHoverTooltip != null && !IsUnityNull(_inventoryHoverTooltip))
        {
            _inventoryHoverTooltip.SetActive(false);
        }
    }

    private static void HideInventoryContainerCustomTooltip()
    {
        HideInventoryContainerCustomTooltipVisual();
        HideInventoryHoverJewelcraftingTooltip();

        _inventoryHoverTooltipNeedsScroll = false;
        _inventoryHoverTooltipMaxScroll = 0f;
        _inventoryHoverTooltipScrollOffset = 0f;
        _inventoryHoverTooltipSignature = "";
        _inventoryHoverTooltipLayoutSignature = "";
    }

    private static bool IsSimpleInventorySlotsNameTooltip(UITooltip tooltip)
    {
        if (tooltip == null || IsUnityNull(tooltip))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(LocalizeTooltipText(GetInventoryContainerHoverTooltipTopic(tooltip))) &&
               string.IsNullOrWhiteSpace(LocalizeTooltipText(GetInventoryContainerHoverTooltipBody(tooltip)));
    }

    private static void UpdateInventorySimpleNameTooltip(UITooltip tooltip)
    {
        string text = LocalizeTooltipText(GetInventoryContainerHoverTooltipTopic(tooltip));
        UpdateSimpleNameTooltipText(tooltip, text);
    }

    internal static void ShowPinnedGemNameTooltip(object owner, string displayName)
    {
        _pinnedGemNameTooltipOwner = owner;
        _pinnedGemNameTooltipHoldUntil = Time.unscaledTime + 1f;
        ShowSimpleNameTooltip(owner, displayName, "", SimpleNameTooltipPlacement.LeftOfCursor);
    }

    internal static void HidePinnedGemNameTooltip(object owner)
    {
        if (ReferenceEquals(_pinnedGemNameTooltipOwner, owner))
        {
            _pinnedGemNameTooltipOwner = null;
            _pinnedGemNameTooltipHoldUntil = -1000f;
        }

        HideSimpleNameTooltip(owner);
    }

    internal static void ShowSimpleNameTooltip(string topic, string text = "")
    {
        ShowSimpleNameTooltip(InventorySimpleNameTooltipFallbackOwner, topic, text);
    }

    internal static void ShowSimpleNameTooltip(object owner, string topic, string text = "")
    {
        ShowSimpleNameTooltip(owner, topic, text, SimpleNameTooltipPlacement.RightOfCursor);
    }

    private static void ShowSimpleNameTooltip(object owner, string topic, string text, SimpleNameTooltipPlacement placement)
    {
        string localizedTopic = LocalizeTooltipText(topic);
        string localizedText = LocalizeTooltipText(text);
        string displayText = string.IsNullOrWhiteSpace(localizedText)
            ? localizedTopic
            : $"{localizedTopic}\n{localizedText}";
        UpdateSimpleNameTooltipText(owner, displayText, placement);
    }

    internal static void HideSimpleNameTooltip()
    {
        HideSimpleNameTooltip(InventorySimpleNameTooltipFallbackOwner);
    }

    internal static void HideSimpleNameTooltip(object owner)
    {
        HideInventorySimpleNameTooltip(owner);
    }

    private static void UpdateSimpleNameTooltipText(object owner, string text)
    {
        UpdateSimpleNameTooltipText(owner, text, SimpleNameTooltipPlacement.RightOfCursor);
    }

    private static void UpdateSimpleNameTooltipText(object owner, string text, SimpleNameTooltipPlacement placement)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            HideInventorySimpleNameTooltip(owner);
            return;
        }

        GameObject? simpleTooltip = EnsureInventorySimpleNameTooltip();
        RectTransform? rect = _inventorySimpleNameTooltipRect;
        TMP_Text? label = _inventorySimpleNameTooltipText;
        if (simpleTooltip == null || rect == null || IsUnityNull(rect) || label == null || IsUnityNull(label))
        {
            return;
        }

        bool contentChanged =
            !InventorySimpleNameTooltipOwner.Visible ||
            !ReferenceEquals(InventorySimpleNameTooltipOwner.Owner, owner) ||
            !string.Equals(_inventorySimpleNameTooltipValue, text, StringComparison.Ordinal) ||
            _inventorySimpleNameTooltipPlacement != placement;

        InventorySimpleNameTooltipOwner.Show(owner);
        simpleTooltip.SetActive(true);

        if (contentChanged)
        {
            label.text = text;
            label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            Vector2 preferred = label.GetPreferredValues(text, 320f, 0f);
            float width = Mathf.Clamp(Mathf.Ceil(preferred.x) + 8f, 24f, 340f);
            float height = Mathf.Clamp(Mathf.Ceil(preferred.y) + 4f, 18f, 96f);
            rect.sizeDelta = new Vector2(width, height);
            _inventorySimpleNameTooltipValue = text;
            _inventorySimpleNameTooltipPlacement = placement;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.position = GetSimpleNameTooltipPosition(rect.sizeDelta.x, placement);
        Utils.ClampUIToScreen(rect);
        rect.SetAsLastSibling();

        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private static Vector3 GetSimpleNameTooltipPosition(float width, SimpleNameTooltipPlacement placement)
    {
        return placement == SimpleNameTooltipPlacement.LeftOfCursor
            ? ZInput.mousePosition + new Vector3(-width - 14f, 16f, 0f)
            : ZInput.mousePosition + new Vector3(14f, 16f, 0f);
    }

    private static GameObject? EnsureInventorySimpleNameTooltip(UITooltip? tooltip = null)
    {
        if (_inventorySimpleNameTooltip != null &&
            !IsUnityNull(_inventorySimpleNameTooltip) &&
            _inventorySimpleNameTooltipRect != null &&
            !IsUnityNull(_inventorySimpleNameTooltipRect) &&
            _inventorySimpleNameTooltipText != null &&
            !IsUnityNull(_inventorySimpleNameTooltipText))
        {
            return _inventorySimpleNameTooltip;
        }

        InventoryGui? gui = InventoryGui.instance;
        Canvas? canvas = gui != null ? gui.GetComponentInParent<Canvas>() : tooltip != null ? tooltip.GetComponentInParent<Canvas>() : null;
        Transform? fallbackParent = gui != null && !IsUnityNull(gui) ? gui.transform.root : tooltip != null && !IsUnityNull(tooltip) ? tooltip.transform.root : null;
        Transform? parent = canvas != null ? canvas.transform : fallbackParent;
        if (parent == null)
        {
            return null;
        }

        _inventorySimpleNameTooltip = new GameObject("InventorySlots_SimpleNameTooltip", typeof(RectTransform));
        _inventorySimpleNameTooltip.transform.SetParent(parent, false);
        _inventorySimpleNameTooltip.SetActive(false);

        RectTransform rect = _inventorySimpleNameTooltip.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        CreateTextRect("Text", rect, out TMP_Text label);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 16f;
        label.color = Color.white;
        label.outlineColor = new Color32(0, 0, 0, 230);
        label.outlineWidth = 0.18f;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        _inventorySimpleNameTooltipRect = rect;
        _inventorySimpleNameTooltipText = label;
        return _inventorySimpleNameTooltip;
    }

    private static void HideInventorySimpleNameTooltip(object owner)
    {
        if (!InventorySimpleNameTooltipOwner.Hide(owner))
        {
            return;
        }

        if (_inventorySimpleNameTooltip != null && !IsUnityNull(_inventorySimpleNameTooltip))
        {
            _inventorySimpleNameTooltip.SetActive(false);
        }

        _inventorySimpleNameTooltipValue = "";
    }

    private static void ForceHideInventorySimpleNameTooltip()
    {
        if (ShouldPreservePinnedGemNameTooltip())
        {
            return;
        }

        InventorySimpleNameTooltipOwner.ForceHide();
        if (_inventorySimpleNameTooltip != null && !IsUnityNull(_inventorySimpleNameTooltip))
        {
            _inventorySimpleNameTooltip.SetActive(false);
        }

        _inventorySimpleNameTooltipValue = "";
    }

    private static bool ShouldPreservePinnedGemNameTooltip()
    {
        return _pinnedGemNameTooltipOwner != null &&
               Time.unscaledTime <= _pinnedGemNameTooltipHoldUntil &&
               InventorySimpleNameTooltipOwner.Visible &&
               ReferenceEquals(InventorySimpleNameTooltipOwner.Owner, _pinnedGemNameTooltipOwner);
    }

    private static void ApplyInventoryContainerCustomTooltipBackground(Image? background, float alpha)
    {
        if (background == null || IsUnityNull(background))
        {
            return;
        }

        background.sprite = GetSolidUiSprite();
        background.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        background.raycastTarget = false;
    }

    private static void LayoutInventoryHoverTooltipTextRect(RectTransform rect, float horizontalPadding, float top, float height)
    {
        SetRectLayout(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(-horizontalPadding * 2f, height));
    }

    private static string LocalizeTooltipText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return Localization.instance != null ? Localization.instance.Localize(text) : text;
    }

    private static float GetInventoryHoverTooltipPreferredTextHeight(TMP_Text text, float textWidth)
    {
        Vector2 preferred = text.GetPreferredValues(text.text ?? "", textWidth, 0f);
        if (float.IsNaN(preferred.y) || float.IsInfinity(preferred.y) || preferred.y < 1f)
        {
            return Mathf.Max(1f, text.preferredHeight);
        }

        return Mathf.Max(1f, preferred.y);
    }

    private static float GetInventoryHoverTooltipMaxPanelHeight()
    {
        float screenHeight = Screen.height > 0 ? Screen.height : InventoryHoverTooltipMaxPanelHeight;
        return Mathf.Clamp(screenHeight * 0.62f, 260f, InventoryHoverTooltipMaxPanelHeight);
    }
}
