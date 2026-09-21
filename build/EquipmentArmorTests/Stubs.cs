using System.Reflection;

public sealed class ItemDrop
{
    public sealed class ItemData
    {
        public SharedData m_shared = new();
        public Dictionary<string, string> m_customData = new();
        public bool m_equipped = true;
        public int m_quality = 1;
        public int ArmorCalls;

        public float GetArmor()
        {
            ArmorCalls++;
            return m_shared.m_armor + (m_quality - 1) * m_shared.m_armorPerLevel;
        }
    }

    public sealed class SharedData
    {
        public float m_armor = 10f;
        public float m_armorPerLevel;
        public float m_weight = 2f;
        public float m_eitrRegenModifier = 0.15f;
        public string m_setName = "test-set";
        public float TestModifier = 0.2f;
    }
}

public sealed class Inventory
{
    public List<ItemDrop.ItemData> m_inventory = new();
}

public class Humanoid
{
    public Inventory Inventory = new();
    public ItemDrop.ItemData? m_helmetItem;
    public ItemDrop.ItemData? m_chestItem;
    public ItemDrop.ItemData? m_legItem;
    public ItemDrop.ItemData? m_shoulderItem;
    public ItemDrop.ItemData? m_utilityItem;
    public ItemDrop.ItemData? m_trinketItem;
    public Inventory GetInventory() => Inventory;
}

public sealed class Player : Humanoid
{
    public string Id = "test-player";
    public static FieldInfo[]? s_equipmentModifierSourceFields =
        [typeof(ItemDrop.SharedData).GetField(nameof(ItemDrop.SharedData.TestModifier))!];
}

namespace InventorySlots
{
    internal enum SlotKind { BuiltIn, CustomEquipment, Quick }

    internal sealed class SlotDefinition(string id, SlotKind kind, bool applyArmor = false)
    {
        public string Id { get; } = id;
        public SlotKind Kind { get; } = kind;
        public bool ApplyArmor { get; set; } = applyArmor;
    }

    public sealed partial class InventorySlotsPlugin
    {
        private const string SlotIdKey = "slot";
        private const string EquippedByKey = "owner";
        internal static readonly List<SlotDefinition> SlotDefinitions = new();

        internal static void Reset()
        {
            SlotDefinitions.Clear();
            ClearCustomEquipmentProjectionCache();
            InvalidateCustomEquipmentProjectionCache();
        }

        internal static void Invalidate() => InvalidateCustomEquipmentProjectionCache();
        internal static float Weight(Player player) => GetCachedCustomEquipmentWeight(player);
        internal static float Eitr(Player player) => GetCachedCustomEquipmentEitrRegen(player);
        internal static int SetCount(Player player) => GetCachedCustomEquipmentSetCount(player, "test-set");
        internal static float Modifier(Player player) => GetCachedCustomEquipmentModifierValues(player)?[0] ?? 0f;
        internal static int EquippedCount(Player player) => GetCustomEquippedItems(player).Count;

        internal static void MarkCustom(Player player, ItemDrop.ItemData item, string slotId)
        {
            item.m_customData[SlotIdKey] = slotId;
            item.m_customData[EquippedByKey] = player.Id;
        }

        private static string GetPlayerId(Player player) => player.Id;
        private static bool TryGetSlotById(string id, out SlotDefinition? slot)
        {
            slot = SlotDefinitions.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            return slot != null;
        }
        private static bool IsInventorySlotsCustomEquipped(ItemDrop.ItemData? item) =>
            item != null && item.m_equipped && item.m_customData.ContainsKey(SlotIdKey);
        private static SlotDefinition? GetSlotFromItemMarker(ItemDrop.ItemData item) =>
            item.m_customData.TryGetValue(SlotIdKey, out string? id)
                ? SlotDefinitions.FirstOrDefault(slot => string.Equals(slot.Id, id, StringComparison.OrdinalIgnoreCase))
                : null;
        private static bool ContainsExactItemReference(Inventory inventory, ItemDrop.ItemData item) =>
            inventory.m_inventory.Any(candidate => ReferenceEquals(candidate, item));
        private static ItemDrop.ItemData? GetBuiltInEquipmentSlotItem(Player player, SlotDefinition slot) =>
            slot.Id switch
            {
                "helmet" => player.m_helmetItem,
                "chest" => player.m_chestItem,
                "legs" => player.m_legItem,
                "cape" => player.m_shoulderItem,
                "utility" => player.m_utilityItem,
                "trinket" => player.m_trinketItem,
                _ => null
            };
    }
}
