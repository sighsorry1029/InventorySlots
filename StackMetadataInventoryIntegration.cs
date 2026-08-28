using System;
using System.Collections.Generic;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal sealed class InventoryStackMetadataMergeState
{
    internal InventoryStackMetadataMergeState(
        Inventory inventory,
        Dictionary<ItemData, int> originalStacks,
        ItemData source,
        Dictionary<string, string> sourceCustomData)
    {
        Inventory = inventory;
        OriginalStacks = originalStacks;
        Source = source;
        SourceCustomData = sourceCustomData;
    }

    internal Inventory Inventory { get; }
    internal Dictionary<ItemData, int> OriginalStacks { get; }
    internal ItemData Source { get; }
    internal Dictionary<string, string> SourceCustomData { get; }
}

internal sealed class InventoryAddItemStackMetadataState
{
    internal InventoryAddItemStackMetadataState(
        InventoryStackMetadataMergeState? mergeState,
        bool lookupActive)
    {
        MergeState = mergeState;
        LookupActive = lookupActive;
    }

    internal InventoryStackMetadataMergeState? MergeState { get; }
    internal bool LookupActive { get; set; }
}

public sealed partial class InventorySlotsPlugin
{
    internal static InventoryAddItemStackMetadataState BeginAutomaticStackMetadataMerge(
        Inventory inventory,
        ItemData item) =>
        new(
            BeginStackMetadataMerge(inventory, item),
            BeginInventoryAddItemDataStackLookup(item));

    internal static void CompleteAutomaticStackMetadataMerge(
        Inventory inventory,
        InventoryAddItemStackMetadataState? state) =>
        CompleteStackMetadataMerge(inventory, state?.MergeState);

    internal static void EndAutomaticStackMetadataMerge(
        InventoryAddItemStackMetadataState? state)
    {
        if (state?.LookupActive != true)
        {
            return;
        }

        EndInventoryAddItemDataStackLookup(active: true);
        state.LookupActive = false;
    }

    private static InventoryStackMetadataMergeState? BeginStackMetadataMerge(
        Inventory inventory,
        ItemData? source)
    {
        if (inventory == null || source == null)
        {
            return null;
        }

        if (!StackMetadataPolicy.HasMergeMetadata(source.m_customData))
        {
            return null;
        }

        Dictionary<ItemData, int> originalStacks = new();
        foreach (ItemData existing in inventory.m_inventory)
        {
            if (existing != null)
            {
                originalStacks[existing] = existing.m_stack;
            }
        }

        Dictionary<string, string> sourceCustomData = source.m_customData == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(source.m_customData, StringComparer.Ordinal);
        return new InventoryStackMetadataMergeState(
            inventory,
            originalStacks,
            source,
            sourceCustomData);
    }

    private static void CompleteStackMetadataMerge(
        Inventory inventory,
        InventoryStackMetadataMergeState? state)
    {
        if (state == null ||
            inventory == null ||
            !ReferenceEquals(inventory, state.Inventory))
        {
            return;
        }

        bool metadataChanged = false;
        foreach (ItemData destination in inventory.m_inventory)
        {
            if (destination?.m_customData == null ||
                !state.OriginalStacks.TryGetValue(destination, out int originalStack) ||
                destination.m_stack <= originalStack)
            {
                continue;
            }

            IDictionary<string, string> sourceCustomData =
                state.Source.m_customData ?? state.SourceCustomData;
            metadataChanged |= StackMetadataPolicy.MergeInto(
                destination.m_customData,
                sourceCustomData);
        }

        // Inventory.AddItem invokes Changed before its Harmony postfix. Notify a
        // second time only when the post-merge metadata actually changed so a
        // container ZDO cannot persist the pre-merge expiry value.
        if (metadataChanged)
        {
            inventory.Changed();
        }
    }

    internal static bool TryPreparePositionalStackMetadataMerge(
        Inventory inventory,
        ItemData item,
        int amount,
        int x,
        int y,
        ref bool result,
        out InventoryStackMetadataMergeState? state)
    {
        state = BeginStackMetadataMerge(inventory, item);
        if (inventory == null ||
            item == null ||
            amount <= 0 ||
            x < 0 ||
            y < 0 ||
            x >= inventory.GetWidth() ||
            y >= inventory.GetHeight())
        {
            return true;
        }

        ItemData? destination = inventory.GetItemAt(x, y);
        if (destination == null ||
            IsTrustedCustomDataStackingItem(item) ||
            StackMetadataPolicy.AreCompatible(
                destination.m_customData,
                item.m_customData))
        {
            return true;
        }

        state = null;
        result = false;
        return false;
    }

    internal static void CompletePositionalStackMetadataMerge(
        Inventory inventory,
        InventoryStackMetadataMergeState? state) =>
        CompleteStackMetadataMerge(inventory, state);

    private static bool CanUseStackMetadataAutomaticStacking(ItemData? item) =>
        item != null &&
        StackMetadataPolicy.CanParticipateInAutomaticStacking(item.m_customData);

    private static bool HasCompatibleStackMetadata(
        ItemData? destination,
        ItemData? source) =>
        destination != null &&
        source != null &&
        StackMetadataPolicy.AreCompatible(
            destination.m_customData,
            source.m_customData);

    private static void MergeStackMetadata(ItemData? destination, ItemData? source)
    {
        if (destination?.m_customData != null && source != null)
        {
            StackMetadataPolicy.MergeInto(
                destination.m_customData,
                source.m_customData);
        }
    }
}
