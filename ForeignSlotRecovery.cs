using System;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static int EnsureForeignSlotItemsPreserved(Player player, Inventory inventory, int minimumHeight, bool recoverToRegularCells, bool warnLockedRows, out bool changed)
    {
        changed = false;
        if (inventory == null)
        {
            return minimumHeight;
        }

        if (recoverToRegularCells)
        {
            foreach (ItemData item in inventory.m_inventory.ToList())
            {
                if (!ShouldRecoverForeignSlotItem(player, inventory, item, minimumHeight))
                {
                    continue;
                }

                Vector2i originalPos = item.m_gridPos;
                if (TryFindFreeAutomaticPlacementCell(
                        player,
                        inventory,
                        incoming: null,
                        out Vector2i target))
                {
                    item.m_gridPos = target;
                    ClearForeignSlotItemState(player, item);
                    changed = true;
                    InventorySafety.ForeignSlotPreservationWarnings.Remove(GetForeignSlotPreservationWarningKey(item, originalPos));
                }
            }
        }

        int targetHeight = GetInventoryPreservationHeight(inventory, minimumHeight);
        if (targetHeight > minimumHeight || recoverToRegularCells)
        {
            foreach (ItemData item in inventory.m_inventory)
            {
                if (ShouldWarnForeignSlotItemPreserved(player, inventory, item, minimumHeight, warnLockedRows))
                {
                    WarnForeignSlotItemPreserved(player, inventory, item, minimumHeight);
                }
            }
        }

        return targetHeight;
    }

    internal static int GetInventoryPreservationHeight(Inventory inventory, int minimumHeight)
    {
        int targetHeight = minimumHeight;
        if (inventory == null)
        {
            return targetHeight;
        }

        int width = inventory.GetWidth();
        foreach (ItemData item in inventory.m_inventory)
        {
            if (ShouldPreserveForeignSlotHeight(item, minimumHeight) && item.m_gridPos.x >= 0 && item.m_gridPos.x < width)
            {
                targetHeight = Math.Max(targetHeight, item.m_gridPos.y + 1);
            }
        }

        return targetHeight;
    }

    private static bool ShouldRecoverForeignSlotItem(Player player, Inventory inventory, ItemData? item, int expectedFullHeight)
    {
        if (item == null)
        {
            return false;
        }

        if (ShouldPreserveForeignSlotHeight(item, expectedFullHeight))
        {
            return true;
        }

        if (IsLegacyExtraSlotsItem(item) &&
            item.m_gridPos.y >= GetFixedRegularRows() &&
            !TryGetSlotAtGridPos(inventory, item.m_gridPos, out _))
        {
            return true;
        }

        return !ShouldPreserveProgressiveRowsDuringLoad(inventory, player) &&
               !item.m_equipped &&
               !((Humanoid)player).IsItemEquiped(item) &&
               IsRegularRowProgressionLookupReady(player) &&
               GetInventoryCellKind(player, inventory, item.m_gridPos) == InventoryCellKind.RegularLocked;
    }

    private static bool ShouldPreserveForeignSlotHeight(ItemData? item, int expectedFullHeight)
    {
        return item != null &&
               item.m_gridPos.y >= expectedFullHeight &&
               item.m_gridPos.y >= 0;
    }

    private static bool IsLegacyExtraSlotsItem(ItemData item)
    {
        return item.m_customData != null &&
               (item.m_customData.ContainsKey(ExtraSlotsEquippedSlotKey) ||
                item.m_customData.ContainsKey(ExtraSlotsEquippedByKey) ||
                item.m_customData.ContainsKey(ExtraSlotsEquippedWeaponShieldKey));
    }

    private static bool ShouldWarnForeignSlotItemPreserved(Player player, Inventory inventory, ItemData? item, int expectedFullHeight, bool warnLockedRows)
    {
        if (item == null)
        {
            return false;
        }

        return ShouldPreserveForeignSlotHeight(item, expectedFullHeight) ||
               warnLockedRows && GetInventoryCellKind(player, inventory, item.m_gridPos) == InventoryCellKind.RegularLocked;
    }

    internal static bool ShouldShowLockedInventoryCellForRecovery(Player player, Inventory inventory, Vector2i pos)
    {
        ItemData item = inventory.GetItemAt(pos.x, pos.y);
        return item != null && GetInventoryCellKind(player, inventory, pos) == InventoryCellKind.RegularLocked;
    }

    private static void ClearForeignSlotItemState(Player player, ItemData item)
    {
        bool removedLegacyState = ClearLegacyExtraSlotsItemState(item);
        bool equipmentStateChanged = ForceClearEquipmentReference(player, item);
        if (removedLegacyState)
        {
            ClearSlotActionState(item);
        }

        if (equipmentStateChanged)
        {
            RefreshExternalEquipmentEffects(player);
        }
    }

    private static bool ClearLegacyExtraSlotsItemState(ItemData item)
    {
        if (item?.m_customData == null)
        {
            return false;
        }

        bool removed = false;
        removed |= item.m_customData.Remove(ExtraSlotsEquippedByKey);
        removed |= item.m_customData.Remove(ExtraSlotsEquippedSlotKey);
        removed |= item.m_customData.Remove(ExtraSlotsEquippedWeaponShieldKey);
        return removed;
    }

    private static void WarnForeignSlotItemPreserved(Player player, Inventory inventory, ItemData item, int expectedFullHeight)
    {
        string key = GetForeignSlotPreservationWarningKey(item, item.m_gridPos);
        if (!InventorySafety.ForeignSlotPreservationWarnings.Add(key))
        {
            return;
        }

        string source = IsLegacyExtraSlotsItem(item) ? "ExtraSlots" : "foreign inventory";
        string reason = ShouldPreserveForeignSlotHeight(item, expectedFullHeight)
            ? "outside the InventorySlots grid"
            : GetInventoryCellKind(player, inventory, item.m_gridPos) == InventoryCellKind.RegularLocked
                ? "in a locked hidden inventory row"
                : "in a foreign slot position";
        Log.LogWarning($"{source} slot item {GetForeignSlotItemName(item)} is {reason} at {FormatGridPos(item.m_gridPos)}. No regular inventory cell was free, so InventorySlots is preserving the item in place. Free inventory space and reconnect to let it recover.");
    }

    private static string GetForeignSlotPreservationWarningKey(ItemData item, Vector2i pos)
    {
        return $"{GetForeignSlotItemName(item)}|{pos.x},{pos.y}";
    }

    private static string GetForeignSlotItemName(ItemData item)
    {
        string prefab = GetItemPrefabName(item);
        return !string.IsNullOrWhiteSpace(prefab) ? prefab : item.m_shared?.m_name ?? "<unknown>";
    }

    private static string FormatGridPos(Vector2i pos)
    {
        return $"({pos.x},{pos.y})";
    }
}
