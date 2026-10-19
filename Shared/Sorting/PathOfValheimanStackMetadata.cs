using System;
using System.Collections.Generic;

#if INVENTORY_ACTIONS
namespace InventoryActions;
#else
namespace InventorySlots;
#endif

internal static class PathOfValheimanStackMetadata
{
    internal static void Register(IEnumerable<string> keys)
    {
        foreach (string key in keys)
        {
            // PoV assigns these per newly crafted stack. They track the original
            // crafted object for quests, not food/brew strength. Keep destination
            // provenance as a pair (including absence); never stamp the remainder
            // of a partially consumed source or copy half of its provenance.
            bool craftProvenance = key == "pt.work.made" || key == "pt.work.made.id";
            StackMetadataPolicy.Register(key, (destination, _) => destination,
                craftProvenance ? static (_, _) => true :
                    static (a, b) => string.Equals(a, b, StringComparison.Ordinal));
        }
    }
}
