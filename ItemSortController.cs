using System;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static class ItemSortController
    {
        public static void EnsureRecipeOutputLookupCache(InventorySortRuntimeState state)
        {
            string signature = GetRecipeOutputLookupSignature();
            if (string.Equals(state.RecipeOutputLookupSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            state.RecipeOutputLookupCache.Clear();
            state.RecipeOutputLookupSignature = signature;

            if (ObjectDB.instance?.m_recipes == null)
            {
                return;
            }

            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                AddRecipeOutputLookup(recipe);
            }
        }

        public static void ClearCaches(InventorySortRuntimeState state)
        {
            state.RecipeOutputLookupCache.Clear();
            state.RecipeOutputLookupSignature = "";
        }
    }
}
