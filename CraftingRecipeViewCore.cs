namespace InventorySlots;

internal static class CraftingRecipeViewCore
{
    public static int CompareFavoritesOnly(
        bool aIsFavorite,
        bool bIsFavorite,
        bool aCanCraft,
        bool bCanCraft,
        int aOriginalIndex,
        int bOriginalIndex)
    {
        int prefix = CompareFavoriteAndCraftable(aIsFavorite, bIsFavorite, aCanCraft, bCanCraft);
        return prefix != 0 ? prefix : aOriginalIndex.CompareTo(bOriginalIndex);
    }

    public static int CompareWithSortKey(
        bool aIsFavorite,
        bool bIsFavorite,
        bool aCanCraft,
        bool bCanCraft,
        SortKey aSortKey,
        SortKey bSortKey,
        int aOriginalIndex,
        int bOriginalIndex,
        CraftingRecipeSortMode mode)
    {
        int prefix = CompareFavoriteAndCraftable(aIsFavorite, bIsFavorite, aCanCraft, bCanCraft);
        if (prefix != 0)
        {
            return prefix;
        }

        int sortComparison = SortKeyComparerCore.Compare(aSortKey, bSortKey, mode);
        return sortComparison != 0 ? sortComparison : aOriginalIndex.CompareTo(bOriginalIndex);
    }

    private static int CompareFavoriteAndCraftable(bool aIsFavorite, bool bIsFavorite, bool aCanCraft, bool bCanCraft)
    {
        if (aIsFavorite != bIsFavorite)
        {
            return aIsFavorite ? -1 : 1;
        }

        if (aCanCraft != bCanCraft)
        {
            return aCanCraft ? -1 : 1;
        }

        return 0;
    }
}
