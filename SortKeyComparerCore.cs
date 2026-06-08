using System;

namespace InventorySlots;

internal static class SortKeyComparerCore
{
    public static int Compare(SortKey a, SortKey b, CraftingRecipeSortMode mode)
    {
        if (ShouldClusterEquipmentSets(mode) && a.ResourceTier == b.ResourceTier && a.HasSet && b.HasSet)
        {
            int setComparison = string.Compare(a.SetKey, b.SetKey, StringComparison.OrdinalIgnoreCase);
            if (setComparison != 0)
            {
                return setComparison;
            }

            int slotComparison = a.EquipmentSlotOrder.CompareTo(b.EquipmentSlotOrder);
            if (slotComparison != 0)
            {
                return slotComparison;
            }
        }

        int comparison = mode == CraftingRecipeSortMode.GroupThenTier
            ? CompareGroupThenTier(a, b)
            : CompareTierThenGroup(a, b);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = a.SetBucket.CompareTo(b.SetBucket);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = string.Compare(a.SetKey, b.SetKey, StringComparison.OrdinalIgnoreCase);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = a.EquipmentSlotOrder.CompareTo(b.EquipmentSlotOrder);
        if (comparison != 0)
        {
            return comparison;
        }

        return string.Compare(a.LocalizedName, b.LocalizedName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldClusterEquipmentSets(CraftingRecipeSortMode mode) =>
        mode == CraftingRecipeSortMode.TierThenGroup;

    private static int CompareTierThenGroup(SortKey a, SortKey b)
    {
        int comparison = b.ResourceTier.CompareTo(a.ResourceTier);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = a.BigGroupRank.CompareTo(b.BigGroupRank);
        if (comparison != 0)
        {
            return comparison;
        }

        return a.GroupRank.CompareTo(b.GroupRank);
    }

    private static int CompareGroupThenTier(SortKey a, SortKey b)
    {
        int comparison = a.BigGroupRank.CompareTo(b.BigGroupRank);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = a.GroupRank.CompareTo(b.GroupRank);
        if (comparison != 0)
        {
            return comparison;
        }

        return b.ResourceTier.CompareTo(a.ResourceTier);
    }
}
