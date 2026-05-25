using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool TryToggleCraftingRecipeFavorite(int originalIndex)
    {
        if (IsRecycleNReclaimReclaimTabActive(InventoryGui.instance))
        {
            return false;
        }

        if (!AreFavoritesEnabled() || !IsFavoriteModifierHeld())
        {
            return false;
        }

        InventoryGui? gui = InventoryGui.instance;
        Player? player = Player.m_localPlayer;
        if (gui == null || player == null || !TryGetCraftingRecipePair(gui, originalIndex, out InventoryGui.RecipeDataPair pair))
        {
            return true;
        }

        ToggleCraftingRecipeFavorite(player, pair);
        InvalidateCraftingRecipeView();
        UpdateCraftingRecipeView(gui);
        EnsureSelectedCraftingRecipeVisible(gui);
        UpdateCraftingPanelRedesign(gui, CraftingPanelUpdateReason.StateChanged);
        return true;
    }

    private static void ToggleCraftingRecipeFavorite(Player player, InventoryGui.RecipeDataPair pair)
    {
        EnsureCraftingFavoritesLoaded(player);
        bool upgradeFavorite = IsUpgradeFavoritePair(pair);
        string key = upgradeFavorite
            ? GetOrCreateUpgradeFavoriteItemKey(pair.ItemData)
            : GetCraftingRecipeFavoriteKey(pair);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        HashSet<string> favoriteKeys = upgradeFavorite ? FavoriteUpgradeItemKeys : FavoriteCraftingRecipeKeys;
        bool added = favoriteKeys.Add(key);
        if (!added)
        {
            favoriteKeys.Remove(key);
            if (upgradeFavorite && pair.ItemData != null)
            {
                RemoveUpgradeFavoriteItemKey(pair.ItemData);
                player.GetInventory()?.Changed();
            }
        }
        else if (upgradeFavorite)
        {
            player.GetInventory()?.Changed();
        }

        _craftingFavoritesVersion++;
        ClearCraftingGroupAvailabilityCache();
        SaveCraftingFavorites(player);
    }

    private static bool ClearCraftingFavorites(Player? player, bool upgradeFavorites)
    {
        if (!AreFavoritesEnabled() || player == null)
        {
            return false;
        }

        EnsureCraftingFavoritesLoaded(player);
        HashSet<string> favoriteKeys = upgradeFavorites ? FavoriteUpgradeItemKeys : FavoriteCraftingRecipeKeys;
        if (favoriteKeys.Count == 0)
        {
            return false;
        }

        if (upgradeFavorites)
        {
            RemoveUpgradeFavoriteItemKeysFromInventory(player, favoriteKeys);
        }

        favoriteKeys.Clear();
        _craftingFavoritesVersion++;
        ClearCraftingGroupAvailabilityCache();
        SaveCraftingFavorites(player);
        return true;
    }

    private static bool IsFavoriteCraftingRecipe(InventoryGui.RecipeDataPair pair)
    {
        if (!AreFavoritesEnabled())
        {
            return false;
        }

        if (IsRecycleNReclaimReclaimTabActive(InventoryGui.instance))
        {
            return false;
        }

        EnsureCraftingFavoritesLoaded(Player.m_localPlayer);
        if (IsUpgradeFavoritePair(pair))
        {
            string upgradeKey = GetUpgradeFavoriteItemKey(pair.ItemData);
            return !string.IsNullOrWhiteSpace(upgradeKey) && FavoriteUpgradeItemKeys.Contains(upgradeKey);
        }

        string key = GetCraftingRecipeFavoriteKey(pair);
        return !string.IsNullOrWhiteSpace(key) && FavoriteCraftingRecipeKeys.Contains(key);
    }

    private static bool IsUpgradeFavoritePair(InventoryGui.RecipeDataPair pair) => pair.ItemData != null;

    private static string GetCraftingRecipeFavoriteKey(InventoryGui.RecipeDataPair pair)
    {
        ItemDrop? item = pair.Recipe != null ? pair.Recipe.m_item : null;
        if (item == null)
        {
            return "";
        }

        string prefabName = item.m_itemData.m_dropPrefab != null ? item.m_itemData.m_dropPrefab.name : item.name;
        return CleanPrefabName(!string.IsNullOrWhiteSpace(prefabName) ? prefabName : item.m_itemData.m_shared.m_name);
    }

    private static string GetUpgradeFavoriteItemKey(ItemData? item)
    {
        return UpgradeFavoriteCore.GetItemId(item?.m_customData, UpgradeFavoriteItemIdKey);
    }

    private static string GetOrCreateUpgradeFavoriteItemKey(ItemData? item)
    {
        if (item == null)
        {
            return "";
        }

        item.m_customData ??= new Dictionary<string, string>();
        return UpgradeFavoriteCore.GetOrCreateItemId(
            item.m_customData,
            UpgradeFavoriteItemIdKey,
            FavoriteUpgradeItemKeys,
            () => Guid.NewGuid().ToString("N"));
    }

    private static void SetUpgradeFavoriteItemKey(ItemData? item, string id)
    {
        if (item == null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        item.m_customData ??= new Dictionary<string, string>();
        UpgradeFavoriteCore.SetItemId(item.m_customData, UpgradeFavoriteItemIdKey, id);
    }

    private static void RemoveUpgradeFavoriteItemKey(ItemData? item)
    {
        UpgradeFavoriteCore.RemoveItemId(item?.m_customData, UpgradeFavoriteItemIdKey);
    }

    private static void RemoveUpgradeFavoriteItemKeysFromInventory(Player player, HashSet<string> favoriteKeys)
    {
        Inventory? inventory = player.GetInventory();
        if (inventory == null)
        {
            return;
        }

        bool changed = false;
        foreach (ItemData item in inventory.m_inventory.Where(item => item?.m_customData != null))
        {
            string key = GetUpgradeFavoriteItemKey(item);
            if (!string.IsNullOrWhiteSpace(key) && favoriteKeys.Contains(key))
            {
                UpgradeFavoriteCore.RemoveItemId(item.m_customData, UpgradeFavoriteItemIdKey);
                changed = true;
            }
        }

        if (changed)
        {
            inventory.Changed();
        }
    }

    private static void EnsureCraftingFavoritesLoaded(Player? player)
    {
        if (!AreFavoritesEnabled() || player == null)
        {
            return;
        }

        string playerId = GetPlayerId(player);
        if (string.Equals(_loadedCraftingFavoritesPlayerId, playerId, StringComparison.Ordinal))
        {
            return;
        }

        FavoriteCraftingRecipeKeys.Clear();
        FavoriteUpgradeItemKeys.Clear();
        _loadedCraftingFavoritesPlayerId = playerId;
        _craftingFavoritesVersion++;
        ClearCraftingGroupAvailabilityCache();

        try
        {
            InventorySlotsClientPlayerState? data = GetClientPlayerState(playerId, create: false);
            if (data == null)
            {
                return;
            }

            foreach (string recipe in data.CraftingFavorites.Where(recipe => !string.IsNullOrWhiteSpace(recipe)))
            {
                FavoriteCraftingRecipeKeys.Add(recipe.Trim());
            }

            foreach (string itemId in data.UpgradeFavorites.Where(itemId => !string.IsNullOrWhiteSpace(itemId)))
            {
                FavoriteUpgradeItemKeys.Add(itemId.Trim());
            }

            _craftingFavoritesVersion++;
            ClearCraftingGroupAvailabilityCache();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to load InventorySlots crafting favorites from {ClientStateFilePath}: {ex.Message}");
        }
    }

    private static void SaveCraftingFavorites(Player player)
    {
        if (player == null)
        {
            return;
        }

        string playerId = GetPlayerId(player);
        _loadedCraftingFavoritesPlayerId = playerId;
        try
        {
            InventorySlotsClientPlayerState data = GetClientPlayerState(playerId, create: true)!;
            data.CraftingFavorites = FavoriteCraftingRecipeKeys
                .OrderBy(recipe => recipe, StringComparer.OrdinalIgnoreCase)
                .ToList();
            data.UpgradeFavorites = FavoriteUpgradeItemKeys
                .OrderBy(itemId => itemId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            SaveClientState();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save InventorySlots crafting favorites: {ex.Message}");
        }
    }

    internal static void CaptureUpgradeFavoriteBeforeCrafting(InventoryGui gui)
    {
        _pendingUpgradeFavoriteItemId = "";
        _pendingUpgradeFavoritePrefab = "";
        _pendingUpgradeFavoriteQuality = -1;
        _pendingUpgradeFavoriteVariant = -1;
        _pendingUpgradeFavoriteGridPos = new Vector2i(-1, -1);

        ItemData? item = gui?.m_craftUpgradeItem;
        string id = GetUpgradeFavoriteItemKey(item);
        if (item?.m_shared == null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _pendingUpgradeFavoriteItemId = id;
        _pendingUpgradeFavoritePrefab = GetItemPrefabName(item);
        _pendingUpgradeFavoriteQuality = item.m_quality + 1;
        _pendingUpgradeFavoriteVariant = item.m_variant;
        _pendingUpgradeFavoriteGridPos = item.m_gridPos;
    }

    internal static void RestoreUpgradeFavoriteAfterCrafting(InventoryGui gui, Player player)
    {
        string id = _pendingUpgradeFavoriteItemId;
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _pendingUpgradeFavoriteItemId = "";

        Inventory? inventory = player?.GetInventory();
        ItemData? upgraded = inventory?.GetItemAt(_pendingUpgradeFavoriteGridPos.x, _pendingUpgradeFavoriteGridPos.y);
        if (!IsPendingUpgradeFavoriteItem(upgraded))
        {
            upgraded = inventory?.m_inventory.FirstOrDefault(IsPendingUpgradeFavoriteItem);
        }

        if (upgraded == null)
        {
            return;
        }

        SetUpgradeFavoriteItemKey(upgraded, id);
        inventory?.Changed();
        _craftingFavoritesVersion++;
        ClearCraftingGroupAvailabilityCache();
        InvalidateCraftingRecipeView();
        if (gui != null)
        {
            UpdateCraftingPanelRedesign(gui, CraftingPanelUpdateReason.StateChanged);
        }
    }

    private static bool IsPendingUpgradeFavoriteItem(ItemData? item)
    {
        return item?.m_shared != null &&
               item.m_quality == _pendingUpgradeFavoriteQuality &&
               item.m_variant == _pendingUpgradeFavoriteVariant &&
               string.Equals(GetItemPrefabName(item), _pendingUpgradeFavoritePrefab, StringComparison.OrdinalIgnoreCase);
    }
}
