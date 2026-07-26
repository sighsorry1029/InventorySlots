using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
internal static class ChatHasFocusInventorySlotsInputPatch
{
    private static void Postfix(ref bool __result)
    {
        __result = __result || InventorySlotsPlugin.IsCraftingSearchFocused();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
internal static class PlayerUseHotbarItemQuickSlotModifierPatch
{
    private static bool Prefix(Player __instance, int index)
    {
        return !InventorySlotsPlugin.ShouldSuppressVanillaHotbarItemUse(__instance, index);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
internal static class InventoryGuiDragSlotItemOutPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, InventoryGrid.Modifier mod)
    {
        if (InventorySlotsPlugin.ShouldBlockContainerPreviewInteraction(__instance))
        {
            return false;
        }

        if (InventorySlotsPlugin.IsMultiUserContainerInteractionPending(__instance, grid, item, pos))
        {
            return false;
        }

        if (mod == InventoryGrid.Modifier.Drop &&
            InventorySlotsPlugin.TryHandleMultiUserContainerDropSelectedItem(
                __instance,
                grid,
                item,
                pos))
        {
            return false;
        }

        InventorySlotsPlugin.TryPinInventoryItemTooltipFromSelection(__instance, grid, pos, mod);
        return !InventorySlotsPlugin.TryHandleRegularItemDragIntoEquipmentSlot(__instance, grid, pos, mod) &&
               !InventorySlotsPlugin.TryHandleSlotItemDragOut(__instance, grid, pos, mod);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
internal static class InventoryGuiContainerPreviewRightClickPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos)
    {
        if (InventorySlotsPlugin.ShouldBlockContainerPreviewInteraction(__instance))
        {
            return false;
        }

        if (InventorySlotsPlugin.IsMultiUserContainerInteractionPending(
                __instance,
                grid,
                item,
                pos))
        {
            return false;
        }

        return !InventorySlotsPlugin.TryHandleMultiUserContainerRightClick(__instance, grid, item, pos);
    }
}

[HarmonyPatch(typeof(InventoryGui), "SetupDragItem")]
internal static class InventoryGuiContainerPreviewDragPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventorySlotsPlugin.ShouldBlockContainerPreviewInteraction(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnDropOutside")]
internal static class InventoryGuiDropSlotItemOutsidePatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        if (InventorySlotsPlugin.ShouldBlockContainerPreviewInteraction(__instance) ||
            InventorySlotsPlugin.TryHandleMultiUserContainerDropOutside(__instance))
        {
            return false;
        }

        return !InventorySlotsPlugin.TryHandleSlotItemDropOutside(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "OnLeftClick")]
internal static class InventoryGridFavoriteLeftClickPatch
{
    private static bool Prefix(InventoryGrid __instance, UIInputHandler clickHandler)
    {
        return InventorySlotsPlugin.HandleFavoriteClick(__instance, clickHandler, leftClick: true);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "OnRightClick")]
internal static class InventoryGridFavoriteRightClickPatch
{
    private static bool Prefix(InventoryGrid __instance, UIInputHandler element)
    {
        return InventorySlotsPlugin.HandleFavoriteClick(__instance, element, leftClick: false);
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateInventory")]
internal static class InventoryGuiUpdateInventoryPatch
{
    private static void Postfix(InventoryGrid ___m_playerGrid, Player player)
    {
        InventorySlotsPlugin.OnInventoryGuiUpdateInventory(___m_playerGrid, player);
    }
}
