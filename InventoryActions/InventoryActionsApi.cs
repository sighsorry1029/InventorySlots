using System;

namespace InventoryActions;

public static class InventoryActionsApi
{
    /// <summary>
    /// Registers a custom-data field whose differing values may be merged.
    /// The callback receives destination and source values (either may be null)
    /// and returns the destination value; null removes the field. It must be
    /// side-effect free, tolerate repeated calls and two null values, and preserve
    /// the format's state and clock semantics. The source is never changed.
    /// Registrations are first-wins, case-sensitive and independent of PoV.
    /// This local policy does not authorize remote MultiUserChest transfers.
    /// </summary>
    public static bool RegisterStackMetadataPolicy(
        string key,
        Func<string?, string?, string?> mergeValues) =>
        StackMetadataPolicy.Register(key, mergeValues);

    /// <summary>
    /// Registers a merge policy with a side-effect-free compatibility check.
    /// Both argument orders must return true; false or an exception rejects the
    /// pair. Either raw value may be null. Unknown fields still compare exactly.
    /// </summary>
    public static bool RegisterStackMetadataPolicy(
        string key,
        Func<string?, string?, string?> mergeValues,
        Func<string?, string?, bool> canMerge) =>
        StackMetadataPolicy.Register(key, mergeValues, canMerge);
}
