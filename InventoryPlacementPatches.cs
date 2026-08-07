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
    private static bool Prefix(Inventory __instance, ItemData item, int stack, ref bool __result)
    {
        return InventorySlotsPlugin.TryOverrideCanAddItem(__instance, item, stack, ref __result);
    }
}

[HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemData))]
internal static class InventoryAddItemDataPatch
{
    private static bool Prefix(
        Inventory __instance,
        ItemData item,
        ref bool __result,
        out InventoryAddItemStackMetadataState? __state)
    {
        __state = null;
        if (!InventorySlotsPlugin.TryValidatePlayerInventoryLimit(__instance, item, item.m_stack, ref __result))
        {
            return false;
        }

        __state = InventorySlotsPlugin.BeginAutomaticStackMetadataMerge(
            __instance,
            item);
        bool runOriginal = InventorySlotsPlugin.TryPreserveLoadedSlotTailItem(__instance, item, ref __result);
        if (!runOriginal)
        {
            InventorySlotsPlugin.EndAutomaticStackMetadataMerge(__state);
        }

        return runOriginal;
    }

    private static void Postfix(
        Inventory __instance,
        ItemData item,
        ref bool __result,
        InventoryAddItemStackMetadataState? __state)
    {
        InventorySlotsPlugin.CompleteAutomaticStackMetadataMerge(
            __instance,
            __state);
        InventorySlotsPlugin.OnInventoryAddItemData(__instance, item, ref __result);
    }

    private static Exception? Finalizer(
        InventoryAddItemStackMetadataState? __state,
        Exception __exception)
    {
        InventorySlotsPlugin.EndAutomaticStackMetadataMerge(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemData), typeof(int), typeof(int), typeof(int))]
internal static class InventoryAddItemXyPatch
{
    private static bool Prefix(
        Inventory __instance,
        ref bool __result,
        ItemData item,
        int amount,
        ref int x,
        ref int y,
        out InventoryStackMetadataMergeState? __state)
    {
        __state = null;
        int requestedAmount = Math.Min(Math.Max(0, amount), Math.Max(0, item.m_stack));
        if (!InventorySlotsPlugin.TryValidatePlayerInventoryLimit(__instance, item, requestedAmount, ref __result))
        {
            return false;
        }

        if (!InventorySlotsPlugin.TryValidatePlayerInventoryInsert(
                __instance,
                item,
                ref x,
                ref y,
                ref __result))
        {
            return false;
        }

        return InventorySlotsPlugin.TryPreparePositionalStackMetadataMerge(
            __instance,
            item,
            requestedAmount,
            x,
            y,
            ref __result,
            out __state);
    }

    private static void Postfix(
        Inventory __instance,
        ItemData item,
        int x,
        int y,
        bool __result,
        InventoryStackMetadataMergeState? __state)
    {
        InventorySlotsPlugin.CompletePositionalStackMetadataMerge(
            __instance,
            __state);
        InventorySlotsPlugin.OnPlayerInventoryItemPlaced(__instance, item, new Vector2i(x, y), __result);
    }
}

[HarmonyPatch(typeof(Inventory), "AddItem", typeof(ItemData), typeof(Vector2i))]
internal static class InventoryAddItemPosPatch
{
    private static bool Prefix(
        Inventory __instance,
        ref bool __result,
        ItemData item,
        ref Vector2i pos,
        out InventoryAddItemStackMetadataState? __state)
    {
        __state = null;
        if (!InventorySlotsPlugin.TryValidatePlayerInventoryLimit(__instance, item, item.m_stack, ref __result))
        {
            return false;
        }

        __state = InventorySlotsPlugin.BeginAutomaticStackMetadataMerge(
            __instance,
            item);
        bool runOriginal = InventorySlotsPlugin.TryValidatePlayerInventoryInsert(
            __instance,
            item,
            ref pos,
            ref __result);
        if (!runOriginal)
        {
            InventorySlotsPlugin.EndAutomaticStackMetadataMerge(__state);
        }

        return runOriginal;
    }

    private static void Postfix(
        Inventory __instance,
        ItemData item,
        Vector2i pos,
        bool __result,
        InventoryAddItemStackMetadataState? __state)
    {
        InventorySlotsPlugin.CompleteAutomaticStackMetadataMerge(
            __instance,
            __state);
        InventorySlotsPlugin.OnPlayerInventoryItemPlaced(__instance, item, pos, __result);
    }

    private static Exception? Finalizer(
        InventoryAddItemStackMetadataState? __state,
        Exception __exception)
    {
        InventorySlotsPlugin.EndAutomaticStackMetadataMerge(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Inventory), "MoveItemToThis", typeof(Inventory), typeof(ItemData), typeof(int), typeof(int), typeof(int))]
internal static class InventoryMoveItemToThisPatch
{
    private static bool Prefix(
        Inventory __instance,
        ref bool __result,
        Inventory fromInventory,
        ItemData item,
        int amount,
        ref int x,
        ref int y,
        out bool __state)
    {
        __state = false;
        if (InventorySlotsPlugin.TryRouteMultiUserContainerPositionalMove(
                __instance,
                fromInventory,
                item,
                amount,
                x,
                y,
                out bool multiUserResult))
        {
            __state = true;
            __result = multiUserResult;
            return false;
        }

        return InventorySlotsPlugin.TryValidatePlayerInventoryMoveItemToThis(__instance, ref __result, fromInventory, item, amount, ref x, ref y);
    }

    private static void Postfix(
        Inventory __instance,
        bool __result,
        ItemData item,
        int x,
        int y,
        bool __state)
    {
        if (!__state)
        {
            InventorySlotsPlugin.OnPlayerInventoryItemPlaced(
                __instance,
                item,
                new Vector2i(x, y),
                __result);
        }
    }
}

[HarmonyPatch(typeof(Inventory), "MoveItemToThis", typeof(Inventory), typeof(ItemData))]
internal static class InventoryMoveItemToThisAutoPatch
{
    private static bool Prefix(Inventory __instance, Inventory fromInventory, ItemData item)
    {
        return !InventorySlotsPlugin.TryRouteMultiUserContainerAutoMove(__instance, fromInventory, item);
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup), typeof(GameObject), typeof(bool), typeof(bool))]
internal static class HumanoidPickupInventoryLimitPatch
{
    private static void Postfix(Humanoid __instance, GameObject go, bool __result)
    {
        InventorySlotsPlugin.OnHumanoidPickupResult(__instance, go, __result);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "DropItem")]
internal static class InventoryGridDropItemPatch
{
    private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemData item, int amount, Vector2i pos, ref bool __result)
    {
        if (InventorySlotsPlugin.TryRouteMultiUserContainerDropItem(
                __instance,
                fromInventory,
                item,
                amount,
                pos,
                out bool multiUserResult))
        {
            __result = multiUserResult;
            return false;
        }

        return InventorySlotsPlugin.ShouldAllowInventoryGridDropItem(__instance, item, pos);
    }
}
