using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private enum Toggle { Off, On }
    private sealed class TestPlugin : Component { }
    private static readonly TestPlugin _instance = new();
    public static bool TestDedicated;
    private static bool IsDedicatedServer => TestDedicated;
#if INVENTORY_SLOTS
    private const string ControllerInputConfigSection = "Controller";
    private static ConfigEntry<Toggle> _enableControllerHotkeys = new(Toggle.On);
    private static readonly object _controllerFavoriteModifierButton = new();
    private static GameObject? _inventoryTrashConfirmDialog;
    private static bool IsControllerHotkeyHeld(object entry) => TestLegacyFavoriteHeld;
#else
    private static class Runtime
    {
        internal static GameObject? TrashConfirmDialog;
        internal static string LoadedFavoritesPlayerId = "";
        internal static readonly System.Collections.Generic.HashSet<Vector2i> FavoriteSlots = new();
    }
    private static readonly System.Collections.Generic.Dictionary<Vector2i, string> FavoriteSlotItems = new();
    private static bool _favoriteMemoryPending, _favoriteMemorySavePending;
    private static float _favoriteMemorySaveRetryAt;
    private static string GetPlayerId(Player player) => player.Id;
#endif
    private static class FavoriteMemoryAccess { internal static bool IsLoading(Player player) => player.Loading; }
    public static bool TestClosing, TestBlocked, TestCanShow = true, TestCanFavorite = true, TestLegacyFavoriteHeld;
    public static bool TestRulesPinned;
    public static int TestRulesClosedFrame = -1;
    public static int TestFavorites, TestPlayerSorts, TestContainerSorts, TestRuleOpens;
    public static bool TestLastRestock;
    public static GameObject? TestLastRuleDragObject;
    public static Vector2i TestLastFavorite;
    private static ConfigEntry<T> ConfigEntry<T>(string section, string name, T value, string description, bool synchronizedSetting) => new(value);
    private static bool IsInventoryPanelClosing(InventoryGui gui) => TestClosing;
    private static bool IsItemRuleInputBlocked() => TestRulesPinned || TestRulesClosedFrame == Time.frameCount;
    private static bool ShouldBlockGlobalHotkeys(Player player) => TestBlocked || IsItemRuleInputBlocked();
    private static bool CanShowItemRules(InventoryGui gui) => TestCanShow;
    private static void UpdateItemRuleControllerInput() { }
    private static bool IsOutOfBounds(Inventory inventory, Vector2i pos) => pos.x < 0 || pos.y < 0 || pos.x >= inventory.Width || pos.y >= inventory.Height;
    private static bool CanFavoriteSlot(Player player, Inventory inventory, Vector2i pos) => TestCanFavorite;
    private static bool CanFavoriteCell(Inventory inventory, Vector2i pos) => TestCanFavorite;
    private static void ToggleFavoriteSlot(Player player, Vector2i pos) { TestFavorites++; TestLastFavorite = pos; }
    private static void SortPlayerInventory(Player player) => TestPlayerSorts++;
    private static void SortCurrentContainer(Player player) => TestContainerSorts++;
    internal static void OpenControllerItemRules(bool restock)
    {
        TestRuleOpens++;
        TestLastRestock = restock;
        TestLastRuleDragObject = InventoryGui.instance.m_dragGo;
        if (TestPinRulesOnOpen) TestRulesPinned = true;
    }

    // Clone hint cleanup is exercised by ButtonCaptionTests, not this input host.
    private static void RemoveClonedInventoryButtonHints(UnityEngine.UI.Button button) { }

    public static InventoryGui TestReset()
    {
        ResetControllerItemMenu();
        TestResetMenuAdapter();
        TestResetGuideAdapter();
        ResetInventoryButtonNavigation();
        TestResetButtonAdapter();
        TestClosing = TestBlocked = TestLegacyFavoriteHeld = TestRulesPinned = false;
        TestDedicated = false;
        _instance.isActiveAndEnabled = true;
        TestCanShow = TestCanFavorite = true;
        TestRulesClosedFrame = -1;
        TestFavorites = TestPlayerSorts = TestContainerSorts = TestRuleOpens = 0;
        TestLastFavorite = default;
        TestLastRuleDragObject = null;
        ZInput.Held.Clear(); ZInput.Down.Clear(); ZInput.ResetCalls.Clear(); ZInput.Exclusive = true;
        Time.frameCount++;
        Player.m_localPlayer = new Player();
        InventoryGui.Visible = true;
        InventoryGui gui = InventoryGui.instance = new InventoryGui();
        gui.m_playerGrid.Inventory = Player.m_localPlayer.Inventory;
        gui.m_playerGrid.m_uiGroup.IsActive = true;
        gui.ContainerGrid.Inventory = new Inventory();
        gui.m_playerGrid.m_selected = new Vector2i(3, 2);
        EventSystem.current = new EventSystem();
#if INVENTORY_SLOTS
        _inventoryTrashConfirmDialog = null;
#else
        Runtime.TrashConfirmDialog = null;
#endif
        BindInventoryControllerConfig();
        _enableControllerHotkeys.Value = Toggle.On;
        OnControllerInputUpdated();
        return gui;
    }
    public static void TestSetInventoryModifier(string value) => _inventoryActionModifier.Value = Enum.Parse<InventoryControllerModifier>(value);
    public static void TestSetWorldModifier(string value) => _favoriteRestockModifier.Value = Enum.Parse<RestockControllerModifier>(value);
    public static void TestEnable(bool value) => _enableControllerHotkeys.Value = value ? Toggle.On : Toggle.Off;
    public static void TestPluginActive(bool value) => _instance.isActiveAndEnabled = value;
    public static string TestInventoryDefault => _inventoryActionModifier.Value.ToString();
    public static string TestWorldDefault => _favoriteRestockModifier.Value.ToString();
    public static bool TestWorldHeld() => IsFavoriteRestockControllerHeld();
    public static bool TestChordHeld() => IsInventoryControllerChordHeld();
    public static void TestSetTrashDialog()
    {
#if INVENTORY_SLOTS
        _inventoryTrashConfirmDialog = new GameObject();
#else
        Runtime.TrashConfirmDialog = new GameObject();
#endif
    }
}
