namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static void FilterCustomEquipmentStatusRemoval(SEMan statusEffects, ref int nameHash)
    {
        Player? player = Player.m_localPlayer;
        if (player != null && ShouldBlockCustomEquipmentStatusRemoval(statusEffects, nameHash))
        {
            nameHash = 0;
        }
    }

    internal static void AddProjectedEquipmentWeight(Humanoid humanoid, ref float result)
    {
        if (IsLocalPlayerHumanoid(humanoid, out Player? player) && player != null)
        {
            result += GetProjectedCustomEquipmentWeight(player);
        }
    }

    internal static void AddProjectedEquipmentEitrRegenModifier(Player player, ref float result)
    {
        if (player == Player.m_localPlayer)
        {
            result += GetProjectedCustomEquipmentEitrRegenModifier(player);
        }
    }

    internal static void AddProjectedBodyArmor(Player player, ref float result)
    {
        if (player == Player.m_localPlayer)
        {
            result += GetProjectedCustomEquipmentArmor(player);
        }
    }

    internal static void ApplyProjectedArmorDamageModifiers(Player player, ref HitData.DamageModifiers mods)
    {
        if (player == Player.m_localPlayer)
        {
            ApplyProjectedCustomEquipmentDamageModifiers(player, ref mods);
        }
    }

    internal static void OnHumanoidUpdateEquipment(Humanoid humanoid, float dt)
    {
        if (IsLocalPlayerHumanoid(humanoid, out Player? player) && player != null)
        {
            DrainProjectedCustomEquipmentDurability(humanoid, player, dt);
        }
    }

    internal static void AddProjectedEquipmentSetCount(Humanoid humanoid, string setName, ref int result)
    {
        if (IsLocalPlayerHumanoid(humanoid, out Player? player) && player != null)
        {
            result += GetProjectedCustomEquipmentSetCount(player, setName);
        }
    }

    internal static void OnPlayerUpdateModifiers(Player player)
    {
        if (player == Player.m_localPlayer && Player.s_equipmentModifierSourceFields != null)
        {
            ApplyProjectedEquipmentModifierValues(player);
        }
    }
}
