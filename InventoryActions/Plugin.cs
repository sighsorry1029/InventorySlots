using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace InventoryActions;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("sighsorry.InventorySlots")]
[BepInIncompatibility("goldenrevolver.quick_stack_store")]
[BepInDependency(ExternalMultiUserChestGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ExtraSlotsGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(EquipmentAndQuickSlotsGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(AzuExtendedPlayerInventoryGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class InventoryActionsPlugin : BaseUnityPlugin
{
    internal const string ModName = "InventoryActions";
    internal const string ModVersion = "1.1.6";
    internal const string Author = "sighsorry";
    internal const string ModGUID = $"{Author}.{ModName}";
    private const string ExternalMultiUserChestGuid = "com.maxsch.valheim.MultiUserChest";
    private const string ExtraSlotsGuid = "shudnal.ExtraSlots";
    private const string EquipmentAndQuickSlotsGuid = "randyknapp.mods.equipmentandquickslots";
    private const string AzuExtendedPlayerInventoryGuid = "Azumatt.AzuExtendedPlayerInventory";
    private static readonly ConfigDefinition AzuEpiSeparatePanelConfig =
        new("2 - Inventory", "Display Equipment in Separate Panel");
    private static BaseUnityPlugin? _extraSlotsPlugin;
    private static System.Func<int>? _extraSlotsPlayerRows;
    private static System.Func<int>? _equipmentAndQuickSlotsVisibleRows;
    private static System.Func<Inventory, int, int>? _azuEpiGetSlotGridLinearIndex;
    private static ConfigFile? _azuEpiConfig;
    private static ConfigEntryBase? _azuEpiSeparatePanelEntry;
    private static bool? _azuEpiDisplaysEquipmentInSeparatePanel;

    private const int PlayerInventoryWidth = 8;
    private const int VanillaPlayerRows = 4;
    private const string FavoriteBorderName = "InventoryActions_FavoriteBorder";
    private const float FavoriteBorderThickness = 2f;
    private const string ClientConfigSection = "2 - Client";
    private const string InventoryButtonsConfigSection = "3 - Inventory Buttons";
    private const string GeneralConfigSection = "1 - General";
    private const float ContainerHoverHoldDuration = 0.5f;
    private const string ContainerActionSuccessFxPrefabName = "fx_HildirChest_Unlock";
    private const string ContainerActionSuccessFxRpc =
        "InventoryActions_ContainerActionTransientFxV1";
    private const int ContainerActionSuccessVfxKind = 1;
    private const int ContainerActionSuccessSfxKind = 2;
    private const int ContainerActionSuccessVfxLimit = 10;
    private const float ContainerActionSuccessFxLifetime = 5f;
    private const float ContainerActionSuccessFxReceiveRange = 64f;
    private const int ContainerActionSuccessFxReceiveLimit = 32;
    private const float ContainerActionSuccessFxReceiveWindow = 1f;

    internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModName);

    private readonly Harmony _harmony = new(ModGUID);
    private static InventoryActionsPlugin _instance = null!;
    private static readonly InventoryActionRuntimeState Runtime = new();
    private static readonly System.Version MinimumSupportedExternalMultiUserChestVersion = new(0, 6, 1);
    private static float _containerActionSuccessFxReceiveWindowStartedAt = -1f;
    private static int _containerActionSuccessFxReceivedInWindow;

    internal static bool IsDedicatedServer => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                                             (Application.isBatchMode && string.Equals(Paths.ProcessName, "valheim_server", System.StringComparison.OrdinalIgnoreCase));

    private static bool HasExternalMultiUserChestActive =>
        Chainloader.PluginInfos.TryGetValue(ExternalMultiUserChestGuid, out PluginInfo pluginInfo) &&
        pluginInfo.Instance != null;

    private static bool HasSupportedExternalMultiUserChestActive =>
        Chainloader.PluginInfos.TryGetValue(ExternalMultiUserChestGuid, out PluginInfo pluginInfo) &&
        pluginInfo.Instance != null &&
        pluginInfo.Metadata.Version.CompareTo(MinimumSupportedExternalMultiUserChestVersion) >= 0;

    public enum Toggle
    {
        Off,
        On
    }

    private void Awake()
    {
        _instance = this;
        LocalizationManager.Localizer.Load(this);
        BindConfigs();
        InitializeExtraSlotsUiCompatibility();
        InitializeEquipmentAndQuickSlotsCompatibility();
        InitializeAzuEpiCompatibility();
        _harmony.PatchAll();
        Log.LogInfo($"{ModName} loaded.");
    }

    private void Update()
    {
        if (IsDedicatedServer)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null || IsUnityNull(player) || player!.m_isLoading)
        {
            _itemRuleEditor?.Hide();
            HideFeatureGuideHud();
            CancelAreaContainerTransfer();
            ResetContainerHold(Runtime.AreaQuickStackHold);
            ResetContainerHold(Runtime.AreaRestockHold);
            return;
        }

        UpdateAreaContainerTransfer(player);
        HandleHoverActions(player);
        UpdateFeatureGuideHud();
    }

    private void OnDestroy()
    {
        RememberFavoriteSlotItems(Player.m_localPlayer, flush: true);
        _extraSlotsPlugin = null;
        _extraSlotsPlayerRows = null;
        _equipmentAndQuickSlotsVisibleRows = null;
        _azuEpiGetSlotGridLinearIndex = null;
        ClearAzuEpiSeparatePanelSubscription();
        DestroyFeatureGuideHud();
        DestroyItemRuleUi();
        DestroyRestockModeIcons();
        _autoPickupExcludedItemsConfig.SettingChanged -= RefreshAutoPickupExclusions;
        _showRuleTooltips.SettingChanged -= RefreshRuleTooltipVisibility;
        _autoPickupExcludedItems.Clear();
        CancelAreaContainerTransfer();
        CloseInventoryTrashConfirmDialog();
        // Keep inventory action patches installed during runtime teardown to avoid item-move logic changing mid-session.
        Config.Save();
    }

    private static void InitializeExtraSlotsUiCompatibility()
    {
        if (IsDedicatedServer || !Chainloader.PluginInfos.TryGetValue(ExtraSlotsGuid, out PluginInfo plugin) || plugin.Instance == null) return;
        try
        {
            // Resolve the optional public API once, but read its live row count on each layout.
            System.Type? api = plugin.Instance.GetType().Assembly.GetType("ExtraSlots.API");
            System.Reflection.MethodInfo? method = api?.GetMethod("GetInventoryHeightPlayer",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, System.Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(int))
            {
                Log.LogWarning("ExtraSlots public inventory-height API is unavailable; using normal inventory height for buttons.");
                return;
            }
            _extraSlotsPlayerRows = (System.Func<int>)System.Delegate.CreateDelegate(typeof(System.Func<int>), method);
            _extraSlotsPlugin = plugin.Instance;
        }
        catch (System.Exception error) { Log.LogWarning($"ExtraSlots UI compatibility initialization failed: {error.Message}"); }
    }

    private static void InitializeEquipmentAndQuickSlotsCompatibility()
    {
        if (IsDedicatedServer ||
            !Chainloader.PluginInfos.TryGetValue(EquipmentAndQuickSlotsGuid, out PluginInfo plugin) ||
            plugin.Instance == null)
        {
            return;
        }

        try
        {
            // EAQS 3.x keeps equipment, quick, and custom slots in hidden rows of the
            // player's Inventory. Resolve its public boundary once and read the live
            // visible-row count whenever InventoryActions lays out or mutates that inventory.
            System.Type? api = plugin.Instance.GetType().Assembly.GetType("EquipmentAndQuickSlots.API");
            System.Reflection.MethodInfo? method = api?.GetMethod(
                "GetVisibleRows",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                System.Type.EmptyTypes,
                null);
            if (method == null || method.ReturnType != typeof(int))
            {
                Log.LogWarning("EquipmentAndQuickSlots public visible-row API is unavailable; hidden-slot compatibility is disabled.");
                return;
            }

            _equipmentAndQuickSlotsVisibleRows =
                (System.Func<int>)System.Delegate.CreateDelegate(typeof(System.Func<int>), method);
            Log.LogInfo("EquipmentAndQuickSlots visible-row compatibility enabled.");
        }
        catch (System.Exception error)
        {
            Log.LogWarning($"EquipmentAndQuickSlots compatibility initialization failed: {error.Message}");
        }
    }

    private static void InitializeAzuEpiCompatibility()
    {
        if (IsDedicatedServer ||
            !Chainloader.PluginInfos.TryGetValue(AzuExtendedPlayerInventoryGuid, out PluginInfo plugin) ||
            plugin.Instance == null)
        {
            return;
        }

        try
        {
            // AzuEPI stores regular and special slots in the same Inventory. Its public
            // slot-index API exposes the live boundary without binding to internal layout types.
            System.Type? api = plugin.Instance.GetType().Assembly.GetType("AzuEPI.API");
            System.Reflection.MethodInfo? method = api?.GetMethod(
                "GetSlotGridLinearIndex",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(Inventory), typeof(int) },
                null);
            if (method == null || method.ReturnType != typeof(int))
            {
                Log.LogWarning("AzuExtendedPlayerInventory public slot-boundary API is unavailable; special-slot compatibility is disabled.");
                return;
            }

            _azuEpiGetSlotGridLinearIndex =
                (System.Func<Inventory, int, int>)System.Delegate.CreateDelegate(
                    typeof(System.Func<Inventory, int, int>), method);

            ConfigFile config = plugin.Instance.Config;
            _azuEpiConfig = config;
            if (config.ContainsKey(AzuEpiSeparatePanelConfig))
            {
                _azuEpiSeparatePanelEntry = config[AzuEpiSeparatePanelConfig];
                RefreshAzuEpiSeparatePanelSetting();
                if (_azuEpiConfig != null && _azuEpiSeparatePanelEntry != null)
                {
                    config.SettingChanged += HandleAzuEpiSettingChanged;
                }
            }
            else
            {
                Log.LogWarning("AzuExtendedPlayerInventory separate-panel setting is unavailable; using the full inventory height for button placement.");
            }

            Log.LogInfo("AzuExtendedPlayerInventory special-slot compatibility enabled.");
        }
        catch (System.Exception error)
        {
            _azuEpiGetSlotGridLinearIndex = null;
            ClearAzuEpiSeparatePanelSubscription();
            Log.LogWarning($"AzuExtendedPlayerInventory compatibility initialization failed: {error.Message}");
        }
    }

    private static void ClearAzuEpiSeparatePanelSubscription()
    {
        // The slot-boundary delegate has a separate lifetime from this UI setting.
        if (_azuEpiConfig != null)
        {
            _azuEpiConfig.SettingChanged -= HandleAzuEpiSettingChanged;
        }
        _azuEpiConfig = null;
        _azuEpiSeparatePanelEntry = null;
        _azuEpiDisplaysEquipmentInSeparatePanel = null;
    }

    private static void HandleAzuEpiSettingChanged(object? sender, SettingChangedEventArgs args)
    {
        if (_azuEpiSeparatePanelEntry != null &&
            ReferenceEquals(args.ChangedSetting, _azuEpiSeparatePanelEntry))
        {
            RefreshAzuEpiSeparatePanelSetting();
        }
    }

    private static void RefreshAzuEpiSeparatePanelSetting()
    {
        ConfigEntryBase? entry = _azuEpiSeparatePanelEntry;
        if (entry == null)
        {
            _azuEpiDisplaysEquipmentInSeparatePanel = null;
            return;
        }

        try
        {
            _azuEpiDisplaysEquipmentInSeparatePanel = System.Convert.ToInt32(entry.BoxedValue) != 0;
        }
        catch (System.Exception error)
        {
            ClearAzuEpiSeparatePanelSubscription();
            Log.LogWarning($"AzuExtendedPlayerInventory separate-panel setting lookup failed: {error.Message}");
        }
    }

}
