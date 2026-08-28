using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private void Awake()
    {
        _instance = this;
        LocalizationManager.Localizer.OnLocalizationComplete += HandleLocalizationComplete;
        LocalizationManager.Localizer.Load(this);
        bool saveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        try
        {
            BindConfigs();
            EnsureDefaultYamlFiles();
            InitializeJewelcraftingSlotCompatibility();
            InitializeBackpackCompatibility();
            InitializeMagicSupremacyCompatibility();
            InitializeHipLanternCompatibility();
            InitializeEpicLootCompatibility();
            InitializeYamlSync();

            _harmony.PatchAll();
            ApplyMyLittleUICraftingCompatibility();
            Config.Save();
        }
        finally
        {
            Config.SaveOnConfigSet = saveOnSet;
        }

        Log.LogInfo($"{ModName} loaded.");
    }

    private void Update()
    {
        ProcessYamlHotReload();
        UpdateMultiUserContainerRuntime();
        UpdateMultiUserContainerBatchRuntime();

        if (IsDedicatedServer)
        {
            return;
        }

        ApplyMyLittleUICraftingCompatibility();

        if (!InventoryGui.IsVisible())
        {
            UpdateMyLittleUICraftingObjectSuppression(InventoryGui.instance, shouldSuppress: false);
            UpdateQuickSlotInventoryPanelsWhileHidden();
        }

        Player? player = Player.m_localPlayer;
        if (IsUnityNull(player) || player!.m_isLoading)
        {
            ClearQuickSlotsHud();
            HideQuickSlotInventoryPanels();
            ClearCustomEquipmentVisuals();
            SetHintActive(TooltipUi.HotbarSwitchHudHint, false);
            SetHintActive(TooltipUi.FeatureGuideHudHint, false);
            return;
        }

        ProcessDeferredInventoryStateEnsure(player);
        HandleHotbarSwitch(player);
        HandleQuickSlotHotkeys(player);
        HandleContainerQuickStackHotkey(player);
        HandleContainerRestockHotkey(player);
        UpdateQuickSlotsHud(player);
        UpdateHotbarSwitchHud();
        ApplyPinnedTooltipSlotLimit();
        RefreshJewelcraftingPinnedTooltips();
        RefreshActiveDynamicTooltipText();
        HandlePinnedTooltipHotkey();
        HandleInventoryPinnedTooltipWheel();

        if (Time.time < _nextRefreshTime)
        {
            return;
        }

        float refreshInterval = Mathf.Max(0.25f, InventoryMaintenanceInterval);
        _nextRefreshTime = Time.time + refreshInterval;
        RefreshItemNameTokens();
        RebuildStationInputTokens();
        RefreshJewelcraftingSlotDefinitionsIfNeeded(player);
        PrunePendingSlotActions(player);
        RefreshHipLanternCompatibilityState(player);
        RefreshCircletExtendedCompatibilityState(player);

        EnsureInventoryState(player, InventoryStateEnsureReason.PeriodicAudit, InventoryStateAuditLevel.HeightOnly);

        float lightAuditInterval = Mathf.Max(refreshInterval, LightSafetyAuditInterval);
        float heavyAuditInterval = Mathf.Max(lightAuditInterval, HeavySafetyAuditInterval);
        bool heavyAuditDue = _nextHeavyAuditTime <= 0f || Time.time >= _nextHeavyAuditTime;
        bool lightAuditDue = _nextLightAuditTime <= 0f || Time.time >= _nextLightAuditTime;

        if (heavyAuditDue && ShouldDelayPeriodicFullIntegrityAudit())
        {
            _nextHeavyAuditTime = Mathf.Max(Time.time + refreshInterval, InventorySafety.HeavyAuditDelayUntil);
            heavyAuditDue = false;
        }

        if (heavyAuditDue)
        {
            _nextHeavyAuditTime = Time.time + heavyAuditInterval;
            _nextLightAuditTime = Time.time + lightAuditInterval;
            EnsureInventoryState(player, InventoryStateEnsureReason.PeriodicAudit, InventoryStateAuditLevel.FullIntegrity);
        }
        else if (lightAuditDue)
        {
            _nextLightAuditTime = Time.time + lightAuditInterval;
            EnsureInventoryState(player, InventoryStateEnsureReason.PeriodicAudit, InventoryStateAuditLevel.SlotLight);
        }
    }

    private static bool ShouldDelayPeriodicFullIntegrityAudit()
    {
        return InventorySafety.HeavyAuditDelayUntil > Time.time;
    }

    private void OnDestroy()
    {
        ShutdownEpicLootCompatibility();
        ShutdownMultiUserContainerRuntime();
        ShutdownContainerPreview();
        LocalizationManager.Localizer.OnLocalizationComplete -= HandleLocalizationComplete;
        StopYamlWatcher();
        Config.Save();
    }

    private static void HandleLocalizationComplete()
    {
        unchecked
        {
            _uiLocalizationVersion++;
        }

        CraftingController.MarkSearchInputDirty();
        CraftingController.MarkGroupRailDirty();
        ClearCraftingEnglishLocalizationCaches();
        CraftingController.MarkRecipeGridLayoutDirty();
    }
}
