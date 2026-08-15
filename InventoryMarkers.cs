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
    private static InventoryGridElementUiCache? GetInventoryGridElementUiCache(InventoryGrid.Element element)
    {
        return IsUnityNull(element?.m_go)
            ? null
            : element!.m_go.GetComponent<InventoryGridElementUiCache>() ?? element.m_go.AddComponent<InventoryGridElementUiCache>();
    }

    private static void UpdateFavoriteBorder(InventoryGrid.Element element, Player player, Inventory inventory, Vector2i pos)
    {
        bool slotFavorite = IsFavoriteSlot(player, pos);
        if (!slotFavorite)
        {
            HideFavoriteBorder(element);
            return;
        }

        RectTransform? border = EnsureFavoriteBorder(element);
        if (border == null)
        {
            return;
        }

        Color color = new(0.1f, 0.55f, 1f, 0.95f);

        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element);
        Image[] images = cache?.FavoriteBorderImages ?? border.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            image.color = color;
        }

        border.gameObject.SetActive(true);
        border.SetAsLastSibling();
    }

    private static RectTransform? EnsureFavoriteBorder(InventoryGrid.Element element)
    {
        if (IsUnityNull(element?.m_go))
        {
            return null;
        }

        GameObject root = element!.m_go!;
        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element!);
        RectTransform? border = cache != null && cache.FavoriteBorder != null && !IsUnityNull(cache.FavoriteBorder)
            ? cache.FavoriteBorder
            : null;
        if (border == null)
        {
            Transform existing = root.transform.Find(FavoriteBorderName);
            border = existing != null ? existing.GetComponent<RectTransform>() : null;
        }

        if (border == null)
        {
            GameObject go = new(FavoriteBorderName, typeof(RectTransform));
            border = (RectTransform)go.transform;
            border.SetParent(root.transform, false);
            CreateFavoriteBorderSide(border, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FavoriteBorderThickness));
            CreateFavoriteBorderSide(border, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FavoriteBorderThickness));
            CreateFavoriteBorderSide(border, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FavoriteBorderThickness, 0f));
            CreateFavoriteBorderSide(border, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FavoriteBorderThickness, 0f));
        }

        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = Vector2.zero;
        border.offsetMax = Vector2.zero;
        border.localScale = Vector3.one;
        border.localRotation = Quaternion.identity;
        if (cache != null)
        {
            cache.FavoriteBorder = border;
            if (cache.FavoriteBorderImages.Length == 0)
            {
                cache.FavoriteBorderImages = border.GetComponentsInChildren<Image>(true);
            }
        }

        return border;
    }

    private static void UpdateInventoryPinnedTooltipGridBorders(InventoryGrid? grid)
    {
        if (grid?.m_elements == null || grid.m_inventory == null)
        {
            return;
        }

        foreach (InventoryGrid.Element element in grid.m_elements)
        {
            if (IsUnityNull(element?.m_go))
            {
                continue;
            }

            if (!element!.m_go.activeSelf)
            {
                HideInventoryPinnedTooltipBorder(element);
                continue;
            }

            Vector2i pos = grid.GetButtonPos(element.m_go);
            UpdateInventoryPinnedTooltipBorder(grid, element, pos);
        }
    }

    private static void RefreshInventoryPinnedTooltipBorders()
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null)
        {
            return;
        }

        UpdateInventoryPinnedTooltipGridBorders(gui.m_playerGrid);
        UpdateInventoryPinnedTooltipGridBorders(gui.m_containerGrid);
    }

    private static void UpdateInventoryPinnedTooltipBorder(InventoryGrid grid, InventoryGrid.Element element, Vector2i pos)
    {
        if (grid.m_inventory == null || IsOutOfBounds(grid.m_inventory, pos))
        {
            HideInventoryPinnedTooltipBorder(element);
            return;
        }

        ItemData? item = grid.m_inventory.GetItemAt(pos.x, pos.y);
        if (item?.m_shared == null || !IsInventoryItemTooltipPinned(grid, pos, item))
        {
            HideInventoryPinnedTooltipBorder(element);
            return;
        }

        RectTransform? marker = EnsureInventoryPinnedTooltipMarker(element);
        if (marker == null)
        {
            return;
        }

        ConfigureInventoryPinnedTooltipMarker(marker);
        marker.gameObject.SetActive(true);
        marker.SetAsLastSibling();
    }

    private static bool IsInventoryItemTooltipPinned(InventoryGrid grid, Vector2i pos, ItemData item)
    {
        for (int slot = 0; slot < GetActivePinnedTooltipSlotCount(); slot++)
        {
            RectTransform? panel = PinnedTooltips.Inventory.Panels[slot];
            if (panel != null &&
                !IsUnityNull(panel) &&
                panel.gameObject.activeSelf &&
                IsSameInventoryPinnedTooltipTarget(slot, grid, pos, item))
            {
                return true;
            }
        }

        return false;
    }

    private static RectTransform? EnsureInventoryPinnedTooltipMarker(InventoryGrid.Element element)
    {
        if (IsUnityNull(element?.m_go))
        {
            return null;
        }

        GameObject root = element!.m_go!;
        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element!);
        RectTransform? marker = cache != null && cache.PinnedTooltipMarker != null && !IsUnityNull(cache.PinnedTooltipMarker)
            ? cache.PinnedTooltipMarker
            : null;
        if (marker == null)
        {
            Transform existing = root.transform.Find(InventoryPinnedTooltipMarkerName);
            marker = existing != null ? existing.GetComponent<RectTransform>() : null;
        }

        if (marker == null)
        {
            marker = CreateTextRect(InventoryPinnedTooltipMarkerName, root.transform, out TMP_Text text, active: false);
            text.text = "T";
            if (cache != null)
            {
                cache.PinnedTooltipText = text;
            }
        }

        if (cache != null)
        {
            cache.PinnedTooltipMarker = marker;
            cache.PinnedTooltipText ??= marker.GetComponent<TMP_Text>();
        }

        ConfigureInventoryPinnedTooltipMarker(marker);
        return marker;
    }

    private static void ConfigureInventoryPinnedTooltipMarker(RectTransform marker)
    {
        if (marker == null || IsUnityNull(marker))
        {
            return;
        }

        CraftingPinnedTooltipMarkerState state = marker.GetComponent<CraftingPinnedTooltipMarkerState>() ?? marker.gameObject.AddComponent<CraftingPinnedTooltipMarkerState>();
        const string signature = "inventory";
        if (string.Equals(state.LayoutSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        SetRectLayout(marker, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4f, 4f), new Vector2(26f, 24f));

        TMP_Text? text = marker.GetComponent<TMP_Text>();
        if (text == null)
        {
            state.LayoutSignature = signature;
            return;
        }

        ApplyDefaultFontAsset(text);
        text.text = "T";
        text.alignment = TextAlignmentOptions.BottomRight;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = 18f;
        text.color = Color.white;
        text.outlineColor = new Color32(0, 0, 0, 230);
        text.outlineWidth = 0.18f;
        text.raycastTarget = false;
        state.LayoutSignature = signature;
    }

    private static void HideInventoryPinnedTooltipBorder(InventoryGrid.Element element)
    {
        if (IsUnityNull(element?.m_go))
        {
            return;
        }

        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element!);
        Transform existing = cache?.PinnedTooltipMarker != null && !IsUnityNull(cache.PinnedTooltipMarker)
            ? cache.PinnedTooltipMarker
            : element!.m_go.transform.Find(InventoryPinnedTooltipMarkerName);
        if (existing != null && existing.gameObject.activeSelf)
        {
            existing.gameObject.SetActive(false);
        }
    }

    private static void CreateFavoriteBorderSide(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject side = new(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)side.transform;
        rect.SetParent(parent, false);
        SetRectLayout(rect, anchorMin, anchorMax, pivot, Vector2.zero, sizeDelta);
        Image image = side.GetComponent<Image>();
        image.raycastTarget = false;
    }

    private static void HideFavoriteBorder(InventoryGrid.Element element)
    {
        if (IsUnityNull(element?.m_go))
        {
            return;
        }

        InventoryGridElementUiCache? cache = GetInventoryGridElementUiCache(element!);
        Transform existing = cache?.FavoriteBorder != null && !IsUnityNull(cache.FavoriteBorder)
            ? cache.FavoriteBorder
            : element!.m_go.transform.Find(FavoriteBorderName);
        if (existing != null && existing.gameObject.activeSelf)
        {
            existing.gameObject.SetActive(false);
        }
    }

}
