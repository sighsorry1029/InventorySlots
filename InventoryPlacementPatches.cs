using System;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

[HarmonyPatch(typeof(Inventory), "FindEmptySlot")]
internal static class InventoryFindEmptySlotPatch
{
    private static bool Prefix(Inventory __instance, ref Vector2i __result)
    {
        return InventorySlotsPlugin.TryOverrideFindEmptySlot(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Inventory), "FindFreeStackItem")]
internal static class InventoryFindFreeStackItemPatch
{
    private static bool Prefix(Inventory __instance, string name, int quality, float worldLevel, ref ItemData? __result)
    {
        return InventorySlotsPlugin.TryOverrideFindFreeStackItem(__instance, name, quality, worldLevel, ref __result);
    }
}

[HarmonyPatch(typeof(Inventory), "GetEmptySlots")]
internal static class InventoryGetEmptySlotsPatch
{
    private static bool Prefix(Inventory __instance, ref int __result)
    {
        return InventorySlotsPlugin.TryOverrideGetEmptySlots(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Inventory), "HaveEmptySlot")]
internal static class InventoryHaveEmptySlotPatch
{
    private static bool Prefix(Inventory __instance, ref bool __result)
    {
        return InventorySlotsPlugin.TryOverrideHaveEmptySlot(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Inventory), "CanAddItem", typeof(ItemData), typeof(int))]
internal static class InventoryCanAddItemPatch
{
    private static void Postfix(Inventory __instance, ItemData item, int stack, ref bool __result)
    {
        InventorySlotsPlugin.OnInventoryCanAddItem(__instance, item, stack, ref __result);
    }
}

[HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemData))]
internal static class InventoryAddItemDataPatch
{
    private static bool Prefix(Inventory __instance, ItemData item, ref bool __result, out bool __state)
    {
        __state = InventorySlotsPlugin.BeginInventoryAddItemDataStackLookup(item);
        bool runOriginal = InventorySlotsPlugin.TryPreserveLoadedSlotTailItem(__instance, item, ref __result);
        if (!runOriginal)
        {
            InventorySlotsPlugin.EndInventoryAddItemDataStackLookup(__state);
            __state = false;
        }

        return runOriginal;
    }

    private static void Postfix(Inventory __instance, ItemData item, ref bool __result)
    {
        InventorySlotsPlugin.OnInventoryAddItemData(__instance, item, ref __result);
    }

    private static Exception? Finalizer(bool __state, Exception __exception)
    {
        InventorySlotsPlugin.EndInventoryAddItemDataStackLookup(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemData), typeof(int), typeof(int), typeof(int))]
internal static class InventoryAddItemXyPatch
{
    private static bool Prefix(Inventory __instance, ref bool __result, ItemData item, ref int x, ref int y)
    {
        return InventorySlotsPlugin.TryValidatePlayerInventoryInsert(__instance, item, ref x, ref y, ref __result);
    }

    private static void Postfix(Inventory __instance, ItemData item, int x, int y, bool __result)
    {
        InventorySlotsPlugin.OnPlayerInventoryItemPlaced(__instance, item, new Vector2i(x, y), __result);
    }
}

[HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemData), typeof(Vector2i))]
internal static class InventoryAddItemPosPatch
{
    private static bool Prefix(Inventory __instance, ref bool __result, ItemData item, ref Vector2i pos)
    {
        return InventorySlotsPlugin.TryValidatePlayerInventoryInsert(__instance, item, ref pos, ref __result);
    }

    private static void Postfix(Inventory __instance, ItemData item, Vector2i pos, bool __result)
    {
        InventorySlotsPlugin.OnPlayerInventoryItemPlaced(__instance, item, pos, __result);
    }
}

[HarmonyPatch(typeof(Inventory), "MoveItemToThis", typeof(Inventory), typeof(ItemData), typeof(int), typeof(int), typeof(int))]
internal static class InventoryMoveItemToThisPatch
{
    private static bool Prefix(Inventory __instance, ref bool __result, Inventory fromInventory, ItemData item, int amount, ref int x, ref int y)
    {
        return InventorySlotsPlugin.TryValidatePlayerInventoryMoveItemToThis(__instance, ref __result, item, ref x, ref y);
    }

    private static void Postfix(Inventory __instance, bool __result, ItemData item, int x, int y)
    {
        InventorySlotsPlugin.OnPlayerInventoryItemPlaced(__instance, item, new Vector2i(x, y), __result);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "DropItem")]
internal static class InventoryGridDropItemPatch
{
    private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemData item, Vector2i pos)
    {
        return InventorySlotsPlugin.ShouldAllowInventoryGridDropItem(__instance, item, pos);
    }
}
