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
            // Craft/brew origin and bonus-drop markers do not encode item strength.
            // Keep destination provenance (including absence), without changing a
            // partial source or stripping a world drop's bonus marker before pickup.
            // Effect values, aging clocks and other processing markers stay exact.
            bool provenance = key == "pt.work.made" || key == "pt.work.made.id" ||
                              key == "pt.work.origin" || key == "pt.bonus";
            StackMetadataPolicy.Register(key, (destination, _) => destination,
                provenance ? static (_, _) => true :
                    static (a, b) => string.Equals(a, b, StringComparison.Ordinal));
        }
    }
}
