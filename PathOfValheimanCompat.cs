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
    private static Func<UITooltip, ItemData?>? _pathOfValheimanTooltipOwner;
    private static Func<GameObject>? _pathOfValheimanTooltipBody;
    private static Func<UITooltip>? _pathOfValheimanCurrentTooltip;
    private static Func<GameObject>? _pathOfValheimanTooltipRoot;
    private static Action? _pathOfValheimanRefreshTooltip;
    private static UITooltip? _pathOfValheimanTooltipSource;
    private static GameObject? _pathOfValheimanNativeTooltip;
    private static readonly InventoryDialogLayering.TooltipLayer PathOfValheimanTooltipLayer = new();
    private static bool _pathOfValheimanTooltipLayerReady;
    private static ItemData? _pathOfValheimanTooltipItem;
    private static bool _pathOfValheimanTooltipWarning;
    private static bool _pathOfValheimanTooltipWasDisabled;
    private static Func<string, bool>? _povHasDeathKeepStat;
    private static bool _povDeathKeepWarning;
    private static MethodInfo? _povToolHudHideMethod;
    private static MethodInfo? _povCellHudHideMethod;
    private static Action? _povToolHudHide;
    private static Action? _povCellHudHide;
    private static Action<Player>? _povCellHudTick;
    private static Func<string>? _povEquipmentHost;
    private static Action<Player, int>? _povHudEat, _povHudDrink;
    private static Action<Player>? _povHudCycleArrows;
    private static bool _povHudBound;
    private static Player? _povUnifiedHudPlayer;

    private static void InitializePathOfValheimanNativeHud(Assembly assembly)
    {
        _povHudBound = false;
        bool nativeReady = false;
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
            nativeReady = true;
        });
        if (!nativeReady) return;
        // Keep the native display fallback if only the new action integration fails.
        InstallPathOfValheimanCapability("unified-hud", harmony =>
        {
            Type state = assembly.GetType("PassiveTree.Gear.State", true)!;
            _povHudEat = (Action<Player, int>)Delegate.CreateDelegate(typeof(Action<Player, int>),
                RequirePathOfValheimanMethod(state, "Eat", typeof(void), typeof(Player), typeof(int)));
            _povHudDrink = (Action<Player, int>)Delegate.CreateDelegate(typeof(Action<Player, int>),
                RequirePathOfValheimanMethod(state, "Drink", typeof(void), typeof(Player), typeof(int)));
            MethodInfo cycle = RequirePathOfValheimanMethod(state, "CycleArrows", typeof(void), typeof(Player));
            _povHudCycleArrows = (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), cycle);
            // Observe the real use call, after PoV's capacity/permanent-potion
            // checks. Never dispatch a second use from a display notification.
            harmony.Patch(AccessTools.Method(typeof(Humanoid), "UseItem", new[] { typeof(Inventory), typeof(ItemData), typeof(bool) }),
                prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(NotifyPathOfValheimanHudItemUse)));
            harmony.Patch(cycle, postfix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(NotifyPathOfValheimanHudArrowUse)));
            _povHudBound = true;
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
        Player player = Player.m_localPlayer;
        if (KeepPathOfValheimanHud() && !(_povUnifiedHudPlayer == player && CanUsePathOfValheimanHud(player) &&
            !IsUnityNull(InventoryPanels.QuickSlotsHotkeyBarRect))) _povCellHudTick!(player);
        else _povCellHudHide!();
    }

    private static bool CanUsePathOfValheimanHud(Player player) =>
        _povHudBound && PovCanMutate(player) && _pov!.Enabled();

    private static void NotifyPathOfValheimanHudItemUse(Humanoid __instance, ItemData item)
    {
        Player player = Player.m_localPlayer;
        if (__instance != player || item == null || !CanUsePathOfValheimanHud(player)) return;
        Inventory inventory = player.GetInventory();
        if (!TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? slot) || slot?.PovCell == null ||
            !(slot.PovCell.StartsWith("food.", StringComparison.Ordinal) || slot.PovCell.StartsWith("potion.", StringComparison.Ordinal)) ||
            inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y) != item || !slot.Accepts(item) ||
            !IsQuickSlotUnlocked(player, slot)) return;
        NotifyQuickSlotHudUse(player, slot);
    }

    private static void NotifyPathOfValheimanHudArrowUse(Player __0)
    {
        if (!CanUsePathOfValheimanHud(__0)) return;
        ItemData item = __0.GetAmmoItem();
        Inventory inventory = __0.GetInventory();
        if (item == null || !TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? slot) ||
            slot?.PovCell?.StartsWith("arrow.", StringComparison.Ordinal) != true ||
            inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y) != item || !slot.Accepts(item) ||
            !IsQuickSlotUnlocked(__0, slot)) return;
        NotifyQuickSlotHudUse(__0, slot);
    }

    private static void UsePathOfValheimanHudSlot(Player player, SlotDefinition slot)
    {
        if (!CanUsePathOfValheimanHud(player) || slot.PovCell == null || !IsQuickSlotUnlocked(player, slot)) return;
        string cell = slot.PovCell;
        int dot = cell.LastIndexOf('.');
        if (dot < 0 || !int.TryParse(cell.Substring(dot + 1), out int index) || index < 0 || index > 2) return;
        if (cell.StartsWith("food.", StringComparison.Ordinal)) _povHudEat!(player, index);
        else if (cell.StartsWith("potion.", StringComparison.Ordinal)) _povHudDrink!(player, index);
        // As with PoV's arrow hotkey, any arrow cell cycles to the next usable stack.
        else if (cell.StartsWith("arrow.", StringComparison.Ordinal)) _povHudCycleArrows!(player);
    }
    private static void ClearPathOfValheimanNativeHud()
    {
        _povToolHudHideMethod = _povCellHudHideMethod = null;
        _povToolHudHide = _povCellHudHide = null;
        _povCellHudTick = null;
        _povEquipmentHost = null;
        _povHudEat = _povHudDrink = null;
        _povHudCycleArrows = null;
        _povHudBound = false;
        _povUnifiedHudPlayer = null;
    }

    private static void InitializePathOfValheimanCompatibility()
    {
        if (_pov == null) return;
        Assembly assembly = _pov.Assembly;
        InstallPathOfValheimanCapability("death-keep", _ => BindPathOfValheimanDeathKeep(assembly));
        InstallPathOfValheimanCapability("delete-ui", _ => InventoryCompatibility.PathOfValheimanTrashMode.Install(assembly, ModGUID));
        InstallPathOfValheimanCapability("item-presentation", _ => BindPathOfValheimanItemPresentation(assembly));
        InitializePathOfValheimanNativeHud(assembly);
        InitializePathOfValheimanCrafting(assembly);
        InstallPathOfValheimanCapability("tooltip", _ => BindPathOfValheimanTooltip(assembly));

        InstallPathOfValheimanCapability("tree-input", _ =>
        {
            _pathOfValheimanTreeOpen = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),
                RequirePathOfValheimanMethod(assembly.GetType("PassiveTree.UI.TreeWindow", true)!, "get_IsOpen", typeof(bool)));
        });
    }

    private static void BindPathOfValheimanTooltip(Assembly assembly)
    {
        Type type = assembly.GetType("PassiveTree.UI.ItemTooltip", true)!;
        var wanted = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), RequirePathOfValheimanMethod(type, "get_Wanted", typeof(bool)));
        var refresh = (Action)Delegate.CreateDelegate(typeof(Action), RequirePathOfValheimanMethod(type, "RefreshCurrent", typeof(void)));
        var body = PathOfValheimanStaticField<GameObject>(type, "sheetGo");
        var current = PathOfValheimanStaticField<UITooltip>(typeof(UITooltip), "m_current");
        var root = PathOfValheimanStaticField<GameObject>(typeof(UITooltip), "m_tooltip");
        Func<UITooltip, ItemData?> owner;
        if (AccessTools.DeclaredMethod(type, "OwnerOf", new[] { typeof(UITooltip) }) != null)
            owner = (Func<UITooltip, ItemData?>)Delegate.CreateDelegate(typeof(Func<UITooltip, ItemData>),
                RequirePathOfValheimanMethod(type, "OwnerOf", typeof(ItemData), typeof(UITooltip)));
        else
        {
            var owners = PathOfValheimanStaticField<ConditionalWeakTable<UITooltip, ItemData>>(type, "owners")();
            if (owners == null) throw new MissingFieldException(type.FullName, "owners");
            owner = tooltip => owners.TryGetValue(tooltip, out ItemData item) ? item : null;
        }
        _pathOfValheimanTooltipOwner = owner;
        _pathOfValheimanCurrentTooltip = current;
        _pathOfValheimanTooltipRoot = root;
        _pathOfValheimanTooltipBody = body;
        _pathOfValheimanRefreshTooltip = refresh;
        _pathOfValheimanTooltipWanted = wanted;
    }

    private static void BindPathOfValheimanDeathKeep(Assembly assembly)
    {
        _povHasDeathKeepStat = null;
        Type? type = assembly.GetType("Valheiman.Api.Stats");
        MethodInfo? method = type?.GetMethod("Has", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string) }, null);
        if (type?.IsVisible != true || method == null || method.ReturnType != typeof(bool) || method.ContainsGenericParameters)
            throw new MissingMethodException("Valheiman.Api.Stats", "Has");
        _povHasDeathKeepStat = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), method);
    }

    private static bool CanUsePathOfValheimanDeathKeep(Player player)
    {
        if (_pov == null) return true;
        // Stats is local-character state. Bound Gear is independent of the
        // equipment-system toggle; read the effect at death, never cache it.
        if (player == null || player != Player.m_localPlayer) return false;
        // Failure removes only the extra node requirement. The caller still
        // applies the configured YAML toggle/selection and death recovery rules.
        try { return _povHasDeathKeepStat?.Invoke("BoundGear") ?? true; }
        catch (Exception error)
        {
            _povHasDeathKeepStat = null;
            if (!_povDeathKeepWarning)
                Log.LogWarning("PoV Bound Gear unlock status unavailable; using configured YAML Death Keep rules: " + error.GetBaseException().Message);
            _povDeathKeepWarning = true;
            return true;
        }
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

    private static bool TryYieldToPathOfValheimanTooltip(UITooltip tooltip)
    {
        _pathOfValheimanTooltipLayerReady = false;
        if (_pathOfValheimanTooltipWanted == null) return false;
        try
        {
            if (!_pathOfValheimanTooltipWanted())
            {
                ClearPathOfValheimanTooltipState();
                _pathOfValheimanTooltipWasDisabled = true;
                return false;
            }
            ItemData? item = _pathOfValheimanTooltipOwner!(tooltip);
            if (!IsInventoryContainerTooltipSource(tooltip) || _pathOfValheimanCurrentTooltip!() != tooltip || _pathOfValheimanTooltipRoot!() == null ||
                item?.m_shared == null)
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
            if (!HasPathOfValheimanTooltipBody())
            {
                PathOfValheimanTooltipLayer.Restore();
                return false;
            }
            _inventoryContainerHoverTooltipSource = null;
            HideInventoryContainerCustomTooltip();
            ForceHideInventorySimpleNameTooltip();
            RestoreVanillaTooltipVisual(_pathOfValheimanTooltipRoot!());
            _pathOfValheimanTooltipLayerReady = true;
            UpdatePathOfValheimanTooltipLayer();
            return true;
        }
        catch (Exception error)
        {
            _pathOfValheimanTooltipLayerReady = false;
            PathOfValheimanTooltipLayer.Restore();
            if (!_pathOfValheimanTooltipWarning)
            {
                _pathOfValheimanTooltipWarning = true;
                Log.LogWarning($"Path of Valheiman tooltip unavailable; using InventorySlots text: {error.Message}");
            }
            return false;
        }
    }

    private static void UpdatePathOfValheimanTooltipLayer()
    {
        InventoryGui? gui = InventoryGui.instance;
        UITooltip? source = _pathOfValheimanTooltipSource;
        GameObject? host = _pathOfValheimanNativeTooltip;
        if (_instance == null || !_instance.isActiveAndEnabled || !_pathOfValheimanTooltipLayerReady ||
            gui == null || !InventoryGui.IsVisible() || IsInventoryPanelClosing(gui) ||
            source == null || !source.gameObject.activeInHierarchy || host == null ||
            UITooltip.m_current != source || UITooltip.m_tooltip != host)
        {
            PathOfValheimanTooltipLayer.Restore();
            return;
        }

        RectTransform? quickPanel = null;
        if (gui.m_playerGrid != null)
            InventoryPanels.QuickSlotPanels.TryGetValue(gui.m_playerGrid.GetInstanceID(), out quickPanel);
        PathOfValheimanTooltipLayer.Update(gui, quickPanel, InventoryPanels.SortButtonLayer, host.transform);
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
        _pathOfValheimanTooltipLayerReady = false;
        PathOfValheimanTooltipLayer.Restore();
        _pathOfValheimanTooltipSource = null;
        _pathOfValheimanNativeTooltip = null;
        _pathOfValheimanTooltipItem = null;
        _pathOfValheimanTooltipWasDisabled = false;
    }

    private static void ShutdownPathOfValheimanCompatibility()
    {
        InventoryCompatibility.PathOfValheimanTrashMode.Dispose();
        ResetPathOfValheimanActionGate();
        _povHasDeathKeepStat = null;
        _povDeathKeepWarning = false;
        foreach (Harmony harmony in PathOfValheimanPatches) harmony.UnpatchSelf();
        PathOfValheimanPatches.Clear();
        ShutdownPathOfValheimanCrafting();
        ClearPathOfValheimanItemPresentation();
        ClearPathOfValheimanNativeHud();
        _pathOfValheimanTreeOpen = null;
        _pathOfValheimanTooltipWanted = null;
        _pathOfValheimanTooltipOwner = null;
        _pathOfValheimanTooltipBody = null;
        _pathOfValheimanCurrentTooltip = null;
        _pathOfValheimanTooltipRoot = null;
        _pathOfValheimanRefreshTooltip = null;
        ClearPathOfValheimanTooltipState();
    }
}
