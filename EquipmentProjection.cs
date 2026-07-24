using System;
using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static void PrepareCustomEquipmentProjection(Humanoid humanoid)
    {
        Player? player = Player.m_localPlayer;
        if (humanoid != (Humanoid)player || player == null)
        {
            ClearPreparedCustomEquipmentStatusEffects();
            return;
        }

        PrepareCustomEquipmentStatusEffects(humanoid, player);
    }

    internal static void PrepareCustomEquipmentStatusEffects(Humanoid humanoid, Player player)
    {
        EquipmentVisuals.StatusEffects.Clear();
        EquipmentVisuals.StatusEffectLevels.Clear();

        if (humanoid == null || player == null)
        {
            return;
        }

        foreach (ItemData item in GetCustomEquippedItems(player))
        {
            PrepareCustomEquipmentStatusEffect(item.m_shared.m_equipStatusEffect, item.m_quality);
            if (humanoid.HaveSetEffect(item))
            {
                PrepareCustomEquipmentStatusEffect(item.m_shared.m_setStatusEffect, item.m_quality);
            }
        }
    }

    private static void PrepareCustomEquipmentStatusEffect(StatusEffect? statusEffect, int itemLevel)
    {
        if (IsUnityNull(statusEffect))
        {
            return;
        }

        EquipmentVisuals.StatusEffects.Add(statusEffect!);
        EquipmentVisuals.StatusEffectLevels.TryGetValue(statusEffect!, out int existingLevel);
        EquipmentVisuals.StatusEffectLevels[statusEffect!] = Math.Max(existingLevel, itemLevel);
    }

    internal static void ApplyPreparedCustomEquipmentStatusEffects(Humanoid humanoid)
    {
        if (humanoid == null)
        {
            ClearPreparedCustomEquipmentStatusEffects();
            return;
        }

        foreach (StatusEffect statusEffect in EquipmentVisuals.StatusEffects)
        {
            if (IsUnityNull(statusEffect))
            {
                continue;
            }

            EquipmentVisuals.StatusEffectLevels.TryGetValue(statusEffect, out int itemLevel);
            ((Character)humanoid).m_seman.AddStatusEffect(statusEffect, false, itemLevel, 0f);
            humanoid.m_equipmentStatusEffects.Add(statusEffect);
        }

        ClearPreparedCustomEquipmentStatusEffects();
    }

    internal static void ClearPreparedCustomEquipmentStatusEffects()
    {
        EquipmentVisuals.StatusEffects.Clear();
        EquipmentVisuals.StatusEffectLevels.Clear();
    }

    internal static bool ShouldPreventCustomEquipmentStatusRemoval(SEMan seMan, int nameHash)
    {
        if (nameHash == 0 || EquipmentVisuals.StatusEffects.Count == 0)
        {
            return false;
        }

        Player? player = Player.m_localPlayer;
        if (player == null || seMan != ((Character)player).m_seman)
        {
            return false;
        }

        return EquipmentVisuals.StatusEffects.Any(statusEffect => !IsUnityNull(statusEffect) && statusEffect.NameHash() == nameHash);
    }

    internal static float GetProjectedCustomEquipmentWeight(Player player)
    {
        return player == null ? 0f : GetCachedCustomEquipmentWeight(player);
    }

    internal static float GetProjectedCustomEquipmentEitrRegenModifier(Player player)
    {
        return player == null ? 0f : GetCachedCustomEquipmentEitrRegen(player);
    }

    internal static float GetCustomEquipmentArmor(Player player)
    {
        return player == null ? 0f : GetCachedCustomEquipmentArmor(player);
    }

    internal static void ApplyCustomEquipmentDamageModifiers(Player player, ref HitData.DamageModifiers modifiers)
    {
        if (player == null)
        {
            return;
        }

        foreach (ItemData item in GetCustomEquippedItems(player))
        {
            if (item?.m_shared != null && item.m_shared.m_damageModifiers.Count > 0)
            {
                modifiers.Apply(item.m_shared.m_damageModifiers);
            }
        }
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
