using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Bootstrap;
using ItemData = ItemDrop.ItemData;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private const string EpicLootStackingGuid = "randyknapp.mods.epicloot";
    private static EpicLootStackingApi? _epicLootStackingApi;

    private static void InitializeEpicLootStacking()
    {
        _epicLootStackingApi = null;
        if (!Chainloader.PluginInfos.TryGetValue(EpicLootStackingGuid, out var plugin) || plugin.Instance == null) return;
        try
        {
            _epicLootStackingApi = new EpicLootStackingApi(plugin.Instance.GetType().Assembly);
        }
        catch (Exception error)
        {
            Log.LogWarning($"EpicLoot automatic stacking is unavailable; custom-data stacks remain protected: {error.GetBaseException().Message}");
        }
    }

    private static bool IsEpicLootStackingItem(ItemData? item) =>
        _epicLootStackingApi != null && _epicLootStackingApi.IsMaterial(item);

    private static bool CanMergeEpicLootStacks(ItemData target, ItemData source)
        => TryGetEpicLootStackMerge(target, source, out _);

    private static bool TryGetEpicLootStackMerge(ItemData target, ItemData source, out Dictionary<string, string>? merged)
    {
        merged = null;
        if (ReferenceEquals(target, source) || !IsEpicLootStackingItem(target) || !IsEpicLootStackingItem(source) ||
            target.m_shared.m_name != source.m_shared.m_name || target.m_quality != source.m_quality ||
            target.m_worldLevel != source.m_worldLevel || target.m_variant != source.m_variant ||
            target.m_cheated != source.m_cheated || target.m_dropPrefab == null || source.m_dropPrefab == null ||
            target.m_dropPrefab.name != source.m_dropPrefab.name) return false;

        merged = _epicLootStackingApi!.GetMergedData(source, target);
        if (merged == null) return false;

        // EpicLoot only validates data it recognizes. Unknown keys (including
        // unresolved EpicLoot components) retain our existing metadata policy.
#if INVENTORY_SLOTS
        return StackMetadataPolicy.AreCompatible(
            WithoutEpicLootMergedKeys(target.m_customData, merged),
            WithoutEpicLootMergedKeys(source.m_customData, merged));
#else
        return EqualUnmergedData(target.m_customData, source.m_customData, merged) &&
               EqualUnmergedData(source.m_customData, target.m_customData, merged);
#endif
    }

#if INVENTORY_SLOTS
    private static bool PrepareEpicLootAutomaticStack(ItemData target, ItemData source)
    {
        if (!TryGetEpicLootStackMerge(target, source, out var merged)) return false;
        // Our FindFreeStackItem replacement bypasses EpicLoot's transpiler. Its
        // original postfix applies these values to the chosen target before the
        // native AddItem loop increments its stack. Preserve that exact boundary;
        // capacity checks above remain read-only and never call this method.
        foreach (var entry in merged!) target.m_customData[entry.Key] = entry.Value;
        return true;
    }
#endif

#if INVENTORY_SLOTS
    private static IDictionary<string, string>? WithoutEpicLootMergedKeys(
        Dictionary<string, string>? data, Dictionary<string, string> merged) =>
        data == null || merged.Count == 0 ? data :
            data.Where(entry => !merged.ContainsKey(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value);
#else
    private static bool EqualUnmergedData(Dictionary<string, string>? left, Dictionary<string, string>? right,
        Dictionary<string, string> merged)
    {
        if (left == null) return true;
        foreach (var entry in left)
            if (!merged.ContainsKey(entry.Key) &&
                (right == null || !right.TryGetValue(entry.Key, out string? value) || value != entry.Value)) return false;
        return true;
    }
#endif

    private static int TransferEpicLootStack(Inventory inventory, ItemData target, ItemData source)
    {
        if (!inventory.ContainsItem(target) || !inventory.ContainsItem(source) ||
            target.m_stack <= 0 || source.m_stack <= 0 || !CanMergeEpicLootStacks(target, source)) return 0;
        int amount = Math.Min(source.m_stack, target.m_shared.m_maxStackSize - target.m_stack);
        if (amount <= 0) return 0;
        int before = source.m_stack;
        // The positional native path runs EpicLoot's validation and metadata
        // application. Never fall back to direct quantity addition after a veto.
        inventory.MoveItemToThis(inventory, source, amount, target.m_gridPos.x, target.m_gridPos.y);
        return inventory.ContainsItem(source) ? Math.Max(0, before - source.m_stack) : before;
    }

    private static bool MergeEpicLootSortableStacks(List<ItemData> items, Inventory inventory)
    {
        if (_epicLootStackingApi == null) return false;
        bool changed = false;
        List<ItemData> materials = items.Where(IsEpicLootStackingItem).ToList();
        for (int i = 0; i < materials.Count; ++i)
            for (int j = i + 1; j < materials.Count; ++j)
                changed |= TransferEpicLootStack(inventory, materials[i], materials[j]) > 0;
        if (changed) items.RemoveAll(item => !inventory.ContainsItem(item));
        return changed;
    }

    private static bool FillEpicLootFavoriteStacks(Inventory inventory, List<ItemData> targets, List<ItemData> sources)
    {
        if (_epicLootStackingApi == null) return false;
        bool changed = false;
        foreach (ItemData target in targets.Where(IsEpicLootStackingItem).OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x))
            foreach (ItemData source in sources)
                changed |= TransferEpicLootStack(inventory, target, source) > 0;
        return changed;
    }

    // No compile-time EpicLoot reference: bind its public classifiers and item
    // data API once per plugin lifetime. Pair checks do not search reflection.
    private sealed class EpicLootStackingApi
    {
        private readonly Func<ItemData, bool> _shard, _rune, _magic, _material;
        private readonly Func<ItemData, ItemData, Dictionary<string, string>?> _merge;
        private bool _failed;

        public EpicLootStackingApi(Assembly assembly)
        {
            Type api = assembly.GetType("EpicLoot.API", true)!;
            _shard = Classifier(api, "IsShardStone");
            _rune = Classifier(api, "IsRunestone");
            _magic = Classifier(api, "IsMagicItem");
            _material = Classifier(api, "IsMagicCraftingMaterial");
            Type extensions = assembly.GetType("EpicLoot.Data.ItemExtensions", true)!;
            Type info = assembly.GetType("EpicLoot.Data.ItemInfo", true)!;
            MethodInfo? data = extensions.GetMethod("Data", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemData) }, null);
            MethodInfo? merge = info.GetMethod("IsStackableWithOtherInfo", BindingFlags.Public | BindingFlags.Instance, null, new[] { info }, null);
            if (data?.ReturnType != info || merge?.ReturnType != typeof(Dictionary<string, string>))
                throw new MissingMethodException("EpicLoot public item-data stacking API");
            var source = Expression.Parameter(typeof(ItemData), "source");
            var target = Expression.Parameter(typeof(ItemData), "target");
            _merge = Expression.Lambda<Func<ItemData, ItemData, Dictionary<string, string>?>>(
                Expression.Call(Expression.Call(data, source), merge, Expression.Call(data, target)), source, target).Compile();
        }

        private static Func<ItemData, bool> Classifier(Type api, string name)
        {
            MethodInfo? method = api.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemData) }, null);
            if (method?.ReturnType != typeof(bool)) throw new MissingMethodException(api.FullName, name);
            return (Func<ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemData, bool>), method);
        }

        public bool IsMaterial(ItemData? item)
        {
            if (item?.m_shared == null || item.m_shared.m_maxStackSize <= 1 ||
                item.m_shared.m_questItem || item.m_shared.m_useDurability) return false;
            try
            {
                if (_shard(item) || _rune(item)) return true;
                if (_magic(item)) return false;
                if (_material(item)) return true;
                string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
                return prefab == "ForestToken" || prefab == "IronBountyToken" || prefab == "GoldBountyToken";
            }
            catch (Exception error)
            {
                Disable(error);
                // Quarantine this item from direct merging; do not turn a failed
                // classification into permission to treat it as ordinary data.
                return true;
            }
        }

        public Dictionary<string, string>? GetMergedData(ItemData source, ItemData target)
        {
            if (_failed) return null;
            try { return _merge(source, target); }
            catch (Exception error) { Disable(error); return null; }
        }

        private void Disable(Exception error)
        {
            if (_failed) return;
            _failed = true;
            Log.LogWarning($"EpicLoot automatic stacking was disabled after an API failure; custom-data stacks remain protected: {error.GetBaseException().Message}");
        }
    }
}
