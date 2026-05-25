using System;
using System.Collections.Generic;
using System.Text;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static string AppendItemRequiresSkillLevelCraftingTooltip(InventoryGui.RecipeDataPair pair, string tooltip)
    {
        string extra = GetItemRequiresSkillLevelCraftingTooltipText(pair);
        return AppendDistinctTooltipLines(tooltip, extra);
    }

    private static string GetItemRequiresSkillLevelCraftingTooltipSignature(InventoryGui.RecipeDataPair pair) =>
        GetItemRequiresSkillLevelCraftingTooltipText(pair);

    private static string GetItemRequiresSkillLevelCraftingTooltipText(InventoryGui.RecipeDataPair pair)
    {
        if (!TryGetItemRequiresSkillLevelApi(out ItemRequiresSkillLevelApi? api) || api == null)
        {
            return "";
        }

        ItemData? item = pair.ItemData ?? pair.Recipe?.m_item?.m_itemData;
        string prefabName = GetItemRequiresSkillLevelRecipePrefabName(pair);
        return api.GetTooltipText(item, prefabName, includeEquip: true, includeCraft: pair.Recipe != null);
    }

    private static string GetItemRequiresSkillLevelRecipePrefabName(InventoryGui.RecipeDataPair pair)
    {
        if (pair.Recipe?.m_item == null)
        {
            return "";
        }

        return CleanPrefabName(pair.Recipe.m_item.gameObject.name);
    }

    private static string AppendDistinctTooltipLines(string tooltip, string extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
        {
            return tooltip;
        }

        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in SplitTooltipLines(tooltip))
        {
            string plain = StripRichText(line).Trim();
            if (!string.IsNullOrWhiteSpace(plain))
            {
                existing.Add(plain);
            }
        }

        StringBuilder builder = new(tooltip ?? "");
        foreach (string line in SplitTooltipLines(extra))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            string plain = StripRichText(trimmed).Trim();
            if (string.IsNullOrWhiteSpace(plain) || !existing.Add(plain))
            {
                continue;
            }

            if (builder.Length > 0 && builder[builder.Length - 1] != '\n')
            {
                builder.Append('\n');
            }

            builder.Append(trimmed);
        }

        return builder.ToString();
    }

    private static bool TryGetItemRequiresSkillLevelApi(out ItemRequiresSkillLevelApi? api)
    {
        const string capability = "ItemRequiresSkillLevel tooltip";
        return TryGetCompatApi(
            ItemRequiresSkillLevelGuid,
            capability,
            CompatRuntime.ItemRequiresSkillLevel,
            ItemRequiresSkillLevelApi.TryCreate,
            "ItemRequiresSkillLevel tooltip compatibility disabled",
            out api);
    }
}
