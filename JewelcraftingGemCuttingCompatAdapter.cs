using System;
using System.Collections;
using System.Reflection;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class JewelcraftingGemCuttingApi
    {
        private readonly FieldInfo _gemUpgradeChancesField;

        private JewelcraftingGemCuttingApi(FieldInfo gemUpgradeChancesField)
        {
            _gemUpgradeChancesField = gemUpgradeChancesField;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingGemCuttingApi? api, out string detail)
        {
            api = null;
            Type? jewelcraftingType = assembly.GetType("Jewelcrafting.Jewelcrafting");
            FieldInfo? gemUpgradeChancesField = jewelcraftingType?.GetField("gemUpgradeChances", BindingFlags.Public | BindingFlags.Static);
            if (gemUpgradeChancesField == null)
            {
                detail = "Jewelcrafting.gemUpgradeChances was not found";
                return false;
            }

            api = new JewelcraftingGemCuttingApi(gemUpgradeChancesField);
            detail = "";
            return true;
        }

        public bool IsGemCuttingRecipe(Recipe recipe)
        {
            string itemName = recipe.m_item != null
                ? recipe.m_item.m_itemData.m_shared.m_name ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            try
            {
                return _gemUpgradeChancesField.GetValue(null) is IDictionary chances &&
                       chances.Contains(itemName);
            }
            catch
            {
                return false;
            }
        }
    }
}
