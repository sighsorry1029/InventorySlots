using ItemData = ItemDrop.ItemData;

// In-memory boundaries only. The transaction, outcome observer, and favorite
// transfer code under test are linked unchanged from production sources.
namespace UnityEngine
{
    public class GameObject { public string name = "Shield"; }
}
public readonly record struct Vector2i(int x, int y);
public class ItemDrop
{
    public string name = "Shield";
    public UnityEngine.GameObject gameObject = new();
    public ItemData m_itemData = new();
    public class SharedData { public int m_maxQuality = 3, m_maxStackSize = 1; public string m_name = "Shield"; }
    public class ItemData
    {
        public int m_quality = 4, m_variant, m_stack = 1, m_worldLevel;
        public float m_durability = 100, m_lastAttackTime;
        public bool m_equipped, m_pickedUp;
        public long m_crafterID;
        public string m_crafterName = "Crafter";
        public object? m_lastProjectile;
        public SharedData m_shared = new();
        public UnityEngine.GameObject m_dropPrefab = new();
        public Vector2i m_gridPos = new(0, 9);
        public Dictionary<string, string> m_customData = new();
        public ItemData Clone()
        {
            ItemData copy = (ItemData)MemberwiseClone();
            copy.m_customData = new(m_customData);
            return copy;
        }
    }
}
public class Recipe { public ItemDrop m_item = new(); }
public class CraftingStation { public bool m_upgrader = true; }
public class Inventory
{
    public List<ItemData> m_inventory = new();
    public bool ContainsItem(ItemData item) => m_inventory.Contains(item);
    public ItemData? GetItemAt(int x, int y) => m_inventory.FirstOrDefault(i => i.m_gridPos == new Vector2i(x, y));
    public int GetWidth() => 8;
    public int GetHeight() => 10;
    public void Changed() { }
    public ItemData? AddItem(string name, int stack, int quality, int variant, long id, string crafter, Vector2i pos, bool cheated, bool pickedUp, bool dropIfFull) => null;
}
public class Character { public void Message(MessageHud.MessageType type, string text, int amount, object? icon) { } }
public class MessageHud { public enum MessageType { Center } }
public class Humanoid : Character
{
    public ItemData? m_rightItem, m_leftItem, m_chestItem, m_legItem, m_ammoItem, m_helmetItem,
        m_shoulderItem, m_utilityItem, m_trinketItem, m_hiddenLeftItem, m_hiddenRightItem;
    public bool IsItemEquiped(ItemData item) => ReferenceEquals(m_chestItem, item);
    public void SetupEquipment() { }
}
public class Player : Humanoid
{
    public static Player? m_localPlayer;
    public Inventory Inventory = new();
    public CraftingStation Station = new();
    public Inventory GetInventory() => Inventory;
    public CraftingStation GetCurrentCraftingStation() => Station;
}
public class InventoryGui
{
    public static InventoryGui? instance;
    public ItemData? m_craftUpgradeItem;
    public Recipe? m_craftRecipe = new();
    public record struct RecipeDataPair(Recipe? Recipe, ItemData? ItemData);
}
namespace InventorySlots
{
    public enum SlotKind { Quick, BuiltIn, CustomEquipment }
    public class SlotDefinition { public string Id = "quick"; public SlotKind Kind; }
    public enum InventoryStateEnsureReason { InventoryChanged }
    public enum InventoryStateAuditLevel { FullIntegrity }
    public enum CraftingPanelUpdateReason { StateChanged }
    public class InventorySlotsClientPlayerState { public List<string> CraftingFavorites = new(), UpgradeFavorites = new(); }
    public class TestLog { public void LogError(object text) { } public void LogWarning(object text) { } }
    public sealed partial class InventorySlotsPlugin
    {
        internal static TestLog Log = new();
        internal static SlotDefinition TestSlot = new();
        internal static bool SocketActive, SlotAllowed = true;
        internal static EquipmentSlotUpgradeTransaction? Active => _activeEquipmentSlotUpgradeTransaction;
        internal const string SlotIdKey = "slot", EquippedByKey = "equipped", UpgradeFavoriteItemIdKey = "favorite";
        private const string ClientStateFilePath = "unused";
        private static string _pendingUpgradeFavoriteItemId = "", _pendingUpgradeFavoritePrefab = "", _loadedCraftingFavoritesPlayerId = "";
        private static int _pendingUpgradeFavoriteQuality, _pendingUpgradeFavoriteVariant, _craftingFavoritesVersion;
        private static Vector2i _pendingUpgradeFavoriteGridPos;
        private static ItemData? _pendingUpgradeFavoriteOriginal, _pendingUpgradeFavoriteRefinementResult;
        private static bool _pendingUpgradeFavoriteIsRefinement;
        private static HashSet<string> FavoriteCraftingRecipeKeys = new(), FavoriteUpgradeItemKeys = new();
        internal static bool IsUsableRegularCell(Inventory inventory, Player player, Vector2i pos) => pos.y < 9;
        private static bool IsJewelcraftingSocketTabActive(InventoryGui gui) => SocketActive;
        private static bool TryGetSlotAtGridPos(Inventory inventory, Vector2i pos, out SlotDefinition? slot) { slot = TestSlot; return pos.y == 9; }
        private static string GetItemPrefabName(ItemData item) => item.m_dropPrefab.name;
        private static string CleanPrefabName(string name) => name;
        private static bool CanUseSpecialSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot) => SlotAllowed;
        private static bool IsItemCompatibleWithSpecialSlot(Player player, ItemData item, SlotDefinition slot) => SlotAllowed;
        private static bool IsQuickSlotUnlocked(Player player, SlotDefinition slot) => true;
        private static string LocalizeUi(string token, string fallback) => fallback;
        private static void RequestInventoryStateEnsure(Player player, InventoryStateEnsureReason reason, InventoryStateAuditLevel audit) { }
        private static ItemData? FindItemForSlot(Player player, Inventory inventory, SlotDefinition slot) => inventory.GetItemAt(0, 9);
        private static bool TryEquipIntoSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot) { item.m_gridPos = new(0, 9); return true; }
        private static bool RestoreSlotEquipmentState(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
        {
            item.m_equipped = true;
            player.m_chestItem = item;
            return true;
        }
        private static ItemData? GetBuiltInEquipmentSlotItem(Player player, SlotDefinition slot) => player.m_chestItem;
        private static void ClearItemSlot(ItemData item) { item.m_customData.Remove(SlotIdKey); item.m_customData.Remove(EquippedByKey); }
        private static bool IsInventorySlotsCustomEquipped(ItemData item) => item.m_customData.ContainsKey(EquippedByKey);
        private static void UnequipInventorySlotsItem(Player player, ItemData item) { item.m_equipped = false; if (player.m_chestItem == item) player.m_chestItem = null; }
        private static void ClearSlotActionState(ItemData item) { }
        private static void ReloadEpicLootRuntimeItemData(Player player) { }
        private static void RefreshExternalEquipmentEffects(Player player) { }
        private static HashSet<Vector2i> BuildOccupiedCellSet(Inventory inventory, ItemData except) => inventory.m_inventory.Where(i => i != except).Select(i => i.m_gridPos).ToHashSet();
        private static bool IsCellOccupied(HashSet<Vector2i> cells, int x, int y) => cells.Contains(new(x, y));
        private static bool CellContainsOnly(Inventory inventory, Vector2i position, ItemData item) =>
            inventory.m_inventory.Where(i => i.m_gridPos == position).All(i => ReferenceEquals(i, item));
        private static bool IsRecycleNReclaimReclaimTabActive(InventoryGui? gui) => false;
        private static bool IsFavoriteModifierHeld() => false;
        private static bool TryGetCraftingRecipePair(InventoryGui gui, int index, out InventoryGui.RecipeDataPair pair) { pair = default; return false; }
        private static void InvalidateCraftingRecipeView() { }
        private static void UpdateCraftingPanelRedesign(InventoryGui gui, CraftingPanelUpdateReason reason) { }
        private static void ClearCraftingGroupAvailabilityCache() { }
        private static string GetPlayerId(Player player) => "test";
        private static InventorySlotsClientPlayerState? GetClientPlayerState(string id, bool create) => null;
        private static void SaveClientState() { }
    }
}
