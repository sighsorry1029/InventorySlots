using System;

namespace InventorySlots;

internal enum CraftingRecipeSortMode
{
    TierThenGroup,
    GroupThenTier
}

internal sealed class SortKey
{
    public static readonly SortKey None = new(0, int.MaxValue, int.MaxValue, "", 99, "");

    public SortKey(int resourceTier, int groupRank, int bigGroupRank, string setKey, int equipmentSlotOrder, string localizedName)
    {
        ResourceTier = resourceTier;
        GroupRank = groupRank;
        BigGroupRank = bigGroupRank;
        SetKey = setKey;
        EquipmentSlotOrder = equipmentSlotOrder;
        LocalizedName = localizedName;
    }

    public int ResourceTier { get; }
    public int GroupRank { get; }
    public int BigGroupRank { get; }
    public string SetKey { get; }
    public bool HasSet => !string.IsNullOrWhiteSpace(SetKey);
    public int SetBucket => HasSet ? 0 : 1;
    public int EquipmentSlotOrder { get; }
    public string LocalizedName { get; }
}

internal readonly struct SortKeyComponents
{
    public SortKeyComponents(int resourceTier, int groupRank, int bigGroupRank, string setKey, int equipmentSlotOrder, string localizedName)
    {
        ResourceTier = resourceTier;
        GroupRank = groupRank;
        BigGroupRank = bigGroupRank;
        SetKey = setKey;
        EquipmentSlotOrder = equipmentSlotOrder;
        LocalizedName = localizedName;
    }

    public int ResourceTier { get; }
    public int GroupRank { get; }
    public int BigGroupRank { get; }
    public string SetKey { get; }
    public int EquipmentSlotOrder { get; }
    public string LocalizedName { get; }

    public SortKey ToSortKey() =>
        new(ResourceTier, GroupRank, BigGroupRank, SetKey, EquipmentSlotOrder, LocalizedName);
}
