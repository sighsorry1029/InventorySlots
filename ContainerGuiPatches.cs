using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(InventoryGui), "OnTakeAll")]
internal static class InventoryGuiSafeTakeAllPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventorySlotsPlugin.TryHandleSafeTakeAll(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnStackAll")]
internal static class InventoryGuiPlaceStacksPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        return !InventorySlotsPlugin.TryHandleVanillaPlaceStacks(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "Show")]
internal static class InventoryGuiShowValidateInventoryPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.OnInventoryGuiShow();
    }
}

[HarmonyPatch(typeof(InventoryGui), "Hide")]
internal static class InventoryGuiHideRestoreContainerPanelPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.OnInventoryGuiHide();
    }
}

[HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
internal static class InventoryGuiCloseContainerRestorePanelPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.OnInventoryGuiCloseContainer();
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
internal static class InventoryGuiUpdateContainerInventorySlotsPatch
{
    private static void Postfix(InventoryGui __instance, Player player)
    {
        InventorySlotsPlugin.OnInventoryGuiUpdateContainer(__instance, player);
    }
}
