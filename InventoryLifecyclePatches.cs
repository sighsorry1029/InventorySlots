using System;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

[HarmonyPatch(typeof(Player), "Awake")]
internal static class PlayerAwakePatch
{
    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerAwake(__instance);
    }
}

[HarmonyPatch(typeof(Player), "OnSpawned")]
internal static class PlayerOnSpawnedPatch
{
    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerSpawned(__instance);
    }
}

[HarmonyPatch(typeof(Player), "Load")]
internal static class PlayerLoadPatch
{
    private static void Prefix(Player __instance, out bool __state)
    {
        __state = InventorySlotsPlugin.BeginPlayerInventoryLoad(__instance);
    }

    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerLoaded(__instance);
    }

    private static Exception? Finalizer(Player __instance, bool __state, Exception __exception)
    {
        InventorySlotsPlugin.EndPlayerInventoryLoad(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Player), "Save")]
internal static class PlayerSavePatch
{
    private static void Prefix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerSaving(__instance);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.ResetCharacter))]
internal static class PlayerResetCharacterInventorySlotsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerProgressionReset(__instance);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.ResetCharacterKnownItems))]
internal static class PlayerResetCharacterKnownItemsInventorySlotsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerProgressionReset(__instance);
    }
}

[HarmonyPatch(typeof(Game), "SpawnPlayer")]
internal static class GameSpawnPlayerFinalizeInventorySlotsPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        InventorySlotsPlugin.OnGameSpawnPlayer();
    }
}

[HarmonyPatch(typeof(Player), "OnInventoryChanged")]
internal static class PlayerInventoryChangedPatch
{
    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerInventoryChanged(__instance);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.AddKnownItem))]
internal static class PlayerAddKnownItemInventorySlotsPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Player __instance)
    {
        return !InventorySlotsPlugin.ShouldSuppressKnownItemRediscovery(__instance);
    }
}

[HarmonyPatch(typeof(Humanoid), "SetupEquipment")]
internal static class HumanoidSetupEquipmentValidateInventoryPatch
{
    private static void Postfix(Humanoid __instance)
    {
        InventorySlotsPlugin.OnHumanoidSetupEquipment(__instance);
    }
}

[HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
internal static class HumanoidSetupVisEquipmentCustomVisualsPatch
{
    private static void Postfix(Humanoid __instance, VisEquipment visEq, bool isRagdoll)
    {
        InventorySlotsPlugin.OnHumanoidSetupVisEquipment(__instance, isRagdoll);
    }
}

[HarmonyPatch(typeof(VisEquipment), "UpdateEquipmentVisuals")]
internal static class VisEquipmentUpdateEquipmentVisualsInventorySlotsPatch
{
    private static void Postfix(VisEquipment __instance)
    {
        InventorySlotsPlugin.UpdateCustomEquipmentVisualsFromZdo(__instance);
    }
}

[HarmonyPatch(typeof(Inventory), "Load", typeof(ZPackage))]
internal static class InventoryLoadValidateInventorySlotsPatch
{
    private static void Prefix(Inventory __instance, out bool __state)
    {
        __state = InventorySlotsPlugin.BeginInventoryLoad(__instance);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Inventory __instance)
    {
        InventorySlotsPlugin.OnInventoryLoaded(__instance);
    }

    private static Exception? Finalizer(Inventory __instance, bool __state, Exception __exception)
    {
        InventorySlotsPlugin.EndInventoryLoad(__instance, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Inventory), "MoveAll")]
internal static class InventoryMoveAllPatch
{
    private static void Postfix(Inventory __instance, Inventory fromInventory)
    {
        InventorySlotsPlugin.OnInventoryMoveAll(__instance, fromInventory);
    }
}

[HarmonyPatch(typeof(Inventory), "MoveInventoryToGrave")]
internal static class InventoryMoveInventoryToGravePatch
{
    private static void Prefix(Inventory __instance, Inventory original)
    {
        InventorySlotsPlugin.OnInventoryMoveInventoryToGrave(__instance, original);
    }
}

[HarmonyPatch(typeof(Humanoid), "DropItem")]
internal static class HumanoidDropInventorySlotsItemPatch
{
    private static void Prefix(Humanoid __instance, Inventory inventory, ItemData item, int amount, out SlotDefinition? __state)
    {
        __state = InventorySlotsPlugin.PrepareHumanoidDropInventorySlotsItem(__instance, inventory, item, amount);
    }

    private static void Postfix(Humanoid __instance, Inventory inventory, ItemData item, bool __result, SlotDefinition? __state)
    {
        InventorySlotsPlugin.RestoreHumanoidDropInventorySlotsItem(__instance, inventory, item, __result, __state);
    }
}
