using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace InventoryActions;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("sighsorry.InventorySlots")]
[BepInIncompatibility("goldenrevolver.quick_stack_store")]
[BepInDependency(ExternalMultiUserChestGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class InventoryActionsPlugin : BaseUnityPlugin
{
    internal const string ModName = "InventoryActions";
    internal const string ModVersion = "1.0.6";
    internal const string Author = "sighsorry";
    internal const string ModGUID = $"{Author}.{ModName}";
    private const string ExternalMultiUserChestGuid = "com.maxsch.valheim.MultiUserChest";

    private const int PlayerInventoryWidth = 8;
    private const int VanillaPlayerRows = 4;
    private const string FavoriteBorderName = "InventoryActions_FavoriteBorder";
    private const float FavoriteBorderThickness = 2f;
    private const string ClientConfigSection = "2 - Client";
    private const string RestockConfigSection = "3 - Restock";
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
        LocalizationManager.Localizer.OnLocalizationComplete += HandleLocalizationComplete;
        LocalizationManager.Localizer.Load(this);
        BindConfigs();
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
            CancelAreaContainerTransfer();
            ResetContainerHold(Runtime.AreaQuickStackHold);
            ResetContainerHold(Runtime.AreaRestockHold);
            return;
        }

        UpdateAreaContainerTransfer(player);
        HandleHoverActions(player);
    }

    private void OnDestroy()
    {
        LocalizationManager.Localizer.OnLocalizationComplete -= HandleLocalizationComplete;
        CancelAreaContainerTransfer();
        // Keep inventory action patches installed during runtime teardown to avoid item-move logic changing mid-session.
        Config.Save();
    }

    private static void HandleLocalizationComplete()
    {
        unchecked
        {
            Runtime.UiLocalizationVersion++;
        }
    }
}
