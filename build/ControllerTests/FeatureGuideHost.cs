using BepInEx.Configuration;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static readonly string TestGuideDirectory = Path.Combine(Path.GetTempPath(), "InventoryGuideTests-" + Guid.NewGuid().ToString("N"));
    private static string _testGuideDirectory = "";
    private sealed class TestLogger { public void LogWarning(string message) => TestStateWarnings.Add(message); }
    public static readonly System.Collections.Generic.List<string> TestStateWarnings = new();
    private static readonly TestLogger Log = new();
#if INVENTORY_SLOTS
    private static class InventoryClient
    {
        internal static bool ClientStateLoaded;
        internal static InventorySlotsClientState ClientState = new();
    }
    private static string ClientStateFilePath => Path.Combine(_testGuideDirectory, "ClientState.yml");
#endif
    public static int TestGuideToggles;
    public static bool TestGuideReady = true;
    public static bool TestGuideCollapsed => IsFeatureGuideCollapsed();
    public static bool TestGuideBindingShown() => ShowControllerFeatureGuideToggle();
    public static void TestShowGuide(bool show) => SetFeatureGuideState(show, false);
    private static bool CanInteractWithFeatureGuideToggle() => TestGuideReady && InventoryGui.instance.m_dragGo == null;
    private static void InvalidateFeatureGuideTextAndMeasurements() => TestGuideToggles++;
    public static void TestGuideTriangle() => ToggleFeatureGuideCollapsed();
    private static string GetShortcutDisplayText(KeyboardShortcut shortcut) => shortcut.ToString();
    private static bool AreShortcutModifiersHeldAllowingAltPair(KeyboardShortcut shortcut) => shortcut.Modifiers.All(key =>
        key is KeyCode.LeftAlt or KeyCode.RightAlt ? Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) : Input.GetKey(key));
    public static void TestGuideKey(KeyCode key, params KeyCode[] modifiers) => _featureGuideToggleKey.Value = new(key, modifiers);
    public static bool TestGuideShown => IsFeatureGuideVisible();
    public static string TestGuideYaml => File.ReadAllText(ClientStateFilePath);
    public static string TestGuideFilePath => ClientStateFilePath;
    public static bool TestGuideSavePending => _featureGuideSavePending;
    public static void TestRetryGuideSave(bool flush = false) => RetryFeatureGuideStateSave(flush);
    public static void TestReloadGuide() {
#if INVENTORY_SLOTS
        InventoryClient.ClientStateLoaded = false;
#else
        _clientState = null;
        _clientStateLoadFailed = false;
#endif
    }
    public static void TestLoadGuideYaml(string yaml) {
        Directory.CreateDirectory(Path.GetDirectoryName(ClientStateFilePath)!);
        File.WriteAllText(ClientStateFilePath, yaml);
        TestReloadGuide();
    }
    public static void TestCleanupGuideFiles() { if (Directory.Exists(TestGuideDirectory)) Directory.Delete(TestGuideDirectory, true); }
    public static void TestGuideHotkey() => HandleFeatureGuideToggleHotkey(Player.m_localPlayer);
    public static string TestGuideHeader(string guide) => AddFeatureGuideToggleHint(guide);
    private static void TestResetGuideAdapter()
    {
        TestGuideToggles = 0;
        TestGuideReady = true;
        TestStateWarnings.Clear();
        _testGuideDirectory = Path.Combine(TestGuideDirectory, Guid.NewGuid().ToString("N"));
#if INVENTORY_SLOTS
        InventoryClient.ClientStateLoaded = true;
        InventoryClient.ClientState = new();
#else
        BepInEx.Paths.ConfigPath = _testGuideDirectory;
        _clientState = new();
        _clientStateLoadFailed = false;
        Runtime.LoadedFavoritesPlayerId = "";
        Runtime.FavoriteSlots.Clear(); FavoriteSlotItems.Clear();
        _favoriteMemoryPending = _favoriteMemorySavePending = false;
        _favoriteMemorySaveRetryAt = 0f;
#endif
        _featureGuideToggleKey = new(new KeyboardShortcut(KeyCode.F6));
        _featureGuideVisibilityFrame = -1;
        _featureGuideSavePending = false;
        _featureGuideSaveRetryAt = 0f;
        _controllerGuideFrame = -1;
        Input.Down.Clear(); Input.Held.Clear();
        UnifiedPopup.Visible = PlayerCustomizaton.Visible = Hud.PieceSelection = Hud.Radial = false;
    }
}
