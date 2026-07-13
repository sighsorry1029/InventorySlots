using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using ServerSync;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal enum InventoryStateEnsureReason
    {
        Unknown,
        PeriodicAudit,
        PlayerAwake,
        PlayerSpawned,
        PlayerLoad,
        PlayerSave,
        InventoryChanged,
        EquipmentChanged,
        InventoryLoad,
        InventoryMove,
        Tombstone,
        GuiShow,
        SlotAction,
        BackupRestore,
        YamlReload,
        JewelcraftingSlotRefresh,
        ConfigChanged,
        ReentrantFollowUp
    }

    internal enum InventoryStateAuditLevel
    {
        None = 0,
        HeightOnly = 1,
        SlotLight = 2,
        FullIntegrity = 3
    }

    private sealed class InventoryPanelRuntimeState
    {
        public readonly Dictionary<int, RectTransform> CustomSlotPanels = new();
        public readonly Dictionary<int, RectTransform> QuickSlotPanels = new();
        public readonly List<MovedPlayerStatPanel> MovedPlayerStatPanels = new();
        public readonly Dictionary<int, Vector3> QuickSlotPanelOutroStartPositions = new();
        public readonly Dictionary<int, GameObject> FavoriteKeyHintObjects = new();
        public readonly Dictionary<int, GameObject> PinnedTooltipKeyHintObjects = new();
        public RectTransform? QuickSlotsHotkeyBarRect;
        public HotkeyBar? QuickSlotsHotkeyBar;
        public RectTransform? PlayerStatPanelHost;
        public RectTransform? InventorySortPanel;
        public RectTransform? CurrencyPocketPanel;
        public Button? ContainerRestockButton;
        public Button? ContainerStoreAllButton;
        public Button? ContainerSortButton;
        public RectTransformSnapshot? TakeAllButtonOriginal;
        public RectTransformSnapshot? StackAllButtonOriginal;
        public RectTransform? TrackedContainerPanel;
        public RectTransform? TrackedContainerWeightPanel;
        public Vector3 ContainerPanelBasePosition;
        public Vector3 ContainerPanelAppliedOffset;
        public Vector3 ContainerWeightPanelBasePosition;
        public float ContainerWeightPanelAppliedYOffset;
        public RectTransform? DraggedInventoryPanel;
        public Vector2 InventoryPanelDragStartLocalMouse;
        public Vector2 InventoryPanelDragStartOffset;
        public bool ContainerPanelBasePositionSet;
        public bool ContainerWeightPanelBasePositionSet;
        public bool DraggingQuickSlotsPanelOffset;
        public bool DraggingEquipmentSlotsPanelOffset;
        public Vector2 EquipmentSlotsPanelRuntimeOffset = EquipmentSlotsPanelFixedOffset;
        public Vector2 QuickSlotsPanelRuntimeOffset = QuickSlotsPanelFixedOffset;
        public int LastExpandableInventoryRows = BaseRows;
        public float QuickSlotPanelIntroStartTime = -1f;
        public float QuickSlotPanelIntroDuration = QuickSlotPanelIntroFallbackDuration;
        public float QuickSlotPanelOutroStartTime = -1f;
        public float QuickSlotPanelOutroDuration = QuickSlotPanelIntroFallbackDuration;
        public float QuickSlotHudElementSpace = 70f;
        public readonly ContainerHoldActionState ContainerRestockHold = new();
        public readonly ContainerHoldActionState ContainerQuickStackHold = new();
        public bool QuickSlotPanelIntroActive;
        public bool QuickSlotPanelOutroActive;
        public bool QuickSlotHudAnchorValid;
        public bool LastExpandableInventoryRowsLoaded;
        public Vector3 QuickSlotHudAnchoredPosition;
    }

    private sealed class ContainerHoldActionState
    {
        public Container? Container;
        public float StartTime = -1f;
        public bool Triggered;
    }

    private sealed class InventorySafetyRuntimeState
    {
        public readonly Dictionary<ItemData, PendingSlotEquip> PendingSlotEquips = new();
        public readonly Dictionary<ItemData, PendingSlotUnequip> PendingSlotUnequips = new();
        public readonly Dictionary<Inventory, int> LoadPreservationInventoryDepth = new();
        public readonly Stack<ItemData?> InventoryAddItemDataStackLookupItems = new();
        public readonly HashSet<string> SlotRecoveryWarnings = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> ForeignSlotPreservationWarnings = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<InventoryStateEnsureReason, int> EnsureCounts = new();
        public readonly Dictionary<ItemData, float> SlotUnequipToInventoryRequests = new();
        public bool RoutingEquipToDedicatedSlot;
        public bool RestoringSlotBackup;
        public bool EnsuringInventoryState;
        public InventoryStateEnsureReason PendingEnsureReason = InventoryStateEnsureReason.Unknown;
        public InventoryStateAuditLevel PendingAuditLevel = InventoryStateAuditLevel.None;
        public InventoryStateEnsureReason DeferredEnsureReason = InventoryStateEnsureReason.Unknown;
        public InventoryStateAuditLevel DeferredAuditLevel = InventoryStateAuditLevel.None;
        public int DeferredEnsureFrame = -1;
        public float HeavyAuditDelayUntil = -1f;
        public int LastFullIntegrityAuditSignature = int.MinValue;
        public int LastSlotLightProjectionSignature = int.MinValue;
        public int InventoryPlacementCacheVersion;
        public Inventory? UsableRegularEmptyCellCacheInventory;
        public int UsableRegularEmptyCellCacheVersion = -1;
        public int UsableRegularEmptyCellCacheContext;
        public int UsableRegularEmptyCellCacheCount;
        public Inventory? DedicatedSlotRouteFailureCacheInventory;
        public int DedicatedSlotRouteFailureCacheVersion = -1;
        public int DedicatedSlotRouteFailureCacheContext;
        public int DedicatedSlotRouteFailureCacheItemKey;
        public Inventory? CanAddItemFailureCacheInventory;
        public int CanAddItemFailureCacheVersion = -1;
        public int CanAddItemFailureCacheContext;
        public int CanAddItemFailureCacheItemKey;
        public int CanAddItemFailureCacheRequestedStack;
        public Inventory? InventoryLimitCountCacheInventory;
        public int InventoryLimitCountCachePlacementVersion = -1;
        public int InventoryLimitCountCacheRuleVersion = -1;
        public readonly Dictionary<string, long> InventoryLimitCountCache = new(StringComparer.OrdinalIgnoreCase);
        public string LastInventoryLimitMessageKey = "";
        public float LastInventoryLimitMessageTime = -1f;
        public bool SlotUnequipInProgress;
        public bool SuppressSlotAutoEquip;
        public bool HandlingSlotDropOutside;
    }

    private sealed class InventoryContainerRuntimeState
    {
        public readonly List<Container> KnownContainers = new();
    }

    private sealed class InventoryDefinitionRuntimeState
    {
        public readonly List<SlotDefinition> SlotDefinitions = new();
        public readonly List<SlotDefinition> CustomPanelSlotCache = new();
        public readonly List<SlotDefinition> VisibleCustomPanelSlotCache = new();
        public readonly List<SlotDefinition> QuickPanelSlotCache = new();
        public readonly Dictionary<int, SlotDefinition> QuickSlotDefinitionCache = new();
        public readonly Dictionary<string, bool> EquipmentSlotUnlockCache = new(StringComparer.Ordinal);
        public readonly Dictionary<string, YamlPredefinedGroup> PredefinedGroupDefinitions = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> PredefinedGroupOrder = new();
        public readonly Dictionary<string, List<string>> PredefinedGroupOrders = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> InventoryLimits = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> ResourceTierByToken = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> CookingStationInputTokens = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> CraftingRecipeFoodInputTokens = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> CookingStationFoodInputTokens = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> FermenterInputTokens = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> FermenterOutputTokens = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> FermenterFoodInputTokens = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> ItemNameTokens = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> RestockTargetStackLimits = new(StringComparer.OrdinalIgnoreCase);
        public int SlotDefinitionVersion;
        public int InventoryLimitVersion;
        public int CustomPanelSlotCacheVersion = -1;
        public int VisibleCustomPanelSlotCacheVersion = -1;
        public string VisibleCustomPanelSlotCacheSignature = "";
        public string EquipmentSlotUnlockCacheSignature = "";
        public int QuickPanelSlotCacheVersion = -1;
        public int QuickPanelSlotCacheUnlockedCount = -1;
        public int QuickSlotDefinitionCacheVersion = -1;
        public int CachedObjectDbItemCount = -1;
        public int CachedStationInputObjectDbItemCount = -1;
        public int CachedStationInputPrefabCount = -1;
        public int CachedStationInputRecipeCount = -1;
        public bool StationInputTokensInitialized;
    }

    private sealed class InventoryClientRuntimeState
    {
        public bool ClientStateLoaded;
        public string LoadedFavoritesPlayerId = "";
        public InventorySlotsClientState ClientState = new();
        public readonly HashSet<Vector2i> FavoriteSlots = new();
    }

    private sealed class EquipmentVisualRuntimeState
    {
        public readonly Dictionary<string, CustomEquipmentVisual> Visuals = new(StringComparer.Ordinal);
        public readonly HashSet<StatusEffect> StatusEffects = new();
        public readonly Dictionary<StatusEffect, int> StatusEffectLevels = new();
        public int LocalPlayerUpdateFrame = -1;
        public int LocalPlayerUpdateSignature = int.MinValue;
        public int LocalPlayerUpdateStateCount;
    }

    private sealed class InventorySortRuntimeState
    {
        public readonly Dictionary<string, Recipe> RecipeOutputLookupCache = new(StringComparer.OrdinalIgnoreCase);
        public string RecipeOutputLookupSignature = "";
    }

    private static CustomSyncedValue<string> _syncedYaml = null!;
    private static FileSystemWatcher? _yamlWatcher;
    private static readonly object YamlReloadLock = new();
    private static readonly InventoryPanelRuntimeState InventoryPanels = new();
    private static readonly InventorySafetyRuntimeState InventorySafety = new();
    private static readonly InventoryContainerRuntimeState InventoryContainers = new();
    private static readonly InventoryDefinitionRuntimeState InventoryDefinitions = new();
    private static readonly InventoryClientRuntimeState InventoryClient = new();
    private static readonly EquipmentVisualRuntimeState EquipmentVisuals = new();
    private static readonly InventorySortRuntimeState InventorySort = new();
    private static List<SlotDefinition> SlotDefinitions => InventoryDefinitions.SlotDefinitions;
    private static Dictionary<string, YamlPredefinedGroup> PredefinedGroupDefinitions => InventoryDefinitions.PredefinedGroupDefinitions;
    private static List<string> PredefinedGroupOrder => InventoryDefinitions.PredefinedGroupOrder;
    private static Dictionary<string, List<string>> PredefinedGroupOrders => InventoryDefinitions.PredefinedGroupOrders;
    private static Dictionary<string, int> InventoryLimits => InventoryDefinitions.InventoryLimits;
    private static Dictionary<string, int> ResourceTierByToken => InventoryDefinitions.ResourceTierByToken;
    private static HashSet<string> CookingStationInputTokens => InventoryDefinitions.CookingStationInputTokens;
    private static HashSet<string> CraftingRecipeFoodInputTokens => InventoryDefinitions.CraftingRecipeFoodInputTokens;
    private static HashSet<string> CookingStationFoodInputTokens => InventoryDefinitions.CookingStationFoodInputTokens;
    private static HashSet<string> FermenterInputTokens => InventoryDefinitions.FermenterInputTokens;
    private static HashSet<string> FermenterOutputTokens => InventoryDefinitions.FermenterOutputTokens;
    private static HashSet<string> FermenterFoodInputTokens => InventoryDefinitions.FermenterFoodInputTokens;
    private static Dictionary<string, string> ItemNameTokens => InventoryDefinitions.ItemNameTokens;
    private static readonly string[] PlayerStatPanelExtraNames = { "Jewelcrafting Synergy", "Trash" };
    private static readonly string[] QuickStackStoreMiniButtonNames = { "sortInventoryButton", "restockAreaButton", "quickStackAreaButton", "favoritingTogglingButton" };
    private float _nextRefreshTime;
    private float _nextLightAuditTime;
    private float _nextHeavyAuditTime;
    private static bool _yamlReloadQueued;
    private static DateTime _yamlReloadAfterUtc;
    private static HashSet<Vector2i> FavoriteSlots => InventoryClient.FavoriteSlots;

    private static bool BeginInventoryLoadPreservation(Inventory? inventory)
    {
        if (inventory == null)
        {
            return false;
        }

        InventorySafety.LoadPreservationInventoryDepth.TryGetValue(inventory, out int depth);
        InventorySafety.LoadPreservationInventoryDepth[inventory] = depth + 1;
        return true;
    }

    private static void EndInventoryLoadPreservation(Inventory? inventory)
    {
        if (inventory == null ||
            !InventorySafety.LoadPreservationInventoryDepth.TryGetValue(inventory, out int depth))
        {
            return;
        }

        if (depth <= 1)
        {
            InventorySafety.LoadPreservationInventoryDepth.Remove(inventory);
            return;
        }

        InventorySafety.LoadPreservationInventoryDepth[inventory] = depth - 1;
    }

    private static bool IsInventoryLoadPreserving(Inventory? inventory) =>
        inventory != null && InventorySafety.LoadPreservationInventoryDepth.ContainsKey(inventory);

    private static void PushInventoryAddItemDataStackLookupItem(ItemData item) =>
        InventorySafety.InventoryAddItemDataStackLookupItems.Push(item);

    private static void PopInventoryAddItemDataStackLookupItem()
    {
        if (InventorySafety.InventoryAddItemDataStackLookupItems.Count > 0)
        {
            InventorySafety.InventoryAddItemDataStackLookupItems.Pop();
        }
    }

    private static ItemData? GetCurrentInventoryAddItemDataStackLookupItem() =>
        InventorySafety.InventoryAddItemDataStackLookupItems.Count > 0
            ? InventorySafety.InventoryAddItemDataStackLookupItems.Peek()
            : null;

    internal static bool IsCompletingSlotUnequip => InventorySafety.SlotUnequipInProgress;
    internal static bool IsHandlingSlotDropOutside => InventorySafety.HandlingSlotDropOutside;
    internal static IReadOnlyList<SlotDefinition> Slots => SlotDefinitions;
}
