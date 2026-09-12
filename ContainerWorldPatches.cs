using HarmonyLib;

namespace InventorySlots;

// Shared viewers do not acquire write ownership. Every mutation still uses a
// short handoff lease. With sharing disabled, vanilla exclusive use is preserved.
[HarmonyPatch(typeof(Container), "RPC_RequestOpen")]
internal static class ContainerOpenAreaLeasePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, long uid, long playerID) =>
        !InventorySlotsPlugin.TryHandleSharedContainerRequest(__instance, uid, playerID, "RPC_OpenResponse") &&
        !InventorySlotsPlugin.TryRejectContainerRequestDuringAreaLease(__instance, uid, "RPC_OpenResponse");
}

[HarmonyPatch(typeof(Container), "RPC_RequestStack")]
internal static class ContainerStackAreaLeasePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, long uid, long playerID) =>
        !InventorySlotsPlugin.TryHandleSharedContainerRequest(__instance, uid, playerID, "RPC_StackResponse") &&
        !InventorySlotsPlugin.TryRejectContainerRequestDuringAreaLease(__instance, uid, "RPC_StackResponse");
}

[HarmonyPatch(typeof(Container), "RPC_RequestTakeAll")]
internal static class ContainerTakeAllAreaLeasePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Container __instance, long uid, long playerID) =>
        !InventorySlotsPlugin.TryHandleSharedContainerRequest(__instance, uid, playerID, "RPC_TakeAllResponse") &&
        !InventorySlotsPlugin.TryRejectContainerRequestDuringAreaLease(__instance, uid, "RPC_TakeAllResponse");
}

[HarmonyPatch(typeof(Container), "StackAll")]
internal static class ContainerStackAllFavoriteProtectionPatch
{
    private static bool Prefix(Container __instance)
    {
        return !InventorySlotsPlugin.TryHandleContainerStackAll(__instance);
    }
}

[HarmonyPatch(typeof(Container), "RPC_TakeAllResponse")]
internal static class ContainerTakeAllResponsInventorySlotsPatch
{
    private static bool Prefix(Container __instance, bool granted) =>
        !InventorySlotsPlugin.TryHandleSharedContainerTakeAllResponse(__instance, granted);

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
