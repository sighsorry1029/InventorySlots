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
    // Callers supply eligible favorite targets and ordinary sortable sources separately.
    // Favorites never donate, move, or disappear. Restock limits do not apply to Sort.
    private static bool FillFavoriteStacks(Inventory inventory, List<ItemData> targets, List<ItemData> sources)
    {
        bool changed = FillEpicLootFavoriteStacks(inventory, targets, sources);
        changed |= FillFavoriteStackAmounts(targets, sources);
        if (changed)
        {
            // Remove exhausted donors only after every quantity/metadata update is complete.
            // The caller notifies after the remaining ordinary stacks have also been sorted.
            foreach (ItemData source in sources.Where(item => item.m_stack == 0))
            {
                inventory.RemoveItem(source);
            }
        }

        return changed;
    }

    private static bool FillFavoriteStackAmounts(List<ItemData> targets, List<ItemData> sources)
    {
        bool changed = false;
        foreach (ItemData target in targets.OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x))
        {
            if (target?.m_shared == null || target.m_stack <= 0 || IsEpicLootStackingItem(target))
            {
                continue;
            }

            int free = Math.Max(0, target.m_shared.m_maxStackSize - target.m_stack);
            foreach (ItemData source in sources)
            {
                if (free == 0) break;
                if (source?.m_shared == null || source.m_stack <= 0 || ReferenceEquals(target, source)) continue;
                if (IsEpicLootStackingItem(source)) continue;

#if INVENTORY_SLOTS
                // Like ordinary Sort merging, never bypass an external mod's metadata policy.
                if (!CanUseStackMetadataAutomaticStacking(target) ||
                    !CanUseStackMetadataAutomaticStacking(source) ||
                    !CanShareInventoryStack(target, source) ||
                    !HasCompatibleStackMetadata(target, source)) continue;
#else
                if (!HasNoCustomData(target) || !HasNoCustomData(source) ||
                    target.m_cheated != source.m_cheated ||
                    !HasSameStackIdentity(target, source)) continue;
#endif

                int amount = Math.Min(free, source.m_stack);
#if INVENTORY_SLOTS
                MergeStackMetadata(target, source);
#endif
                target.m_stack += amount;
                source.m_stack -= amount;
                free -= amount;
                changed = true;
            }
        }

        return changed;
    }
}
