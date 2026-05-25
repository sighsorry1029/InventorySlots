using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(Humanoid), "UpdateEquipmentStatusEffects")]
internal static class HumanoidUpdateEquipmentStatusEffectsPatch
{
    private static void Prefix(Humanoid __instance)
    {
        InventorySlotsPlugin.TryPrepareCustomEquipmentProjection(__instance);
    }

    private static void Postfix(Humanoid __instance)
    {
        InventorySlotsPlugin.ApplyCustomEquipmentProjection(__instance);
    }

    private static void Finalizer()
    {
        InventorySlotsPlugin.ClearCustomEquipmentProjection();
    }
}

[HarmonyPatch(typeof(SEMan), "RemoveStatusEffect", typeof(int), typeof(bool))]
internal static class SEManRemoveStatusEffectInventorySlotsPatch
{
    private static void Prefix(SEMan __instance, ref int nameHash)
    {
        InventorySlotsPlugin.FilterCustomEquipmentStatusRemoval(__instance, ref nameHash);
    }
}

[HarmonyPatch(typeof(Humanoid), "GetEquipmentWeight")]
internal static class HumanoidGetEquipmentWeightPatch
{
    private static void Postfix(Humanoid __instance, ref float __result)
    {
        InventorySlotsPlugin.AddProjectedEquipmentWeight(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Player), "GetEquipmentEitrRegenModifier")]
internal static class PlayerGetEquipmentEitrRegenModifierPatch
{
    private static void Postfix(Player __instance, ref float __result)
    {
        InventorySlotsPlugin.AddProjectedEquipmentEitrRegenModifier(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Player), "GetBodyArmor")]
internal static class PlayerGetBodyArmorInventorySlotsPatch
{
    private static void Postfix(Player __instance, ref float __result)
    {
        InventorySlotsPlugin.AddProjectedBodyArmor(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Player), "ApplyArmorDamageMods")]
internal static class PlayerApplyArmorDamageModsInventorySlotsPatch
{
    private static void Postfix(Player __instance, ref HitData.DamageModifiers mods)
    {
        InventorySlotsPlugin.ApplyProjectedArmorDamageModifiers(__instance, ref mods);
    }
}

[HarmonyPatch(typeof(Humanoid), "UpdateEquipment")]
internal static class HumanoidUpdateEquipmentPatch
{
    private static void Postfix(Humanoid __instance, float dt)
    {
        InventorySlotsPlugin.OnHumanoidUpdateEquipment(__instance, dt);
    }
}

[HarmonyPatch(typeof(Humanoid), "GetSetCount")]
internal static class HumanoidGetSetCountPatch
{
    private static void Postfix(Humanoid __instance, string setName, ref int __result)
    {
        InventorySlotsPlugin.AddProjectedEquipmentSetCount(__instance, setName, ref __result);
    }
}

[HarmonyPatch(typeof(Player), "UpdateModifiers")]
internal static class PlayerUpdateModifiersPatch
{
    private static void Postfix(Player __instance)
    {
        InventorySlotsPlugin.OnPlayerUpdateModifiers(__instance);
    }
}
