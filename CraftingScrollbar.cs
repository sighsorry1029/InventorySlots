using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

internal sealed class CraftingRecipeScrollbarMarker : MonoBehaviour
{
    public bool Initialized { get; set; }
}

public sealed partial class InventorySlotsPlugin
{
    private static void UpdateCraftingRecipeScrollbar(InventoryGui gui, RectTransform grid)
    {
        RectTransform? rect = EnsureCraftingRecipeScrollbar(gui);
        if (rect == null || CraftingScrollbar.RecipeScrollbarComponent == null)
        {
            return;
        }

        EnsureCraftingVanillaRecipeScrollbarsHidden(gui);

        int pageCount = GetCraftingRecipePageCount(gui);
        bool visible = pageCount > 1;
        CraftingRecipeScrollbarStamp stamp = new(pageCount, _craftingRecipePage, visible, grid.anchoredPosition.x, grid.anchoredPosition.y);
        if (CraftingController.CanReuseRecipeScrollbar(stamp))
        {
            return;
        }

        rect.gameObject.SetActive(visible);
        if (!visible)
        {
            CraftingController.StoreRecipeScrollbarStamp(stamp);
            return;
        }

        Scrollbar scrollbar = CraftingScrollbar.RecipeScrollbarComponent;
        CraftingRecipeScrollbarMarker marker = rect.GetComponent<CraftingRecipeScrollbarMarker>() ?? rect.gameObject.AddComponent<CraftingRecipeScrollbarMarker>();
        if (!marker.Initialized)
        {
            scrollbar.onValueChanged.AddListener(OnCraftingRecipeScrollbarChanged);
            marker.Initialized = true;
        }

        SetCraftingTopLeftRect(gui.m_crafting, rect, GetCraftingRecipeScrollbarPosition(gui, grid), new Vector2(16f, CraftingRecipeIconRows * CraftingRecipeGridCellSpace - 6f));
        SetCraftingRecipeScrollbarGraphicsVisible(rect, visible: true);
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = Mathf.Clamp01(1f / pageCount);

        CraftingScrollbar.UpdatingRecipeScrollbar = true;
        scrollbar.value = pageCount <= 1 ? 1f : 1f - _craftingRecipePage / (float)(pageCount - 1);
        CraftingScrollbar.UpdatingRecipeScrollbar = false;
        CraftingController.StoreRecipeScrollbarStamp(stamp);
    }

    private static void EnsureCraftingVanillaRecipeScrollbarsHidden(InventoryGui gui)
    {
        if (_craftingVanillaRecipeScrollbarsHidden)
        {
            return;
        }

        SetCraftingVanillaRecipeScrollbarsVisible(gui, visible: false);
        _craftingVanillaRecipeScrollbarsHidden = true;
    }

    private static Vector2 GetCraftingRecipeScrollbarPosition(InventoryGui gui, RectTransform grid)
    {
        Vector2 offset = CraftingRecipeScrollbarFixedOffset;
        float panelRight = GetRectWidth(gui.m_crafting);
        if (panelRight <= 1f)
        {
            panelRight = grid.anchoredPosition.x + CraftingRecipeGridColumns * CraftingRecipeGridCellSpace;
        }

        return new Vector2(panelRight + offset.x, grid.anchoredPosition.y + offset.y);
    }

    private static RectTransform? EnsureCraftingRecipeScrollbar(InventoryGui gui)
    {
        if (gui.m_crafting == null)
        {
            return null;
        }

        if (_craftingRecipeScrollbar != null && !IsUnityNull(_craftingRecipeScrollbar) && _craftingRecipeScrollbar!.parent == gui.m_crafting)
        {
            CraftingScrollbar.RecipeScrollbarComponent = _craftingRecipeScrollbar.GetComponent<Scrollbar>();
            return _craftingRecipeScrollbar;
        }

        Transform? existing = gui.m_crafting.Find(CraftingRecipeScrollbarName);
        _craftingRecipeScrollbar = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (_craftingRecipeScrollbar == null)
        {
            _craftingRecipeScrollbar = CreateCraftingRecipeScrollbar(gui);
        }

        _craftingRecipeScrollbar.SetParent(gui.m_crafting, false);
        CraftingScrollbar.RecipeScrollbarComponent = _craftingRecipeScrollbar.GetComponent<Scrollbar>();
        return _craftingRecipeScrollbar;
    }

    private static RectTransform CreateCraftingRecipeScrollbar(InventoryGui gui)
    {
        RectTransform rect;
        if (gui.m_recipeListScroll != null && !IsUnityNull(gui.m_recipeListScroll))
        {
            GameObject clone = UnityEngine.Object.Instantiate(gui.m_recipeListScroll.gameObject, gui.m_crafting, false);
            clone.name = CraftingRecipeScrollbarName;
            rect = clone.GetComponent<RectTransform>() ?? clone.AddComponent<RectTransform>();
        }
        else
        {
            rect = new GameObject(CraftingRecipeScrollbarName, typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
            rect.SetParent(gui.m_crafting, false);
            Image background = rect.GetComponent<Image>();
            background.sprite = GetSolidUiSprite();
            background.color = new Color(0.06f, 0.035f, 0.015f, 0.88f);
            background.raycastTarget = true;
        }

        Scrollbar scrollbar = rect.GetComponent<Scrollbar>() ?? rect.gameObject.AddComponent<Scrollbar>();
        scrollbar.onValueChanged.RemoveAllListeners();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        if (scrollbar.handleRect == null)
        {
            Transform? handle = rect.Find("Sliding Area/Handle") ?? rect.GetComponentsInChildren<Image>(includeInactive: true)
                .Select(image => image.transform)
                .FirstOrDefault(transform => transform != rect);
            scrollbar.handleRect = handle != null ? handle.GetComponent<RectTransform>() : null;
        }

        if (scrollbar.targetGraphic == null && scrollbar.handleRect != null)
        {
            scrollbar.targetGraphic = scrollbar.handleRect.GetComponent<Graphic>();
        }

        if (scrollbar.handleRect == null)
        {
            RectTransform slidingArea = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            slidingArea.SetParent(rect, false);
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = new Vector2(3f, 3f);
            slidingArea.offsetMax = new Vector2(-3f, -3f);

            RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            handle.SetParent(slidingArea, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;

            Image handleImage = handle.GetComponent<Image>();
            handleImage.sprite = GetSolidUiSprite();
            handleImage.color = new Color(1f, 0.67f, 0.24f, 0.94f);
            handleImage.raycastTarget = true;

            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
        }

        SetCraftingRecipeScrollbarGraphicsVisible(rect, visible: true);
        return rect;
    }

    private static void SetCraftingRecipeScrollbarGraphicsVisible(RectTransform rect, bool visible)
    {
        foreach (Graphic graphic in rect.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            if (graphic == null || IsUnityNull(graphic))
            {
                continue;
            }

            graphic.enabled = visible;
            graphic.raycastTarget = visible;
        }

        Scrollbar scrollbar = rect.GetComponent<Scrollbar>();
        if (scrollbar != null)
        {
            scrollbar.interactable = visible;
            if (scrollbar.targetGraphic != null)
            {
                scrollbar.targetGraphic.enabled = visible;
                scrollbar.targetGraphic.raycastTarget = visible;
            }
        }
    }

    private static void SetCraftingVanillaRecipeScrollbarsVisible(InventoryGui gui, bool visible)
    {
        if (gui.m_crafting == null)
        {
            return;
        }

        foreach (Scrollbar scrollbar in gui.m_crafting.GetComponentsInChildren<Scrollbar>(includeInactive: true))
        {
            if (scrollbar == null || IsUnityNull(scrollbar) || scrollbar == CraftingScrollbar.RecipeScrollbarComponent)
            {
                continue;
            }

            Transform transform = scrollbar.transform;
            if (transform.name.StartsWith("InventorySlots_", StringComparison.Ordinal) ||
                IsForeignCraftingUiTransform(gui, transform))
            {
                continue;
            }

            scrollbar.gameObject.SetActive(visible);
        }

        foreach (Graphic graphic in gui.m_crafting.GetComponentsInChildren<Graphic>(includeInactive: true))
        {
            if (graphic == null || IsUnityNull(graphic) || !ShouldControlVanillaCraftingScrollGraphic(gui, graphic.transform))
            {
                continue;
            }

            graphic.enabled = visible;
            if (!visible)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private static bool ShouldControlVanillaCraftingScrollGraphic(InventoryGui gui, Transform transform)
    {
        if (transform == null || gui.m_crafting == null || !transform.IsChildOf(gui.m_crafting))
        {
            return false;
        }

        if (IsOwnedCraftingUiTransform(transform) || IsForeignCraftingUiTransform(gui, transform))
        {
            return false;
        }

        if (gui.m_recipeListScroll != null &&
            (transform == gui.m_recipeListScroll.transform || transform.IsChildOf(gui.m_recipeListScroll.transform)))
        {
            return true;
        }

        Transform? cursor = transform;
        while (cursor != null && cursor != gui.m_crafting)
        {
            string lowerName = cursor.name.ToLowerInvariant();
            if (lowerName.Contains("scroll"))
            {
                return true;
            }

            cursor = cursor.parent;
        }

        return false;
    }

    private static void OnCraftingRecipeScrollbarChanged(float value)
    {
        if (CraftingScrollbar.UpdatingRecipeScrollbar || InventoryGui.instance == null || !ShouldShowCraftingPanelRedesign(InventoryGui.instance))
        {
            return;
        }

        InventoryGui gui = InventoryGui.instance;
        UpdateCraftingRecipeView(gui);
        int pageCount = GetCraftingRecipePageCount(gui);
        if (pageCount <= 1)
        {
            _craftingRecipePage = 0;
            return;
        }

        int page = Mathf.Clamp(Mathf.RoundToInt((1f - value) * (pageCount - 1)), 0, pageCount - 1);
        if (page == _craftingRecipePage)
        {
            return;
        }

        _craftingRecipePage = page;
        CraftingController.ClearHoveredRecipe();
        CraftingController.MarkRecipeGridLayoutDirty();
        int pageStart = _craftingRecipePage * GetCraftingRecipeGridCapacity();
        if (pageStart >= 0 && pageStart < CraftingRecipes.View.Count)
        {
            SetCraftingRecipeWithStoredVariant(gui, CraftingRecipes.View[pageStart].OriginalIndex, center: false);
        }
    }
}

