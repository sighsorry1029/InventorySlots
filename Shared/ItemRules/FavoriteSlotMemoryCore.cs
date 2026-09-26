using System;
using System.Collections.Generic;
using System.Linq;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

// Kept independent of Unity so slot memory and destination policy can be tested
// with the same code used by both mods.
internal static class FavoriteSlotMemoryCore
{
    internal static bool Observe<T>(Dictionary<T, string> memory, ISet<T> favorites,
        Func<T, string?> occupiedPrefab) where T : notnull
    {
        bool changed = false;
        foreach (T cell in memory.Keys.Where(cell => !favorites.Contains(cell)).ToArray())
            changed |= memory.Remove(cell);
        foreach (T cell in favorites)
        {
            string? prefab = occupiedPrefab(cell);
            // Empty/temporarily unavailable cells retain their last item.
            if (prefab == null || prefab.Length == 0) continue;
            if (memory.TryGetValue(cell, out string? previous) &&
                string.Equals(previous, prefab, StringComparison.Ordinal)) continue;
            memory[cell] = prefab;
            changed = true;
        }
        return changed;
    }

    internal static bool TrySelect<T>(IReadOnlyList<T> orderedFavorites, IReadOnlyDictionary<T, string> memory,
        string prefab, bool hasExistingFavorite, Func<T, bool> canUseEmptyCell, out T cell) where T : notnull
    {
        bool hasRememberedCell = false;
        foreach (T candidate in orderedFavorites)
        {
            if (!memory.TryGetValue(candidate, out string? remembered) ||
                !string.Equals(remembered, prefab, StringComparison.Ordinal)) continue;
            hasRememberedCell = true;
            if (!canUseEmptyCell(candidate)) continue;
            cell = candidate;
            return true;
        }
        // A full/locked/occupied original slot must not cause a replacement in
        // some other slot. Unavailable items reserve their remembered slots too.
        if (!hasRememberedCell && !hasExistingFavorite)
            foreach (T candidate in orderedFavorites)
                if (!memory.ContainsKey(candidate) && canUseEmptyCell(candidate))
                {
                    cell = candidate;
                    return true;
                }
        cell = default!;
        return false;
    }
}
