using System;
using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static int CompareItemsForSort(ItemData a, ItemData b)
    {
        return CompareItemsForSort(a, b, GetInventoryItemSortKey(a), GetInventoryItemSortKey(b), 0, 0);
    }

    private static int CompareItemsForSort(ItemData a, ItemData b, SortKey aKey, SortKey bKey, int aOriginalIndex, int bOriginalIndex)
    {
        CraftingRecipeSortMode mode = _inventorySortMode?.Value ?? CraftingRecipeSortMode.GroupThenTier;
        int comparison = SortKeyComparerCore.Compare(aKey, bKey, mode);

        if (comparison == 0)
        {
            comparison = -a.m_quality.CompareTo(b.m_quality);
        }

        if (comparison == 0)
        {
            comparison = -a.m_stack.CompareTo(b.m_stack);
        }

        if (comparison == 0)
        {
            comparison = aOriginalIndex.CompareTo(bOriginalIndex);
        }

        return comparison;
    }
    private static int GetInventoryItemResourceTier(ItemData item)
    {
        if (ResourceTierByToken.Count == 0)
        {
            return 0;
        }

        int tier = int.MinValue;
        foreach (string token in GetItemIdentityTokens(item))
        {
            string normalized = NormalizeResourceToken(token);
            if (ResourceTierByToken.TryGetValue(normalized, out int tokenTier))
            {
                tier = Mathf.Max(tier, tokenTier);
            }
        }

        Recipe? recipe = FindCraftingRecipeForItem(item);
        if (recipe != null)
        {
            tier = Mathf.Max(tier, GetCraftingRecipeResourceTier(recipe));
        }

        return tier;
    }

    private static Recipe? FindCraftingRecipeForItem(ItemData item)
    {
        if (item?.m_shared == null || ObjectDB.instance?.m_recipes == null)
        {
            return null;
        }

        ItemSortController.EnsureRecipeOutputLookupCache(InventorySort);
        foreach (string token in GetItemRecipeLookupTokens(item))
        {
            if (InventorySort.RecipeOutputLookupCache.TryGetValue(token, out Recipe recipe))
            {
                return recipe;
            }
        }

        return null;
    }

    private static string GetRecipeOutputLookupSignature()
    {
        ObjectDB? objectDb = ObjectDB.instance;
        if (IsUnityNull(objectDb) || objectDb!.m_recipes == null)
        {
            return "none";
        }

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + objectDb.GetInstanceID();
            hash = hash * 31 + objectDb.m_recipes.Count;
            foreach (Recipe recipe in objectDb.m_recipes)
            {
                if (recipe == null)
                {
                    continue;
                }

                hash = hash * 31 + recipe.GetInstanceID();
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(recipe.name ?? "");
                hash = hash * 31 + (recipe.m_item != null ? recipe.m_item.GetInstanceID() : 0);
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(recipe.m_item != null ? recipe.m_item.name ?? "" : "");
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(recipe.m_item?.m_itemData?.m_shared?.m_name ?? "");
            }

            return hash.ToString();
        }
    }

    private static void AddRecipeOutputLookup(Recipe? recipe)
    {
        if (recipe?.m_item == null || recipe.m_item.m_itemData?.m_shared == null)
        {
            return;
        }

        ItemData output = recipe.m_item.m_itemData;
        AddRecipeOutputLookupToken(InventorySort, "prefab", GetItemPrefabName(output), recipe);
        AddRecipeOutputLookupToken(InventorySort, "prefab", CleanPrefabName(recipe.m_item.name), recipe);
        AddRecipeOutputLookupToken(InventorySort, "name", output.m_shared.m_name, recipe);
    }

    private static void AddRecipeOutputLookupToken(InventorySortRuntimeState state, string kind, string? token, Recipe recipe)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        string clean = CleanPrefabName(token!);
        string key = GetRecipeOutputLookupKey(kind, clean);
        if (!string.IsNullOrWhiteSpace(clean) && !state.RecipeOutputLookupCache.ContainsKey(key))
        {
            state.RecipeOutputLookupCache[key] = recipe;
        }
    }

    private static IEnumerable<string> GetItemRecipeLookupTokens(ItemData item)
    {
        string itemPrefab = GetItemPrefabName(item);
        string sharedName = item.m_shared.m_name ?? "";
        if (!string.IsNullOrWhiteSpace(itemPrefab))
        {
            yield return GetRecipeOutputLookupKey("prefab", itemPrefab);
        }

        if (!string.IsNullOrWhiteSpace(sharedName))
        {
            yield return GetRecipeOutputLookupKey("name", sharedName);
        }
    }

    private static string GetRecipeOutputLookupKey(string kind, string token) => $"{kind}:{CleanPrefabName(token)}";

    private static void ClearInventorySortCaches()
    {
        ItemSortController.ClearCaches(InventorySort);
    }

    private static string GetLocalizedItemName(ItemData item)
    {
        string name = item?.m_shared?.m_name ?? "";
        return Localization.instance != null ? Localization.instance.Localize(name) : name;
    }
}
