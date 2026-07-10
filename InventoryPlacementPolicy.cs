using System;
using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static InventoryPlacementScope GetInventoryPlacementScope(Inventory inventory, out Player? player)
    {
        player = null;
        if (inventory == null)
        {
            return InventoryPlacementScope.General;
        }

        if (TryGetLocalPlayerInventory(inventory, out player))
        {
            return ShouldPreserveProgressiveRowsDuringLoad(inventory, player)
                ? InventoryPlacementScope.LoadPreservation
                : InventoryPlacementScope.LocalPlayer;
        }

        if (IsInventoryLoadPreserving(inventory))
        {
            return InventoryPlacementScope.LoadPreservation;
        }

        return IsContainerInventory(inventory)
            ? InventoryPlacementScope.Container
            : InventoryPlacementScope.General;
    }

    private static bool IsContainerInventory(Inventory inventory)
    {
        if (inventory == null)
        {
            return false;
        }

        Container? currentContainer = InventoryGui.instance != null ? InventoryGui.instance.m_currentContainer : null;
        if (currentContainer != null && !IsUnityNull(currentContainer) && currentContainer.m_inventory == inventory)
        {
            return true;
        }

        for (int i = InventoryContainers.KnownContainers.Count - 1; i >= 0; i--)
        {
            Container container = InventoryContainers.KnownContainers[i];
            if (container == null || IsUnityNull(container))
            {
                InventoryContainers.KnownContainers.RemoveAt(i);
                continue;
            }

            if (container.m_inventory == inventory)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryOverrideFindEmptySlot(Inventory inventory, ref Vector2i result)
    {
        InventoryPlacementScope scope = GetInventoryPlacementScope(inventory, out Player? player);
        switch (InventoryPlacementPolicyCore.SelectQueryPlan(scope))
        {
            case InventoryPlacementQueryPlan.TopFirstAllCells:
                result = FindTopFirstEmptySlot(inventory);
                return false;
            case InventoryPlacementQueryPlan.LoadPreservationRegularCells:
                result = FindTopFirstRegularLoadPreservationSlot(inventory);
                return false;
            case InventoryPlacementQueryPlan.LocalPlayerRegularCells:
                result = TryFindFreeRegularCell(player!, inventory, out Vector2i pos)
                    ? pos
                    : new Vector2i(-1, -1);
                return false;
            default:
                return true;
        }
    }

    private static Vector2i FindTopFirstEmptySlot(Inventory inventory)
    {
        HashSet<Vector2i> occupied = BuildOccupiedCellSet(inventory);
        bool found = InventoryPlacementPolicyCore.TrySelectTopFirstCell(
            inventory.GetWidth(),
            inventory.GetHeight(),
            (_, _) => true,
            (x, y) => IsCellOccupied(occupied, x, y),
            out InventorySlotSafetyCore.GridCell cell);
        return found
            ? new Vector2i(cell.X, cell.Y)
            : new Vector2i(-1, -1);
    }

    internal static bool TryOverrideFindFreeStackItem(Inventory inventory, string name, int quality, float worldLevel, ref ItemData? result)
    {
        result = null;
        if (inventory == null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        ItemData? sourceItem = GetCurrentInventoryAddItemDataStackLookupItem();
        if (sourceItem != null)
        {
            if (IsTrustedCustomDataStackingItem(sourceItem))
            {
                return true;
            }

            if (!HasNoCustomData(sourceItem))
            {
                return false;
            }
        }

        ItemData? best = null;
        foreach (ItemData item in inventory.m_inventory)
        {
            if (!CanStackIntoItem(item, name, quality, worldLevel))
            {
                continue;
            }

            if (best == null ||
                item.m_gridPos.y < best.m_gridPos.y ||
                item.m_gridPos.y == best.m_gridPos.y && item.m_gridPos.x < best.m_gridPos.x)
            {
                best = item;
            }
        }

        result = best;
        return false;
    }

    private static Vector2i FindTopFirstRegularLoadPreservationSlot(Inventory inventory)
    {
        if (TryFindFreeRegularLoadPreservationCell(inventory, out Vector2i pos))
        {
            return pos;
        }

        return new Vector2i(-1, -1);
    }

    private static bool TryFindFreeRegularLoadPreservationCell(Inventory inventory, out Vector2i pos)
    {
        int regularRows = Math.Min(GetFixedRegularRows(), inventory.GetHeight());
        HashSet<Vector2i> occupied = BuildOccupiedCellSet(inventory);
        bool found = InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventory.GetWidth(),
            regularRows,
            (x, y) => !IsExternalReservedForCompat(new Vector2i(x, y)),
            (x, y) => IsCellOccupied(occupied, x, y),
            out InventorySlotSafetyCore.GridCell cell);
        pos = new Vector2i(cell.X, cell.Y);
        return found;
    }

    private static bool IsInventorySlotsTailCell(Inventory inventory, Vector2i pos) =>
        inventory != null &&
        InventorySlotSafetyCore.IsInventorySlotsTailCell(
            inventory.GetWidth(),
            GetFixedRegularRows(),
            new InventorySlotSafetyCore.GridCell(pos.x, pos.y));

    private static int CountRegularLoadPreservationEmptyCells(Inventory inventory)
    {
        int count = 0;
        int regularRows = Math.Min(GetFixedRegularRows(), inventory.GetHeight());
        HashSet<Vector2i> occupied = BuildOccupiedCellSet(inventory);
        for (int y = 0; y < regularRows; y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                Vector2i pos = new(x, y);
                if (!IsExternalReservedForCompat(pos) && !IsCellOccupied(occupied, x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool CanStackIntoItem(ItemData? item, string name, int quality, float worldLevel)
    {
        return item?.m_shared != null &&
               HasNoCustomData(item) &&
               item.m_shared.m_name == name &&
               item.m_quality == quality &&
               item.m_stack < item.m_shared.m_maxStackSize &&
               (float)item.m_worldLevel == worldLevel;
    }

    internal static bool BeginInventoryAddItemDataStackLookup(ItemData item)
    {
        PushInventoryAddItemDataStackLookupItem(item);
        return true;
    }

    internal static void EndInventoryAddItemDataStackLookup(bool active)
    {
        if (active)
        {
            PopInventoryAddItemDataStackLookupItem();
        }
    }

    internal static bool TryOverrideGetEmptySlots(Inventory inventory, ref int result)
    {
        InventoryPlacementScope scope = GetInventoryPlacementScope(inventory, out Player? player);
        switch (InventoryPlacementPolicyCore.SelectQueryPlan(scope))
        {
            case InventoryPlacementQueryPlan.TopFirstAllCells:
                result = CountAllEmptyCells(inventory);
                return false;
            case InventoryPlacementQueryPlan.LoadPreservationRegularCells:
                result = CountRegularLoadPreservationEmptyCells(inventory);
                return false;
            case InventoryPlacementQueryPlan.LocalPlayerRegularCells:
                result = CountUsableRegularEmptyCells(inventory, player!);
                return false;
            default:
                return true;
        }
    }

    private static int CountAllEmptyCells(Inventory inventory) =>
        CountAllEmptyCells(inventory, BuildOccupiedCellSet(inventory));

    private static int CountAllEmptyCells(Inventory inventory, HashSet<Vector2i> occupied) =>
        InventoryPlacementPolicyCore.CountTopFirstPolicyEmptyCells(
            inventory.GetWidth(),
            inventory.GetHeight(),
            (_, _) => true,
            (x, y) => IsCellOccupied(occupied, x, y));

    private static int CountUsableRegularEmptyCells(Inventory inventory, Player player)
    {
        return CountUsableRegularEmptyCells(inventory, player, BuildOccupiedCellSet(inventory));
    }

    private static int CountUsableRegularEmptyCells(Inventory inventory, Player player, HashSet<Vector2i> occupied)
    {
        int count = 0;
        int usableRows = Math.Min(GetUsableRegularRows(player), inventory.GetHeight());
        for (int y = 0; y < usableRows; y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                Vector2i pos = new(x, y);
                if (IsUsableRegularCell(inventory, player, pos) && !IsCellOccupied(occupied, x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountUsableRegularEmptyCellsCached(Inventory inventory, Player player)
    {
        int context = ComputeInventoryPlacementCacheContext(player, inventory);
        if (ReferenceEquals(InventorySafety.UsableRegularEmptyCellCacheInventory, inventory) &&
            InventorySafety.UsableRegularEmptyCellCacheVersion == InventorySafety.InventoryPlacementCacheVersion &&
            InventorySafety.UsableRegularEmptyCellCacheContext == context)
        {
            return InventorySafety.UsableRegularEmptyCellCacheCount;
        }

        int count = CountUsableRegularEmptyCells(inventory, player);
        InventorySafety.UsableRegularEmptyCellCacheInventory = inventory;
        InventorySafety.UsableRegularEmptyCellCacheVersion = InventorySafety.InventoryPlacementCacheVersion;
        InventorySafety.UsableRegularEmptyCellCacheContext = context;
        InventorySafety.UsableRegularEmptyCellCacheCount = count;
        return count;
    }

    internal static bool TryOverrideHaveEmptySlot(Inventory inventory, ref bool result)
    {
        InventoryPlacementScope scope = GetInventoryPlacementScope(inventory, out Player? player);
        switch (InventoryPlacementPolicyCore.SelectQueryPlan(scope))
        {
            case InventoryPlacementQueryPlan.TopFirstAllCells:
                result = FindTopFirstEmptySlot(inventory).x >= 0;
                return false;
            case InventoryPlacementQueryPlan.LoadPreservationRegularCells:
                result = TryFindFreeRegularLoadPreservationCell(inventory, out _);
                return false;
            case InventoryPlacementQueryPlan.LocalPlayerRegularCells:
                result = TryFindFreeRegularCell(player!, inventory, out _);
                return false;
            default:
                return true;
        }
    }

    internal static bool TryOverrideCanAddItem(Inventory inventory, ItemData item, int stack, ref bool result)
    {
        if (item?.m_shared == null || !TryGetLocalPlayerInventory(inventory, out Player? player))
        {
            return true;
        }

        result = CanAddItemToUsablePlayerSlots(player!, inventory, item, stack);
        return false;
    }

    private static bool CanAddItemToUsablePlayerSlots(Player player, Inventory inventory, ItemData item, int stack)
    {
        int requestedStack = stack <= 0 ? item.m_stack : stack;
        if (requestedStack <= 0)
        {
            return true;
        }

        if (TryGetCachedCanAddItemFailure(player, inventory, item, requestedStack))
        {
            return false;
        }

        int maxStack = Math.Max(1, item.m_shared.m_maxStackSize);
        long capacity = CountStackSpaceForIncomingItem(inventory, item);
        if (capacity >= requestedStack)
        {
            return true;
        }

        capacity += (long)CountUsableRegularEmptyCellsCached(inventory, player) * maxStack;
        if (capacity >= requestedStack)
        {
            return true;
        }

        if (CanAutoPlaceItemInSpecialSlot(player, inventory, item))
        {
            capacity += maxStack;
        }

        bool canAdd = capacity >= requestedStack;
        if (!canAdd)
        {
            CacheCanAddItemFailure(player, inventory, item, requestedStack);
        }

        return canAdd;
    }

    private static bool TryGetCachedCanAddItemFailure(Player player, Inventory inventory, ItemData item, int requestedStack)
    {
        if (!CanCacheCanAddItemFailure(item))
        {
            return false;
        }

        int context = ComputeInventoryPlacementCacheContext(player, inventory);
        int itemKey = ComputeCanAddItemCacheItemKey(item);
        return ReferenceEquals(InventorySafety.CanAddItemFailureCacheInventory, inventory) &&
               InventorySafety.CanAddItemFailureCacheVersion == InventorySafety.InventoryPlacementCacheVersion &&
               InventorySafety.CanAddItemFailureCacheContext == context &&
               InventorySafety.CanAddItemFailureCacheItemKey == itemKey &&
               InventorySafety.CanAddItemFailureCacheRequestedStack == requestedStack;
    }

    private static void CacheCanAddItemFailure(Player player, Inventory inventory, ItemData item, int requestedStack)
    {
        if (!CanCacheCanAddItemFailure(item))
        {
            return;
        }

        InventorySafety.CanAddItemFailureCacheInventory = inventory;
        InventorySafety.CanAddItemFailureCacheVersion = InventorySafety.InventoryPlacementCacheVersion;
        InventorySafety.CanAddItemFailureCacheContext = ComputeInventoryPlacementCacheContext(player, inventory);
        InventorySafety.CanAddItemFailureCacheItemKey = ComputeCanAddItemCacheItemKey(item);
        InventorySafety.CanAddItemFailureCacheRequestedStack = requestedStack;
    }

    private static bool CanCacheCanAddItemFailure(ItemData item)
    {
        return item?.m_shared != null && (HasNoCustomData(item) || IsTrustedCustomDataStackingItem(item));
    }

    private static int ComputeInventoryPlacementCacheContext(Player player, Inventory inventory)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (inventory?.GetWidth() ?? 0);
            hash = hash * 31 + (inventory?.GetHeight() ?? 0);
            hash = hash * 31 + GetUsableRegularRows(player);
            hash = hash * 31 + InventoryDefinitions.SlotDefinitionVersion;
            hash = hash * 31 + GetKnownMaterialHash(player);
            hash = hash * 31 + (inventory?.m_inventory?.Count ?? 0);
            return hash;
        }
    }

    private static int ComputeCanAddItemCacheItemKey(ItemData item)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(item.m_shared?.m_name ?? "");
            hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(GetItemPrefabName(item));
            hash = hash * 31 + item.m_quality;
            hash = hash * 31 + item.m_worldLevel.GetHashCode();
            hash = hash * 31 + Math.Max(1, item.m_shared?.m_maxStackSize ?? 1);
            return hash;
        }
    }

    private static HashSet<Vector2i> BuildOccupiedCellSet(Inventory inventory)
    {
        HashSet<Vector2i> occupied = new();
        if (inventory?.m_inventory == null)
        {
            return occupied;
        }

        foreach (ItemData item in inventory.m_inventory)
        {
            if (item != null)
            {
                occupied.Add(item.m_gridPos);
            }
        }

        return occupied;
    }

    private static bool IsCellOccupied(HashSet<Vector2i> occupied, int x, int y)
    {
        return occupied.Contains(new Vector2i(x, y));
    }

    private static void InvalidateInventoryPlacementCaches()
    {
        unchecked
        {
            InventorySafety.InventoryPlacementCacheVersion++;
        }

        InventorySafety.UsableRegularEmptyCellCacheInventory = null;
        InventorySafety.UsableRegularEmptyCellCacheVersion = -1;
        InventorySafety.UsableRegularEmptyCellCacheContext = 0;
        InventorySafety.UsableRegularEmptyCellCacheCount = 0;
        InventorySafety.CanAddItemFailureCacheInventory = null;
        InventorySafety.CanAddItemFailureCacheVersion = -1;
        InventorySafety.CanAddItemFailureCacheContext = 0;
        InventorySafety.CanAddItemFailureCacheItemKey = 0;
        InventorySafety.CanAddItemFailureCacheRequestedStack = 0;
    }

    private static int CountStackSpaceForIncomingItem(Inventory inventory, ItemData item)
    {
        if (inventory == null || item?.m_shared == null)
        {
            return 0;
        }

        bool trustedCustomDataStacking = IsTrustedCustomDataStackingItem(item);
        if (!trustedCustomDataStacking && !HasNoCustomData(item))
        {
            return 0;
        }

        int capacity = 0;
        foreach (ItemData existing in inventory.m_inventory)
        {
            if (!CanStackIncomingItemInto(existing, item, trustedCustomDataStacking))
            {
                continue;
            }

            capacity += Math.Max(0, existing.m_shared.m_maxStackSize - existing.m_stack);
        }

        return capacity;
    }

    private static bool CanStackIncomingItemInto(ItemData? existing, ItemData incoming, bool trustedCustomDataStacking)
    {
        if (existing?.m_shared == null || incoming?.m_shared == null)
        {
            return false;
        }

        if (trustedCustomDataStacking)
        {
            return existing.m_shared.m_name == incoming.m_shared.m_name &&
                   existing.m_quality == incoming.m_quality &&
                   existing.m_stack < existing.m_shared.m_maxStackSize &&
                   (float)existing.m_worldLevel == incoming.m_worldLevel;
        }

        return CanStackIntoItem(existing, incoming.m_shared.m_name, incoming.m_quality, incoming.m_worldLevel);
    }

    internal static void OnInventoryAddItemData(Inventory inventory, ItemData item, ref bool result)
    {
        if (result || item == null || !TryGetLocalPlayerInventory(inventory, out Player? player))
        {
            return;
        }

        result = TryAutoPlaceItemInSpecialSlot(player!, inventory, item);
    }

    internal static bool TryPreserveLoadedSlotTailItem(Inventory inventory, ItemData item, ref bool result)
    {
        if (item == null)
        {
            return true;
        }

        Vector2i target = item.m_gridPos;
        if (!TryPreserveLoadedSlotTailItem(inventory, item, ref target, ref result))
        {
            return true;
        }

        return false;
    }

    private static bool TryPreserveLoadedSlotTailItem(Inventory inventory, ItemData item, ref Vector2i target, ref bool result)
    {
        if (item == null || !TryPrepareLoadPreservationTailInsert(inventory, target))
        {
            return false;
        }

        EnsureInventoryHeightForLoad(inventory);
        if (target.y >= inventory.GetHeight())
        {
            inventory.m_height = target.y + 1;
        }

        if (InventorySlotSafetyCore.TrySelectLoadPreservationTailCell(
                inventory.GetWidth(),
                inventory.GetHeight(),
                GetFixedRegularRows(),
                new InventorySlotSafetyCore.GridCell(target.x, target.y),
                (x, y) => IsInventoryCellOccupiedByOtherItem(inventory, item, x, y),
                out InventorySlotSafetyCore.GridCell selected))
        {
            target = new Vector2i(selected.X, selected.Y);
            if (target.y >= inventory.GetHeight())
            {
                inventory.m_height = target.y + 1;
            }
        }

        item.m_gridPos = target;
        if (!inventory.ContainsItem(item))
        {
            inventory.m_inventory.Add(item);
        }

        result = true;
        return true;
    }

    private static bool TryPrepareLoadPreservationTailInsert(Inventory inventory, Vector2i target)
    {
        if (inventory == null ||
            !IsInventoryLoadPreserving(inventory) ||
            !IsInventorySlotsTailCell(inventory, target))
        {
            return false;
        }

        EnsureInventoryHeightForLoad(inventory);
        if (target.y >= inventory.GetHeight())
        {
            inventory.m_height = target.y + 1;
        }

        return true;
    }

    private static bool IsInventoryCellOccupiedByOtherItem(Inventory inventory, ItemData item, int x, int y)
    {
        foreach (ItemData other in inventory.m_inventory)
        {
            if (other != null && other != item && other.m_gridPos.x == x && other.m_gridPos.y == y)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryValidatePlayerInventoryInsert(Inventory inventory, ItemData item, ref Vector2i pos, ref bool result)
    {
        return TryValidatePlayerInventoryInsert(inventory, item, ref pos, ref result, preserveLoadedTailItem: true);
    }

    private static bool TryValidatePlayerInventoryInsert(Inventory inventory, ItemData item, ref Vector2i pos, ref bool result, bool preserveLoadedTailItem)
    {
        if (item == null)
        {
            return true;
        }

        if (preserveLoadedTailItem && TryPreserveLoadedSlotTailItem(inventory, item, ref pos, ref result))
        {
            return false;
        }

        if (!preserveLoadedTailItem && TryPrepareLoadPreservationTailInsert(inventory, pos))
        {
            return true;
        }

        if (!TryGetLocalPlayerInventory(inventory, out Player? player))
        {
            return true;
        }

        if (CanUseCell(player!, inventory, item, pos))
        {
            return true;
        }

        if (TryRedirectUnsafePlayerInventoryInsert(player!, inventory, item, ref pos))
        {
            return true;
        }

        result = false;
        return false;
    }

    internal static bool TryValidatePlayerInventoryInsert(Inventory inventory, ItemData item, ref int x, ref int y, ref bool result)
    {
        Vector2i pos = new(x, y);
        bool runOriginal = TryValidatePlayerInventoryInsert(inventory, item, ref pos, ref result);
        x = pos.x;
        y = pos.y;
        return runOriginal;
    }

    internal static bool TryValidatePlayerInventoryMoveItemToThis(Inventory inventory, ref bool result, ItemData item, ref int x, ref int y)
    {
        Vector2i pos = new(x, y);
        bool runOriginal = TryValidatePlayerInventoryInsert(inventory, item, ref pos, ref result, preserveLoadedTailItem: false);
        x = pos.x;
        y = pos.y;
        return runOriginal;
    }

    internal static void OnPlayerInventoryItemPlaced(Inventory inventory, ItemData item, Vector2i pos, bool result)
    {
        if (!result || item == null || !TryGetLocalPlayerInventory(inventory, out Player? player))
        {
            return;
        }

        if (TryGetSlotAtGridPos(inventory, pos, out SlotDefinition? slot))
        {
            ItemData placed = inventory.GetItemAt(pos.x, pos.y) ?? item;
            TryEquipIntoSlot(player!, inventory, placed, slot!);
        }
    }
}
