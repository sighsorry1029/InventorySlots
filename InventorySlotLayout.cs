using System;
using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static string QuickSlotProgressionCachePlayerId = "";
    private static int QuickSlotProgressionCachedRows;

    private static Vector3 GetSidePanelBasePosition(Vector3 origin, int inventoryWidth, float elementSpace)
    {
        return origin + new Vector3((inventoryWidth + SidePanelGapColumns) * elementSpace, 0f, 0f);
    }

    internal static bool TryGetSlotAtGridPos(Inventory inventory, Vector2i pos, out SlotDefinition? slot)
    {
        slot = null;
        int width = inventory.GetWidth();
        int fixedRegularRows = GetFixedRegularRows();
        if (pos.y < fixedRegularRows)
        {
            return false;
        }

        int index = (pos.y - fixedRegularRows) * width + pos.x;
        if (index < 0 || index >= SlotDefinitions.Count)
        {
            return false;
        }

        slot = SlotDefinitions[index];
        return true;
    }

    internal static bool TryGetSlotById(string id, out SlotDefinition? slot)
    {
        slot = null;
        for (int i = 0; i < SlotDefinitions.Count; i++)
        {
            SlotDefinition candidate = SlotDefinitions[i];
            if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private static List<SlotDefinition> GetCustomPanelSlots(Player? player = null, Inventory? inventory = null)
    {
        if (!IsEquipmentSlotProgressionEnabled() || player == null)
        {
            return GetCachedCustomPanelSlots();
        }

        string signature = GetVisibleEquipmentSlotUnlockSignature(player, inventory);
        if (InventoryDefinitions.VisibleCustomPanelSlotCacheVersion == InventoryDefinitions.SlotDefinitionVersion &&
            string.Equals(InventoryDefinitions.VisibleCustomPanelSlotCacheSignature, signature, StringComparison.Ordinal))
        {
            return InventoryDefinitions.VisibleCustomPanelSlotCache;
        }

        InventoryDefinitions.VisibleCustomPanelSlotCache.Clear();
        foreach (SlotDefinition slot in GetCachedCustomPanelSlots())
        {
            if (IsEquipmentSlotUnlocked(player, inventory, slot))
            {
                InventoryDefinitions.VisibleCustomPanelSlotCache.Add(slot);
            }
        }

        InventoryDefinitions.VisibleCustomPanelSlotCacheVersion = InventoryDefinitions.SlotDefinitionVersion;
        InventoryDefinitions.VisibleCustomPanelSlotCacheSignature = signature;
        return InventoryDefinitions.VisibleCustomPanelSlotCache;
    }

    private static List<SlotDefinition> GetQuickPanelSlots(Player? player = null)
    {
        int unlockedCount = GetUnlockedQuickSlotCount(player);
        if (InventoryDefinitions.QuickPanelSlotCacheVersion == InventoryDefinitions.SlotDefinitionVersion &&
            InventoryDefinitions.QuickPanelSlotCacheUnlockedCount == unlockedCount)
        {
            return InventoryDefinitions.QuickPanelSlotCache;
        }

        InventoryDefinitions.QuickPanelSlotCache.Clear();
        for (int i = 0; i < SlotDefinitions.Count; i++)
        {
            SlotDefinition slot = SlotDefinitions[i];
            if (slot.Kind == SlotKind.Quick && slot.QuickSlotIndex >= 0 && slot.QuickSlotIndex < unlockedCount)
            {
                InventoryDefinitions.QuickPanelSlotCache.Add(slot);
            }
        }

        InventoryDefinitions.QuickPanelSlotCacheVersion = InventoryDefinitions.SlotDefinitionVersion;
        InventoryDefinitions.QuickPanelSlotCacheUnlockedCount = unlockedCount;
        return InventoryDefinitions.QuickPanelSlotCache;
    }

    private static bool TryGetQuickSlotDefinition(int quickSlotIndex, out SlotDefinition? slot)
    {
        if (InventoryDefinitions.QuickSlotDefinitionCacheVersion != InventoryDefinitions.SlotDefinitionVersion)
        {
            InventoryDefinitions.QuickSlotDefinitionCache.Clear();
            for (int i = 0; i < SlotDefinitions.Count; i++)
            {
                SlotDefinition candidate = SlotDefinitions[i];
                if (candidate.Kind == SlotKind.Quick && candidate.QuickSlotIndex >= 0)
                {
                    InventoryDefinitions.QuickSlotDefinitionCache[candidate.QuickSlotIndex] = candidate;
                }
            }

            InventoryDefinitions.QuickSlotDefinitionCacheVersion = InventoryDefinitions.SlotDefinitionVersion;
        }

        return InventoryDefinitions.QuickSlotDefinitionCache.TryGetValue(quickSlotIndex, out slot);
    }

    private static Vector2i GetSlotGridPos(Inventory inventory, SlotDefinition slot)
    {
        int index = SlotDefinitions.IndexOf(slot);
        int width = inventory.GetWidth();
        return new Vector2i(index % width, GetFixedRegularRows() + index / width);
    }

    private static List<SlotDefinition> GetCachedCustomPanelSlots()
    {
        if (InventoryDefinitions.CustomPanelSlotCacheVersion == InventoryDefinitions.SlotDefinitionVersion)
        {
            return InventoryDefinitions.CustomPanelSlotCache;
        }

        InventoryDefinitions.CustomPanelSlotCache.Clear();
        for (int i = 0; i < SlotDefinitions.Count; i++)
        {
            SlotDefinition slot = SlotDefinitions[i];
            if (slot.Kind != SlotKind.Quick)
            {
                InventoryDefinitions.CustomPanelSlotCache.Add(slot);
            }
        }

        InventoryDefinitions.CustomPanelSlotCacheVersion = InventoryDefinitions.SlotDefinitionVersion;
        return InventoryDefinitions.CustomPanelSlotCache;
    }

    internal static bool IsLockedRowCell(Player player, Vector2i pos)
    {
        return pos.y >= GetUsableRegularRows(player) && pos.y < GetFixedRegularRows();
    }

    private static bool IsExternalReservedCell(Vector2i pos, bool includeRestockableSlots)
    {
        return IsBetterArcheryQuiverCell(pos, includeRestockableSlots);
    }

    internal static bool IsExternalReservedForCompat(Vector2i pos)
    {
        return IsExternalReservedCell(pos, includeRestockableSlots: true);
    }

    private static bool IsOutOfBounds(Inventory inventory, Vector2i pos)
    {
        return pos.x < 0 || pos.x >= inventory.GetWidth() || pos.y < 0 || pos.y >= inventory.GetHeight();
    }

    internal static int GetInventoryFullHeight(int width)
    {
        return GetFullHeightForWidth(width);
    }

    private static int GetFullHeightForWidth(int width)
    {
        return GetFixedRegularRows() + GetSlotTailRows(width);
    }

    private static int GetFixedRegularRows()
    {
        return BaseRows + MaxSupportedExtraRows;
    }

    private static int GetSlotTailRows(int width)
    {
        return SlotDefinitions.Count == 0
            ? 0
            : Mathf.CeilToInt(SlotDefinitions.Count / (float)Mathf.Max(1, width));
    }

    private static int GetUsableRegularRows(Player player)
    {
        return BaseRows + CalculateUnlockedRows(player);
    }

    private static int CalculateUnlockedRows(Player player)
    {
        if (_progressiveRowsEnabled == null || _progressiveRowsEnabled.Value.IsOff())
        {
            return GetMaxExtraRows();
        }

        int maxRows = GetMaxExtraRows();
        int unlocked = 0;
        for (int i = 0; i < maxRows; i++)
        {
            bool rowUnlocked = IsRowUnlocked(player, _rowUnlockItems[i].Value);
            if (rowUnlocked)
            {
                unlocked++;
                continue;
            }

            break;
        }

        return unlocked;
    }

    private static int GetMaxExtraRows()
    {
        return _maxExtraRows == null ? MaxSupportedExtraRows : Mathf.Clamp(_maxExtraRows.Value, 0, MaxSupportedExtraRows);
    }

    private static int GetQuickSlotCount()
    {
        return _quickSlotCount == null ? 0 : Mathf.Clamp(_quickSlotCount.Value, 0, MaxSupportedQuickSlots);
    }

    private static int GetUnlockedQuickSlotCount(Player? player)
    {
        int configuredSlots = GetQuickSlotCount();
        if (configuredSlots <= 0)
        {
            return 0;
        }

        int unlockedRows = GetUnlockedQuickSlotRows(player);
        return Mathf.Clamp(unlockedRows * QuickSlotPanelColumns, 0, configuredSlots);
    }

    private static int GetUnlockedQuickSlotRows(Player? player)
    {
        int configuredSlots = GetQuickSlotCount();
        if (configuredSlots <= 0)
        {
            return 0;
        }

        int reservedRows = Mathf.CeilToInt(configuredSlots / (float)QuickSlotPanelColumns);
        if (reservedRows <= 1 || player == null || _quickSlotProgressionEnabled == null || _quickSlotProgressionEnabled.Value.IsOff())
        {
            return reservedRows;
        }

        int unlockedRows = 1;
        if (reservedRows >= 2 && _quickSlotRowUnlockItems.Length > 0 && IsRowUnlocked(player, _quickSlotRowUnlockItems[0].Value))
        {
            unlockedRows = 2;
        }
        else
        {
            return GetStableUnlockedQuickSlotRows(player, unlockedRows, reservedRows);
        }

        if (reservedRows >= 3 && _quickSlotRowUnlockItems.Length > 1 && IsRowUnlocked(player, _quickSlotRowUnlockItems[1].Value))
        {
            unlockedRows = 3;
        }

        return GetStableUnlockedQuickSlotRows(player, unlockedRows, reservedRows);
    }

    private static int GetStableUnlockedQuickSlotRows(Player player, int unlockedRows, int reservedRows)
    {
        string playerId = GetPlayerId(player);
        if (!string.Equals(QuickSlotProgressionCachePlayerId, playerId, StringComparison.Ordinal))
        {
            QuickSlotProgressionCachePlayerId = playerId;
            QuickSlotProgressionCachedRows = 0;
        }

        if (QuickSlotProgressionCachedRows <= 0)
        {
            QuickSlotProgressionCachedRows = unlockedRows;
        }
        else if (!player.m_isLoading && player.m_knownMaterial is { Count: > 0 })
        {
            QuickSlotProgressionCachedRows = Math.Max(QuickSlotProgressionCachedRows, unlockedRows);
        }

        return Mathf.Clamp(Math.Max(unlockedRows, QuickSlotProgressionCachedRows), 1, reservedRows);
    }

    private static bool IsQuickSlotUnlocked(Player? player, SlotDefinition slot)
    {
        return slot.Kind != SlotKind.Quick || player == null || slot.QuickSlotIndex >= 0 && slot.QuickSlotIndex < GetUnlockedQuickSlotCount(player);
    }

    private static bool IsSpecialSlotUnlocked(Player? player, Inventory? inventory, SlotDefinition slot)
    {
        return slot.Kind == SlotKind.Quick
            ? IsQuickSlotUnlocked(player, slot)
            : IsEquipmentSlotUnlocked(player, inventory, slot);
    }

    private static bool IsEquipmentSlotUnlocked(Player? player, Inventory? inventory, SlotDefinition slot)
    {
        if (slot.Kind == SlotKind.Quick || !IsEquipmentSlotProgressionEnabled() || player == null)
        {
            return true;
        }

        if (InventoryContainsAcceptedSlotItem(inventory, slot) ||
            PlayerHasAcceptedEquippedItem(player, slot))
        {
            return true;
        }

        RefreshEquipmentSlotUnlockCache(player);
        if (InventoryDefinitions.EquipmentSlotUnlockCache.TryGetValue(slot.Id, out bool cached))
        {
            return cached;
        }

        bool unlocked = PlayerKnowsAcceptedSlotItem(player, slot);
        InventoryDefinitions.EquipmentSlotUnlockCache[slot.Id] = unlocked;
        return unlocked;
    }

    private static bool IsEquipmentSlotProgressionEnabled()
    {
        return _equipmentSlotProgressionEnabled != null && _equipmentSlotProgressionEnabled.Value.IsOn();
    }

    private static bool InventoryContainsAcceptedSlotItem(Inventory? inventory, SlotDefinition slot)
    {
        if (inventory?.m_inventory == null)
        {
            return false;
        }

        foreach (ItemData item in inventory.m_inventory)
        {
            if (item != null && slot.Accepts(item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PlayerHasAcceptedEquippedItem(Player player, SlotDefinition slot)
    {
        Humanoid humanoid = player;
        ItemData? item = slot.Id switch
        {
            "helmet" => humanoid.m_helmetItem,
            "chest" => humanoid.m_chestItem,
            "legs" => humanoid.m_legItem,
            "cape" => humanoid.m_shoulderItem,
            "trinket" => humanoid.m_trinketItem,
            "utility" => humanoid.m_utilityItem,
            _ => null
        };

        return item != null && slot.Accepts(item);
    }

    private static void RefreshEquipmentSlotUnlockCache(Player player)
    {
        string signature = GetEquipmentSlotUnlockCacheSignature(player);
        if (string.Equals(InventoryDefinitions.EquipmentSlotUnlockCacheSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        InventoryDefinitions.EquipmentSlotUnlockCache.Clear();
        InventoryDefinitions.EquipmentSlotUnlockCacheSignature = signature;
        InvalidateVisibleCustomPanelSlots();
    }

    private static string GetVisibleEquipmentSlotUnlockSignature(Player player, Inventory? inventory)
    {
        int objectDbCount = ObjectDB.instance?.m_items?.Count ?? -1;
        return $"{GetPlayerId(player)}|{InventoryDefinitions.SlotDefinitionVersion}|{objectDbCount}|{GetKnownMaterialHash(player)}|{GetAcceptedEquipmentInventoryHash(inventory)}";
    }

    private static string GetEquipmentSlotUnlockCacheSignature(Player player)
    {
        int objectDbCount = ObjectDB.instance?.m_items?.Count ?? -1;
        return $"{GetPlayerId(player)}|{InventoryDefinitions.SlotDefinitionVersion}|{objectDbCount}|{GetKnownMaterialHash(player)}";
    }

    private static int GetKnownMaterialHash(Player player)
    {
        unchecked
        {
            int hash = 17;
            if (player.m_knownMaterial != null)
            {
                foreach (string material in player.m_knownMaterial)
                {
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(material ?? "");
                }
            }

            return hash;
        }
    }

    private static int GetAcceptedEquipmentInventoryHash(Inventory? inventory)
    {
        unchecked
        {
            int hash = 17;
            if (inventory?.m_inventory != null)
            {
                foreach (ItemData item in inventory.m_inventory)
                {
                    if (item?.m_shared == null)
                    {
                        continue;
                    }

                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(item.m_shared.m_name ?? "");
                    hash = hash * 31 + item.m_quality;
                    hash = hash * 31 + item.m_variant;
                }
            }

            return hash;
        }
    }

    private static bool PlayerKnowsAcceptedSlotItem(Player player, SlotDefinition slot)
    {
        if (player.m_knownMaterial == null || player.m_knownMaterial.Count == 0)
        {
            return false;
        }

        RefreshItemNameTokens();
        ObjectDB objectDb = ObjectDB.instance;
        if (IsUnityNull(objectDb) || objectDb.m_items == null)
        {
            return false;
        }

        foreach (GameObject prefab in objectDb.m_items)
        {
            if (IsUnityNull(prefab))
            {
                continue;
            }

            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            ItemData? item = drop?.m_itemData;
            if (item?.m_shared == null || !slot.Accepts(item))
            {
                continue;
            }

            if (PlayerKnowsItem(player, prefab, item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PlayerKnowsItem(Player player, GameObject prefab, ItemData item)
    {
        string sharedName = item.m_shared?.m_name ?? "";
        return PlayerKnowsMaterial(player, sharedName) ||
               PlayerKnowsMaterialToken(player, prefab.name) ||
               PlayerKnowsMaterialToken(player, sharedName) ||
               PlayerKnowsMaterialToken(player, StripLocalizationToken(sharedName));
    }

    private static bool PlayerKnowsMaterialToken(Player player, string token)
    {
        return ItemNameTokens.TryGetValue(CleanPrefabName(token), out string sharedName) &&
               PlayerKnowsMaterial(player, sharedName);
    }

    private static bool PlayerKnowsMaterial(Player player, string sharedName)
    {
        return !string.IsNullOrWhiteSpace(sharedName) &&
               player.m_knownMaterial != null &&
               player.m_knownMaterial.Contains(sharedName);
    }

    private static void InvalidateSlotDefinitionCaches()
    {
        unchecked
        {
            InventoryDefinitions.SlotDefinitionVersion++;
        }

        InventoryDefinitions.CustomPanelSlotCacheVersion = -1;
        InventoryDefinitions.QuickPanelSlotCacheVersion = -1;
        InventoryDefinitions.QuickPanelSlotCacheUnlockedCount = -1;
        InventoryDefinitions.QuickSlotDefinitionCacheVersion = -1;
        InventoryDefinitions.CustomPanelSlotCache.Clear();
        InventoryDefinitions.QuickPanelSlotCache.Clear();
        InventoryDefinitions.QuickSlotDefinitionCache.Clear();
        InvalidateVisibleCustomPanelSlots();
    }

    private static void InvalidateVisibleCustomPanelSlots()
    {
        InventoryDefinitions.VisibleCustomPanelSlotCacheVersion = -1;
        InventoryDefinitions.VisibleCustomPanelSlotCacheSignature = "";
    }

    private static bool IsRowUnlocked(Player player, string configuredItems)
    {
        foreach (string token in SplitConfiguredList(configuredItems))
        {
            if (player.m_knownMaterial.Contains(token))
            {
                return true;
            }

            if (ItemNameTokens.TryGetValue(token, out string sharedName) && player.m_knownMaterial.Contains(sharedName))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> SplitConfiguredList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string token in value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = token.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}
