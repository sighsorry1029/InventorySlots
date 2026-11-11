using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InventorySlots;

// Only UI objects are reused. Native recipe discovery, requirements, row
// initialization and every Harmony prefix/postfix still execute on each rebuild.
internal static class CraftingRecipeRowReuse
{
    internal static bool ListReady;
    internal static bool RowReady;
    private static Owner? _owner;
    private static Rebuild? _rebuild;
    private static Add? _add;
    private static readonly HashSet<string> Reported = new();

    private sealed class Owner
    {
        internal readonly InventoryGui Gui;
        internal readonly Player Player;
        internal readonly GameObject Prefab;
        internal readonly RectTransform Root;
        internal Dictionary<GameObject, Row> Rows = new();
        internal Owner(InventoryGui gui, Player player)
        { Gui = gui; Player = player; Prefab = gui.m_recipeElementPrefab; Root = gui.m_recipeListRoot; }
    }

    internal readonly struct Key : IEquatable<Key>
    {
        private readonly Recipe _recipe;
        private readonly ItemDrop.ItemData? _item;
        internal Key(Recipe recipe, ItemDrop.ItemData? item) { _recipe = recipe; _item = item; }
        public bool Equals(Key other) => ReferenceEquals(_recipe, other._recipe) && ReferenceEquals(_item, other._item);
        public override bool Equals(object? obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => unchecked(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_recipe) * 397 ^
            (_item == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_item)));
    }

    internal sealed class Row
    {
        internal readonly GameObject Go;
        internal readonly Key Key;
        internal readonly Button Button;
        internal readonly GameObject Selected;
        internal readonly Image Icon;
        internal readonly bool SelectedDefault, IconEnabledDefault, InteractableDefault;
        internal Button.ButtonClickedEvent? ClickEvent;
        internal UnityAction? ClickAction;
        internal Row? Next;
        internal bool Held;
        internal Row(GameObject go, Recipe recipe, ItemDrop.ItemData? item, Button button, GameObject selected, Image icon)
        {
            Go = go; Key = new Key(recipe, item); Button = button; Selected = selected; Icon = icon;
            SelectedDefault = selected.activeSelf; IconEnabledDefault = icon.enabled; InteractableDefault = button.interactable;
        }
        internal bool IsUsable(RectTransform root) => Go != null && Go.transform.parent == root &&
            Button != null && Selected != null && Icon != null;
        internal void Reset()
        {
            // Do not remove listeners installed by the prefab or another mod.
            if (ClickEvent != null && ClickAction != null) ClickEvent.RemoveListener(ClickAction);
            ClickEvent = null; ClickAction = null;
            Selected.SetActive(SelectedDefault);
            Icon.enabled = IconEnabledDefault;
            Button.interactable = InteractableDefault;
            Go.transform.SetAsLastSibling();
        }
    }

    internal sealed class Rebuild
    {
        internal readonly InventoryGui Gui;
        internal readonly Rebuild? Previous;
        private Owner? Owner;
        private Dictionary<GameObject, Row>? OldRows;
        private readonly Dictionary<Key, Row> Available = new();
        private readonly List<Row> Released = new();
        internal bool Enabled, Started, Ended, Nested;
        internal Rebuild(InventoryGui gui, Rebuild? previous) { Gui = gui; Previous = previous; }

        internal void Begin()
        {
            if (Started || Ended) return;
            Started = true;
            if (Previous != null || Nested || !ListReady || !RowReady ||
                !InventorySlotsPlugin.CanReuseNativeCraftingRows(Gui) || !CraftingRecipeRowReuseHooks.Permit())
            { _owner = null; return; }
            Player player = Player.m_localPlayer;
            if (_owner == null || _owner.Gui != Gui || _owner.Player != player ||
                _owner.Prefab != Gui.m_recipeElementPrefab || _owner.Root != Gui.m_recipeListRoot)
                _owner = new Owner(Gui, player);
            Owner = _owner;
            OldRows = Owner.Rows;
            Owner.Rows = new Dictionary<GameObject, Row>();
            Enabled = true;
        }

        internal bool Release(Object value)
        {
            if (!Enabled || Owner == null || value is not GameObject go || OldRows == null ||
                !OldRows.TryGetValue(go, out Row row)) return false;
            OldRows.Remove(go);
            if (!row.IsUsable(Owner.Root)) return false;
            go.SetActive(false);
            row.Held = true;
            row.Next = Available.TryGetValue(row.Key, out Row next) ? next : null;
            Available[row.Key] = row;
            Released.Add(row);
            return true;
        }

        private Row? Take(Recipe recipe, ItemDrop.ItemData? item)
        {
            var key = new Key(recipe, item);
            while (Available.TryGetValue(key, out Row row))
            {
                if (row.Next == null) Available.Remove(key); else Available[key] = row.Next;
                row.Next = null;
                if (!row.IsUsable(Owner!.Root)) continue;
                row.Reset();
                row.Held = false;
                return row;
            }
            return null;
        }

        internal GameObject Rent(GameObject prefab, Transform parent, Recipe recipe, ItemDrop.ItemData? item, Add call)
        {
            if (!Enabled || Owner == null || prefab != Owner.Prefab || parent != Owner.Root)
                return Object.Instantiate(prefab, parent);
            Row? row = Take(recipe, item);
            if (row == null)
            {
                GameObject created = Object.Instantiate(prefab, parent);
                Button button = created.GetComponent<Button>();
                Transform selected = created.transform.Find("selected");
                Image? icon = created.transform.Find("icon")?.GetComponent<Image>();
                // A foreign row template keeps its native path; never guess its structure.
                if (button == null || selected == null || icon == null) return created;
                row = new Row(created, recipe, item, button, selected.gameObject, icon);
            }
            call.Row = row;
            return row.Go;
        }

        internal void Commit(Add call)
        {
            if (call.Row != null && Owner != null) Owner.Rows[call.Row.Go] = call.Row;
        }

        internal void End()
        {
            if (Ended) return;
            Ended = true; Enabled = false;
            // No reserve beyond this rebuild: shrinking a nocost list releases
            // all unused rows immediately, just as the original list did.
            foreach (Row row in Released)
            {
                if (row.Held && row.Go != null) DestroyUnused(row.Go);
                row.Held = false; row.Next = null;
            }
            Released.Clear(); Available.Clear(); OldRows = null;
        }
    }

    internal sealed class Add
    {
        internal readonly InventoryGui Gui;
        internal readonly Add? Previous;
        internal readonly Rebuild? Rebuild;
        internal Row? Row;
        internal bool Completed;
        internal Add(InventoryGui gui, Add? previous, Rebuild? rebuild)
        { Gui = gui; Previous = previous; Rebuild = rebuild; }
    }

    internal static Rebuild Enter(InventoryGui gui)
    {
        if (_rebuild != null) { _rebuild.Nested = true; _rebuild.Enabled = false; }
        // A disabled barrier also covers foreign prefixes which replace the body.
        return _rebuild = new Rebuild(gui, _rebuild);
    }
    internal static void Begin(InventoryGui gui) { if (_rebuild?.Gui == gui) _rebuild.Begin(); }
    internal static void End(InventoryGui gui) { if (_rebuild?.Gui == gui) _rebuild.End(); }
    internal static Exception? Leave(Rebuild? state, Exception? error)
    {
        if (state == null) return error;
        try { state.End(); }
        finally
        {
            if (error != null || state.Nested) _owner = null;
            _rebuild = state.Previous;
        }
        return error;
    }
    internal static void Release(Object value, InventoryGui gui)
    {
        if (_rebuild?.Gui != gui || !_rebuild.Release(value)) Object.Destroy(value);
    }
    internal static Add EnterRow(InventoryGui gui)
    {
        if (_add != null && _rebuild != null) { _rebuild.Nested = true; _rebuild.Enabled = false; }
        return _add = new Add(gui, _add, _rebuild?.Gui == gui ? _rebuild : null);
    }
    internal static GameObject Rent(GameObject prefab, Transform parent, InventoryGui gui, Recipe recipe, ItemDrop.ItemData? item)
    {
        Add? call = _add;
        return call?.Gui == gui && call.Rebuild != null
            ? call.Rebuild.Rent(prefab, parent, recipe, item, call) : Object.Instantiate(prefab, parent);
    }
    internal static void Bind(Button.ButtonClickedEvent click, UnityAction action, InventoryGui gui)
    {
        click.AddListener(action);
        if (_add?.Gui != gui || _add.Row == null) return;
        _add.Row.ClickEvent = click; _add.Row.ClickAction = action;
    }
    internal static void CompleteRow(InventoryGui gui)
    {
        if (_add?.Gui != gui) return;
        _add.Completed = true;
        _add.Rebuild?.Commit(_add);
    }
    internal static Exception? LeaveRow(Add? state, Exception? error)
    {
        if (state == null) return error;
        try
        {
            // A failed initialization must not leak a rented row. Once the native
            // list has accepted it, even a failing foreign postfix must retain it.
            if (!state.Completed && state.Row?.Go != null) DestroyUnused(state.Row.Go);
        }
        finally { _add = state.Previous; }
        return error;
    }
    internal static void Clear(InventoryGui? gui = null)
    {
        if (gui == null || _owner?.Gui == gui) _owner = null;
        if (_rebuild != null && (gui == null || _rebuild.Gui == gui)) _rebuild.Enabled = false;
    }

    internal static void Warn(string reason)
    {
        if (Reported.Add(reason)) InventorySlotsPlugin.Log.LogWarning("Recipe row reuse is unavailable; ordinary row creation is retained. " + reason);
    }

    private static void DestroyUnused(GameObject row)
    {
        try { Object.Destroy(row); }
        catch (Exception error) { Warn("Unused row cleanup failed: " + error.Message); }
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", typeof(List<Recipe>))]
internal static class InventoryGuiRecipeRowReuseListPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(InventoryGui __instance, out CraftingRecipeRowReuse.Rebuild __state) => __state = CraftingRecipeRowReuse.Enter(__instance);
    private static Exception? Finalizer(CraftingRecipeRowReuse.Rebuild __state, Exception? __exception) => CraftingRecipeRowReuse.Leave(__state, __exception);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var destroy = AccessTools.Method(typeof(Object), nameof(Object.Destroy), new[] { typeof(Object) });
        CraftingRecipeRowReuse.ListReady = false;
        if (code.Count(i => i.Calls(destroy)) != 1 || !code.Any(i => i.opcode == OpCodes.Ret))
        { CraftingRecipeRowReuse.Warn("Native recipe-list destruction changed."); return code; }
        var result = new List<CodeInstruction>();
        result.Add(new CodeInstruction(OpCodes.Ldarg_0));
        result.Add(CodeInstruction.Call(typeof(CraftingRecipeRowReuse), nameof(CraftingRecipeRowReuse.Begin)));
        foreach (CodeInstruction instruction in code)
        {
            if (instruction.Calls(destroy))
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
                result.Add(CodeInstruction.Call(typeof(CraftingRecipeRowReuse), nameof(CraftingRecipeRowReuse.Release)));
            }
            else if (instruction.opcode == OpCodes.Ret)
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
                result.Add(CodeInstruction.Call(typeof(CraftingRecipeRowReuse), nameof(CraftingRecipeRowReuse.End)));
                result.Add(instruction);
            }
            else result.Add(instruction);
        }
        CraftingRecipeRowReuse.ListReady = true;
        return result;
    }
}

[HarmonyPatch(typeof(InventoryGui), "AddRecipeToList", typeof(Player), typeof(Recipe), typeof(ItemDrop.ItemData), typeof(bool))]
internal static class InventoryGuiRecipeRowReuseAddPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(InventoryGui __instance, out CraftingRecipeRowReuse.Add __state) => __state = CraftingRecipeRowReuse.EnterRow(__instance);
    private static Exception? Finalizer(CraftingRecipeRowReuse.Add __state, Exception? __exception) => CraftingRecipeRowReuse.LeaveRow(__state, __exception);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        bool Instantiate(CodeInstruction i) => i.operand is MethodInfo m && m.DeclaringType == typeof(Object) &&
            m.Name == nameof(Object.Instantiate) && m.IsGenericMethod && m.GetGenericArguments().SequenceEqual(new[] { typeof(GameObject) }) &&
            m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(GameObject), typeof(Transform) });
        var add = AccessTools.Method(typeof(UnityEvent), nameof(UnityEvent.AddListener), new[] { typeof(UnityAction) });
        CraftingRecipeRowReuse.RowReady = false;
        if (code.Count(Instantiate) != 1 || code.Count(i => i.Calls(add)) != 1 || !code.Any(i => i.opcode == OpCodes.Ret))
        { CraftingRecipeRowReuse.Warn("Native recipe-row construction/listener binding changed."); return code; }
        var result = new List<CodeInstruction>();
        foreach (CodeInstruction instruction in code)
        {
            if (Instantiate(instruction))
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
                result.Add(new CodeInstruction(OpCodes.Ldarg_2));
                result.Add(new CodeInstruction(OpCodes.Ldarg_3));
                result.Add(CodeInstruction.Call(typeof(CraftingRecipeRowReuse), nameof(CraftingRecipeRowReuse.Rent)));
            }
            else if (instruction.Calls(add))
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
                result.Add(CodeInstruction.Call(typeof(CraftingRecipeRowReuse), nameof(CraftingRecipeRowReuse.Bind)));
            }
            else if (instruction.opcode == OpCodes.Ret)
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
                result.Add(CodeInstruction.Call(typeof(CraftingRecipeRowReuse), nameof(CraftingRecipeRowReuse.CompleteRow)));
                result.Add(instruction);
            }
            else result.Add(instruction);
        }
        CraftingRecipeRowReuse.RowReady = true;
        return result;
    }
}
