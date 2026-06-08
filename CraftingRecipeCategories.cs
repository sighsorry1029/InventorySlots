using System;
using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;
using AnimationState = ItemDrop.ItemData.AnimationState;
using ItemType = ItemDrop.ItemData.ItemType;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static List<CraftingRecipeGroupFilter> CreateCraftingRecipeGroupFilters()
    {
        List<CraftingRecipeGroupFilter> filters = new()
        {
            new("favorite", "Favorite", "Favor", _ => false, iconPrefab: "piece_chest_treasure")
        };

        foreach (BuiltInItemGroupSection section in ItemGroupRegistry.Sections)
        {
            filters.Add(new CraftingRecipeGroupFilter(section.Id, section.Tab, section.Label, GetCraftingRecipeGroupMatcher(section.Id), iconPrefab: section.IconPrefab));
        }

        return filters;
    }

    private static Func<ItemData, bool> GetCraftingRecipeGroupMatcher(string sectionId) =>
        sectionId switch
        {
            "melee" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "ranged" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "magic" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "tool" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "armor" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "food" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "consumable" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "meadbase" => item => ItemMatchesTopLevelGroup(item, sectionId),
            "misc" => item => ItemMatchesTopLevelGroup(item, sectionId),
            _ => throw new InvalidOperationException($"Unknown built-in item group section '{sectionId}'.")
        };

}
