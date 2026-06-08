using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;
using Requirement = Piece.Requirement;

namespace InventorySlots;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("Azumatt.AzuExtendedPlayerInventory")]
[BepInIncompatibility("shudnal.ExtraSlots")]
[BepInIncompatibility("shudnal.ExtraSlotsCustomSlots")]
[BepInIncompatibility("randyknapp.mods.equipmentandquickslots")]
[BepInIncompatibility("com.bruce.valheim.comfyquickslots")]
[BepInIncompatibility("goldenrevolver.quick_stack_store")]
[BepInDependency("Azumatt.AzuCraftyBoxes", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("org.bepinex.plugins.jewelcrafting", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("vapok.mods.adventurebackpacks", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("org.bepinex.plugins.backpacks", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("RustyMods.RustyBags", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Dreanegade.Magic_Supremacy", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(EpicLootGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(TooltipExpansionGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CurrencyPocketGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(MyLittleUIGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(RecycleNReclaimGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(VeiledRecipesGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class InventorySlotsPlugin : BaseUnityPlugin
{
    private const int BaseRows = 4;
    private const int InventoryWidth = 8;
    private const int MaxSupportedExtraRows = 5;
    private const int MaxSupportedQuickSlots = 9;
    private const string SlotIdKey = "InventorySlotsSlotId";
    private const string EquippedByKey = "InventorySlotsEquippedBy";
    private const string BackupKey = "InventorySlotsBackup";
    private const string UpgradeFavoriteItemIdKey = "InventorySlotsUpgradeFavoriteId";
    private const string ExtraSlotsEquippedByKey = "ExtraSlotsEquippedBy";
    private const string ExtraSlotsEquippedSlotKey = "ExtraSlotsEquippedSlot";
    private const string ExtraSlotsEquippedWeaponShieldKey = "ExtraSlotsEquippedWeaponShield";
    private const string JewelcraftingRingSlotId = "jewelcrafting.ring";
    private const string JewelcraftingNecklaceSlotId = "jewelcrafting.necklace";
    private const string AdventureBackpackSlotId = "adventurebackpacks.backpack";
    private const string SmoothbrainBackpackSlotId = "smoothbrainbackpacks.backpack";
    private const string RustyBagSlotId = "rustybags.bag";
    private const string RustyQuiverSlotId = "rustybags.quiver";
    private const string MagicSupremacyBeltSlotId = "magicsupremacy.belt";
    private const string MagicSupremacyNativeBeltSlotId = "belt";
    private const string MultiUserChestIgnoreZdoKey = "MUC_Ignore";
    private const string YamlFileName = "InventorySlots.yml";
    private const string ClientStateFileName = "InventorySlots.Client.yml";
    private const string CustomSlotPanelName = "InventorySlots_CustomSlotPanel";
    private const string QuickSlotPanelName = "InventorySlots_QuickSlotPanel";
    private const string SlotPanelDragBorderName = "InventorySlots_DragBorder";
    private const string PlayerStatPanelHostName = "InventorySlots_PlayerStatPanelHost";
    private const string InventorySortPanelName = "InventorySlots_InventorySortPanel";
    private const string InventoryTrashPanelName = "InventorySlots_TrashPanel";
    private const string CurrencyPocketPanelName = "CoinPocketUI";
    private const int CurrencyPocketInventoryRow = 4;
    private const float CurrencyPocketOutsideGap = 6f;
    private const string QuickSlotsHotkeyBarName = "InventorySlots_QuickSlotsHotKeyBar";
    private const string FavoriteKeyHintName = "InventorySlots_FavoriteKeyHint";
    private const string PinnedTooltipKeyHintName = "InventorySlots_PinnedTooltipKeyHint";
    private const string InventoryPinnedTooltipNamePrefix = "InventorySlots_InventoryPinnedTooltip_";
    private const string InventoryPinnedJewelcraftingTooltipRootName = "InventorySlots_JewelcraftingTooltipRoot";
    private const string InventoryPinnedTooltipMarkerName = "InventorySlots_PinnedTooltipMarker";
    private const string FavoriteBorderName = "InventorySlots_FavoriteBorder";
    private const float FavoriteBorderThickness = 2f;
    private const float QuickSlotsHudSlotBackgroundAlpha = 0.25f;
    private const string RpcRequestSort = "InventorySlots_RequestSort";
    private const int CustomSlotPanelRows = 3;
    private const int QuickSlotPanelColumns = 3;
    private const int QuickSlotPanelRows = 3;
    private const float EquipmentPanelGapRows = 0.35f;
    private const float SidePanelGapColumns = 1.45f;
    private const float SidePanelBackgroundPadding = 16f;
    private const float PlayerStatPanelGap = -8f;
    private const int HotbarRowsToSwitch = 2;
    private const int HotbarSwitchFavoriteRow = 1;
    private const float ContainerActionPairButtonWidthMultiplier = 1f;
    private const float SortButtonOutsideGap = 1f;
    private const float QuickSlotPanelIntroFallbackDuration = 0.25f;
    private const float ContainerHoverHoldDurationDefault = 0.5f;
    private const float ContainerHoverHoldDurationMin = 0.1f;
    private const float ContainerHoverHoldDurationMax = 0.5f;
    private const int PinnedTooltipSlotCount = 3;
    private const float PinnedTooltipFixedPanelGap = 15f;
    private const float InventoryMaintenanceInterval = 1f;
    private const float LightSafetyAuditInterval = 5f;
    private const float HeavySafetyAuditInterval = 30f;
    private const int MaxInventoryStateAuditPasses = 3;
    private const float PendingSlotActionTimeout = 30f;
    private const float InventoryPinnedJewelcraftingReservedHeight = 190f;
    private const string ProgressiveSlotsConfigSection = "2 - Progressive Slots";
    private const string ClientConfigSection = "3 - Client";
    private const string ClientUiConfigSection = "4 - Client UI";
    private const string ClientKeysConfigSection = "5 - Client Keys";
    private const string RestockConfigSection = "6 - Restock";
    private const string ControllerInputConfigSection = "7 - Controller Input";
    private static readonly Vector2 HotbarSwitchHintOffset = new(-50f, -32f);
    private static readonly Color HotbarSwitchHintColor = new(174f / 255f, 224f / 255f, 1f, 1f);
    private const float HotbarSwitchHintSize = 64f;
    private const float HotbarSwitchHintFontSize = 16f;
    private static readonly Vector2 InventoryWheelHintOffset = new(0f, -32f);
    private static readonly Color InventoryWheelHintColor = new(174f / 255f, 224f / 255f, 1f, 1f);
    private const float InventoryWheelHintSize = 0f;
    private static readonly Vector2 PinnedTooltipFixedPanelSize = new(345f, 600f);
    private static readonly Vector2 InventoryPinnedTooltipFixedOffset = new(828f, -384f);
    private static readonly Vector2 CraftingPinnedTooltipFixedOffset = new(-900f, 0f);
    private static readonly Vector2 QuickSlotsPanelFixedOffset = new(-80f, -552f);
    private static readonly Vector2 QuickSlotsHudFallbackPosition = new(64f, -520f);
    private static readonly Vector2 EquipmentSlotsPanelFixedOffset = new(-80f, 0f);
    private static readonly Vector2 PlayerStatPanelsFixedOffset = Vector2.zero;
    private static readonly Vector2 ArmorPanelFixedOffset = new(-5f, 15f);
    private static readonly Vector2 WeightPanelFixedOffset = new(-5f, -5f);
    private static readonly Vector2 SynergyPanelFixedOffset = new(-5f, -25f);
    private static readonly Vector2 InventorySortButtonFixedOffset = new(2f, 2f);
    private static readonly Vector2 ContainerSortButtonFixedOffset = Vector2.zero;
    private const float ContainerWeightPanelFixedYOffset = 200f;

    internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource(ModName);

    private readonly Harmony _harmony = new(ModGUID);
    private static InventorySlotsPlugin _instance = null!;

    internal static bool IsDedicatedServer => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                                             (Application.isBatchMode && string.Equals(Paths.ProcessName, "valheim_server", StringComparison.OrdinalIgnoreCase));
    public enum Toggle
    {
        Off,
        On
    }

    public enum CraftingHoverTooltipMode
    {
        Full,
        TitleOnly,
        Off
    }

    public enum InventoryRowsDisplayMode
    {
        Fixed,
        Expandable
    }

    public enum PinnedTooltipSlotMode
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    public enum GamepadUiScrollSource
    {
        RightStickY,
        DPadVertical,
        RightStickYOrDPadVertical
    }

    public enum ControllerDPadHotkeyMode
    {
        InventoryNavigation,
        Hotkeys,
        HotkeysWhileHoldingModifier
    }

    public enum ControllerHotkeyAction
    {
        Off,
        JoyButtonA,
        JoyButtonB,
        JoyButtonX,
        JoyButtonY,
        JoyLBumper,
        JoyRBumper,
        JoyLTrigger,
        JoyRTrigger,
        JoyBack,
        JoyStart,
        JoyLStick,
        JoyRStick,
        JoyDPadUp,
        JoyDPadDown,
        JoyDPadLeft,
        JoyDPadRight,
        JoyHotbarUse,
        JoyAltKeys,
        AltPlace,
        JoyUse
    }

}

