using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const float CraftingListWidth = 196f;
    private const float CraftingListScrollbarWidth = 8f;
    private const float CraftingListScrollbarGap = 2f;
    private const float CraftingListDetailGap = CraftingListScrollbarWidth + CraftingListScrollbarGap * 2f;
    private static bool _craftingListViewActive;
    private static int _craftingListRevealedSelection = int.MinValue;
    private static RectTransform? _craftingViewButton;
    private static TMP_Text? _craftingViewButtonLabel;
    private static int _craftingViewButtonLocalizationVersion = -1;
    private static bool _craftingViewButtonShowsList;
    private static string _craftingListStatusText = "";
    private static CraftingListDetailState _craftingListDetails = new();

    private sealed class CraftingListDetailState
    {
        public RectTransform? Root;
        public Image? Icon;
        public TMP_Text? Title;
        public TMP_Text? Body;
        public RectTransform? GemRow;
        public readonly List<JewelcraftingGemIconData> GemIcons = new();
        public string GemSignature = "";
        public RectTransform? Style;
        public TMP_Text? StyleLabel;
        public CraftingRecipeStyleButtonMarker? StyleMarker;
        public readonly ScrollableTooltipBodyState Scroll = new();
        public CraftingTabAdapterKind Tab;
        public Recipe? Recipe;
        public ItemDrop.ItemData? Item;
        public int Variant = -1;
        public bool CanCraft;
        public int LocalizationVersion = -1;
        public float NextRefresh;
        public string BodyText = "";
        public string TitleText = "";
        public string StatusText = "";
    }

    private static void UpdateCraftingViewMode(CraftingTabAdapterState adapter, bool visible)
    {
        bool list = visible && CraftingViewCore.UseList(_craftingViewMode?.Value ?? CraftingViewMode.List, adapter.Kind);
        if (_craftingListViewActive == list) return;
        _craftingListViewActive = list;
        _craftingListRevealedSelection = int.MinValue;
        _craftingListDetails.NextRefresh = 0f;
        _craftingRecipePage = 0;
        CraftingController.ClearHoveredRecipeAndRequestMouseSync();
        HideCraftingTooltipRecipeOverlay();
        CraftingController.MarkRecipeGridLayoutDirty();
        ResetCraftingFrameFastPathStamp();
        // Do not use HideCraftingPanelRedesign: it clears the crafting queue.
        if (!list && _craftingListDetails.Root != null) _craftingListDetails.Root.gameObject.SetActive(false);
    }

    private static int GetCraftingRecipePageStart() => CraftingViewCore.PageStart(
        _craftingListViewActive, _craftingRecipePage, GetCraftingRecipeGridCapacity());

    private static void UpdateCraftingViewControls(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter)
    {
        if (!adapter.IsRedesign)
        {
            HideCraftingListViewUi();
            return;
        }

        if (_craftingViewButton == null || _craftingViewButton.parent != gui.m_crafting)
        {
            if (_craftingViewButton != null) UnityEngine.Object.Destroy(_craftingViewButton.gameObject);
            _craftingViewButton = new GameObject("InventorySlots_CraftingViewButton", typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(UITooltip)).GetComponent<RectTransform>();
            _craftingViewButton.SetParent(gui.m_crafting, false);
            Image image = _craftingViewButton.GetComponent<Image>();
            ApplyVanillaButtonImage(gui.m_variantButton, image);
            image.color = new Color(0.05f, 0.035f, 0.025f, 0.92f);
            image.raycastTarget = true;
            CreateTextRect("Label", _craftingViewButton, out TMP_Text label);
            _craftingViewButtonLabel = label;
            label.fontSize = 15f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.86f, 0.52f, 0.95f);
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            _craftingViewButtonLocalizationVersion = -1;
            _craftingViewButton.GetComponent<UIInputHandler>().m_onLeftClick += _ =>
            {
                if (_craftingViewMode != null)
                    _craftingViewMode.Value = _craftingViewMode.Value == CraftingViewMode.Grid ? CraftingViewMode.List : CraftingViewMode.Grid;
            };
        }

        _craftingViewButton.gameObject.SetActive(true);
        if (CraftingUi.SearchInputRect != null)
        {
            const float buttonWidth = 62f;
            RectTransform search = CraftingUi.SearchInputRect;
            SetTopLeftRectLayout(gui.m_crafting, _craftingViewButton,
                search.anchoredPosition - new Vector2(buttonWidth + CraftingSortModeButtonGap, 0f),
                new Vector2(buttonWidth, search.sizeDelta.y));
        }
        if (_craftingViewButtonLocalizationVersion != _uiLocalizationVersion || _craftingViewButtonShowsList != _craftingListViewActive)
        {
            _craftingViewButtonLabel!.text = _craftingListViewActive ? LocalizeUi("$inventoryslots_view_list", "List") : LocalizeUi("$inventoryslots_view_grid", "Grid");
            ApplyDefaultFontAsset(_craftingViewButtonLabel);
            ConfigureSimpleTooltip(_craftingViewButton.gameObject, LocalizeUi("$inventoryslots_view_toggle", "Switch between grid and list view"), true);
            _craftingViewButtonLocalizationVersion = _uiLocalizationVersion;
            _craftingViewButtonShowsList = _craftingListViewActive;
        }
        UpdateCraftingListDetails(gui, grid, adapter);
    }

    private static void ConfigureCraftingListRow(CraftingRecipeGridCell cell, InventoryGui.RecipeDataPair pair, float rowHeight, bool actionAvailable)
    {
        if (cell.Name == null)
            cell.Name = CreateTextRect("Name", cell.Rect).GetComponent<TMP_Text>();
        TMP_Text name = cell.Name;
        ApplyDefaultFontAsset(name);
        name.gameObject.SetActive(true);
        name.text = GetCraftingRecipeDisplayName(pair);
        if (pair.ItemData != null && !IsVeiledRecipeMasked(pair)) name.text += $" <color=#ffcc66>{pair.ItemData.m_quality}</color>";
        name.fontSize = 14f;
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.textWrappingMode = TextWrappingModes.Normal;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.color = actionAvailable ? Color.white : new Color(0.75f, 0.75f, 0.75f);
        name.raycastTarget = false;
        SetTopLeftRectLayout(cell.Rect, name.rectTransform, new Vector2(rowHeight + 2f, 0f), new Vector2(CraftingListWidth - rowHeight - 8f, rowHeight));
        if (cell.Icon != null)
            SetTopLeftRectLayout(cell.Rect, cell.Icon.rectTransform, new Vector2(3f, -3f), new Vector2(rowHeight - 6f, rowHeight - 6f));
        if (cell.Amount != null) cell.Amount.gameObject.SetActive(false);
        if (cell.Quality != null) cell.Quality.gameObject.SetActive(false);
        if (cell.Food != null) cell.Food.gameObject.SetActive(false);
        Transform? style = cell.Rect.Find(CraftingRecipeStyleButtonName);
        if (style != null) style.gameObject.SetActive(false); // Style selection lives in the detail header.
    }

    private static void EnsureCraftingListDetails(InventoryGui gui)
    {
        if (_craftingListDetails.Root != null && _craftingListDetails.Root.parent == gui.m_crafting) return;
        if (_craftingListDetails.Root != null) UnityEngine.Object.Destroy(_craftingListDetails.Root.gameObject);
        _craftingListDetails = new CraftingListDetailState();
        CraftingListDetailState state = _craftingListDetails;
        state.Root = new GameObject("InventorySlots_CraftingListDetails", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        state.Root.SetParent(gui.m_crafting, false);
        Image background = state.Root.GetComponent<Image>();
        background.sprite = GetSolidUiSprite();
        background.color = new Color(0.055f, 0.035f, 0.025f, 0.42f);
        background.raycastTarget = true;
        state.Icon = CreateTopLeftImageChild("Icon", state.Root, Color.white, true).GetComponent<Image>();
        state.Icon.raycastTarget = false;
        state.Title = CreateTextRect("Name", state.Root).GetComponent<TMP_Text>();
        state.Body = CreateTextRect("Description", state.Root).GetComponent<TMP_Text>();
        state.Title.color = new Color(1f, 0.82f, 0.36f);
        state.Title.fontSize = 23f;
        state.Title.textWrappingMode = TextWrappingModes.Normal;
        state.Title.overflowMode = TextOverflowModes.Ellipsis;
        state.Title.alignment = TextAlignmentOptions.MidlineLeft;
        state.Body.fontSize = 16f;
        state.Body.color = Color.white;
        state.Body.alignment = TextAlignmentOptions.TopLeft;
        state.Title.raycastTarget = state.Body.raycastTarget = false;
        ScrollableTooltipBody.Ensure(state.Root, state.Body, state.Scroll, GetSolidUiSprite(), 30f,
            scrollRectEnabled: true, inertia: false, handleRaycastTarget: true, scrollbarRaycastTarget: true);
        // Passive tooltip bodies ignore pointer input. This interactive viewport
        // must receive raycasts so wheel/drag events reach its parent ScrollRect.
        state.Scroll.Viewport!.GetComponent<Image>().raycastTarget = true;
        state.Style = new GameObject("Style", typeof(RectTransform), typeof(Image), typeof(UIInputHandler), typeof(CraftingRecipeStyleButtonMarker)).GetComponent<RectTransform>();
        state.Style.SetParent(state.Root, false);
        ApplyVanillaButtonImage(gui.m_variantButton, state.Style.GetComponent<Image>());
        CreateTextRect("Label", state.Style, out TMP_Text styleLabel);
        state.StyleLabel = styleLabel;
        state.StyleMarker = state.Style.GetComponent<CraftingRecipeStyleButtonMarker>();
        styleLabel.fontSize = 13f;
        styleLabel.alignment = TextAlignmentOptions.Center;
        styleLabel.raycastTarget = false;
        SetTopLeftRectLayout(state.Style, styleLabel.rectTransform, Vector2.zero, new Vector2(52f, 22f));
        state.Style.GetComponent<UIInputHandler>().m_onLeftClick += input => ShowCraftingRecipeStyleDialog(input.GetComponent<CraftingRecipeStyleButtonMarker>());
    }

    private static void UpdateCraftingListDetails(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter)
    {
        if (!_craftingListViewActive)
        {
            if (_craftingListDetails.Root != null) _craftingListDetails.Root.gameObject.SetActive(false);
            return;
        }
        EnsureCraftingListDetails(gui);
        CraftingListDetailState state = _craftingListDetails;
        RectTransform root = state.Root!;
        float width = GetCraftingRecipeIconAreaSize() - CraftingListWidth - CraftingListDetailGap;
        float height = GetCraftingRecipeIconAreaSize();
        SetTopLeftRectLayout(gui.m_crafting, root, grid.anchoredPosition + new Vector2(CraftingListWidth + CraftingListDetailGap, 0f), new Vector2(width, height));
        root.gameObject.SetActive(true);
        // Stay alongside the recipe grid; modal/variant and pinned panels keep
        // their established layering rather than being pushed behind this panel.
        if (root.GetSiblingIndex() < grid.GetSiblingIndex()) root.SetSiblingIndex(grid.GetSiblingIndex());
        state.Scroll.ScrollRect!.scrollSensitivity = 30f * GetMouseUiScrollMultiplier();
        int index = GetSelectedCraftingRecipeIndexSafe(gui, acceptOneLevelHigher: false);
        bool hasRecipe = FindCraftingRecipeViewIndex(index) >= 0 && TryGetCraftingRecipePair(gui, index, out _);
        if (!hasRecipe)
        {
            state.Title!.text = LocalizeUi("$inventoryslots_view_no_recipe", "No matching recipes");
            state.Icon!.gameObject.SetActive(false);
            state.Style!.gameObject.SetActive(false);
            state.Scroll.ScrollView!.gameObject.SetActive(false);
            state.Scroll.Scrollbar!.gameObject.SetActive(false);
            HideCraftingGemIconRow(ref state.GemRow);
            state.GemIcons.Clear();
            state.GemSignature = "";
            SetTopLeftRectLayout(root, state.Title.rectTransform, new Vector2(12f, -12f), new Vector2(width - 24f, 60f));
            state.Recipe = null;
            state.NextRefresh = 0f;
            return;
        }
        TryGetCraftingRecipePair(gui, index, out InventoryGui.RecipeDataPair pair);
        int variant = GetCraftingRecipeVariant(pair);
        bool selectionChanged = state.Tab != adapter.Kind || state.Recipe != pair.Recipe ||
            !ReferenceEquals(state.Item, pair.ItemData) || state.Variant != variant;
        bool localizationChanged = state.LocalizationVersion != _uiLocalizationVersion;
        bool statusChanged = state.StatusText != _craftingListStatusText;
        if (!selectionChanged && !localizationChanged && !statusChanged && state.CanCraft == pair.CanCraft && Time.unscaledTime < state.NextRefresh) return;
        state.NextRefresh = Time.unscaledTime + 0.25f;
        state.Tab = adapter.Kind;
        state.LocalizationVersion = _uiLocalizationVersion;
        state.Recipe = pair.Recipe;
        state.Item = pair.ItemData;
        state.Variant = variant;
        state.CanCraft = pair.CanCraft;
        state.StatusText = _craftingListStatusText;
        string title = GetCraftingRecipeDisplayName(pair);
        string body = GetCraftingRecipeTooltip(pair);
        if (!string.IsNullOrWhiteSpace(state.StatusText))
        {
            // Keep action warnings inside the scrollable description, ahead of
            // item stats, without imposing the bottom HUD's fixed-height limit.
            body = $"<color=#FFB847>{state.StatusText}</color>\n\n{body}";
        }
        ItemDrop.ItemData? gemItem = HasJewelcraftingActive && !IsVeiledRecipeMasked(pair)
            ? GetCraftingJewelcraftingTooltipItem(pair) : null;
        string gemSignature = GetCraftingHoverGemIconSignature(gemItem);
        bool gemsChanged = selectionChanged || state.GemSignature != gemSignature;
        if (gemsChanged)
        {
            state.GemIcons.Clear();
            if (gemItem?.m_shared != null) state.GemIcons.AddRange(GetJewelcraftingGemIconData(gemItem));
            state.GemSignature = gemSignature;
        }
        bool layoutChanged = selectionChanged || localizationChanged || gemsChanged || state.TitleText != title || state.BodyText != body;
        state.TitleText = title;
        state.BodyText = body;
        state.Title!.text = title;
        state.Body!.text = body;
        ApplyDefaultFontAsset(state.Title);
        ApplyDefaultFontAsset(state.Body);
        ConfigureCraftingRecipeCellIcon(state.Icon, pair, 60f);
        SetTopLeftRectLayout(root, state.Icon!.rectTransform, new Vector2(8f, -8f), new Vector2(52f, 52f));
        SetTopLeftRectLayout(root, state.Title.rectTransform, new Vector2(68f, -6f), new Vector2(width - 80f, 62f));
        bool styleVisible = pair.ItemData == null && !IsVeiledRecipeMasked(pair) && GetCraftingRecipeVariantCount(pair) > 1;
        state.Style!.gameObject.SetActive(styleVisible);
        SetTopLeftRectLayout(root, state.Style, new Vector2(8f, -66f), new Vector2(52f, 22f));
        state.StyleLabel!.text = LocalizeUi("$inventoryslots_style", "Style");
        CraftingRecipeStyleButtonMarker marker = state.StyleMarker!;
        marker.Gui = gui;
        marker.Index = index;
        if (!layoutChanged) return;
        float top = styleVisible ? 96f : 76f;
        const float gemIconSize = 24f;
        if (UpdateCraftingGemIconRow(root, state.GemIcons, ref state.GemRow, Vector2.zero,
                gemIconSize, 5f, enableIconTooltips: false))
        {
            RectTransform row = state.GemRow!;
            SetTopLeftRectLayout(root, row, new Vector2(12f, -top), row.sizeDelta);
            // The shared tooltip row offsets icons above its baseline. In this
            // header, center them inside the row so the body starts below it.
            for (int i = 0; i < state.GemIcons.Count; i++)
            {
                RectTransform icon = (RectTransform)row.GetChild(i);
                icon.anchoredPosition = new Vector2(icon.anchoredPosition.x, 0f);
            }
            top += gemIconSize + 8f;
        }
        float textWidth = width - 28f;
        float contentHeight = Mathf.Max(1f, state.Body.GetPreferredValues(body, textWidth, 10000f).y);
        float offset = selectionChanged || statusChanged ? 0f : state.Scroll.Content!.anchoredPosition.y;
        state.Scroll.ScrollView!.gameObject.SetActive(true);
        ScrollableTooltipBody.LayoutPixelScroll(state.Scroll, state.Body, textWidth, 12f, top, height - top - 12f,
            contentHeight, offset, -8f, 4f, enableScrollRectWhenNeeded: true);
    }

    private static bool IsMouseOverCraftingListDetails() => _craftingListViewActive &&
        _craftingListDetails.Root != null && _craftingListDetails.Root.gameObject.activeInHierarchy &&
        RectContainsScreenPoint(_craftingListDetails.Root, GetUiMousePosition());

    private static void HandleCraftingListDetailGamepadScroll()
    {
        // Native ScrollRect handles the mouse. Reuse the mod's controller input
        // only when it is the active source, so the same event is not applied twice.
        if (Mathf.Abs(Input.mouseScrollDelta.y) >= 0.01f || !IsGamepadUiScrollActive()) return;
        ScrollableTooltipBodyState scroll = _craftingListDetails.Scroll;
        if (scroll.Content == null || scroll.Viewport == null) return;
        float delta = GetUiScrollDelta(UiScrollInputMode.Continuous) * 30f;
        float maxScroll = Mathf.Max(0f, scroll.Content.rect.height - scroll.Viewport.rect.height);
        float offset = Mathf.Clamp(scroll.Content.anchoredPosition.y - delta, 0f, maxScroll);
        ScrollableTooltipBody.ApplyPixelScrollPosition(scroll, offset, maxScroll);
    }

    private static void HideCraftingListViewUi()
    {
        _craftingListStatusText = "";
        if (_craftingViewButton != null) _craftingViewButton.gameObject.SetActive(false);
        if (_craftingListDetails.Root != null) _craftingListDetails.Root.gameObject.SetActive(false);
    }

    internal static void DestroyCraftingListViewUi(InventoryGui? gui = null)
    {
        if (gui != null &&
            (_craftingViewButton == null || _craftingViewButton.parent != gui.m_crafting) &&
            (_craftingListDetails.Root == null || _craftingListDetails.Root.parent != gui.m_crafting)) return;
        HideCraftingListViewUi();
        if (_craftingViewButton != null) UnityEngine.Object.Destroy(_craftingViewButton.gameObject);
        if (_craftingListDetails.Root != null) UnityEngine.Object.Destroy(_craftingListDetails.Root.gameObject);
        _craftingViewButton = null;
        _craftingViewButtonLabel = null;
        _craftingViewButtonLocalizationVersion = -1;
        _craftingListDetails = new CraftingListDetailState();
        _craftingListViewActive = false;
        _craftingListRevealedSelection = int.MinValue;
    }
}
