using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
{
    private const string PathOfValheimanGuid = "treextr.pathofvalheiman";
    private static bool _povPresent;
    private static bool _povUnavailable;
    private static bool _povMucWarning;
    private static bool _povStorageWarning;
    private static Func<Inventory, bool>? _povOwnsInventory;
    private static Func<Player, int>? _povBackpackRows;
    private static Func<Player, Vector2i, bool>? _povIsCellPosition;
    private static Func<bool>? _povEquipmentEnabled;
    private static Func<string?>? _povStorageProvider;
    private static Func<bool>? _povStorageSuspended;
    private static int _povQuickStackDepth;
    private delegate void PovStackResponseStart(Container container, bool granted, out bool state);
    private static PovStackResponseStart? _povStackResponseStart;
    private static Action<bool>? _povStackResponseFinish;
    private static ConditionalWeakTable<Container, object> _povCompletedStackResponses = new();

    private void InitializePathOfValheimanCompatibility()
    {
        if (IsDedicatedServer || !Chainloader.PluginInfos.TryGetValue(PathOfValheimanGuid, out var plugin)) return;
        _povPresent = true;
        if (plugin.Instance != null)
        {
            try { InventoryCompatibility.PathOfValheimanTrashMode.Install(plugin.Instance.GetType().Assembly, ModGUID); }
            catch (Exception error) { Log.LogWarning("PoV Delete button compatibility unavailable: " + error.GetBaseException().Message); }
        }
        try
        {
            if (plugin.Instance == null) throw new InvalidOperationException("PoV plugin has not initialized");
            BindPathOfValheimanCompatibility(plugin.Instance.GetType().Assembly, _harmony);
            Log.LogInfo("Path of Valheiman inventory boundaries and favorite protection enabled.");
        }
        catch (Exception error) { DisablePathOfValheimanActions(error); }
    }

    // Bind public API queries once without a compile-time PoV dependency or version pin.
    // Native ownership and action guards still need internal hooks: API 1.0.0 does
    // not expose storage readiness, action filters, or the native RPC lifecycle.
    private static void BindPathOfValheimanCompatibility(Assembly assembly, Harmony harmony)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        MethodInfo Require(string type, string name, Type result, params Type[] parameters)
        {
            MethodInfo? method = assembly.GetType(type)?.GetMethod(name, flags, null, parameters, null);
            if (method == null || method.ReturnType != result || method.ContainsGenericParameters)
                throw new MissingMethodException(type, name);
            return method;
        }
        MethodInfo RequireApi(string type, string name, Type result, params Type[] parameters)
        {
            MethodInfo method = Require("Valheiman.Api." + type, name, result, parameters);
            if (!method.IsPublic || method.DeclaringType?.IsVisible != true)
                throw new MissingMethodException("Valheiman.Api." + type, name);
            return method;
        }

        var atLeast = (Func<int, int, bool>)Delegate.CreateDelegate(typeof(Func<int, int, bool>),
            RequireApi("Pov", "AtLeast", typeof(bool), typeof(int), typeof(int)));
        var has = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>),
            RequireApi("Pov", "Has", typeof(bool), typeof(string)));
        // AtLeast requires the same API major, allowing additive 1.x updates.
        if (!atLeast(1, 0) || !has("equipment.cells") || !has("storage"))
            throw new InvalidOperationException("PoV API 1.x with equipment.cells and storage is required");

        MethodInfo owns = Require("PassiveTree.Gear.EquipRows", "Owns", typeof(bool), typeof(Inventory));
        MethodInfo backpack = RequireApi("Equipment", "BackpackRows", typeof(int), typeof(Player));
        MethodInfo isCell = RequireApi("Equipment", "IsCellPosition", typeof(bool), typeof(Player), typeof(Vector2i));
        MethodInfo enabled = RequireApi("Equipment", "get_On", typeof(bool));
        MethodInfo provider = RequireApi("Storage", "get_ProviderId", typeof(string));
        MethodInfo suspended = RequireApi("Storage", "get_Suspended", typeof(bool));
        MethodInfo movable = Require("PassiveTree.QuickStack", "Movable", typeof(List<ItemDrop.ItemData>), typeof(Player));
        MethodInfo cell = Require("PassiveTree.QuickStack", "ForeignCell", typeof(bool), typeof(Vector2i));
        MethodInfo sort = Require("PassiveTree.QuickStack", "Sort", typeof(void), typeof(Player));
        MethodInfo run = Require("PassiveTree.QuickStack", "Run", typeof(void), typeof(Player));
        MethodInfo responseStart = Require("PassiveTree.QuickStack+Response", "Prefix", typeof(void), typeof(Container), typeof(bool), typeof(bool).MakeByRefType());
        MethodInfo responseFinish = Require("PassiveTree.QuickStack+Response", "Finalizer", typeof(void), typeof(bool));
        MethodInfo response = AccessTools.DeclaredMethod(typeof(Container), "RPC_StackResponse", new[] { typeof(long), typeof(bool) });
        Patches? patches = Harmony.GetPatchInfo(response);
        if (patches == null || !HasPovPatch(patches.Prefixes, responseStart) || !HasPovPatch(patches.Finalizers, responseFinish))
            throw new InvalidOperationException("PoV native stack-response hooks do not match the supported contract");
        _povOwnsInventory = (Func<Inventory, bool>)Delegate.CreateDelegate(typeof(Func<Inventory, bool>), owns);
        _povBackpackRows = (Func<Player, int>)Delegate.CreateDelegate(typeof(Func<Player, int>), backpack);
        _povIsCellPosition = (Func<Player, Vector2i, bool>)Delegate.CreateDelegate(typeof(Func<Player, Vector2i, bool>), isCell);
        _povEquipmentEnabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), enabled);
        _povStorageProvider = (Func<string?>)Delegate.CreateDelegate(typeof(Func<string?>), provider);
        _povStorageSuspended = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), suspended);
        _povStackResponseStart = (PovStackResponseStart)Delegate.CreateDelegate(typeof(PovStackResponseStart), responseStart);
        _povStackResponseFinish = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), responseFinish);
        _povPresent = true;
        // Retain any installed guards if a later hook fails. InventoryActions' own
        // actions also fail closed independently of these optional PoV hooks.
        harmony.Patch(run, prefix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovQuickStackStarting)),
            finalizer: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovQuickStackFinished)));
        // Patch the small helper before generating Sort's wrapper to prevent Mono
        // from inlining its old, unprotected body into that wrapper.
        harmony.Patch(cell, postfix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovProtectSortCell)));
        harmony.Patch(movable, postfix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovFilterMovableItems)));
        harmony.Patch(sort, prefix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovSortStarting)));
        // HarmonyX still runs later prefixes when an earlier prefix rejects an RPC.
        // PoV must not consume its pending request on such a response. Replace only
        // its two observers, preserving their actual bodies and paired state.
        harmony.Unpatch(response, responseStart);
        harmony.Unpatch(response, responseFinish);
        harmony.Patch(response,
            prefix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovStackResponseStarting)) { priority = Priority.Last },
            finalizer: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovStackResponseFinished)));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(Container), nameof(Container.StackAll)),
            // -1 means "unspecified" to Harmony; -2 really runs after Last (0).
            prefix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovNativeStackStarting)) { priority = Priority.Last - 2 });
        harmony.Patch(AccessTools.DeclaredMethod(typeof(ZNet), "StopAll", new[] { typeof(bool) }),
            postfix: new HarmonyMethod(typeof(InventoryActionsPlugin), nameof(PovNetworkStopped)));
    }

    private static bool HasPovPatch(IEnumerable<Patch> patches, MethodInfo method)
    {
        foreach (Patch patch in patches)
            if (patch.owner == PathOfValheimanGuid && patch.PatchMethod == method && patch.priority == Priority.Normal) return true;
        return false;
    }

    private static bool PovStackResponseStarting(Container __instance, bool granted, bool __runOriginal, out bool __state)
    {
        __state = false;
        if (!__runOriginal) return true;
        // After consuming a PoV response, an unsolicited duplicate must not reach
        // vanilla StackAll without PoV's item filter. A new permitted native request
        // reopens the chest; the underlying RPC has no request ID to compare.
        if (_povCompletedStackResponses.TryGetValue(__instance, out _)) return false;
        _povStackResponseStart!(__instance, granted, out __state);
        if (__state) _povCompletedStackResponses.GetValue(__instance, _ => new object());
        return true;
    }

    private static void PovStackResponseFinished(bool __state) => _povStackResponseFinish!(__state);

    private static void PovNativeStackStarting(Container __instance, bool __runOriginal)
    {
        // Run after the shared request fence. Rejected attempts must not reopen a
        // completed response, and local RPC dispatch can happen inside StackAll.
        if (__runOriginal) _povCompletedStackResponses.Remove(__instance);
    }

    private static void PovNetworkStopped() => _povCompletedStackResponses = new();

    private static void DisablePathOfValheimanActions(Exception error)
    {
        if (!_povUnavailable)
            Log.LogWarning($"Path of Valheiman compatibility unavailable; player inventory actions are blocked to protect equipment/storage slots: {error.Message}");
        _povUnavailable = true;
    }

    // Keep Player static initialization out of ordinary inventory-only calls when
    // PoV is absent (including early startup and standalone compatibility checks).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetPathOfValheimanRows(Inventory? inventory, out int rows)
    {
        rows = 0;
        if (!_povPresent || inventory == null) return false;
        Player? player = Player.m_localPlayer;
        if (!IsPlayerInventory(player, inventory)) return false;
        if (_povUnavailable || player!.m_isLoading) return true;
        try
        {
            // ProviderId includes a registered provider before activation. Neither
            // it nor IsCellPosition identifies empty reserved external cells, so
            // do not infer readiness/capacity from a non-null ID or physical height.
            if (_povStorageSuspended!() || _povStorageProvider!() != null)
            {
                if (!_povStorageWarning)
                    Log.LogWarning("PoV external storage is registered or suspended; player inventory actions are blocked because its empty reserved cells cannot be verified.");
                _povStorageWarning = true;
                return true;
            }
            // Owns remains true during PoV's delayed release after Equipment system
            // is turned off. Its live boundary, not the option alone, owns those cells.
            if (_povOwnsInventory!(inventory))
            {
                rows = _povBackpackRows!(player);
                if (rows < 1 || rows >= inventory.GetHeight())
                    throw new InvalidOperationException("Invalid visible inventory boundary");
                return true;
            }
            if (!_povEquipmentEnabled!()) return false;
        }
        catch (Exception error) { DisablePathOfValheimanActions(error); }
        // Unknown API or enabled equipment awaiting Attach: no automatic destinations.
        // Chests keep their full capacity even if the optional integration fails.
        rows = 0;
        return true;
    }

    private static bool IsPathOfValheimanProtectedCell(Inventory inventory, Vector2i cell)
    {
        if (!_povPresent || !TryGetPathOfValheimanRows(inventory, out int rows)) return false;
        if (cell.y < 0 || cell.y >= rows || inventory.GetItemAt(cell.x, cell.y)?.m_equipped == true) return true;
        try { return _povIsCellPosition!(Player.m_localPlayer, cell); }
        catch (Exception error) { DisablePathOfValheimanActions(error); return true; }
    }

    private static bool PovSortStarting(Player __0)
    {
        return !_povUnavailable && __0 != null && __0 == Player.m_localPlayer && !__0.m_isLoading &&
               (!TryGetPathOfValheimanRows(GetPlayerInventory(__0), out int rows) || rows > 0);
    }

    private static void PovFilterMovableItems(Player __0, List<ItemDrop.ItemData> __result)
    {
        Inventory? inventory = GetPlayerInventory(__0);
        if (!PovSortStarting(__0) || inventory == null)
        {
            __result.Clear();
            return;
        }
        // Retain PoV's native sort order, node requirements, metadata and stacking policy.
        __result.RemoveAll(item => !IsRegularActionItem(inventory, item) ||
            IsPathOfValheimanProtectedCell(inventory, item.m_gridPos) || IsFavoriteProtected(__0, inventory, item));
    }

    private static void PovProtectSortCell(Vector2i __0, ref bool __result)
    {
        if (__result) return;
        Player? player = Player.m_localPlayer;
        Inventory? inventory = GetPlayerInventory(player);
        __result = _povUnavailable || inventory == null || !IsRegularPlayerCell(inventory, __0) ||
                   IsPathOfValheimanProtectedCell(inventory, __0) || IsFavoriteSlot(player!, inventory, __0);
    }

    private static bool PovQuickStackStarting(Player __0, out bool __state)
    {
        __state = false;
        if (!PovSortStarting(__0) || _areaContainerTransfer != null || _mucTransfer != null || _mucTransferPending != null) return false;
        if (HasExternalMultiUserChestActive)
        {
            // PoV's native owner/StackAll protocol is not MUC's transaction protocol.
            // The existing InventoryActions chest shortcut remains available with MUC.
            if (!_povMucWarning)
                Log.LogWarning("PoV nearby quick stack is disabled with MultiUserChest; use InventoryActions' chest quick-stack shortcut instead.");
            _povMucWarning = true;
            return false;
        }
        ++_povQuickStackDepth;
        __state = true;
        return true;
    }

    private static void PovQuickStackFinished(bool __state)
    {
        if (__state) --_povQuickStackDepth;
    }
}
