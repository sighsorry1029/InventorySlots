using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

[HarmonyPatch(typeof(Humanoid), "UseItem", typeof(Inventory), typeof(ItemData), typeof(bool))]
internal static class HumanoidUseItemRouteToDedicatedSlotPatch
{
    private static bool Prefix(Humanoid __instance, Inventory inventory, ItemData item)
    {
        return InventorySlotsPlugin.ShouldAllowHumanoidUseItem(__instance, inventory, item);
    }
}

[HarmonyPatch(typeof(Humanoid), "EquipItem", typeof(ItemData), typeof(bool))]
internal static class HumanoidEquipItemRouteToDedicatedSlotPatch
{
    private static bool Prefix(Humanoid __instance, ItemData item, ref bool __result)
    {
        return !InventorySlotsPlugin.TryOverrideHumanoidEquipItem(__instance, item, ref __result);
    }
}

[HarmonyPatch(typeof(Humanoid), "IsItemEquiped")]
internal static class HumanoidIsItemEquipedPatch
{
    private static void Postfix(Humanoid __instance, ItemData item, ref bool __result)
    {
        InventorySlotsPlugin.OnHumanoidIsItemEquipped(__instance, item, ref __result);
    }
}

[HarmonyPatch(typeof(Humanoid), "UnequipItem")]
internal static class HumanoidUnequipItemPatch
{
    private static void Prefix(Humanoid __instance, ItemData item, out bool __state)
    {
        __state = InventorySlotsPlugin.PrepareHumanoidUnequipItem(__instance, item);
    }

    private static void Postfix(Humanoid __instance, ItemData item, bool __state)
    {
        InventorySlotsPlugin.OnHumanoidUnequipItem(__instance, item, __state);
    }

    private static void Finalizer(bool __state)
    {
        InventorySlotsPlugin.CompleteHumanoidUnequipItem(__state);
    }
}

[HarmonyPatch(typeof(Humanoid), "UnequipAllItems")]
internal static class HumanoidUnequipAllItemsPatch
{
    private static void Postfix(Humanoid __instance)
    {
        InventorySlotsPlugin.OnHumanoidUnequipAllItems(__instance);
    }
}
