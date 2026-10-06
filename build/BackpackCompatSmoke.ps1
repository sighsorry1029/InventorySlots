#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
function Read-Block([string]$file, [string]$declaration) {
    $source = [IO.File]::ReadAllText((Join-Path $root $file))
    $start = $source.IndexOf($declaration, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production declaration: $declaration" }
    $open = $source.IndexOf('{', $start)
    $depth = 1; $end = $open + 1
    # Selected bodies have balanced braces, including interpolation expressions.
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { ++$depth }
        if ($source[$end] -eq '}') { --$depth }
        ++$end
    }
    if ($depth -ne 0) { throw "Unbalanced production declaration: $declaration" }
    return $source.Substring($start, $end - $start)
}
$adapter = Read-Block 'BackpackCompatAdapters.cs' 'private sealed class AdventureBackpacksApi'
$show = Read-Block 'InventoryPatchHandlers.cs' 'internal static void OnInventoryGuiShow('
$intro = Read-Block 'InventoryQuickSlotPanels.cs' 'internal static void StartQuickSlotPanelIntroAnimation('
$patch = Read-Block 'ContainerGuiPatches.cs' 'internal static class InventoryGuiShowValidateInventoryPatch'
$production = @"
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AdventureBackpacks.API.Client;
using ItemData = ItemDrop.ItemData;
namespace InventorySlots {
public sealed partial class InventorySlotsPlugin {
$adapter
$show
$intro
}
$patch
}
"@
$hostCode = @'
public sealed class ItemDrop { public sealed class ItemData { public bool Backpack = true; } }
public sealed class Player { public static Player? m_localPlayer; public bool m_isLoading; }
public sealed class Animator { public bool Visible; public bool GetBool(string name) => Visible; }
public sealed class InventoryGui {
    public static InventoryGui? instance;
    public static bool Visible;
    public Animator? m_animator = new();
    public bool ContainerOpen, ThrowOnClose;
    public int Closes;
    public static bool IsVisible() => Visible;
    public bool IsContainerOpen() => ContainerOpen;
    public void CloseContainer() { Closes++; if (ThrowOnClose) throw new InvalidOperationException("close failed"); ContainerOpen = false; }
}
public static class Time { public static float unscaledTime; }
namespace AdventureBackpacks.API.Client {
    public static class ABAPIClient {
        public static bool Loaded = true;
        public static bool IsLoaded() => Loaded;
        public static bool IsBackpack(ItemDrop.ItemData item) => item.Backpack;
        public static bool IsBackpackEquipped(Player player) => true;
    }
}
namespace AdventureBackpacks.Patches {
    public static class InventoryGuiPatches { public static bool BackpackIsOpen, BackpackEquipped; }
}
namespace AdventureBackpacks.Extensions {
    public static class PlayerExtensions {
        public static int Destroys;
        public static Player? DestroyedPlayer;
        public static void DestroyBackpackContainerProxy(Player player) { Destroys++; DestroyedPlayer = player; }
        public static void DestroyBackpackContainerProxy() => throw new Exception("Wrong overload");
    }
}
namespace InventorySlots {
public sealed partial class InventorySlotsPlugin {
    private sealed class TestLogger { public int Warnings; public void LogWarning(string text) => Warnings++; }
    private static readonly TestLogger Log = new();
    private static class InventoryPanels {
        public static bool QuickSlotPanelOutroActive, QuickSlotPanelIntroActive;
        public static float QuickSlotPanelIntroStartTime, QuickSlotPanelIntroDuration;
        public static readonly List<int> QuickSlotPanelOutroStartPositions = new();
    }
    private enum InventoryStateEnsureReason { GuiShow }
    private enum InventoryStateAuditLevel { SlotLight }
    private static int Ensures, BeforeShows, RealShows, Checks;
    private static void RequestInventoryStateEnsure(Player p, InventoryStateEnsureReason reason, InventoryStateAuditLevel level) => Ensures++;
    private static float GetInventoryGuiAnimationDuration() => 0.25f;
    internal static void BeforeRealInventoryGuiShown() => BeforeShows++;
    private static void OnRealInventoryGuiShown() => RealShows++;
    private static void Check(bool result, string message) { Checks++; if (!result) throw new Exception(message); }
    private static bool ShowPrefix(InventoryGui gui) {
        object?[] args = { gui, null };
        typeof(InventoryGuiShowValidateInventoryPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
        return (bool)args[1]!;
    }
    private static void ShowPostfix(bool state) => typeof(InventoryGuiShowValidateInventoryPatch)
        .GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { state });
    private static Assembly BadContract(Type flagType, bool readOnly, Type returnType) {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("ABFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("main");
        var gui = module.DefineType("AdventureBackpacks.Patches.InventoryGuiPatches", TypeAttributes.Public);
        gui.DefineField("BackpackIsOpen", flagType, FieldAttributes.Public | FieldAttributes.Static | (readOnly ? FieldAttributes.InitOnly : (FieldAttributes)0));
        gui.DefineField("BackpackEquipped", typeof(bool), FieldAttributes.Public | FieldAttributes.Static);
        gui.CreateType();
        var extensions = module.DefineType("AdventureBackpacks.Extensions.PlayerExtensions", TypeAttributes.Public);
        var method = extensions.DefineMethod("DestroyBackpackContainerProxy", MethodAttributes.Public | MethodAttributes.Static, returnType, new[] { typeof(Player) });
        var il = method.GetILGenerator();
        if (returnType == typeof(int)) il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        extensions.CreateType();
        return assembly;
    }
    public static int RunBackpackSmoke() {
        var assembly = typeof(InventorySlotsPlugin).Assembly;
        Check(AdventureBackpacksApi.TryCreate(assembly, out var api, out var detail) && api != null && detail == "", "Matching cleanup contract enables adapter");
        ABAPIClient.Loaded = false;
        Check(!AdventureBackpacksApi.TryCreate(assembly, out _, out _), "Missing optional mod stays disabled");
        ABAPIClient.Loaded = true;
        Check(!AdventureBackpacksApi.TryCreate(typeof(object).Assembly, out _, out _), "Missing cleanup contract fails closed");
        Check(!AdventureBackpacksApi.TryCreate(BadContract(typeof(int), false, typeof(void)), out _, out _), "Wrong flag type rejected");
        Check(!AdventureBackpacksApi.TryCreate(BadContract(typeof(bool), true, typeof(void)), out _, out _), "Readonly flag rejected");
        Check(!AdventureBackpacksApi.TryCreate(BadContract(typeof(bool), false, typeof(int)), out _, out _), "Wrong cleanup return type rejected");
        var player = Player.m_localPlayer = new Player();
        var item = new ItemDrop.ItemData();
        var gui = InventoryGui.instance = new InventoryGui { ContainerOpen = true };
        AdventureBackpacks.Patches.InventoryGuiPatches.BackpackIsOpen = true;
        AdventureBackpacks.Patches.InventoryGuiPatches.BackpackEquipped = true;
        api!.OnCustomBackpackUnequipping(player, item);
        Check(gui.Closes == 1 && !gui.ContainerOpen, "Open backpack container closes once");
        Check(!AdventureBackpacks.Patches.InventoryGuiPatches.BackpackIsOpen && !AdventureBackpacks.Patches.InventoryGuiPatches.BackpackEquipped, "Unequip clears both external flags");
        Check(AdventureBackpacks.Extensions.PlayerExtensions.Destroys == 1 && ReferenceEquals(AdventureBackpacks.Extensions.PlayerExtensions.DestroyedPlayer, player), "Only current player's proxy is released using Player overload");
        InventoryGui.instance = null;
        AdventureBackpacks.Patches.InventoryGuiPatches.BackpackIsOpen = true;
        api.OnCustomBackpackUnequipping(player, item);
        Check(!AdventureBackpacks.Patches.InventoryGuiPatches.BackpackIsOpen && AdventureBackpacks.Extensions.PlayerExtensions.Destroys == 2, "Missing GUI still clears stale external state");
        AdventureBackpacks.Patches.InventoryGuiPatches.BackpackIsOpen = true;
        api.OnCustomBackpackUnequipping(new Player(), item);
        api.OnCustomBackpackUnequipping(player, new ItemDrop.ItemData { Backpack = false });
        Check(AdventureBackpacks.Patches.InventoryGuiPatches.BackpackIsOpen && AdventureBackpacks.Extensions.PlayerExtensions.Destroys == 2, "Remote player and non-backpack cannot change local state");
        InventoryGui.instance = gui;
        gui.ContainerOpen = true; gui.ThrowOnClose = true;
        api.OnCustomBackpackUnequipping(player, item);
        Check(Log.Warnings == 1, "Cleanup failure does not interrupt equipment handling");
        gui.ThrowOnClose = false;

        InventoryGui.Visible = true; gui.m_animator!.Visible = true;
        InventoryPanels.QuickSlotPanelIntroActive = true;
        InventoryPanels.QuickSlotPanelIntroStartTime = 4;
        Time.unscaledTime = 8;
        ShowPostfix(ShowPrefix(gui));
        Check(InventoryPanels.QuickSlotPanelIntroStartTime == 4, "Repeated Show preserves active intro progress");
        gui.m_animator.Visible = false; // Native IsVisible still true just after Hide.
        InventoryPanels.QuickSlotPanelOutroActive = true;
        InventoryPanels.QuickSlotPanelOutroStartPositions.Add(1);
        bool reopening = ShowPrefix(gui);
        Check(!reopening, "Closing is not already open despite IsVisible grace frames");
        gui.m_animator.Visible = true;
        ShowPostfix(reopening);
        Check(!InventoryPanels.QuickSlotPanelOutroActive && InventoryPanels.QuickSlotPanelOutroStartPositions.Count == 0 && InventoryPanels.QuickSlotPanelIntroStartTime == 8, "Reopening cancels outro and restarts intro");
        InventoryPanels.QuickSlotPanelOutroActive = true;
        ShowPostfix(true);
        Check(!InventoryPanels.QuickSlotPanelOutroActive, "Show always cancels outstanding outro even with stale caller state");
        InventoryGui.Visible = false; gui.m_animator.Visible = true;
        Check(!ShowPrefix(gui), "Hover preview does not count as an open inventory");
        InventoryGui.Visible = true; gui.m_animator.Visible = false;
        bool outerState = ShowPrefix(gui);
        gui.m_animator.Visible = true;
        bool nestedState = ShowPrefix(gui);
        Time.unscaledTime = 16;
        ShowPostfix(nestedState);
        ShowPostfix(outerState);
        Check(InventoryPanels.QuickSlotPanelIntroStartTime == 16, "Nested Show cannot overwrite outer Harmony state");
        Check(BeforeShows == 5 && RealShows == 5 && Ensures == 5, "Existing show hooks and inventory checks still execute");
        return Checks;
    }
}
}
'@
Add-Type -TypeDefinition ($production + $hostCode) -Language CSharp
$count = [InventorySlots.InventorySlotsPlugin]::RunBackpackSmoke()
Write-Output "PASS: $count source-linked backpack cleanup and inventory transition checks; no Unity/game session executed."
