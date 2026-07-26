using System;
using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(InventoryGrid), "UpdateGamepad")]
internal static class InventoryGridMultiUserContainerGamepadBarrierPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(InventoryGrid __instance)
    {
        return !InventorySlotsPlugin.ShouldBlockMultiUserContainerGamepadInput(
            __instance);
    }
}

[HarmonyPatch(typeof(InventoryGrid), "UpdateInventory")]
internal static class InventoryGridMultiUserContainerProjectionPatch
{
    private sealed class ProjectionState
    {
        public Inventory? RealInventory { get; set; }
        public bool Restored { get; set; }
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref Inventory inventory, out ProjectionState __state)
    {
        __state = new ProjectionState();
        if (!InventorySlotsPlugin.TryGetMultiUserContainerProjection(inventory, out Inventory projection))
        {
            return;
        }

        __state.RealInventory = inventory;
        inventory = projection;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(InventoryGrid __instance, ProjectionState __state)
    {
        RestoreRealInventory(__instance, __state);
    }

    private static Exception? Finalizer(InventoryGrid __instance, ProjectionState __state, Exception? __exception)
    {
        RestoreRealInventory(__instance, __state);
        return __exception;
    }

    private static void RestoreRealInventory(InventoryGrid grid, ProjectionState state)
    {
        if (state.Restored || state.RealInventory == null)
        {
            return;
        }

        InventorySlotsPlugin.RestoreMultiUserContainerGridInventory(grid, state.RealInventory);
        state.Restored = true;
    }
}
