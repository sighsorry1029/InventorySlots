using System;
using System.Reflection;
using HarmonyLib;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Func<Container, bool>? _povIsAnvil, _povIsAnvilCache, _povViewingAnvil;
    private static InventoryGui? _povAnvilGui, _povAnvilOpeningGui;

    private readonly struct PovAnvilShowState
    {
        internal PovAnvilShowState(InventoryGui? opening, InventoryGui? owner) { Opening = opening; Owner = owner; }
        internal readonly InventoryGui? Opening, Owner;
    }

    private static void InstallPathOfValheimanAnvilUi(Harmony harmony, Assembly assembly)
    {
        Type anvil = assembly.GetType("PassiveTree.RunicCraft.Anvil", true)!;
        Func<Container, bool> Bind(string name) => (Func<Container, bool>)Delegate.CreateDelegate(
            typeof(Func<Container, bool>), RequirePathOfValheimanMethod(anvil, name, typeof(bool), typeof(Container)));
        var isAnvil = Bind("Is");
        var isCache = Bind("IsCache");
        var viewing = Bind("Viewing"); // Open also requires ownership; the UI must work while ownership is pending.
        MethodInfo refresh = RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.UI.RunicAnvilWindow", true)!,
            "Refresh", typeof(void), typeof(InventoryGui));
        MethodInfo show = AccessTools.DeclaredMethod(typeof(InventoryGui), nameof(InventoryGui.Show), new[] { typeof(Container), typeof(int) });
        harmony.Patch(show, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(BeginPathOfValheimanAnvilShow)),
            finalizer: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(EndPathOfValheimanAnvilShow)));
        harmony.Patch(refresh, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(BeforePathOfValheimanAnvilRefresh)),
            postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(AfterPathOfValheimanAnvilRefresh)));
        _povIsAnvil = isAnvil;
        _povIsAnvilCache = isCache;
        _povViewingAnvil = viewing;
    }

    private static void BeginPathOfValheimanAnvilShow(InventoryGui __instance, Container __0, out PovAnvilShowState __state)
    {
        __state = new PovAnvilShowState(_povAnvilOpeningGui, _povAnvilGui);
        // Show builds recipes before assigning m_currentContainer. This scope only
        // routes our UI; native Show and all access/ownership checks still run.
        _povAnvilOpeningGui = null;
        if (IsDedicatedServer || __0 == null || _povIsAnvil == null) return;
        try { if (_povIsAnvil(__0) && !_povIsAnvilCache!(__0)) _povAnvilOpeningGui = __instance; }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); }
    }

    private static Exception? EndPathOfValheimanAnvilShow(InventoryGui __instance, PovAnvilShowState __state, Exception? __exception)
    {
        _povAnvilOpeningGui = __state.Opening;
        if (__exception != null && _povAnvilGui == __instance) _povAnvilGui = __state.Owner;
        return __exception;
    }

    private static bool IsViewingPathOfValheimanAnvil(InventoryGui gui)
    {
        if (IsDedicatedServer || gui == null || gui != InventoryGui.instance || _povViewingAnvil == null) return false;
        try { return _povViewingAnvil(gui.m_currentContainer); }
        catch (Exception error) { WarnPathOfValheimanCrafting(error); return false; }
    }

    private static bool IsPathOfValheimanAnvilActive(InventoryGui? gui) =>
        gui != null && (_povAnvilOpeningGui == gui || _povAnvilGui == gui || IsViewingPathOfValheimanAnvil(gui));

    private static bool TryYieldCraftingToPathOfValheimanAnvil(InventoryGui gui)
    {
        if (!IsPathOfValheimanAnvilActive(gui)) return false;
        ClosePathOfValheimanCharacterUi(); // Restores alpha/raycasts, retains CharacterSelected and Tab.
        if (_povAnvilGui != gui)
        {
            _povAnvilGui = gui;
            // Restore geometry BEFORE native Refresh builds its controls and takes
            // visibility snapshots. The native window owns its own Restore/Hide.
            HideCraftingPanelRedesign();
            RestorePathOfValheimanRadiusButton(gui);
        }
        HideOwnedCraftingRedesignUi();
        HidePathOfValheimanReturns();
        HideCraftingRedesignBottomControls(gui);
        return true;
    }

    private static void BeforePathOfValheimanAnvilRefresh(InventoryGui __0)
    {
        if (IsViewingPathOfValheimanAnvil(__0)) TryYieldCraftingToPathOfValheimanAnvil(__0);
    }

    private static void AfterPathOfValheimanAnvilRefresh(InventoryGui __0)
    {
        if (_povAnvilGui != __0 || IsViewingPathOfValheimanAnvil(__0)) return;
        // Native Refresh has now restored its snapshots. Rebuild only after that
        // handoff, so it cannot overwrite the ordinary crafting view's visibility.
        _povAnvilGui = null;
        if (InventoryGui.IsVisible() && !IsInventoryPanelClosing(__0))
            UpdateCraftingPanelRedesign(__0, CraftingPanelUpdateReason.StateChanged);
    }

    internal static void ClearPathOfValheimanAnvilUi(InventoryGui? gui = null)
    {
        if (gui == null || _povAnvilGui == gui) _povAnvilGui = null;
        if (gui == null || _povAnvilOpeningGui == gui) _povAnvilOpeningGui = null;
    }
}
