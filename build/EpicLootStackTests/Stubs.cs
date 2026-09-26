using ItemData = ItemDrop.ItemData;

public sealed class Prefab(string name) { public string name = name; }
public record struct Vector2i(int x, int y);
public sealed class ItemDrop
{
    public sealed class SharedData
    {
        public string m_name = "";
        public int m_maxStackSize = 50;
        public bool m_questItem, m_useDurability;
    }
    public sealed class ItemData
    {
        public SharedData m_shared = new();
        public Prefab? m_dropPrefab;
        public Dictionary<string, string> m_customData = new();
        public int m_stack, m_quality = 1, m_variant, m_worldLevel;
        public bool m_cheated;
        public Vector2i m_gridPos;
        public string Kind = "material";
    }
}

// Fake native host: it deliberately owns the mutation and metadata application.
// Production code must call this path; tests can veto or limit its progress.
public sealed class Inventory
{
    public readonly List<ItemData> Items = new();
    public int Moves, MoveCap = int.MaxValue;
    public bool Veto, ReturnFalse;
    public bool ContainsItem(ItemData item) => Items.Contains(item);
    public void RemoveItem(ItemData item) => Items.Remove(item);
    public bool MoveItemToThis(Inventory from, ItemData source, int amount, int x, int y)
    {
        ++Moves;
        if (Veto) return false;
        ItemData target = Items.Single(item => item.m_gridPos == new Vector2i(x, y));
        var merged = EpicLoot.Data.ItemExtensions.Data(source).IsStackableWithOtherInfo(EpicLoot.Data.ItemExtensions.Data(target));
        if (merged == null) return false;
        amount = Math.Min(amount, MoveCap);
        target.m_stack += amount;
        source.m_stack -= amount;
        foreach (var entry in merged) target.m_customData[entry.Key] = entry.Value;
#if INVENTORY_SLOTS
        InventorySlots.StackMetadataPolicy.MergeInto(target.m_customData, source.m_customData);
#endif
        if (source.m_stack == 0) from.RemoveItem(source);
        return !ReturnFalse;
    }
}

namespace BepInEx.Bootstrap
{
    public sealed class PluginInfo { public object? Instance; }
    public static class Chainloader
    {
        public static readonly Dictionary<string, PluginInfo> PluginInfos = new();
    }
}

namespace EpicLoot
{
    public sealed class Plugin;
    public static class API
    {
        public static string? FailKind;
        public static bool IsShardStone(ItemData item) => item.Kind == FailKind ? throw new Exception("classifier error") : item.Kind == "shard";
        public static bool IsRunestone(ItemData item) => item.Kind == "rune";
        public static bool IsMagicItem(ItemData item) => item.Kind == "equipment";
        public static bool IsMagicCraftingMaterial(ItemData item) => item.Kind == "material";
    }
}
namespace EpicLoot.Data
{
    public static class ItemExtensions
    {
        public static ItemInfo Data(ItemData item) => new(item);
    }
    public sealed class ItemInfo(ItemData item)
    {
        public static bool Fail;
        public Dictionary<string, string>? IsStackableWithOtherInfo(ItemInfo other)
        {
            if (Fail) throw new InvalidOperationException("API failure");
            item.m_customData.TryGetValue("epic.effect", out string? effect);
            other.Item.m_customData.TryGetValue("epic.effect", out string? otherEffect);
            if (effect != otherEffect) return null;
            var result = new Dictionary<string, string>();
            if (effect != null) result["epic.effect"] = effect;
            // A recognized custom component can define its own merge result.
            if (item.m_customData.TryGetValue("epic.value", out string? value) &&
                other.Item.m_customData.TryGetValue("epic.value", out string? otherValue))
                result["epic.value"] = Math.Max(int.Parse(value), int.Parse(otherValue)).ToString();
            return result;
        }
        private ItemData Item => item;
    }
}

#if INVENTORY_SLOTS
namespace InventorySlots
{
    public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions
{
    public sealed partial class InventoryActionsPlugin
#endif
    {
        public sealed class TestLog { public int Warnings; public void LogWarning(string text) { ++Warnings; } }
        public static readonly TestLog Log = new();
        public static void Setup(bool loaded = true)
        {
            BepInEx.Bootstrap.Chainloader.PluginInfos.Clear();
            if (loaded) BepInEx.Bootstrap.Chainloader.PluginInfos.Add("randyknapp.mods.epicloot", new() { Instance = new EpicLoot.Plugin() });
            EpicLoot.Data.ItemInfo.Fail = false;
            EpicLoot.API.FailKind = null;
            InitializeEpicLootStacking();
        }
        public static bool Eligible(ItemData item) => IsEpicLootStackingItem(item);
        public static bool Pair(ItemData target, ItemData source) => CanMergeEpicLootStacks(target, source);
        public static bool Sort(Inventory inv, List<ItemData> items) => MergeEpicLootSortableStacks(items, inv);
        public static bool Fill(Inventory inv, List<ItemData> targets, List<ItemData> sources) => FillFavoriteStacks(inv, targets, sources);
        private static bool HasNoCustomData(ItemData item) => item.m_customData.Count == 0;
        private static bool HasSameStackIdentity(ItemData a, ItemData b) => a.m_shared.m_name == b.m_shared.m_name && a.m_quality == b.m_quality && a.m_worldLevel == b.m_worldLevel && a.m_cheated == b.m_cheated;
#if INVENTORY_SLOTS
        public static bool PrepareAuto(ItemData target, ItemData source) => PrepareEpicLootAutomaticStack(target, source);
        private static bool CanUseStackMetadataAutomaticStacking(ItemData item) => StackMetadataPolicy.CanParticipateInAutomaticStacking(item.m_customData);
        private static bool CanShareInventoryStack(ItemData a, ItemData b) => HasSameStackIdentity(a, b);
        private static bool HasCompatibleStackMetadata(ItemData a, ItemData b) => StackMetadataPolicy.AreCompatible(a.m_customData, b.m_customData);
        private static void MergeStackMetadata(ItemData a, ItemData b) => StackMetadataPolicy.MergeInto(a.m_customData, b.m_customData);
#endif
    }
}
