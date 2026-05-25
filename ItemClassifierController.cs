using System;
using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static class ItemClassifierController
    {
        public static string GetInventoryItemBigGroupId(
            ItemClassifierRuntimeState state,
            ItemData item,
            IReadOnlyList<CraftingRecipeGroupFilter> groupFilters)
        {
            if (item?.m_shared == null)
            {
                return "";
            }

            ItemClassification classification = GetItemClassification(state, item);
            if (classification.BigGroupResolved)
            {
                return classification.BigGroupId;
            }

            for (int i = 0; i < groupFilters.Count; i++)
            {
                CraftingRecipeGroupFilter filter = groupFilters[i];
                if (filter.Id != "favorite" && filter.Matches(item))
                {
                    classification.BigGroupId = filter.Id;
                    classification.BigGroupResolved = true;
                    return classification.BigGroupId;
                }
            }

            classification.BigGroupId = "";
            classification.BigGroupResolved = true;
            return classification.BigGroupId;
        }

        public static bool ItemMatchesBuiltInPredefinedGroup(ItemClassifierRuntimeState state, ItemData item, string groupId)
        {
            string id = NormalizeGroupId(groupId);
            if (item?.m_shared == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            ItemClassification classification = GetItemClassification(state, item);
            if (classification.BuiltInGroupMatches.TryGetValue(id, out bool cached))
            {
                return cached;
            }

            bool result = ItemMatchesBuiltInPredefinedGroupUncached(item, id);
            classification.BuiltInGroupMatches[id] = result;
            return result;
        }

        public static void ClearCaches(ItemClassifierRuntimeState state)
        {
            state.Cache.Clear();
            state.AppliedVersion = state.Version;
        }

        private static ItemClassification GetItemClassification(ItemClassifierRuntimeState state, ItemData item)
        {
            EnsureCacheFresh(state);
            string itemKey = GetItemClassifierCacheKey(item);
            if (state.Cache.TryGetValue(itemKey, out ItemClassification cached))
            {
                return cached;
            }

            if (state.Cache.Count >= MaxItemClassifierCacheItems)
            {
                state.Cache.Clear();
            }

            ItemClassification classification = new();
            state.Cache[itemKey] = classification;
            return classification;
        }

        private static void EnsureCacheFresh(ItemClassifierRuntimeState state)
        {
            int version = GetItemClassifierCacheVersion();
            if (state.AppliedVersion == version)
            {
                return;
            }

            state.Cache.Clear();
            state.AppliedVersion = version;
        }
    }
}
