using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;
using ItemType = ItemDrop.ItemData.ItemType;
using Requirement = Piece.Requirement;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static void OnCraftingRecipeListUpdated(InventoryGui gui)
    {
        if (gui == null)
        {
            return;
        }

        TryRefreshSelectedJewelcraftingSocketRecipePair(gui);
        string signature = GetCraftingRecipeListChangeSignature(gui);
        if (!CraftingController.TryStoreRecipeListChangeSignature(signature))
        {
            return;
        }

        UpdateCraftingPanelRedesign(gui, CraftingPanelUpdateReason.RecipeListChanged);
    }

    private static bool TryRefreshSelectedJewelcraftingSocketRecipePair(InventoryGui? gui)
    {
        if (gui == null ||
            !IsJewelcraftingSocketTabActive(gui) ||
            !TryFindLatestJewelcraftingSocketRecipePair(
                gui,
                gui.m_selectedRecipe.Recipe,
                gui.m_selectedRecipe.ItemData,
                out InventoryGui.RecipeDataPair pair))
        {
            return false;
        }

        gui.m_selectedRecipe = pair;
        return true;
    }

    private static bool TryFindLatestJewelcraftingSocketRecipePair(
        InventoryGui? gui,
        Recipe? recipe,
        ItemData? item,
        out InventoryGui.RecipeDataPair pair)
    {
        pair = default;
        if (gui?.m_availableRecipes == null || recipe == null || item == null)
        {
            return false;
        }

        foreach (InventoryGui.RecipeDataPair candidate in gui.m_availableRecipes)
        {
            if (!ReferenceEquals(candidate.Recipe, recipe) ||
                !ReferenceEquals(candidate.ItemData, item))
            {
                continue;
            }

            pair = candidate;
            return true;
        }

        return false;
    }

    private static string GetCraftingRecipeListChangeSignature(InventoryGui gui)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + gui.GetInstanceID();
            hash = hash * 31 + (IsCraftingCraftTabSelected(gui) ? 1 : 0);
            hash = hash * 31 + (IsCraftingUpgradeTabSelected(gui) ? 1 : 0);
            CraftingTabAdapterState adapter = GetCraftingTabAdapterState(gui);
            hash = hash * 31 + adapter.Kind.GetHashCode();

            int count = gui.m_availableRecipes?.Count ?? -1;
            hash = hash * 31 + count;
            if (gui.m_availableRecipes != null)
            {
                for (int i = 0; i < gui.m_availableRecipes.Count; i++)
                {
                    hash = hash * 31 + GetCraftingRecipePairChangeHash(gui.m_availableRecipes[i]);
                }
            }

            if (adapter.IsRecycleNReclaim)
            {
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(GetRecycleNReclaimContextSignature());
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(GetRecycleNReclaimRecipeListSignature(gui));
            }

            return $"{count}:{hash}";
        }
    }

    private static int GetCraftingRecipePairChangeHash(InventoryGui.RecipeDataPair pair)
    {
        unchecked
        {
            int hash = 17;
            Recipe? recipe = pair.Recipe;
            ItemData? item = pair.ItemData;
            hash = hash * 31 + (recipe != null ? recipe.GetInstanceID() : 0);
            hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(recipe != null ? recipe.name ?? "" : "");
            hash = hash * 31 + (recipe?.m_item != null ? recipe.m_item.GetInstanceID() : 0);
            hash = hash * 31 + (recipe != null && recipe.m_enabled ? 1 : 0);
            hash = hash * 31 + (recipe?.m_amount ?? 0);
            hash = hash * 31 + (recipe?.m_resources?.Length ?? 0);
            hash = hash * 31 + (pair.CanCraft ? 1 : 0);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(GetVeiledRecipeDisplaySignature(pair));
            hash = hash * 31 + (item != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item) : 0);
            if (item?.m_shared == null)
            {
                return hash;
            }

            hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(GetItemPrefabName(item));
            hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(item.m_shared.m_name ?? "");
            hash = hash * 31 + item.m_quality;
            hash = hash * 31 + item.m_variant;
            hash = hash * 31 + item.m_stack;
            hash = hash * 31 + item.m_gridPos.x;
            hash = hash * 31 + item.m_gridPos.y;
            hash = hash * 31 + (item.m_equipped ? 1 : 0);
            return hash;
        }
    }

    private static bool UpdateCraftingRecipeView(InventoryGui gui)
    {
        if (gui.m_availableRecipes == null)
        {
            CraftingRecipes.View.Clear();
            CraftingRecipes.ViewIndexByOriginal.Clear();
            CraftingController.StoreRecipeViewSignature("");
            CraftingController.MarkRecipeGridLayoutDirty();
            return true;
        }

        CraftingTabAdapterState adapter = GetCraftingTabAdapterState(gui);
        string signature = GetCraftingRecipeViewSignature(gui, adapter);
        if (CraftingController.CanReuseRecipeView(signature))
        {
            return false;
        }

        CraftingRecipes.View.Clear();
        for (int i = 0; i < gui.m_availableRecipes.Count; i++)
        {
            InventoryGui.RecipeDataPair pair = gui.m_availableRecipes[i];
            if (pair.Recipe == null ||
                !ShouldIncludeRecipeInCraftingTabView(adapter, pair))
            {
                continue;
            }

            bool isVeiledRecipeMasked = IsVeiledRecipeMasked(pair);
            CraftingRecipes.View.Add(new CraftingRecipeViewEntry(
                i,
                pair,
                !adapter.IsRecycleNReclaim && IsFavoriteCraftingRecipe(pair),
                GetCraftingRecipeSortKey(pair),
                isVeiledRecipeMasked,
                IsVeiledRecipePreview(pair)));
        }

        CraftingRecipes.View.Sort((a, b) => CompareCraftingRecipeViewEntriesForAdapter(adapter, a, b));
        RebuildCraftingRecipeViewIndexCache();
        CraftingController.StoreRecipeViewSignature(signature);
        CraftingController.MarkRecipeGridLayoutDirty();
        return true;
    }

    private static void RebuildCraftingRecipeViewIndexCache()
    {
        CraftingRecipes.ViewIndexByOriginal.Clear();
        for (int i = 0; i < CraftingRecipes.View.Count; i++)
        {
            CraftingRecipes.ViewIndexByOriginal[CraftingRecipes.View[i].OriginalIndex] = i;
        }
    }

    private static string GetCraftingRecipeViewSignature(InventoryGui gui, CraftingTabAdapterState? adapter = null)
    {
        CraftingTabAdapterState currentAdapter = adapter ?? GetCraftingTabAdapterState(gui);
        string sortMode = _craftingRecipeSortMode?.Value.ToString() ?? "";
        return $"{GetCraftingRecipeListContextSignature(gui, currentAdapter)}|{_selectedCraftingGroupId}|{_craftingSearchQuery}|{sortMode}|{_craftingFavoritesVersion}|{_loadedCraftingFavoritesPlayerId}|{GetVeiledRecipeGroupingSignature()}";
    }

    private static string GetCraftingRecipeListContextSignature(InventoryGui gui, CraftingTabAdapterState? adapter = null)
    {
        int listId = gui.m_availableRecipes != null
            ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(gui.m_availableRecipes)
            : 0;
        int count = gui.m_availableRecipes?.Count ?? -1;
        CraftingTabAdapterState currentAdapter = adapter ?? GetCraftingTabAdapterState(gui);
        string recycleNReclaimSignature = currentAdapter.IsRecycleNReclaim
            ? $"{GetRecycleNReclaimContextSignature()}|{GetRecycleNReclaimRecipeListSignature(gui)}"
            : "";
        return $"list={listId}|count={count}|craft={IsCraftingCraftTabSelected(gui)}|upgrade={IsCraftingUpgradeTabSelected(gui)}|adapter={currentAdapter.Kind}|rnr={recycleNReclaimSignature}";
    }

    private static int GetSelectedCraftingRecipeIndexSafe(InventoryGui gui)
    {
        try
        {
            return gui.GetSelectedRecipeIndex(acceptOneLevelHigher: true);
        }
        catch
        {
            return -1;
        }
    }

    private static SortKey GetCraftingRecipeSortKey(InventoryGui.RecipeDataPair pair)
    {
        CraftingRecipePairCacheKey cacheKey = GetCraftingRecipePairCacheKey(pair);
        if (cacheKey.IsValid && CraftingRecipes.SortKeyCache.TryGetValue(cacheKey, out SortKey cached))
        {
            return cached;
        }

        SortKey key = CreateCraftingRecipeSortKey(pair);
        if (cacheKey.IsValid)
        {
            CraftingRecipes.SortKeyCache[cacheKey] = key;
        }

        return key;
    }

    private static int GetCraftingRecipeResourceTier(Recipe? recipe)
    {
        if (ResourceTierByToken.Count == 0)
        {
            return 0;
        }

        if (recipe?.m_resources == null)
        {
            return int.MinValue;
        }

        int tier = int.MinValue;
        foreach (Requirement requirement in recipe.m_resources)
        {
            if (requirement?.m_resItem == null || requirement.m_amount <= 0)
            {
                continue;
            }

            if (TryGetResourceTier(requirement.m_resItem, out int requirementTier))
            {
                tier = Mathf.Max(tier, requirementTier);
            }
        }

        return tier;
    }

    private static bool TryGetResourceTier(ItemDrop itemDrop, out int tier)
    {
        tier = 0;
        if (itemDrop == null || itemDrop.m_itemData?.m_shared == null)
        {
            return false;
        }

        foreach (string token in GetResourceTokens(itemDrop))
        {
            if (ResourceTierByToken.TryGetValue(token, out tier))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetResourceTokens(ItemDrop itemDrop)
    {
        string prefabName = CleanPrefabName(itemDrop.name);
        string itemPrefabName = GetItemPrefabName(itemDrop.m_itemData);
        string sharedName = itemDrop.m_itemData.m_shared.m_name ?? "";
        string localizedName = Localization.instance != null ? Localization.instance.Localize(sharedName) : sharedName;

        foreach (string value in new[] { prefabName, itemPrefabName, sharedName, StripLocalizationToken(sharedName), localizedName })
        {
            string token = NormalizeResourceToken(value);
            if (!string.IsNullOrWhiteSpace(token))
            {
                yield return token;
            }
        }
    }

    private static int GetItemPredefinedGroupRank(ItemData? item, string bigGroupId, int bigGroupRank)
    {
        if (item?.m_shared == null)
        {
            return int.MaxValue;
        }

        List<string> order = GetPredefinedGroupOrderForCraftingGroup(bigGroupId);
        for (int i = 0; i < order.Count; i++)
        {
            if (ItemMatchesPredefinedGroup(item, order[i]))
            {
                return i;
            }
        }

        return bigGroupRank >= 0 ? 1000 + bigGroupRank : int.MaxValue;
    }

    private static List<string> GetPredefinedGroupOrderForCraftingGroup(string bigGroupId)
    {
        List<string> order = new();
        void AddRange(string key)
        {
            if (!PredefinedGroupOrders.TryGetValue(NormalizeGroupId(key), out List<string> groupOrder))
            {
                return;
            }

            foreach (string id in groupOrder)
            {
                if (!order.Contains(id))
                {
                    order.Add(id);
                }
            }
        }

        AddRange(bigGroupId);
        AddRange("global");
        return order;
    }

    private static string GetCraftingRecipeBigGroupId(InventoryGui.RecipeDataPair pair)
    {
        for (int i = 0; i < CraftingRecipeGroupFilters.Count; i++)
        {
            CraftingRecipeGroupFilter filter = CraftingRecipeGroupFilters[i];
            if (filter.Id != "favorite" && RecipeMatchesCraftingGroup(pair, filter))
            {
                return filter.Id;
            }
        }

        return "";
    }

    private static int GetItemBigGroupRank(string bigGroupId)
    {
        if (string.IsNullOrWhiteSpace(bigGroupId))
        {
            return int.MaxValue;
        }

        string normalized = NormalizeGroupId(bigGroupId);
        for (int i = 0; i < CraftingRecipeGroupFilters.Count; i++)
        {
            CraftingRecipeGroupFilter filter = CraftingRecipeGroupFilters[i];
            if (filter.Id != "favorite" && string.Equals(NormalizeGroupId(filter.Id), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static string GetCraftingEquipmentSetKey(ItemData? item)
    {
        if (item?.m_shared == null || !IsArmorLikeItemType(item.m_shared.m_itemType))
        {
            return "";
        }

        string setName = item.m_shared.m_setName ?? "";
        if (string.IsNullOrWhiteSpace(setName))
        {
            setName = item.m_shared.m_setStatusEffect != null ? item.m_shared.m_setStatusEffect.m_name : "";
        }

        if (!string.IsNullOrWhiteSpace(setName))
        {
            return NormalizeSetKey(StripLocalizationToken(setName));
        }

        return DeriveEquipmentSetKeyFromName(GetItemPrefabName(item), item.m_shared.m_name);
    }

    private static string DeriveEquipmentSetKeyFromName(string prefabName, string sharedName)
    {
        string key = !string.IsNullOrWhiteSpace(prefabName) ? prefabName : StripLocalizationToken(sharedName);
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        foreach (string token in new[] { "Helmet", "Armor", "Chest", "Legs", "Leg", "Cape", "Shoulder", "Hood", "Tunic", "Pants", "Cuirass", "Greaves", "Robe" })
        {
            key = RemoveTokenIgnoreCase(key, token);
        }

        return NormalizeSetKey(key);
    }

    private static string RemoveTokenIgnoreCase(string value, string token)
    {
        int index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            value = value.Remove(index, token.Length);
            index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static string NormalizeSetKey(string value)
    {
        string normalized = NormalizeResourceToken(value);
        return string.IsNullOrWhiteSpace(normalized) ? "" : normalized;
    }

    private static int GetCraftingEquipmentSlotOrder(ItemData? item)
    {
        if (item?.m_shared == null)
        {
            return 99;
        }

        return item.m_shared.m_itemType switch
        {
            ItemType.Helmet => 0,
            ItemType.Chest => 1,
            ItemType.Legs => 2,
            ItemType.Shoulder => 3,
            ItemType.Utility => 4,
            ItemType.Trinket => 5,
            _ => 99
        };
    }

    private static bool IsArmorLikeItemType(ItemType itemType) =>
        itemType is ItemType.Helmet or ItemType.Chest or ItemType.Legs or ItemType.Shoulder or ItemType.Utility or ItemType.Trinket;

    private static void EnsureSelectedCraftingRecipeVisible(InventoryGui gui)
    {
        if (CraftingRecipes.View.Count == 0)
        {
            return;
        }

        int selectedIndex = gui.GetSelectedRecipeIndex();
        if (FindCraftingRecipeViewIndex(selectedIndex) >= 0)
        {
            return;
        }

        CraftingController.ClearHoveredRecipe();
        SetCraftingRecipeWithStoredVariant(gui, CraftingRecipes.View[0].OriginalIndex, center: false);
    }

    private static int FindCraftingRecipeViewIndex(int originalIndex)
    {
        return originalIndex >= 0 && CraftingRecipes.ViewIndexByOriginal.TryGetValue(originalIndex, out int index) ? index : -1;
    }

    private static bool TryGetCraftingRecipePair(InventoryGui gui, int originalIndex, out InventoryGui.RecipeDataPair pair)
    {
        pair = default;
        if (gui.m_availableRecipes == null || originalIndex < 0 || originalIndex >= gui.m_availableRecipes.Count)
        {
            return false;
        }

        pair = gui.m_availableRecipes[originalIndex];
        return pair.Recipe != null;
    }

    private static bool RecipeMatchesSelectedCraftingGroup(InventoryGui.RecipeDataPair pair)
    {
        if (string.IsNullOrEmpty(_selectedCraftingGroupId))
        {
            return true;
        }

        CraftingRecipeGroupFilter? filter = CraftingRecipeGroupFilters.FirstOrDefault(group => group.Id == _selectedCraftingGroupId);
        return filter != null && RecipeMatchesCraftingGroup(pair, filter);
    }

    private static bool RecipeMatchesCraftingGroup(InventoryGui.RecipeDataPair pair, CraftingRecipeGroupFilter filter)
    {
        if (filter.Id == "favorite")
        {
            return IsFavoriteCraftingRecipe(pair);
        }

        CraftingRecipePairCacheKey cacheKey = GetCraftingRecipePairCacheKey(pair);
        if (cacheKey.IsValid)
        {
            string filterId = NormalizeGroupId(filter.Id);
            if (CraftingRecipes.GroupMatchCache.TryGetValue(cacheKey, out Dictionary<string, bool> matches) &&
                matches.TryGetValue(filterId, out bool cached))
            {
                return cached;
            }

            bool result = RecipeMatchesCraftingGroupUncached(pair, filter);
            if (!CraftingRecipes.GroupMatchCache.TryGetValue(cacheKey, out matches))
            {
                matches = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                CraftingRecipes.GroupMatchCache[cacheKey] = matches;
            }

            matches[filterId] = result;
            return result;
        }

        return RecipeMatchesCraftingGroupUncached(pair, filter);
    }

    private static bool RecipeMatchesCraftingGroupUncached(InventoryGui.RecipeDataPair pair, CraftingRecipeGroupFilter filter)
    {
        ItemData? item = GetCraftingRecipeItemData(pair);
        return item != null && filter.Matches(item) && IsFirstMatchingCraftingGroup(item, filter);
    }

    private static bool IsFirstMatchingCraftingGroup(ItemData item, CraftingRecipeGroupFilter targetFilter)
    {
        foreach (CraftingRecipeGroupFilter filter in CraftingRecipeGroupFilters)
        {
            if (filter.Id == "favorite")
            {
                continue;
            }

            if (string.Equals(filter.Id, targetFilter.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (filter.Matches(item))
            {
                return false;
            }
        }

        return true;
    }

    private static ItemData? GetCraftingRecipeItemData(InventoryGui.RecipeDataPair pair)
    {
        if (pair.ItemData != null)
        {
            return pair.ItemData;
        }

        return pair.Recipe != null && pair.Recipe.m_item != null ? pair.Recipe.m_item.m_itemData : null;
    }

    private static CraftingRecipePairCacheKey GetCraftingRecipePairCacheKey(InventoryGui.RecipeDataPair pair)
    {
        ItemData? item = GetCraftingRecipeItemData(pair);
        string itemKey = item?.m_shared == null
            ? ""
            : $"{GetItemPrefabName(item)}|{item.m_shared.m_name}|{item.m_variant}|{item.m_quality}";
        return new CraftingRecipePairCacheKey(pair.Recipe, itemKey);
    }
}
