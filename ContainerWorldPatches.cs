using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(Container), "StackAll")]
internal static class ContainerStackAllFavoriteProtectionPatch
{
    private static bool Prefix(Container __instance)
    {
        return !InventorySlotsPlugin.TryHandleContainerStackAll(__instance);
    }
}

[HarmonyPatch(typeof(Container), "RPC_TakeAllRespons")]
internal static class ContainerTakeAllResponsInventorySlotsPatch
{
    private static void Postfix(Container __instance, bool granted)
    {
        InventorySlotsPlugin.OnContainerTakeAllResponse(__instance, granted);
    }
}

[HarmonyPatch(typeof(Container), "GetHoverText")]
internal static class ContainerRestockHoverTextPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Container __instance, ref string __result)
    {
        InventorySlotsPlugin.AppendContainerRestockHoverText(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Container), "Interact")]
internal static class ContainerRestockInteractPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, Humanoid character)
    {
        return !InventorySlotsPlugin.ShouldSuppressContainerInteractForRestock(__instance, character);
    }
}

[HarmonyPatch(typeof(Container), "Awake")]
internal static class ContainerAwakeTombstoneHeightPatch
{
    private static void Prefix(Container __instance)
    {
        InventorySlotsPlugin.EnsureTombstoneContainerHeight(__instance, reloadInventory: false, persistHeight: false);
    }

    private static void Postfix(Container __instance)
    {
        InventorySlotsPlugin.RegisterContainer(__instance);
        InventorySlotsPlugin.EnsureTombstoneContainerHeight(__instance, reloadInventory: false, persistHeight: true);
    }
}

[HarmonyPatch(typeof(Container), "OnDestroyed")]
internal static class ContainerDestroyedInventorySlotsPatch
{
    private static void Postfix(Container __instance)
    {
        InventorySlotsPlugin.UnregisterContainer(__instance);
    }
}
