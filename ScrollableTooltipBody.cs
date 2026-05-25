using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySlots;

internal sealed class ScrollableTooltipBodyState
{
    public RectTransform? ScrollView { get; set; }
    public RectTransform? Viewport { get; set; }
    public RectTransform? Content { get; set; }
    public ScrollRect? ScrollRect { get; set; }
    public Scrollbar? Scrollbar { get; set; }
    public TMP_Text? Text { get; set; }
    public RectTransformSnapshot? TextOriginalSnapshot { get; set; }
}

internal readonly struct ScrollableTooltipBodyLayoutResult
{
    public ScrollableTooltipBodyLayoutResult(float scrollOffset, float maxScroll, bool needsScroll)
    {
        ScrollOffset = scrollOffset;
        MaxScroll = maxScroll;
        NeedsScroll = needsScroll;
    }

    public float ScrollOffset { get; }
    public float MaxScroll { get; }
    public bool NeedsScroll { get; }
}

internal static class ScrollableTooltipBody
{
    private static readonly Color DefaultHandleColor = new(1f, 0.78f, 0.38f, 0.75f);
    private static readonly Color DefaultScrollbarBackgroundColor = new(0f, 0f, 0f, 0.18f);

    public static RectTransform Ensure(
        RectTransform panel,
        TMP_Text text,
        ScrollableTooltipBodyState state,
        Sprite? sprite,
        float scrollSensitivity,
        bool scrollRectEnabled,
        bool inertia,
        bool handleRaycastTarget,
        bool scrollbarRaycastTarget)
    {
        state.ScrollView = EnsureChildRect(
            panel,
            state.ScrollView,
            "TextScrollView",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));

        state.Viewport = EnsureChildRect(
            state.ScrollView,
            state.Viewport,
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));

        state.Content = EnsureChildRect(
            state.Viewport,
            state.Content,
            "Content",
            typeof(RectTransform));

        ConfigureTransparentImage(state.ScrollView, sprite);
        ConfigureTransparentImage(state.Viewport, sprite);
        if (state.Viewport.GetComponent<RectMask2D>() == null)
        {
            state.Viewport.gameObject.AddComponent<RectMask2D>();
        }

        state.ScrollRect = state.ScrollView.GetComponent<ScrollRect>() ?? state.ScrollView.gameObject.AddComponent<ScrollRect>();
        state.ScrollRect.enabled = scrollRectEnabled;
        state.ScrollRect.horizontal = false;
        state.ScrollRect.vertical = true;
        state.ScrollRect.movementType = ScrollRect.MovementType.Clamped;
        state.ScrollRect.inertia = inertia;
        state.ScrollRect.scrollSensitivity = scrollSensitivity;
        state.ScrollRect.viewport = state.Viewport;
        state.ScrollRect.content = state.Content;

        RectTransform textRect = text.rectTransform;
        bool textChanged = IsNull(state.Text) || state.Text != text;
        if (textChanged)
        {
            state.TextOriginalSnapshot = null;
        }

        if ((textChanged || state.TextOriginalSnapshot == null) && textRect.parent != state.Content)
        {
            state.TextOriginalSnapshot = new RectTransformSnapshot(textRect);
        }

        state.Text = text;
        if (textRect.parent != state.Content)
        {
            textRect.SetParent(state.Content, false);
        }

        text.enabled = true;
        text.gameObject.SetActive(true);
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.Normal;

        EnsureScrollbar(panel, state, sprite, handleRaycastTarget, scrollbarRaycastTarget);
        return state.Content;
    }

    public static void Restore(ScrollableTooltipBodyState state)
    {
        bool restoredText = false;
        RectTransform? restoredParent = null;
        if (!IsNull(state.Text) && state.TextOriginalSnapshot != null)
        {
            state.TextOriginalSnapshot.Restore();
            restoredText = true;
            restoredParent = state.Text!.rectTransform.parent as RectTransform;
        }

        if (!restoredText &&
            !IsNull(state.Text) &&
            !IsNull(state.ScrollView) &&
            state.ScrollView!.parent is RectTransform fallbackParent)
        {
            RectTransform textRect = state.Text!.rectTransform;
            int siblingIndex = state.ScrollView.GetSiblingIndex();
            if (textRect.parent != fallbackParent)
            {
                textRect.SetParent(fallbackParent, false);
            }

            textRect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, fallbackParent.childCount - 1));
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.identity;
            restoredParent = fallbackParent;
        }

        if (!IsNull(state.Text))
        {
            state.Text!.enabled = true;
            state.Text.gameObject.SetActive(true);
        }

        if (!IsNull(state.ScrollRect))
        {
            state.ScrollRect!.enabled = false;
            state.ScrollRect.verticalScrollbar = null;
        }

        if (!IsNull(state.Scrollbar))
        {
            state.Scrollbar!.gameObject.SetActive(false);
            state.Scrollbar.enabled = false;
        }

        if (!IsNull(state.ScrollView))
        {
            state.ScrollView!.gameObject.SetActive(false);
        }

        if (!IsNull(restoredParent))
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(restoredParent!);
        }

        state.Text = null;
        state.TextOriginalSnapshot = null;
    }

    public static ScrollableTooltipBodyLayoutResult LayoutPixelScroll(
        ScrollableTooltipBodyState state,
        TMP_Text text,
        float textWidth,
        float horizontalPadding,
        float top,
        float viewportHeight,
        float contentHeight,
        float scrollOffset,
        float scrollbarOutsideOffset,
        float scrollbarWidth,
        bool enableScrollRectWhenNeeded)
    {
        if (IsNull(state.ScrollView) ||
            IsNull(state.Viewport) ||
            IsNull(state.Content))
        {
            return new ScrollableTooltipBodyLayoutResult(0f, 0f, false);
        }

        RectTransform scrollView = state.ScrollView!;
        scrollView.anchorMin = new Vector2(0f, 1f);
        scrollView.anchorMax = new Vector2(1f, 1f);
        scrollView.pivot = new Vector2(0.5f, 1f);
        scrollView.anchoredPosition = new Vector2(0f, -top);
        scrollView.sizeDelta = new Vector2(-horizontalPadding * 2f, viewportHeight);
        scrollView.localScale = Vector3.one;
        scrollView.localRotation = Quaternion.identity;

        RectTransform viewport = state.Viewport!;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.localScale = Vector3.one;
        viewport.localRotation = Quaternion.identity;

        RectTransform content = state.Content!;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(textWidth, contentHeight);

        RectTransform textRect = text.rectTransform;
        if (textRect.parent != content)
        {
            textRect.SetParent(content, false);
        }

        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(textWidth, contentHeight);
        textRect.localScale = Vector3.one;
        textRect.localRotation = Quaternion.identity;

        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
        scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
        bool needsScroll = maxScroll > 1f;
        ApplyPixelScrollPosition(state, scrollOffset, maxScroll);

        if (!IsNull(state.Scrollbar))
        {
            RectTransform scrollbarRect = (RectTransform)state.Scrollbar!.transform;
            LayoutFloatingScrollbar(scrollbarRect, scrollbarOutsideOffset, scrollbarWidth, top, viewportHeight);
            state.Scrollbar.gameObject.SetActive(needsScroll);
            state.Scrollbar.enabled = needsScroll;
            state.Scrollbar.size = needsScroll ? Mathf.Clamp01(viewportHeight / contentHeight) : 1f;
        }

        if (!IsNull(state.ScrollRect))
        {
            state.ScrollRect!.enabled = enableScrollRectWhenNeeded && needsScroll;
            state.ScrollRect.verticalScrollbar = needsScroll ? state.Scrollbar : null;
            state.ScrollRect.verticalScrollbarVisibility = needsScroll
                ? ScrollRect.ScrollbarVisibility.Permanent
                : ScrollRect.ScrollbarVisibility.AutoHide;
        }

        return new ScrollableTooltipBodyLayoutResult(scrollOffset, maxScroll, needsScroll);
    }

    public static void ApplyPixelScrollPosition(ScrollableTooltipBodyState state, float scrollOffset, float maxScroll)
    {
        if (IsNull(state.Content))
        {
            return;
        }

        state.Content!.anchoredPosition = new Vector2(0f, scrollOffset);
        if (!IsNull(state.Scrollbar))
        {
            state.Scrollbar!.value = maxScroll > 1f
                ? 1f - Mathf.Clamp01(scrollOffset / maxScroll)
                : 1f;
        }
    }

    public static void LayoutStretchScrollbar(RectTransform rect, float outsideOffset, float width, float topReserved, float bottomReserved)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(outsideOffset, bottomReserved);
        rect.offsetMax = new Vector2(outsideOffset + width, -topReserved);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    public static ScrollableTooltipBodyState FromPinnedCache(PinnedTooltipPanelUiCache cache)
    {
        return new ScrollableTooltipBodyState
        {
            ScrollView = cache.TextScrollView,
            Viewport = cache.TextViewport,
            Content = cache.TextContent,
            ScrollRect = cache.TextScrollRect,
            Scrollbar = cache.TextScrollbar
        };
    }

    public static void ApplyToPinnedCache(ScrollableTooltipBodyState state, PinnedTooltipPanelUiCache cache)
    {
        cache.TextScrollView = state.ScrollView;
        cache.TextViewport = state.Viewport;
        cache.TextContent = state.Content;
        cache.TextScrollRect = state.ScrollRect;
        cache.TextScrollbar = state.Scrollbar;
    }

    private static void EnsureScrollbar(
        RectTransform panel,
        ScrollableTooltipBodyState state,
        Sprite? sprite,
        bool handleRaycastTarget,
        bool scrollbarRaycastTarget)
    {
        RectTransform scrollbarRect = EnsureScrollbarRect(panel, state.Scrollbar);
        state.Scrollbar = scrollbarRect.GetComponent<Scrollbar>() ?? scrollbarRect.gameObject.AddComponent<Scrollbar>();

        RectTransform handleArea = EnsureChildRect(scrollbarRect, null, "Sliding Area", typeof(RectTransform));
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = Vector2.zero;
        handleArea.offsetMax = Vector2.zero;

        RectTransform handle = EnsureChildRect(handleArea, null, "Handle", typeof(RectTransform), typeof(Image));
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;

        Image handleImage = handle.GetComponent<Image>() ?? handle.gameObject.AddComponent<Image>();
        handleImage.sprite = sprite;
        handleImage.color = DefaultHandleColor;
        handleImage.raycastTarget = handleRaycastTarget;

        state.Scrollbar.handleRect = handle;
        state.Scrollbar.targetGraphic = handleImage;
        state.Scrollbar.direction = Scrollbar.Direction.BottomToTop;

        Image background = scrollbarRect.GetComponent<Image>() ?? scrollbarRect.gameObject.AddComponent<Image>();
        background.sprite = sprite;
        background.color = DefaultScrollbarBackgroundColor;
        background.raycastTarget = scrollbarRaycastTarget;
        state.Scrollbar.gameObject.SetActive(false);

        if (!IsNull(state.ScrollRect))
        {
            state.ScrollRect!.verticalScrollbar = state.Scrollbar;
            state.ScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }

    private static RectTransform EnsureScrollbarRect(RectTransform parent, Scrollbar? current)
    {
        RectTransform? currentRect = !IsNull(current) ? current!.transform as RectTransform : null;
        if (!IsNull(currentRect) && currentRect!.parent == parent)
        {
            currentRect.gameObject.SetActive(true);
            return currentRect;
        }

        Transform? existing = parent.Find("TextScrollbar");
        RectTransform? rect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (IsNull(rect))
        {
            rect = new GameObject("TextScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
        }

        rect!.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.gameObject.SetActive(true);
        return rect;
    }

    private static RectTransform EnsureChildRect(RectTransform parent, RectTransform? current, string name, params System.Type[] components)
    {
        if (!IsNull(current) && current!.parent == parent)
        {
            current.gameObject.SetActive(true);
            return current;
        }

        Transform? existing = parent.Find(name);
        RectTransform? rect = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (IsNull(rect))
        {
            rect = new GameObject(name, components).GetComponent<RectTransform>();
        }

        rect!.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.gameObject.SetActive(true);
        return rect;
    }

    private static void ConfigureTransparentImage(RectTransform rect, Sprite? sprite)
    {
        Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
    }

    private static void LayoutFloatingScrollbar(RectTransform rect, float outsideOffset, float width, float top, float viewportHeight)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(outsideOffset, -top);
        rect.sizeDelta = new Vector2(width, viewportHeight);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static bool IsNull(Object? obj)
    {
        return obj == null;
    }
}
