using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static List<CraftingRecipeGroupFilter> CreateCraftingRecipeGroupFilters()
    {
        List<CraftingRecipeGroupFilter> filters = new()
        {
            new("favorite", "Favor", _ => false, iconPrefab: "piece_chest_treasure")
        };

        foreach (BuiltInItemGroupSection section in ItemGroupRegistry.Sections)
        {
            string sectionId = section.Id;
            filters.Add(new CraftingRecipeGroupFilter(
                sectionId,
                section.Label,
                item => ItemMatchesTopLevelGroup(item, sectionId),
                iconPrefab: section.IconPrefab));
        }

        return filters;
    }

}
