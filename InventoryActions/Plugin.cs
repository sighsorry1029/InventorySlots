using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace InventoryActions;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("sighsorry.InventorySlots")]
[BepInIncompatibility("goldenrevolver.quick_stack_store")]
public sealed partial class InventoryActionsPlugin : BaseUnityPlugin
{
    internal const string ModName = "InventoryActions";
    internal const string ModVersion = "1.0.2";
    internal const string Author = "sighsorry";
    internal const string ModGUID = $"{Author}.{ModName}";

    private const int PlayerInventoryWidth = 8;
    private const int VanillaPlayerRows = 4;
    private const string FavoriteBorderName = "InventoryActions_FavoriteBorder";
    private const float FavoriteBorderThickness = 2f;
    private const string ClientConfigSection = "2 - Client";
    private const string RestockConfigSection = "3 - Restock";
    private const string GeneralConfigSection = "1 - General";
    private const float ContainerHoverHoldDurationDefault = 0.5f;
    private const float ContainerHoverHoldDurationMin = 0.1f;
    private const float ContainerHoverHoldDurationMax = 0.5f;
    private const string ContainerActionSuccessFxPrefabName = "fx_HildirChest_Unlock";
    private const int ContainerActionSuccessFxMaxMode = 12;

    internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModName);

    private readonly Harmony _harmony = new(ModGUID);
    private static InventoryActionsPlugin _instance = null!;
    private static readonly InventoryActionRuntimeState Runtime = new();

    internal static bool IsDedicatedServer => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                                             (Application.isBatchMode && string.Equals(Paths.ProcessName, "valheim_server", System.StringComparison.OrdinalIgnoreCase));

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
            ResetContainerHold(Runtime.AreaQuickStackHold);
            ResetContainerHold(Runtime.AreaRestockHold);
            return;
        }

        HandleHoverActions(player);
    }

    private void OnDestroy()
    {
        LocalizationManager.Localizer.OnLocalizationComplete -= HandleLocalizationComplete;
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
