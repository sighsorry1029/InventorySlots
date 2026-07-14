using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace ColoredFork;

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
internal static class InventoryGridFoodForkPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        Inventory? ___m_inventory,
        List<InventoryGrid.Element>? ___m_elements,
        Color ___m_foodHealthColor,
        Color ___m_foodStaminaColor,
        Color ___m_foodEitrColor)
    {
        if (___m_inventory == null || ___m_elements == null)
        {
            return;
        }

        int width = ___m_inventory.GetWidth();
        if (width <= 0)
        {
            return;
        }

        foreach (ItemData item in ___m_inventory.GetAllItems())
        {
            if (item == null || !TryGetDominantFoodStat(item, out FoodStat stat))
            {
                continue;
            }

            int index = item.m_gridPos.y * width + item.m_gridPos.x;
            if (index < 0 || index >= ___m_elements.Count)
            {
                continue;
            }

            InventoryGrid.Element element = ___m_elements[index];
            if (element?.m_food == null)
            {
                continue;
            }

            element.m_food.enabled = true;
            element.m_food.color = stat switch
            {
                FoodStat.Health => ___m_foodHealthColor,
                FoodStat.Stamina => ___m_foodStaminaColor,
                FoodStat.Eitr => ___m_foodEitrColor,
                _ => element.m_food.color
            };
        }
    }

    private static bool TryGetDominantFoodStat(ItemData item, out FoodStat stat)
    {
        ItemData.SharedData shared = item.m_shared.m_appendToolTip?.m_itemData?.m_shared ?? item.m_shared;
        return FoodStatCore.TryGetDominant(shared.m_food, shared.m_foodStamina, shared.m_foodEitr, out stat);
    }
}
