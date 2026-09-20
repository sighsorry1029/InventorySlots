using System;
using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static List<string> _emptyFavoriteRestockKeys = new();

    // Called only by the area-transfer executor, after access/ownership and
    // inventory refresh. This adds targets; it does not bypass the transfer policy.
    private static int RestockMissingFavoriteItems(Player player, Inventory inventory, Inventory containerInventory)
    {
        if (_emptyFavoriteRestockKeys.Count == 0) return 0;
        // Loading saved bindings must not depend on whether live inventory
        // observation is currently deferred by slot maintenance.
        EnsureFavoritesLoaded(player);
        RememberFavoriteSlotItems(player);
#if INVENTORY_SLOTS
        Dictionary<string, int> limits = InventoryDefinitions.RestockTargetStackLimits;
        const ContainerTakeStacksMode mode = ContainerTakeStacksMode.AreaFavoriteRestock;
#else
        Dictionary<string, int> limits = Runtime.RestockTargetStackLimits;
        const RestockMode mode = RestockMode.AreaFavoriteRestock;
#endif
        int movedAmount = 0;
        List<ItemData> seededTargets = new();
        ItemData[] sources = containerInventory.GetAllItems().ToArray();
        foreach (string key in _emptyFavoriteRestockKeys)
        {
            foreach (ItemData source in sources)
            {
                if (source?.m_shared == null || source.m_stack <= 0 || source.m_shared.m_maxStackSize <= 1 ||
                    !containerInventory.GetAllItems().Contains(source) || !CanUseContainerActionStacking(source) ||
                    RestockTargetLimitCore.ResolveConfiguredKey(limits, GetRestockTargetLookupTokens(source)) != key) continue;

                while (source.m_stack > 0 && containerInventory.GetAllItems().Contains(source) &&
                       TryFindEmptyFavoriteRestockCell(player, inventory, source, out Vector2i cell))
                {
                    int amount = GetRestockTransferAmount(containerInventory, source, GetRestockTargetStack(source), mode);
                    if (amount <= 0) break;

                    int before = source.m_stack;
                    // Native positional transfer clones the actual source, preserving
                    // quality, custom data and cheat identity. Never fabricate a prefab item.
                    bool movedOk = inventory.MoveItemToThis(containerInventory, source, amount, cell.x, cell.y);
                    int moved = CountMovedFromContainerSource(containerInventory, source, before, amount, movedOk);
                    if (moved <= 0) break;
                    movedAmount += moved;
                    ItemData target = inventory.GetItemAt(cell.x, cell.y);
                    if (target == null) break;
                    seededTargets.Clear();
                    seededTargets.Add(target);
                    // Finish this slot before restoring a second remembered slot
                    // of the same item. The source snapshot is revalidated above.
                    movedAmount += RestockTargetsFromContainer(inventory, containerInventory, seededTargets, mode);
                }
            }
        }

        if (movedAmount > 0)
        {
            RememberFavoriteSlotItems(player);
#if INVENTORY_SLOTS
            containerInventory.Changed();
#else
            NotifyInventoryChanged(containerInventory);
#endif
        }
        return movedAmount;
    }

    private static bool HasExistingFavoriteRestockItem(Player player, Inventory inventory, ItemData source)
    {
        // Existing stacks suppress anonymous fallback, but do not erase the
        // player's choice to keep this item in several remembered favorite slots.
        return inventory.GetAllItems().Any(item => item?.m_shared != null && item.m_stack > 0 &&
            string.Equals(item.m_shared.m_name, source.m_shared.m_name, StringComparison.OrdinalIgnoreCase) &&
            IsEligibleFavoriteRestockCell(player, inventory, item.m_gridPos));
    }

    private static bool TryFindEmptyFavoriteRestockCell(Player player, Inventory inventory, ItemData source, out Vector2i cell)
    {
        List<Vector2i> orderedFavorites = RememberedFavoriteCells.OrderBy(pos => pos.y).ThenBy(pos => pos.x).ToList();
        return FavoriteSlotMemoryCore.TrySelect(orderedFavorites, FavoriteSlotItems, GetFavoriteMemoryPrefab(source),
            HasExistingFavoriteRestockItem(player, inventory, source), candidate =>
        {
            if (candidate.x < 0 || candidate.y < 0 || candidate.x >= inventory.GetWidth() || candidate.y >= inventory.GetHeight() ||
                !IsEligibleFavoriteRestockCell(player, inventory, candidate) || inventory.GetItemAt(candidate.x, candidate.y) != null) return false;
#if INVENTORY_SLOTS
            // Quick slots can restrict which item types they accept.
            if (!CanUseCell(player, inventory, source, candidate)) return false;
#endif
            return true;
        }, out cell);
    }

    private static bool IsEligibleFavoriteRestockCell(Player player, Inventory inventory, Vector2i cell)
    {
#if INVENTORY_SLOTS
        return InventoryActionCellPolicyCore.CanUseFavoriteRestockTarget(GetInventoryCellKind(player, inventory, cell)) &&
            IsFavoriteSlot(player, cell);
#else
        return CanFavoriteCell(inventory, cell) && IsFavoriteSlot(player, inventory, cell);
#endif
    }
}
