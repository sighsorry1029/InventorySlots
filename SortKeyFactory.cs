using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static SortKey CreateCraftingRecipeSortKey(InventoryGui.RecipeDataPair pair)
    {
        ItemData? item = GetCraftingRecipeItemData(pair);
        string bigGroupId = GetCraftingRecipeBigGroupId(pair);
        string localizedName = item?.m_shared != null ? GetLocalizedItemName(item) : GetCraftingRecipeDisplayName(pair);
        return CreateSortKey(
            item,
            GetCraftingRecipeResourceTier(pair.Recipe),
            bigGroupId,
            localizedName);
    }

    private static SortKey GetInventoryItemSortKey(ItemData item)
    {
        if (item?.m_shared == null)
        {
            return SortKey.None;
        }

        string bigGroupId = GetInventoryItemBigGroupId(item);
        return CreateSortKey(
            item,
            GetInventoryItemResourceTier(item),
            bigGroupId,
            GetLocalizedItemName(item));
    }

    private static SortKey CreateSortKey(ItemData? item, int resourceTier, string bigGroupId, string localizedName)
    {
        int bigGroupRank = GetItemBigGroupRank(bigGroupId);
        int groupRank = GetItemPredefinedGroupRank(item, bigGroupId, bigGroupRank);
        return new SortKey(
            resourceTier,
            groupRank,
            bigGroupRank,
            GetCraftingEquipmentSetKey(item),
            GetCraftingEquipmentSlotOrder(item),
            localizedName);
    }
}
