using System;
using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly List<ItemData> CustomEquippedItemsCache = new();
    private static readonly Dictionary<string, int> CustomEquipmentSetCountCache = new(StringComparer.Ordinal);
    private static Player? _customEquipmentCachePlayer;
    private static Inventory? _customEquipmentCacheInventory;
    private static string _customEquipmentCachePlayerId = "";
    private static int _customEquipmentCacheVersion;
    private static int _customEquipmentCacheBuiltVersion = -1;
    private static float _customEquipmentCacheWeight;
    private static float _customEquipmentCacheEitrRegen;
    private static float _customEquipmentCacheArmor;
    private static float[]? _customEquipmentModifierValuesCache;

    private static void InvalidateCustomEquipmentProjectionCache()
    {
        unchecked
        {
            _customEquipmentCacheVersion++;
        }
    }

    private static IReadOnlyList<ItemData> GetCustomEquippedItems(Player player)
    {
        EnsureCustomEquipmentProjectionCache(player);
        return CustomEquippedItemsCache;
    }

    private static float GetCachedCustomEquipmentWeight(Player player)
    {
        EnsureCustomEquipmentProjectionCache(player);
        return _customEquipmentCacheWeight;
    }

    private static float GetCachedCustomEquipmentEitrRegen(Player player)
    {
        EnsureCustomEquipmentProjectionCache(player);
        return _customEquipmentCacheEitrRegen;
    }

    private static float GetCachedCustomEquipmentArmor(Player player)
    {
        EnsureCustomEquipmentProjectionCache(player);
        return _customEquipmentCacheArmor;
    }

    private static float[]? GetCachedCustomEquipmentModifierValues(Player player)
    {
        EnsureCustomEquipmentProjectionCache(player);
        return _customEquipmentModifierValuesCache;
    }

    private static int GetCachedCustomEquipmentSetCount(Player player, string setName)
    {
        EnsureCustomEquipmentProjectionCache(player);
        return CustomEquipmentSetCountCache.TryGetValue(setName, out int count) ? count : 0;
    }

    private static void EnsureCustomEquipmentProjectionCache(Player player)
    {
        if (player == null)
        {
            ClearCustomEquipmentProjectionCache();
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        string playerId = GetPlayerId(player);
        if (inventory != null &&
            _customEquipmentCachePlayer == player &&
            _customEquipmentCacheInventory == inventory &&
            string.Equals(_customEquipmentCachePlayerId, playerId, StringComparison.Ordinal) &&
            _customEquipmentCacheBuiltVersion == _customEquipmentCacheVersion)
        {
            return;
        }

        ClearCustomEquipmentProjectionCache();
        _customEquipmentCachePlayer = player;
        _customEquipmentCacheInventory = inventory;
        _customEquipmentCachePlayerId = playerId;
        _customEquipmentCacheBuiltVersion = _customEquipmentCacheVersion;

        if (inventory == null)
        {
            return;
        }

        foreach (ItemData item in inventory.m_inventory)
        {
            if (item == null ||
                !IsInventorySlotsCustomEquipped(item) ||
                !item.m_customData.TryGetValue(EquippedByKey, out string equippedBy) ||
                equippedBy != playerId)
            {
                continue;
            }

            CustomEquippedItemsCache.Add(item);
            if (item.m_shared == null)
            {
                continue;
            }

            _customEquipmentCacheWeight += item.m_shared.m_weight;
            _customEquipmentCacheEitrRegen += item.m_shared.m_eitrRegenModifier;
            if (item.m_shared.m_armor > 0f)
            {
                _customEquipmentCacheArmor += item.GetArmor();
            }

            string setName = item.m_shared.m_setName;
            if (!string.IsNullOrEmpty(setName))
            {
                CustomEquipmentSetCountCache.TryGetValue(setName, out int count);
                CustomEquipmentSetCountCache[setName] = count + 1;
            }

            AddCachedCustomEquipmentModifierValues(item);
        }
    }

    private static void AddCachedCustomEquipmentModifierValues(ItemData item)
    {
        System.Reflection.FieldInfo[]? fields = Player.s_equipmentModifierSourceFields;
        if (fields == null || item.m_shared == null)
        {
            return;
        }

        if (_customEquipmentModifierValuesCache == null || _customEquipmentModifierValuesCache.Length != fields.Length)
        {
            _customEquipmentModifierValuesCache = new float[fields.Length];
        }

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].GetValue(item.m_shared) is float value)
            {
                _customEquipmentModifierValuesCache[i] += value;
            }
        }
    }

    private static void ClearCustomEquipmentProjectionCache()
    {
        CustomEquippedItemsCache.Clear();
        CustomEquipmentSetCountCache.Clear();
        _customEquipmentCachePlayer = null;
        _customEquipmentCacheInventory = null;
        _customEquipmentCachePlayerId = "";
        _customEquipmentCacheBuiltVersion = -1;
        _customEquipmentCacheWeight = 0f;
        _customEquipmentCacheEitrRegen = 0f;
        _customEquipmentCacheArmor = 0f;
        if (_customEquipmentModifierValuesCache != null)
        {
            Array.Clear(_customEquipmentModifierValuesCache, 0, _customEquipmentModifierValuesCache.Length);
        }
    }
}
