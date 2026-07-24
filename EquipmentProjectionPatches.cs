using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(Humanoid), "UpdateEquipmentStatusEffects")]
internal static class HumanoidUpdateEquipmentStatusEffectsPatch
{
    private static void Prefix(Humanoid __instance)
    {
        InventorySlotsPlugin.PrepareCustomEquipmentProjection(__instance);
    }

    private static void Postfix(Humanoid __instance)
    {
        InventorySlotsPlugin.ApplyPreparedCustomEquipmentStatusEffects(__instance);
    }

    private static void Finalizer()
    {
        InventorySlotsPlugin.ClearPreparedCustomEquipmentStatusEffects();
    }
}

[HarmonyPatch(typeof(SEMan), "RemoveStatusEffect", typeof(int), typeof(bool))]
internal static class SEManRemoveStatusEffectInventorySlotsPatch
{
    private static void Prefix(SEMan __instance, ref int nameHash)
    {
        Player? player = Player.m_localPlayer;
        if (player != null && InventorySlotsPlugin.ShouldPreventCustomEquipmentStatusRemoval(__instance, nameHash))
        {
            nameHash = 0;
        }
    }
}

[HarmonyPatch(typeof(Humanoid), "GetEquipmentWeight")]
internal static class HumanoidGetEquipmentWeightPatch
{
    private static void Postfix(Humanoid __instance, ref float __result)
    {
        if (InventorySlotsPlugin.IsLocalPlayerHumanoid(__instance, out Player? player) && player != null)
        {
            __result += InventorySlotsPlugin.GetProjectedCustomEquipmentWeight(player);
        }
    }
}

[HarmonyPatch(typeof(Player), "GetEquipmentEitrRegenModifier")]
internal static class PlayerGetEquipmentEitrRegenModifierPatch
{
    private static void Postfix(Player __instance, ref float __result)
    {
        if (__instance == Player.m_localPlayer)
        {
            __result += InventorySlotsPlugin.GetProjectedCustomEquipmentEitrRegenModifier(__instance);
        }
    }
}

[HarmonyPatch(typeof(Player), "GetBodyArmor")]
internal static class PlayerGetBodyArmorInventorySlotsPatch
{
    private static void Postfix(Player __instance, ref float __result)
    {
        if (__instance == Player.m_localPlayer)
        {
            __result += InventorySlotsPlugin.GetCustomEquipmentArmor(__instance);
        }
    }
}

[HarmonyPatch(typeof(Player), "ApplyArmorDamageMods")]
internal static class PlayerApplyArmorDamageModsInventorySlotsPatch
{
    private static void Postfix(Player __instance, ref HitData.DamageModifiers mods)
    {
        if (__instance == Player.m_localPlayer)
        {
            InventorySlotsPlugin.ApplyCustomEquipmentDamageModifiers(__instance, ref mods);
        }
    }
}

[HarmonyPatch(typeof(Humanoid), "UpdateEquipment")]
internal static class HumanoidUpdateEquipmentPatch
{
    private static void Postfix(Humanoid __instance, float dt)
    {
        if (InventorySlotsPlugin.IsLocalPlayerHumanoid(__instance, out Player? player) && player != null)
        {
            InventorySlotsPlugin.DrainProjectedCustomEquipmentDurability(__instance, player, dt);
        }
    }
}

[HarmonyPatch(typeof(Humanoid), "GetSetCount")]
internal static class HumanoidGetSetCountPatch
{
    private static void Postfix(Humanoid __instance, string setName, ref int __result)
    {
        if (InventorySlotsPlugin.IsLocalPlayerHumanoid(__instance, out Player? player) && player != null)
        {
            __result += InventorySlotsPlugin.GetProjectedCustomEquipmentSetCount(player, setName);
        }
    }
}

[HarmonyPatch(typeof(Player), "UpdateModifiers")]
internal static class PlayerUpdateModifiersPatch
{
    private static void Postfix(Player __instance)
    {
        if (__instance == Player.m_localPlayer && Player.s_equipmentModifierSourceFields != null)
        {
            InventorySlotsPlugin.ApplyProjectedEquipmentModifierValues(__instance);
        }
    }
}
