using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private static bool _povStackMetadataReady;
    private static Func<ItemData, bool>? _normalizeFineDiningStack;

    private static void BindPathOfValheimanStackMetadata(Assembly assembly)
    {
        MethodInfo? keys = assembly.GetType("PassiveTree.InventorySlotsCompat")?.GetMethod(
            "OurKeys", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null);
        if (keys?.ReturnType != typeof(IEnumerable<string>))
            throw new MissingMethodException("PassiveTree.InventorySlotsCompat", "OurKeys");
        PathOfValheimanStackMetadata.Register(((Func<IEnumerable<string>>)Delegate.CreateDelegate(
            typeof(Func<IEnumerable<string>>), keys))());
        _povStackMetadataReady = true;
    }

    private static void InitializePathOfValheimanFoodStacking()
    {
        // Start runs after optional plugins' Awake, including FineDining's setup.
        // Bind once; never discover reflection members during stack comparisons.
        if (!_povStackMetadataReady || _povUnavailable ||
            !Chainloader.PluginInfos.TryGetValue("sighsorry.FineDining", out var plugin) || plugin.Instance == null) return;
        var instance = plugin.Instance;
        try { BindFineDiningStackMetadata(instance.GetType().Assembly, () => instance != null); }
        catch (Exception error)
        {
            Log.LogWarning("PoV/FineDining automatic stacking unavailable; food metadata remains protected: " + error.GetBaseException().Message);
        }
    }

    private static void BindFineDiningStackMetadata(Assembly assembly, Func<bool> isAvailable)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var policies = new List<(string Key, Func<string?, string?, string?> Merge, Func<string?, string?, bool> Guard)>();
        void Bind(string keyTypeName, string keyField, string ownerName, string mergeName, string guardName)
        {
            Type owner = assembly.GetType("FineDining." + ownerName, true)!;
            var key = assembly.GetType("FineDining." + keyTypeName, true)!.GetField(keyField, flags);
            var merge = owner.GetMethod(mergeName, flags, null, new[] { typeof(string), typeof(string) }, null);
            var guard = owner.GetMethod(guardName, flags, null, new[] { typeof(string), typeof(string) }, null);
            if (key?.IsLiteral != true || key.FieldType != typeof(string) ||
                merge?.ReturnType != typeof(string) || guard?.ReturnType != typeof(bool))
                throw new MissingMethodException(owner.FullName, mergeName + "/" + guardName);
            policies.Add(((string)key.GetRawConstantValue()!,
                (Func<string?, string?, string?>)Delegate.CreateDelegate(typeof(Func<string, string, string>), merge),
                (Func<string?, string?, bool>)Delegate.CreateDelegate(typeof(Func<string, string, bool>), guard)));
        }
        // Reuse FineDining's guarded composition, including paused/invalid clocks.
        // Resolve all members before registering any: an unsupported DLL stays closed.
        Bind("SpoilageClock", "ExpiryDataKey", "DecayRuntime", "ComposeStackClockValues", "CanMergeStackClockValues");
        Bind("FreshnessRuntime", "AssignedLifetimeDataKey", "FreshnessRuntime", "ComposeAssignedLifetimeValues", "CanMergeAssignedLifetimeValues");
        Bind("SpoilageClock", "SpoiledDataKey", "SpoilageClock", "ComposeSpoiledValues", "CanMergeSpoiledValues");
        Func<ItemData, bool> ItemCallback(string type, string name)
        {
            MethodInfo? method = assembly.GetType("FineDining." + type, true)!.GetMethod(name, flags,
                null, new[] { typeof(ItemData) }, null);
            if (method?.ReturnType != typeof(bool)) throw new MissingMethodException(type, name);
            return (Func<ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemData, bool>), method);
        }
        var spoiled = ItemCallback("SpoilageClock", "IsSpoiled");
        var complete = ItemCallback("DecayRuntime", "CompleteKeepOriginalExpiry");
        _normalizeFineDiningStack = item => spoiled(item) && complete(item);
        foreach (var policy in policies)
            // FineDining removes its native AddItem hooks in OnDestroy. A stale
            // delegate must not authorize transfers after its owner disappears.
            StackMetadataPolicy.Register(policy.Key, policy.Merge,
                (a, b) => isAvailable() && policy.Guard(a, b));
    }

    private static bool CanUseSupportedStackMetadata(ItemData item) =>
        HasNoCustomData(item) || _povStackMetadataReady && !_povUnavailable &&
        StackMetadataPolicy.CanParticipateInAutomaticStacking(item.m_customData);

    private static bool HasCompatibleActionStackMetadata(ItemData target, ItemData source) =>
        StackMetadataPolicy.AreCompatible(target.m_customData, source.m_customData);

    private static void MergeActionStackMetadata(ItemData target, ItemData source)
        => MergeActionStackMetadata(target, source.m_customData);

    private static bool MergeActionStackMetadata(ItemData target, IDictionary<string, string>? sourceData)
    {
        target.m_customData ??= new Dictionary<string, string>();
        bool changed = StackMetadataPolicy.MergeInto(target.m_customData, sourceData);
        // FD completes spoilage by clearing clocks/lifetime. Per-key mergers
        // alone cannot express that cross-key invariant; use the owner's routine.
        return (_normalizeFineDiningStack?.Invoke(target) ?? false) || changed;
    }

    private static bool MoveActionStack(Inventory destination, Inventory origin, ItemData source,
        int amount, ItemData target)
    {
        if (!origin.ContainsItem(source) ||
            !ReferenceEquals(destination.GetItemAt(target.m_gridPos.x, target.m_gridPos.y), target) ||
            !CanStackIntoTargetForTopFirstMove(target, source)) return false;
        if (IsEpicLootStackingItem(target) || IsEpicLootStackingItem(source) ||
            !StackMetadataPolicy.HasMergeMetadata(target.m_customData) &&
            !StackMetadataPolicy.HasMergeMetadata(source.m_customData))
            return destination.MoveItemToThis(origin, source, amount, target.m_gridPos.x, target.m_gridPos.y);

        int before = target.m_stack;
        var sourceData = source.m_customData == null ? new Dictionary<string, string>() :
            new Dictionary<string, string>(source.m_customData);
        try
        {
            return destination.MoveItemToThis(origin, source, amount, target.m_gridPos.x, target.m_gridPos.y);
        }
        finally
        {
            // FineDining's tracker does not cover every local chest our existing
            // actions support. Compose after actual growth, including a partial
            // move before another hook throws. Owner mergers are idempotent if
            // its tracker already ran; the original exception still propagates.
            if (destination.ContainsItem(target) && target.m_stack > before)
            {
                if (MergeActionStackMetadata(target, sourceData))
                    NotifyInventoryChanged(destination);
            }
        }
    }

    private static bool MergePathOfValheimanSortableStacks(List<ItemData> items, Inventory inventory)
    {
        if (!_povStackMetadataReady || _povUnavailable) return false;
        bool changed = false;
        for (int i = 0; i < items.Count; ++i)
        for (int j = i + 1; j < items.Count; ++j)
        {
            ItemData target = items[i], source = items[j];
            // Leave the established metadata-free and EpicLoot paths in charge.
            if (HasNoCustomData(target) && HasNoCustomData(source) ||
                IsEpicLootStackingItem(target) || IsEpicLootStackingItem(source) ||
                target.m_stack <= 0 || source.m_stack <= 0 ||
                !CanStackIntoTargetForTopFirstMove(target, source)) continue;
            int amount = Math.Min(source.m_stack, target.m_shared.m_maxStackSize - target.m_stack);
            if (amount <= 0) continue;
            MergeActionStackMetadata(target, source);
            target.m_stack += amount;
            source.m_stack -= amount;
            changed = true;
        }
        if (changed)
        {
            // Apply all quantities/metadata before Changed can serialize contents.
            for (int i = items.Count - 1; i >= 0; --i)
                if (items[i].m_stack == 0) { inventory.RemoveItem(items[i]); items.RemoveAt(i); }
        }
        return changed;
    }
}
