using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{

    private static readonly List<Harmony> PathOfValheimanPatches = new();
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
    private static bool _povMultiUserChestWarning;
    private static MethodInfo? _povToolHudHideMethod;
    private static MethodInfo? _povCellHudHideMethod;
    private static Action? _povToolHudHide;
    private static Action? _povCellHudHide;
    private static Action<Player>? _povCellHudTick;
    private static Func<string>? _povEquipmentHost;

    private static void InitializePathOfValheimanNativeHud(Assembly assembly)
    {
        InstallPathOfValheimanCapability("native-hud", harmony =>
        {
            Type tools = assembly.GetType("PassiveTree.Gear.ToolBar", true)!;
            Type cells = assembly.GetType("PassiveTree.Gear.HudCells", true)!;
            _povToolHudHideMethod = RequirePathOfValheimanMethod(tools, "Hide", typeof(void));
            _povCellHudHideMethod = RequirePathOfValheimanMethod(cells, "Hide", typeof(void));
            _povToolHudHide = (Action)Delegate.CreateDelegate(typeof(Action), _povToolHudHideMethod);
            _povCellHudHide = (Action)Delegate.CreateDelegate(typeof(Action), _povCellHudHideMethod);
            _povCellHudTick = (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), RequirePathOfValheimanMethod(cells, "Tick", typeof(void), typeof(Player)));
            _povEquipmentHost = _pov!.Bind<Func<string>>("Valheiman.Api.Equipment", "get_HostedBy");
            MethodInfo target = AccessTools.DeclaredMethod(assembly.GetType("PassiveTree.Gear.Module", true), "LateUpdate");
            if (target == null || target.IsStatic || target.ReturnType != typeof(void) || target.GetParameters().Length != 0)
                throw new MissingMethodException("PoV equipment LateUpdate");
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(KeepPathOfValheimanNativeHud)));
        });
    }

    private static IEnumerable<CodeInstruction> KeepPathOfValheimanNativeHud(IEnumerable<CodeInstruction> source)
    {
        var instructions = source.ToList();
        if (instructions.Count(i => i.Calls(_povToolHudHideMethod)) != 2 || instructions.Count(i => i.Calls(_povCellHudHideMethod)) != 2)
            throw new InvalidOperationException("PoV HUD ownership branches changed");
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(_povToolHudHideMethod)) instruction.operand = AccessTools.Method(typeof(InventorySlotsPlugin), nameof(UpdatePathOfValheimanToolHud));
            else if (instruction.Calls(_povCellHudHideMethod)) instruction.operand = AccessTools.Method(typeof(InventorySlotsPlugin), nameof(UpdatePathOfValheimanCellHud));
            yield return instruction;
        }
    }

    private static bool KeepPathOfValheimanHud() => _pov?.Enabled() == true && _povEquipmentHost?.Invoke() == ModGUID;
    private static void UpdatePathOfValheimanToolHud()
    {
        // Tools use ordinary InventorySlots quick slots; PoV has no tool bank.
        _povToolHudHide!();
    }
    private static void UpdatePathOfValheimanCellHud()
    {
        if (KeepPathOfValheimanHud()) _povCellHudTick!(Player.m_localPlayer);
        else _povCellHudHide!();
    }
    private static void ClearPathOfValheimanNativeHud()
    {
        _povToolHudHideMethod = _povCellHudHideMethod = null;
        _povToolHudHide = _povCellHudHide = null;
        _povCellHudTick = null;
        _povEquipmentHost = null;
    }

    private static void InitializePathOfValheimanCompatibility()
    {
        if (_pov == null) return;
        Assembly assembly = _pov.Assembly;
        InstallPathOfValheimanCapability("delete-ui", _ => InventoryCompatibility.PathOfValheimanTrashMode.Install(assembly, ModGUID));
        InstallPathOfValheimanCapability("item-presentation", _ => BindPathOfValheimanItemPresentation(assembly));
        InitializePathOfValheimanNativeHud(assembly);
        InitializePathOfValheimanCrafting(assembly);
        InstallPathOfValheimanCapability("tidy-pack", harmony =>
        {
            Type type = assembly.GetType("PassiveTree.QuickStack", true)!;
            MethodInfo sort = RequirePathOfValheimanMethod(type, "Sort", typeof(void), typeof(Player));
            MethodInfo movable = RequirePathOfValheimanMethod(type, "Movable", typeof(List<ItemData>), typeof(Player));
            MethodInfo run = RequirePathOfValheimanMethod(type, "Run", typeof(void), typeof(Player));
            harmony.Patch(sort, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(SortPathOfValheimanInventory)));
            harmony.Patch(movable, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(FilterPathOfValheimanMovableItems)));
            harmony.Patch(run, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(AllowPathOfValheimanNearbyQuickStack)));
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

    private static bool SortPathOfValheimanInventory(Player __0)
    {
        if (__0 != null && __0 == Player.m_localPlayer && !ContainerAreaPlayerLoading(__0) && InventoryGui.instance != null)
            SortPlayerInventory(__0, preserveEquipped: true);
        return false;
    }

    private static bool AllowPathOfValheimanNearbyQuickStack()
    {
        if (!HasExternalMultiUserChestActive) return true;
        if (!_povMultiUserChestWarning)
        {
            _povMultiUserChestWarning = true;
            Log.LogWarning("PoV nearby quick stack is disabled with MultiUserChest because it uses a different transfer protocol.");
        }
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
        InventoryCompatibility.PathOfValheimanTrashMode.Dispose();
        foreach (Harmony harmony in PathOfValheimanPatches) harmony.UnpatchSelf();
        PathOfValheimanPatches.Clear();
        ShutdownPathOfValheimanCrafting();
        ClearPathOfValheimanItemPresentation();
        ClearPathOfValheimanNativeHud();
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
