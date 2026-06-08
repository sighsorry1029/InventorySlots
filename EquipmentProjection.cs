using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool TryPrepareCustomEquipmentProjection(Humanoid humanoid)
    {
        Player? player = Player.m_localPlayer;
        if (humanoid != (Humanoid)player || player == null)
        {
            ClearPreparedCustomEquipmentStatusEffects();
            return false;
        }

        PrepareCustomEquipmentStatusEffects(humanoid, player);
        return true;
    }

    internal static void ApplyCustomEquipmentProjection(Humanoid humanoid)
    {
        ApplyPreparedCustomEquipmentStatusEffects(humanoid);
    }

    internal static void ClearCustomEquipmentProjection()
    {
        ClearPreparedCustomEquipmentStatusEffects();
    }

    internal static bool ShouldBlockCustomEquipmentStatusRemoval(SEMan seMan, int nameHash)
    {
        return ShouldPreventCustomEquipmentStatusRemoval(seMan, nameHash);
    }

    internal static float GetProjectedCustomEquipmentWeight(Player player)
    {
        return player == null ? 0f : GetCachedCustomEquipmentWeight(player);
    }

    internal static float GetProjectedCustomEquipmentEitrRegenModifier(Player player)
    {
        return player == null ? 0f : GetCachedCustomEquipmentEitrRegen(player);
    }

    internal static float GetProjectedCustomEquipmentArmor(Player player)
    {
        return GetCustomEquipmentArmor(player);
    }

    internal static void ApplyProjectedCustomEquipmentDamageModifiers(Player player, ref HitData.DamageModifiers modifiers)
    {
        ApplyCustomEquipmentDamageModifiers(player, ref modifiers);
    }

    internal static void DrainProjectedCustomEquipmentDurability(Humanoid humanoid, Player player, float dt)
    {
        if (humanoid == null || player == null)
        {
            return;
        }

        foreach (ItemData item in GetCustomEquippedItems(player))
        {
            if (item.m_shared.m_useDurability)
            {
                humanoid.DrainEquipedItemDurability(item, dt);
            }
        }
    }

    internal static int GetProjectedCustomEquipmentSetCount(Player player, string setName)
    {
        return player == null || string.IsNullOrEmpty(setName)
            ? 0
            : GetCachedCustomEquipmentSetCount(player, setName);
    }

    internal static void ApplyProjectedEquipmentModifierValues(Player player)
    {
        if (player == null || Player.s_equipmentModifierSourceFields == null)
        {
            return;
        }

        float[]? modifierValues = GetCachedCustomEquipmentModifierValues(player);
        if (modifierValues == null)
        {
            return;
        }

        int count = System.Math.Min(player.m_equipmentModifierValues.Length, modifierValues.Length);
        for (int i = 0; i < count; i++)
        {
            player.m_equipmentModifierValues[i] += modifierValues[i];
        }
    }

    internal static bool ClearAllCustomEquipmentState(Player player)
    {
        if (player == null)
        {
            return false;
        }

        bool changed = false;
        foreach (ItemData item in GetCustomEquippedItems(player).ToArray())
        {
            ClearCustomEquipmentState(item);
            changed = true;
        }

        return changed;
    }

    internal static bool TryGetCustomEquippedItemForApi(Player player, System.Func<ItemData?, bool> predicate, out ItemData? item)
    {
        item = null;
        if (player == null || predicate == null)
        {
            return false;
        }

        foreach (ItemData candidate in GetCustomEquippedItems(player))
        {
            if (predicate(candidate))
            {
                item = candidate;
                return true;
            }
        }

        return false;
    }

    internal static bool IsLocalPlayerHumanoid(Humanoid humanoid, out Player? player)
    {
        player = Player.m_localPlayer;
        return player != null && humanoid == (Humanoid)player;
    }
}
