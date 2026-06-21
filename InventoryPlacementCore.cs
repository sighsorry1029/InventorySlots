using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool TryMoveToFirstFreeRegularCell(Player player, Inventory inventory, ItemData item)
    {
        if (!TryFindFreeRegularCell(player, inventory, out Vector2i candidate))
        {
            return false;
        }

        item.m_gridPos = candidate;
        return true;
    }

    private static bool TryRelocateBlockingItem(Player player, Inventory inventory, ItemData blocking, ItemData incoming)
    {
        if (inventory.ContainsItem(incoming) && IsUsableRegularCell(inventory, player, incoming.m_gridPos) && inventory.GetItemAt(incoming.m_gridPos.x, incoming.m_gridPos.y) == incoming)
        {
            blocking.m_gridPos = incoming.m_gridPos;
            return true;
        }

        return TryMoveToFirstFreeRegularCell(player, inventory, blocking);
    }

    private static bool TryRelocateSlotEquipBlockingItem(Player player, Inventory inventory, ItemData blocking, ItemData incoming, Vector2i incomingOriginalPos, ref bool incomingOriginalPosUsed)
    {
        if (!incomingOriginalPosUsed &&
            inventory.ContainsItem(incoming) &&
            IsUsableRegularCell(inventory, player, incomingOriginalPos) &&
            inventory.GetItemAt(incomingOriginalPos.x, incomingOriginalPos.y) == incoming &&
            CellContainsOnly(inventory, incomingOriginalPos, incoming))
        {
            blocking.m_gridPos = incomingOriginalPos;
            incomingOriginalPosUsed = true;
            return true;
        }

        return TryMoveToFirstFreeRegularCell(player, inventory, blocking);
    }

    private static bool CellContainsOnly(Inventory inventory, Vector2i pos, ItemData allowedItem)
    {
        foreach (ItemData item in inventory.m_inventory)
        {
            if (item != null && item != allowedItem && item.m_gridPos == pos)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUsableRegularCell(Inventory inventory, Player player, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(player, inventory, pos);
        return kind == InventoryCellKind.Hotbar || kind == InventoryCellKind.RegularUnlocked;
    }

    internal static bool TryFindFreeRegularCell(Player player, Inventory inventory, out Vector2i pos)
    {
        HashSet<Vector2i> occupied = BuildOccupiedCellSet(inventory);
        bool found = InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventory.GetWidth(),
            GetUsableRegularRows(player),
            (x, y) => IsUsableRegularCell(inventory, player, new Vector2i(x, y)),
            (x, y) => IsCellOccupied(occupied, x, y),
            out InventorySlotSafetyCore.GridCell cell);
        pos = new Vector2i(cell.X, cell.Y);
        return found;
    }

    internal static bool TryRedirectUnsafePlayerInventoryInsert(Player player, Inventory inventory, ItemData item, ref Vector2i pos)
    {
        if (player == null || inventory == null || item == null)
        {
            return false;
        }

        if (CanUseCell(player, inventory, item, pos))
        {
            return true;
        }

        if (TryFindSafeInsertCell(player, inventory, item, out Vector2i fallback))
        {
            Log.LogDebug($"Redirected unsafe inventory insert for {item.m_shared?.m_name ?? "<unknown>"} from {pos} to {fallback}.");
            pos = fallback;
            return true;
        }

        return false;
    }

    private static bool TryFindSafeInsertCell(Player player, Inventory inventory, ItemData item, out Vector2i pos)
    {
        if (TryFindFreeRegularCell(player, inventory, out pos))
        {
            return true;
        }

        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (!CanUseEmptySpecialSlot(player, inventory, item, slot))
            {
                continue;
            }

            Vector2i candidate = GetSlotGridPos(inventory, slot);
            if (CanUseCell(player, inventory, item, candidate) && CellContainsOnly(inventory, candidate, item))
            {
                pos = candidate;
                return true;
            }
        }

        pos = new Vector2i(-1, -1);
        return false;
    }

    internal static bool CanUseCell(Player player, Inventory inventory, ItemData item, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(player, inventory, pos);
        if (kind == InventoryCellKind.ExternalReserved)
        {
            return true;
        }

        if (kind is InventoryCellKind.Equipment or InventoryCellKind.CustomEquipment or InventoryCellKind.Quick)
        {
            return TryGetUsableSpecialSlotAtCell(player, inventory, item, pos, out _);
        }

        return kind == InventoryCellKind.Hotbar ||
               kind == InventoryCellKind.RegularUnlocked ||
               kind == InventoryCellKind.RegularLocked && ShouldPreserveProgressiveRowsDuringLoad(inventory, player);
    }

    internal static InventoryCellKind GetInventoryCellKind(Player? player, Inventory inventory, Vector2i pos)
    {
        if (inventory == null || IsOutOfBounds(inventory, pos))
        {
            return InventoryCellKind.Outside;
        }

        if (TryGetSlotAtGridPos(inventory, pos, out SlotDefinition? slot))
        {
            if (slot!.Kind == SlotKind.Quick && player != null && !IsQuickSlotUnlocked(player, slot))
            {
                return InventoryCellKind.QuickLocked;
            }

            if (slot.Kind != SlotKind.Quick && player != null && !IsEquipmentSlotUnlocked(player, inventory, slot))
            {
                return InventoryCellKind.EquipmentLocked;
            }

            return slot!.Kind switch
            {
                SlotKind.Quick => InventoryCellKind.Quick,
                SlotKind.CustomEquipment => InventoryCellKind.CustomEquipment,
                _ => InventoryCellKind.Equipment
            };
        }

        if (IsExternalReservedCell(pos, includeRestockableSlots: true))
        {
            return InventoryCellKind.ExternalReserved;
        }

        int fixedRows = GetFixedRegularRows();
        if (pos.y >= fixedRows)
        {
            return InventoryCellKind.Outside;
        }

        if (player != null && IsLockedRowCell(player, pos))
        {
            return InventoryCellKind.RegularLocked;
        }

        return pos.y == 0 ? InventoryCellKind.Hotbar : InventoryCellKind.RegularUnlocked;
    }

    private static bool CanUseSpecialSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        return item != null &&
               slot != null &&
               !IsJewelcraftingUtilityGemBlockedForSlot(item, slot) &&
               IsSpecialSlotUnlocked(player, inventory, slot) &&
               slot.Accepts(item);
    }

    private static bool CanUseEmptySpecialSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        return CanUseSpecialSlot(player, inventory, item, slot) &&
               FindItemForSlot(player, inventory, slot) == null;
    }

    private static bool TryGetUsableSpecialSlotAtCell(Player player, Inventory inventory, ItemData item, Vector2i pos, out SlotDefinition? slot)
    {
        slot = null;
        if (!TryGetSlotAtGridPos(inventory, pos, out SlotDefinition? resolved) ||
            resolved == null ||
            !CanUseSpecialSlot(player, inventory, item, resolved))
        {
            return false;
        }

        slot = resolved;
        return true;
    }

    private static bool TryGetEmptyUsableSpecialSlotAtCell(Player player, Inventory inventory, ItemData item, Vector2i pos, out SlotDefinition? slot)
    {
        if (!TryGetUsableSpecialSlotAtCell(player, inventory, item, pos, out slot))
        {
            return false;
        }

        return inventory.GetItemAt(pos.x, pos.y) == null;
    }

    private static bool IsRegularActionItem(Player player, Inventory inventory, ItemData item, bool includeHotbar)
    {
        InventoryCellKind kind = GetInventoryCellKind(player, inventory, item.m_gridPos);
        return InventoryActionCellPolicyCore.CanUseContainerActionSource(kind, includeHotbar);
    }

    private static bool IsFavoriteProtected(Player player, Inventory inventory, ItemData item)
    {
        return AreFavoritesEnabled() && IsFavoriteSlot(player, item.m_gridPos);
    }

    private static bool IsFavoriteSlot(Player player, Vector2i pos)
    {
        if (!AreFavoritesEnabled())
        {
            return false;
        }

        EnsureFavoritesLoaded(player);
        return FavoriteSlots.Contains(pos);
    }

    private static bool AreFavoritesEnabled()
    {
        return true;
    }
}
