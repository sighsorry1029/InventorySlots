using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly List<CookingStation> StationTokenCookingStations = new();
    private static readonly List<Fermenter> StationTokenFermenters = new();

    private static void RebuildStationInputTokens(bool force = false)
    {
        ObjectDB? objectDb = ObjectDB.instance;
        int objectDbItemCount = !IsUnityNull(objectDb) && objectDb!.m_items != null ? objectDb.m_items.Count : -1;
        int recipeCount = !IsUnityNull(objectDb) && objectDb!.m_recipes != null ? objectDb.m_recipes.Count : -1;
        int prefabCount = !IsUnityNull(ZNetScene.instance) && ZNetScene.instance.m_prefabs != null ? ZNetScene.instance.m_prefabs.Count : -1;

        if (IsUnityNull(ZNetScene.instance) || ZNetScene.instance.m_prefabs == null)
        {
            bool tokensCleared = CookingStationInputTokens.Count > 0 ||
                                 CraftingRecipeFoodInputTokens.Count > 0 ||
                                 CookingStationFoodInputTokens.Count > 0 ||
                                 FermenterInputTokens.Count > 0 ||
                                 FermenterOutputTokens.Count > 0 ||
                                 FermenterFoodInputTokens.Count > 0;
            StationTokenCookingStations.Clear();
            StationTokenFermenters.Clear();
            CookingStationInputTokens.Clear();
            CraftingRecipeFoodInputTokens.Clear();
            CookingStationFoodInputTokens.Clear();
            FermenterInputTokens.Clear();
            FermenterOutputTokens.Clear();
            FermenterFoodInputTokens.Clear();
            _stationInputTokensInitialized = false;
            _cachedStationInputObjectDbItemCount = objectDbItemCount;
            _cachedStationInputPrefabCount = prefabCount;
            _cachedStationInputRecipeCount = recipeCount;
            if (tokensCleared)
            {
                ClearCraftingRecipeCaches();
            }

            return;
        }

        bool stationSourceChanged = force ||
                                    !_stationInputTokensInitialized ||
                                    _cachedStationInputObjectDbItemCount != objectDbItemCount ||
                                    _cachedStationInputPrefabCount != prefabCount ||
                                    _cachedStationInputRecipeCount != recipeCount;
        if (!stationSourceChanged)
        {
            return;
        }

        RebuildStationComponentCache();
        _cachedStationInputObjectDbItemCount = objectDbItemCount;
        _cachedStationInputPrefabCount = prefabCount;
        _cachedStationInputRecipeCount = recipeCount;

        HashSet<string> cookingStationInputTokens = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> craftingRecipeFoodInputTokens = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> cookingStationFoodInputTokens = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> fermenterInputTokens = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> fermenterOutputTokens = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> fermenterFoodInputTokens = new(StringComparer.OrdinalIgnoreCase);
        CollectStationConversionTokens(
            cookingStationInputTokens,
            craftingRecipeFoodInputTokens,
            cookingStationFoodInputTokens,
            fermenterInputTokens,
            fermenterOutputTokens,
            fermenterFoodInputTokens);

        bool tokensChanged = !CookingStationInputTokens.SetEquals(cookingStationInputTokens) ||
                             !CraftingRecipeFoodInputTokens.SetEquals(craftingRecipeFoodInputTokens) ||
                             !CookingStationFoodInputTokens.SetEquals(cookingStationFoodInputTokens) ||
                             !FermenterInputTokens.SetEquals(fermenterInputTokens) ||
                             !FermenterOutputTokens.SetEquals(fermenterOutputTokens) ||
                             !FermenterFoodInputTokens.SetEquals(fermenterFoodInputTokens);

        _stationInputTokensInitialized = true;
        if (!tokensChanged)
        {
            return;
        }

        CookingStationInputTokens.Clear();
        CookingStationInputTokens.UnionWith(cookingStationInputTokens);
        CraftingRecipeFoodInputTokens.Clear();
        CraftingRecipeFoodInputTokens.UnionWith(craftingRecipeFoodInputTokens);
        CookingStationFoodInputTokens.Clear();
        CookingStationFoodInputTokens.UnionWith(cookingStationFoodInputTokens);
        FermenterInputTokens.Clear();
        FermenterInputTokens.UnionWith(fermenterInputTokens);
        FermenterOutputTokens.Clear();
        FermenterOutputTokens.UnionWith(fermenterOutputTokens);
        FermenterFoodInputTokens.Clear();
        FermenterFoodInputTokens.UnionWith(fermenterFoodInputTokens);
        ClearCraftingRecipeCaches();
    }

    private static void RebuildStationComponentCache()
    {
        StationTokenCookingStations.Clear();
        StationTokenFermenters.Clear();
        if (IsUnityNull(ZNetScene.instance) || ZNetScene.instance.m_prefabs == null)
        {
            return;
        }

        foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
        {
            if (IsUnityNull(prefab))
            {
                continue;
            }

            foreach (CookingStation station in prefab.GetComponentsInChildren<CookingStation>(includeInactive: true))
            {
                if (!IsUnityNull(station))
                {
                    StationTokenCookingStations.Add(station);
                }
            }

            foreach (Fermenter station in prefab.GetComponentsInChildren<Fermenter>(includeInactive: true))
            {
                if (!IsUnityNull(station))
                {
                    StationTokenFermenters.Add(station);
                }
            }
        }
    }

    private static void CollectStationConversionTokens(
        HashSet<string> cookingStationInputTokens,
        HashSet<string> craftingRecipeFoodInputTokens,
        HashSet<string> cookingStationFoodInputTokens,
        HashSet<string> fermenterInputTokens,
        HashSet<string> fermenterOutputTokens,
        HashSet<string> fermenterFoodInputTokens)
    {
        CollectCraftingRecipeFoodInputTokens(craftingRecipeFoodInputTokens);

        for (int i = StationTokenCookingStations.Count - 1; i >= 0; i--)
        {
            CookingStation station = StationTokenCookingStations[i];
            if (IsUnityNull(station))
            {
                StationTokenCookingStations.RemoveAt(i);
                continue;
            }

            AddStationConversionItemsWithFoodInputs(
                station,
                cookingStationInputTokens,
                null,
                cookingStationFoodInputTokens);
        }

        for (int i = StationTokenFermenters.Count - 1; i >= 0; i--)
        {
            Fermenter station = StationTokenFermenters[i];
            if (IsUnityNull(station))
            {
                StationTokenFermenters.RemoveAt(i);
                continue;
            }

            AddStationConversionItemsWithFoodInputs(
                station,
                fermenterInputTokens,
                fermenterOutputTokens,
                fermenterFoodInputTokens);
        }
    }

    private static void CollectCraftingRecipeFoodInputTokens(HashSet<string> foodInputTokens)
    {
        ObjectDB? objectDb = ObjectDB.instance;
        if (IsUnityNull(objectDb) || objectDb!.m_recipes == null)
        {
            return;
        }

        foreach (Recipe recipe in objectDb.m_recipes)
        {
            if (recipe == null || recipe.m_resources == null || !IsFoodStatOutput(recipe.m_item))
            {
                continue;
            }

            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                AddItemIdentityTokens(foodInputTokens, requirement.m_resItem);
            }
        }
    }

    private static void AddStationConversionItemsWithFoodInputs(
        Component? station,
        HashSet<string>? inputTokens,
        HashSet<string>? outputTokens,
        HashSet<string>? foodInputTokens)
    {
        if (IsUnityNull(station))
        {
            return;
        }

        FieldInfo? conversionField = GetConversionField(station!.GetType(), "m_conversion", "conversion", "conversions", "m_conversions");
        if (conversionField == null)
        {
            return;
        }

        object? value;
        try
        {
            value = conversionField.GetValue(station);
        }
        catch
        {
            return;
        }

        if (value is not System.Collections.IEnumerable conversions || value is string)
        {
            return;
        }

        foreach (object conversion in conversions)
        {
            try
            {
                ItemDrop? input = GetStationConversionItemDrop(conversion, "m_from", "from", "m_input", "input");
                ItemDrop? output = GetStationConversionItemDrop(conversion, "m_to", "to", "m_output", "output", "m_result", "result");
                AddItemIdentityTokens(inputTokens, input);
                AddItemIdentityTokens(outputTokens, output);
                if (foodInputTokens != null && IsFoodStatOutput(output))
                {
                    AddItemIdentityTokens(foodInputTokens, input);
                }
            }
            catch
            {
                // Some modded station conversion objects expose unusual fields; skip those entries only.
            }
        }
    }

    private static ItemDrop? GetStationConversionItemDrop(object? conversion, params string[] fieldNames)
    {
        if (conversion == null)
        {
            return null;
        }

        FieldInfo? itemField = GetConversionField(conversion.GetType(), fieldNames);
        if (itemField == null)
        {
            return null;
        }

        return TryGetItemDrop(itemField.GetValue(conversion));
    }

    private static bool IsFoodStatOutput(ItemDrop? itemDrop)
    {
        ItemDrop.ItemData? item = itemDrop?.m_itemData;
        return item?.m_shared != null &&
               (GetFoodHealth(item) > 0f || GetFoodStamina(item) > 0f || GetFoodEitr(item) > 0f);
    }

    private static FieldInfo? GetConversionField(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    private static ItemDrop? TryGetItemDrop(object? value)
    {
        switch (value)
        {
            case ItemDrop itemDrop:
                return itemDrop;
            case GameObject gameObject:
                return gameObject.GetComponent<ItemDrop>();
            case Component component:
                return component.GetComponent<ItemDrop>();
            default:
                return null;
        }
    }

    private static void AddItemIdentityTokens(HashSet<string>? targetTokens, ItemDrop? itemDrop)
    {
        if (targetTokens == null || itemDrop == null || itemDrop.m_itemData?.m_shared == null)
        {
            return;
        }

        AddStationInputToken(targetTokens, itemDrop.name);
        AddStationInputToken(targetTokens, itemDrop.m_itemData.m_dropPrefab != null ? itemDrop.m_itemData.m_dropPrefab.name : "");
        AddExactStationInputToken(targetTokens, itemDrop.m_itemData.m_shared.m_name);
    }

    private static void AddStationInputToken(HashSet<string> targetTokens, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        string clean = CleanPrefabName(token!.Trim());
        if (!string.IsNullOrWhiteSpace(clean))
        {
            targetTokens.Add(clean);
        }

        string stripped = StripLocalizationToken(clean);
        if (!string.IsNullOrWhiteSpace(stripped))
        {
            targetTokens.Add(stripped);
        }

        string normalized = NormalizeResourceToken(clean);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            targetTokens.Add(normalized);
        }
    }

    private static void AddExactStationInputToken(HashSet<string> targetTokens, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        string clean = CleanPrefabName(token!.Trim());
        if (!string.IsNullOrWhiteSpace(clean))
        {
            targetTokens.Add(clean);
        }
    }
}
