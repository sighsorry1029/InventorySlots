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
    private static bool _povStorageWarning;
    private static Func<Inventory, bool>? _povOwnsInventory;
    private static Func<Player, int>? _povBackpackRows;
    private static Func<Player, Vector2i, bool>? _povIsCellPosition;
    private static Func<bool>? _povEquipmentEnabled;
    private static Func<string?>? _povStorageProvider;
    private static Func<bool>? _povStorageSuspended;
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
    // Native ownership still needs a checked internal query; action execution and
    // response ownership are isolated by the shared PoV action adapter.
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
        _povOwnsInventory = (Func<Inventory, bool>)Delegate.CreateDelegate(typeof(Func<Inventory, bool>), owns);
        _povBackpackRows = (Func<Player, int>)Delegate.CreateDelegate(typeof(Func<Player, int>), backpack);
        _povIsCellPosition = (Func<Player, Vector2i, bool>)Delegate.CreateDelegate(typeof(Func<Player, Vector2i, bool>), isCell);
        _povEquipmentEnabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), enabled);
        _povStorageProvider = (Func<string?>)Delegate.CreateDelegate(typeof(Func<string?>), provider);
        _povStorageSuspended = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), suspended);
        _povPresent = true;
        BindPathOfValheimanStackMetadata(assembly);
        try
        {
            string? warning = PathOfValheimanStackCompatibility.Install(assembly, ModGUID);
            if (warning != null) Log.LogWarning("PoV metadata comparison changed; native restrictions remain unchanged: " + warning);
        }
        catch (Exception error)
        {
            Log.LogWarning("PoV stack metadata bridge unavailable; native metadata checks remain unchanged: " + error.GetBaseException().Message);
        }
        InventoryCompatibility.PathOfValheimanActions.Install(assembly, ModGUID);
    }

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

}
