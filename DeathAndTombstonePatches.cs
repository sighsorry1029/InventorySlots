using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(Player), "UnequipDeathDropItems")]
internal static class PlayerUnequipDeathDropItemsInventorySlotsPatch
{
    private static void Prefix(Player __instance, out bool __state)
    {
        __state = InventorySlotsPlugin.PreparePlayerDeathDropUnequip(__instance);
    }

    private static void Finalizer(bool __state)
    {
        InventorySlotsPlugin.CompletePlayerDeathDropUnequip(__state);
    }
}

[HarmonyPatch(typeof(Player), "CreateTombStone")]
internal static class PlayerCreateTombStoneKeepOnDeathPatch
{
    private static void Prefix(Player __instance, out InventorySlotsPlugin.TombStonePreparationState __state)
    {
        __state = InventorySlotsPlugin.PrepareCreateTombStone(__instance);
    }

    private static void Postfix(Player __instance, InventorySlotsPlugin.TombStonePreparationState __state)
    {
        InventorySlotsPlugin.CompleteCreateTombStone(__instance, __state, finalAttempt: false);
    }

    private static void Finalizer(Player __instance, InventorySlotsPlugin.TombStonePreparationState __state)
    {
        InventorySlotsPlugin.CompleteCreateTombStone(__instance, __state, finalAttempt: true);
    }
}

[HarmonyPatch(typeof(TombStone), "Interact")]
internal static class TombStoneInteractHeightPatch
{
    private static void Prefix(TombStone __instance, bool hold)
    {
        InventorySlotsPlugin.OnTombStoneInteract(__instance, hold);
    }
}

[HarmonyPatch(typeof(TombStone), "EasyFitInInventory")]
internal static class TombStoneEasyFitInInventoryPatch
{
    private static bool Prefix(TombStone __instance, Player player, ref bool __result)
    {
        return InventorySlotsPlugin.TryOverrideTombStoneEasyFit(__instance, player, ref __result);
    }

    private static void Postfix(Player player)
    {
        InventorySlotsPlugin.OnTombStoneInventoryStateChanged(player);
    }
}

[HarmonyPatch(typeof(TombStone), "OnTakeAllSuccess")]
internal static class TombStoneOnTakeAllSuccessPatch
{
    private static void Postfix(TombStone __instance)
    {
        InventorySlotsPlugin.OnTombStoneTakeAllSuccess(__instance);
    }
}
