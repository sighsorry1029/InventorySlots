using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    internal static bool HandleFavoriteClick(InventoryGrid grid, UIInputHandler clickHandler)
    {
        if (!ShouldHandleFavoriteClick(grid))
        {
            return true;
        }

        Player player = Player.m_localPlayer;
        Inventory? inventory = GetPlayerInventory(player);
        if (inventory == null || grid.m_inventory != inventory)
        {
            return true;
        }

        Vector2i pos = grid.GetButtonPos(clickHandler.gameObject);
        if (pos.x < 0 || pos.y < 0 || IsOutOfBounds(inventory, pos))
        {
            return true;
        }

        if (!CanFavoriteCell(inventory, pos))
        {
            return true;
        }

        ToggleFavoriteSlot(player, pos);
        return false;
    }

    private static bool ShouldHandleFavoriteClick(InventoryGrid grid)
    {
        if (grid == null || InventoryGui.instance == null || Player.m_localPlayer == null)
        {
            return false;
        }

        Player player = Player.m_localPlayer;
        if (player.m_isLoading || player.IsTeleporting())
        {
            return false;
        }

        if (InventoryGui.instance.m_dragGo != null || grid != InventoryGui.instance.m_playerGrid)
        {
            return false;
        }

        return _favoriteModifierKey != null && IsShortcutHeldAllowingAltPair(_favoriteModifierKey.Value);
    }

    private static void ToggleFavoriteSlot(Player player, Vector2i pos)
    {
        EnsureFavoritesLoaded(player);
        bool added = Runtime.FavoriteSlots.Add(pos);
        if (!added)
        {
            Runtime.FavoriteSlots.Remove(pos);
        }

        SaveFavorites(player);
        RefreshFavoriteBorders();
    }

    private static bool IsFavoriteProtected(Player player, Inventory inventory, ItemDrop.ItemData item)
    {
        return item?.m_shared != null &&
               IsPlayerInventory(player, inventory) &&
               IsFavoriteSlot(player, inventory, item.m_gridPos);
    }

    private static bool IsFavoriteSlot(Player player, Inventory inventory, Vector2i pos)
    {
        EnsureFavoritesLoaded(player);
        return CanFavoriteCell(inventory, pos) && Runtime.FavoriteSlots.Contains(pos);
    }

    private static void EnsureFavoritesLoaded(Player player)
    {
        if (player == null)
        {
            return;
        }

        string playerId = GetPlayerId(player);
        if (string.Equals(Runtime.LoadedFavoritesPlayerId, playerId, StringComparison.Ordinal))
        {
            return;
        }

        Runtime.FavoriteSlots.Clear();
        Runtime.LoadedFavoritesPlayerId = playerId;
        string path = GetFavoriteFilePath(playerId);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split(',');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0].Trim(), out int x) ||
                    !int.TryParse(parts[1].Trim(), out int y) ||
                    x < 0 ||
                    y < 0)
                {
                    continue;
                }

                Runtime.FavoriteSlots.Add(new Vector2i(x, y));
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to load InventoryActions favorites from {path}: {ex.Message}");
        }
    }

    private static void SaveFavorites(Player player)
    {
        if (player == null)
        {
            return;
        }

        string playerId = GetPlayerId(player);
        Runtime.LoadedFavoritesPlayerId = playerId;
        string path = GetFavoriteFilePath(playerId);
        try
        {
            string[] lines = Runtime.FavoriteSlots
                .OrderBy(slot => slot.y)
                .ThenBy(slot => slot.x)
                .Select(slot => $"{slot.x},{slot.y}")
                .ToArray();
            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save InventoryActions favorites to {path}: {ex.Message}");
        }
    }

    private static void RefreshFavoriteBorders()
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui?.m_playerGrid == null || Player.m_localPlayer == null)
        {
            return;
        }

        UpdateFavoriteBorders(gui.m_playerGrid, Player.m_localPlayer);
    }

    private static void UpdateFavoriteBorders(InventoryGrid grid, Player player)
    {
        Inventory? inventory = GetPlayerInventory(player);
        if (grid?.m_elements == null || inventory == null || grid.m_inventory != inventory)
        {
            return;
        }

        EnsureFavoritesLoaded(player);
        Color borderColor = GetFavoriteBorderColor();
        foreach (InventoryGrid.Element element in grid.m_elements)
        {
            if (element?.m_go == null || IsUnityNull(element.m_go))
            {
                continue;
            }

            if (!element.m_go.activeSelf)
            {
                HideFavoriteBorder(element);
                continue;
            }

            Vector2i pos = grid.GetButtonPos(element.m_go);
            if (!CanFavoriteCell(inventory, pos) || !Runtime.FavoriteSlots.Contains(pos))
            {
                HideFavoriteBorder(element);
                continue;
            }

            InventoryGridElementMarker marker = element.m_go.GetComponent<InventoryGridElementMarker>() ?? element.m_go.AddComponent<InventoryGridElementMarker>();
            RectTransform? border = EnsureFavoriteBorder(element, marker);
            if (border == null)
            {
                continue;
            }

            foreach (Image image in marker.FavoriteBorderImages)
            {
                if (image != null && image.color != borderColor)
                {
                    image.color = borderColor;
                }
            }

            if (!border.gameObject.activeSelf)
            {
                border.gameObject.SetActive(true);
            }

            if (border.GetSiblingIndex() != element.m_go.transform.childCount - 1)
            {
                border.SetAsLastSibling();
            }
        }
    }

    private static Color GetFavoriteBorderColor() =>
        _favoriteBorderColor != null ? _favoriteBorderColor.Value : FavoriteBorderDefaultColor;

    private static RectTransform? EnsureFavoriteBorder(InventoryGrid.Element element, InventoryGridElementMarker marker)
    {
        if (element?.m_go == null || IsUnityNull(element.m_go))
        {
            return null;
        }

        GameObject root = element.m_go;
        RectTransform? border = marker.FavoriteBorder != null && !IsUnityNull(marker.FavoriteBorder)
            ? marker.FavoriteBorder
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

        if (border.parent != root.transform)
        {
            border.SetParent(root.transform, false);
        }

        if (border.anchorMin != Vector2.zero) border.anchorMin = Vector2.zero;
        if (border.anchorMax != Vector2.one) border.anchorMax = Vector2.one;
        if (border.offsetMin != Vector2.zero) border.offsetMin = Vector2.zero;
        if (border.offsetMax != Vector2.zero) border.offsetMax = Vector2.zero;
        if (border.localScale != Vector3.one) border.localScale = Vector3.one;
        if (border.localRotation != Quaternion.identity) border.localRotation = Quaternion.identity;
        bool refreshImages = marker.FavoriteBorder != border || marker.FavoriteBorderImages.Length == 0;
        if (!refreshImages)
        {
            foreach (Image image in marker.FavoriteBorderImages)
            {
                if (image == null || !image.transform.IsChildOf(border))
                {
                    refreshImages = true;
                    break;
                }
            }
        }

        if (refreshImages)
        {
            marker.FavoriteBorderImages = border.GetComponentsInChildren<Image>(true);
        }

        marker.FavoriteBorder = border;
        return border;
    }

    private static void CreateFavoriteBorderSide(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject side = new(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)side.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        Image image = side.GetComponent<Image>();
        image.raycastTarget = false;
    }

    private static void HideFavoriteBorder(InventoryGrid.Element element)
    {
        if (element?.m_go == null || IsUnityNull(element.m_go))
        {
            return;
        }

        InventoryGridElementMarker? marker = element.m_go.GetComponent<InventoryGridElementMarker>();
        Transform? existing = marker?.FavoriteBorder != null && !IsUnityNull(marker.FavoriteBorder)
            ? marker.FavoriteBorder
            : element.m_go.transform.Find(FavoriteBorderName);
        if (existing != null && existing.gameObject.activeSelf)
        {
            existing.gameObject.SetActive(false);
        }
    }
}
