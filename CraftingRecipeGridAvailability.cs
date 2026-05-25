namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static int GetCraftingRecipeGridAvailabilityHash(InventoryGui gui, int pageStart)
    {
        unchecked
        {
            int hash = 17;
            int capacity = GetCraftingRecipeGridCapacity();
            for (int slotIndex = 0; slotIndex < capacity; slotIndex++)
            {
                int viewIndex = pageStart + slotIndex;
                if (viewIndex < 0 || viewIndex >= CraftingRecipes.View.Count)
                {
                    hash = hash * 31 - 1;
                    continue;
                }

                InventoryGui.RecipeDataPair pair = CraftingRecipes.View[viewIndex].Pair;
                int originalIndex = CraftingRecipes.View[viewIndex].OriginalIndex;
                hash = hash * 31 + originalIndex;
                hash = hash * 31 + (pair.Recipe != null && pair.Recipe.m_enabled ? 1 : 0);
                hash = hash * 31 + (pair.CanCraft ? 1 : 0);
                hash = hash * 31 + (IsCraftingRecipeActionAvailable(gui, pair, originalIndex) ? 1 : 0);
                if (IsRecycleNReclaimReclaimTabActive(gui) && TryGetRecycleNReclaimRecyclingImpedimentCount(originalIndex, out int impediments))
                {
                    hash = hash * 31 + impediments;
                }
            }

            return hash;
        }
    }
}
