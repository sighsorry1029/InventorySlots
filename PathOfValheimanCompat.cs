using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string PathOfValheimanGuid = "treextr.pathofvalheiman";
    private static readonly List<Harmony> PathOfValheimanPatches = new();
    private static readonly ConditionalWeakTable<Player, PathOfValheimanRows> PathOfValheimanRowCache = new();
    private static bool _pathOfValheimanRowsEnabled;
    private static Func<Player, object?>? _pathOfValheimanProgress;
    private static Func<object, int>? _pathOfValheimanReadRows;
    private static Func<IEnumerable<string>>? _pathOfValheimanMetadataKeys;
    private static bool _pathOfValheimanMetadataRegistered;
    private static bool _pathOfValheimanRowsWarning;
    private static Func<bool>? _pathOfValheimanTreeOpen;
    private static Func<bool>? _pathOfValheimanTooltipWanted;
    private static ConditionalWeakTable<UITooltip, ItemData>? _pathOfValheimanTooltipOwners;
    private static Func<GameObject>? _pathOfValheimanTooltipBody;
    private static Func<UITooltip>? _pathOfValheimanCurrentTooltip;
    private static Func<GameObject>? _pathOfValheimanTooltipRoot;
    private static Action? _pathOfValheimanRefreshTooltip;
    private static UITooltip? _pathOfValheimanTooltipSource;
    private static GameObject? _pathOfValheimanNativeTooltip;
    private static ItemData? _pathOfValheimanTooltipItem;
    private static bool _pathOfValheimanTooltipWarning;
    private static bool _pathOfValheimanTooltipWasDisabled;

    private static void InitializePathOfValheimanCompatibility()
    {
        if (!Chainloader.PluginInfos.TryGetValue(PathOfValheimanGuid, out var plugin) || plugin.Instance == null)
            return;

        Assembly assembly = plugin.Instance.GetType().Assembly;
        // Each capability is validated as a unit. No hard dependency or version-number gate.
        InstallPathOfValheimanCapability("metadata", harmony =>
        {
            Type type = assembly.GetType("PassiveTree.InventorySlotsCompat", throwOnError: true)!;
            MethodInfo run = RequirePathOfValheimanMethod(type, "Run", typeof(void));
            MethodInfo keys = RequirePathOfValheimanMethod(type, "OurKeys", typeof(IEnumerable<string>));
            _pathOfValheimanMetadataKeys = (Func<IEnumerable<string>>)Delegate.CreateDelegate(typeof(Func<IEnumerable<string>>), keys);
            harmony.Patch(run, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(RegisterPathOfValheimanMetadata)));
        });

        if (IsDedicatedServer) return;

        InitializePathOfValheimanCrafting(assembly);

        InstallPathOfValheimanCapability("rows", harmony =>
        {
            Type trade = assembly.GetType("PassiveTree.TradeMechanics", true)!;
            Type progress = assembly.GetType("PassiveTree.Progress", true)!;
            Type stats = assembly.GetType("PassiveTree.StatSet", true)!;
            MethodInfo tick = RequirePathOfValheimanMethod(trade, "TickRows", typeof(void), typeof(Player));
            MethodInfo peek = RequirePathOfValheimanMethod(progress, "Peek", progress, typeof(Player));
            MethodInfo rows = RequirePathOfValheimanMethod(trade, "Rows", typeof(int), stats);
            FieldInfo statsField = AccessTools.Field(progress, "Stats");
            if (statsField == null || statsField.IsStatic || statsField.FieldType != stats)
                throw new MissingFieldException(progress.FullName, "Stats");
            MethodInfo panel = RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.InventoryRowsPanel+Gui_Update", true)!,
                "Postfix", typeof(void), typeof(InventoryGui));
            _pathOfValheimanProgress = (Func<Player, object?>)Delegate.CreateDelegate(typeof(Func<Player, object?>), peek);
            ParameterExpression value = Expression.Parameter(typeof(object));
            _pathOfValheimanReadRows = Expression.Lambda<Func<object, int>>(
                Expression.Call(rows, Expression.Field(Expression.Convert(value, progress), statsField)), value).Compile();
            harmony.Patch(tick, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(UpdatePathOfValheimanRows)));
            harmony.Patch(panel, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(SkipPathOfValheimanRowsPanel)));
            _pathOfValheimanRowsEnabled = true;
        });

        InstallPathOfValheimanCapability("tidy-pack", harmony =>
        {
            Type type = assembly.GetType("PassiveTree.QuickStack", true)!;
            MethodInfo sort = RequirePathOfValheimanMethod(type, "Sort", typeof(void), typeof(Player));
            MethodInfo movable = RequirePathOfValheimanMethod(type, "Movable", typeof(List<ItemData>), typeof(Player));
            harmony.Patch(sort, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(SortPathOfValheimanInventory)));
            harmony.Patch(movable, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(FilterPathOfValheimanMovableItems)));
        });

        InstallPathOfValheimanCapability("tooltip", _ =>
        {
            Type type = assembly.GetType("PassiveTree.UI.ItemTooltip", true)!;
            var wanted = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), RequirePathOfValheimanMethod(type, "get_Wanted", typeof(bool)));
            var refresh = (Action)Delegate.CreateDelegate(typeof(Action), RequirePathOfValheimanMethod(type, "RefreshCurrent", typeof(void)));
            var body = PathOfValheimanStaticField<GameObject>(type, "sheetGo");
            var current = PathOfValheimanStaticField<UITooltip>(typeof(UITooltip), "m_current");
            var root = PathOfValheimanStaticField<GameObject>(typeof(UITooltip), "m_tooltip");
            var owners = PathOfValheimanStaticField<ConditionalWeakTable<UITooltip, ItemData>>(type, "owners")();
            if (owners == null) throw new MissingFieldException(type.FullName, "owners");
            _pathOfValheimanTooltipOwners = owners;
            _pathOfValheimanCurrentTooltip = current;
            _pathOfValheimanTooltipRoot = root;
            _pathOfValheimanTooltipBody = body;
            _pathOfValheimanRefreshTooltip = refresh;
            _pathOfValheimanTooltipWanted = wanted;
        });

        InstallPathOfValheimanCapability("tree-input", _ =>
        {
            _pathOfValheimanTreeOpen = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),
                RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.UI.TreeWindow", true)!, "get_IsOpen", typeof(bool)));
        });
    }

    private static MethodInfo RequirePathOfValheimanMethod(Type type, string name, Type result, params Type[] args)
    {
        MethodInfo method = AccessTools.DeclaredMethod(type, name, args);
        if (method == null || !method.IsStatic || method.ReturnType != result || method.ContainsGenericParameters)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }

    private static Func<T> PathOfValheimanStaticField<T>(Type type, string name)
    {
        FieldInfo field = AccessTools.DeclaredField(type, name);
        if (field == null || !field.IsStatic || field.FieldType != typeof(T))
            throw new MissingFieldException(type.FullName, name);
        return Expression.Lambda<Func<T>>(Expression.Field(null, field)).Compile();
    }

    private static void InstallPathOfValheimanCapability(string name, Action<Harmony> install)
    {
        var harmony = new Harmony(ModGUID + ".pov." + name);
        try
        {
            install(harmony);
            PathOfValheimanPatches.Add(harmony);
            Log.LogInfo($"Path of Valheiman compatibility enabled: {name}.");
        }
        catch (Exception error)
        {
            harmony.UnpatchSelf();
            Log.LogWarning($"Path of Valheiman compatibility unavailable ({name}): {error.Message}");
        }
    }

    private static bool RegisterPathOfValheimanMetadata()
    {
        if (!_pathOfValheimanMetadataRegistered)
        {
            // Keep PoV's declared-key discovery, but never discard differences in item potency,
            // duration or provenance. Other mods' registrations retain first-wins semantics.
            foreach (string key in _pathOfValheimanMetadataKeys!())
            {
                if (key != null && key.StartsWith("pt.", StringComparison.Ordinal))
                    StackMetadataPolicy.Register(key, static (destination, _) => destination,
                        static (destination, source) => string.Equals(destination, source, StringComparison.Ordinal));
            }
            _pathOfValheimanMetadataRegistered = true;
        }
        return false;
    }

    private static int GetPathOfValheimanRegularRows(Player player, int baseRows)
    {
        if (!_pathOfValheimanRowsEnabled) return baseRows;
        PathOfValheimanRows state = PathOfValheimanRowCache.GetValue(player, static _ => new PathOfValheimanRows());
        if (!state.Loaded)
        {
            player.m_customData.TryGetValue(PathOfValheimanRows.SaveKey, out string stored);
            state.Load(stored);
        }
        return state.TotalRows(baseRows, GetFixedRegularRows());
    }

    private static bool UpdatePathOfValheimanRows(Player __0)
    {
        // Never let PoV use the physical height: it includes our equipment/quick-slot tail.
        Player player = __0;
        if (IsUnityNull(player) || player != Player.m_localPlayer || ContainerAreaPlayerLoading(player)) return false;
        try
        {
            object? progress = _pathOfValheimanProgress!(player); // Peek does not construct/load Progress.
            if (progress == null) return false;
            int previous = GetUsableRegularRows(player);
            PathOfValheimanRows state = PathOfValheimanRowCache.GetValue(player, static _ => new PathOfValheimanRows());
            int desired = PathOfValheimanRows.ClampBonus(_pathOfValheimanReadRows!(progress));
            int baseRows = InventorySlotSafetyCore.GetCombinedRegularRows(GetNativeInventoryRows(player), CalculateUnlockedRows(player));
            int highestOccupiedRow = -1;
            foreach (ItemData item in player.GetInventory().GetAllItems())
            {
                int row = item.m_gridPos.y;
                if (row >= baseRows && row < previous) highestOccupiedRow = Math.Max(highestOccupiedRow, row);
            }
            state.Update(desired, baseRows, highestOccupiedRow);
            if (desired > 0) player.m_customData[PathOfValheimanRows.SaveKey] = desired.ToString(System.Globalization.CultureInfo.InvariantCulture);
            else player.m_customData.Remove(PathOfValheimanRows.SaveKey);
            if (previous != GetUsableRegularRows(player))
            {
                InvalidateInventoryPlacementCaches();
                RevealRegularRowsAfterKnownItem(player, previous);
                RequestInventoryStateEnsure(player, InventoryStateEnsureReason.InventoryChanged, InventoryStateAuditLevel.HeightOnly);
            }
        }
        catch (Exception error)
        {
            if (!_pathOfValheimanRowsWarning)
            {
                _pathOfValheimanRowsWarning = true;
                Log.LogWarning($"Path of Valheiman row refresh failed; retaining current rows: {error.Message}");
            }
        }
        return false;
    }

    private static bool SkipPathOfValheimanRowsPanel() => false;

    private static bool SortPathOfValheimanInventory(Player __0)
    {
        if (__0 != null && __0 == Player.m_localPlayer && !ContainerAreaPlayerLoading(__0) && InventoryGui.instance != null)
            SortPlayerInventory(__0, preserveEquipped: true);
        return false;
    }

    private static void FilterPathOfValheimanMovableItems(Player __0, List<ItemData> __result)
    {
        if (__result == null || __0 == null || __0 != Player.m_localPlayer) return;
        Inventory inventory = __0.GetInventory();
        for (int i = __result.Count - 1; i >= 0; i--)
        {
            ItemData item = __result[i];
            if (item == null || item.m_equipped || !inventory.ContainsItem(item) ||
                GetInventoryCellKind(__0, inventory, item.m_gridPos) != InventoryCellKind.RegularUnlocked ||
                IsFavoriteProtected(__0, inventory, item))
                __result.RemoveAt(i);
        }
    }

    private static bool TryYieldToPathOfValheimanTooltip(UITooltip tooltip)
    {
        if (_pathOfValheimanTooltipWanted == null) return false;
        try
        {
            if (!_pathOfValheimanTooltipWanted())
            {
                ClearPathOfValheimanTooltipState();
                _pathOfValheimanTooltipWasDisabled = true;
                return false;
            }
            if (!IsInventoryContainerTooltipSource(tooltip) || _pathOfValheimanCurrentTooltip!() != tooltip || _pathOfValheimanTooltipRoot!() == null ||
                !_pathOfValheimanTooltipOwners!.TryGetValue(tooltip, out ItemData item) || item?.m_shared == null)
            {
                ClearPathOfValheimanTooltipState();
                return false;
            }
            if (_pathOfValheimanTooltipSource != tooltip || _pathOfValheimanNativeTooltip != _pathOfValheimanTooltipRoot!() ||
                !ReferenceEquals(_pathOfValheimanTooltipItem, item))
            {
                _pathOfValheimanTooltipSource = tooltip;
                _pathOfValheimanNativeTooltip = _pathOfValheimanTooltipRoot!();
                _pathOfValheimanTooltipItem = item;
                // Do not rebuild an existing PoV card: its Alt-pinned links own event handlers.
                // Refresh once for a live Off->On switch or a card not yet produced by PoV.
                if (_pathOfValheimanTooltipWasDisabled || !HasPathOfValheimanTooltipBody())
                    _pathOfValheimanRefreshTooltip!();
                _pathOfValheimanTooltipWasDisabled = false;
            }
            if (!HasPathOfValheimanTooltipBody()) return false;
            _inventoryContainerHoverTooltipSource = null;
            HideInventoryContainerCustomTooltip();
            ForceHideInventorySimpleNameTooltip();
            RestoreVanillaTooltipVisual(_pathOfValheimanTooltipRoot!());
            return true;
        }
        catch (Exception error)
        {
            if (!_pathOfValheimanTooltipWarning)
            {
                _pathOfValheimanTooltipWarning = true;
                Log.LogWarning($"Path of Valheiman tooltip unavailable; using InventorySlots text: {error.Message}");
            }
            return false;
        }
    }

    private static bool HasPathOfValheimanTooltipBody()
    {
        GameObject body = _pathOfValheimanTooltipBody!();
        // The native root is inactive during its hover delay; don't flash our plain tooltip first.
        return body != null && body.activeSelf && body.transform.IsChildOf(_pathOfValheimanTooltipRoot!().transform);
    }

    private static bool IsPathOfValheimanTreeOpen()
    {
        try { return _pathOfValheimanTreeOpen?.Invoke() == true; }
        catch (Exception error)
        {
            _pathOfValheimanTreeOpen = null;
            Log.LogWarning($"Path of Valheiman tree input check unavailable: {error.Message}");
            return false;
        }
    }

    private static void ClearPathOfValheimanTooltipState()
    {
        _pathOfValheimanTooltipSource = null;
        _pathOfValheimanNativeTooltip = null;
        _pathOfValheimanTooltipItem = null;
        _pathOfValheimanTooltipWasDisabled = false;
    }

    private static void ShutdownPathOfValheimanCompatibility()
    {
        foreach (Harmony harmony in PathOfValheimanPatches) harmony.UnpatchSelf();
        PathOfValheimanPatches.Clear();
        ShutdownPathOfValheimanCrafting();
        _pathOfValheimanRowsEnabled = false;
        _pathOfValheimanProgress = null;
        _pathOfValheimanReadRows = null;
        _pathOfValheimanTreeOpen = null;
        _pathOfValheimanTooltipWanted = null;
        _pathOfValheimanTooltipOwners = null;
        _pathOfValheimanTooltipBody = null;
        _pathOfValheimanCurrentTooltip = null;
        _pathOfValheimanTooltipRoot = null;
        _pathOfValheimanRefreshTooltip = null;
        ClearPathOfValheimanTooltipState();
    }
}
