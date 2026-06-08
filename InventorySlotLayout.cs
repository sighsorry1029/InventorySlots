using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static string QuickSlotProgressionCachePlayerId = "";
    private static int QuickSlotProgressionCachedRows;

    internal static bool TryGetSlotAtGridPos(Inventory inventory, Vector2i pos, out SlotDefinition? slot)
    {
        return InventoryDefinitionController.TryGetSlotAtGridPos(InventoryDefinitions, inventory, pos, GetFixedRegularRows(), out slot);
    }

    internal static bool TryGetSlotById(string id, out SlotDefinition? slot)
    {
        return InventoryDefinitionController.TryGetSlotById(InventoryDefinitions, id, out slot);
    }

    private static List<SlotDefinition> GetCustomPanelSlots()
    {
        return InventoryDefinitionController.GetCustomPanelSlots(InventoryDefinitions);
    }

    private static List<SlotDefinition> GetQuickPanelSlots(Player? player = null)
    {
        return InventoryDefinitionController.GetQuickPanelSlots(InventoryDefinitions, GetUnlockedQuickSlotCount(player));
    }

    private static bool TryGetQuickSlotDefinition(int quickSlotIndex, out SlotDefinition? slot)
    {
        return InventoryDefinitionController.TryGetQuickSlotDefinition(InventoryDefinitions, quickSlotIndex, out slot);
    }

    private static Vector2i GetSlotGridPos(Inventory inventory, SlotDefinition slot)
    {
        return InventoryDefinitionController.GetSlotGridPos(InventoryDefinitions, inventory, slot, GetFixedRegularRows());
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

    private static int GetFullHeight()
    {
        return GetFullHeightForWidth(InventoryWidth);
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
        return InventoryDefinitionController.GetSlotTailRows(InventoryDefinitions, width);
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

    private static void InvalidateSlotDefinitionCaches()
    {
        InventoryDefinitionController.InvalidateCaches(InventoryDefinitions);
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
