using System;
using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public static class InventorySlotsApi
{
    public const string BeingSpoiledExpiryWorldTicksKey =
        StackMetadataPolicy.BeingSpoiledExpiryWorldTicksKey;

    /// <summary>
    /// Registers a custom-data field whose differing values do not prevent two
    /// otherwise identical stacks from merging. The callback receives the current
    /// destination and source values (either may be null) and returns the value to
    /// store on the destination; returning null removes the field. The callback
    /// may be invoked more than once and with two null values, so it must be
    /// side-effect free. It must also preserve any state encoded by the destination
    /// value and use a shared clock when comparing time-based formats, except when
    /// the owning format requires an already-final state to take precedence.
    /// Registrations are first-wins and keys remain case-sensitive. The built-in
    /// BeingSpoiled fallback is replaceable once by BeingSpoiled's authoritative
    /// callback, making either plugin load order safe.
    /// </summary>
    public static bool RegisterStackMetadataPolicy(
        string key,
        Func<string?, string?, string?> mergeValues) =>
        StackMetadataPolicy.Register(key, mergeValues);

    public static bool TryGetCustomEquippedItem(Player player, Func<ItemData?, bool> predicate, out ItemData? item) =>
        InventorySlotsPlugin.TryGetCustomEquippedItemForApi(player, predicate, out item);

    public static bool TryGetCustomEquipmentVisualRoots(VisEquipment visEquipment, ItemData item, List<GameObject> roots) =>
        InventorySlotsPlugin.TryGetCustomEquipmentVisualRootsForApi(visEquipment, item, roots);
}
