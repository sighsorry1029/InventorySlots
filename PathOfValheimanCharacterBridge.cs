using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

// Optional API binding only: no item copies, storage ownership or rune accounting.
internal sealed class PathOfValheimanLooksBridge
{
    internal readonly Func<ItemData, bool> CanChange, IsOpen;
    internal readonly Func<ItemData, IReadOnlyList<ItemData>> Choices;
    internal readonly Func<ItemData> Hidden;
    internal readonly Func<Player, ItemData, ItemData> Current;
    internal readonly Func<ItemData, ItemData, bool> SameLook;
    internal readonly Func<Player, int> Runes;
    internal readonly Func<Player, ItemData, ItemData, bool, string> WhyNot;
    internal readonly Func<Player, IList<(ItemData, ItemData, bool)>, string> Apply;
    internal readonly Action<Player, ItemData> Reset;

    internal PathOfValheimanLooksBridge(Assembly assembly)
    {
        Type type = assembly.GetType("Valheiman.Api.Looks", true)!;
        CanChange = Bind<Func<ItemData, bool>>("CanChange");
        IsOpen = Bind<Func<ItemData, bool>>("IsOpen");
        Choices = Bind<Func<ItemData, IReadOnlyList<ItemData>>>("Choices");
        Hidden = Bind<Func<ItemData>>("get_HiddenLook");
        Current = Bind<Func<Player, ItemData, ItemData>>("Current");
        SameLook = Bind<Func<ItemData, ItemData, bool>>("SameLook");
        Runes = Bind<Func<Player, int>>("Runes");
        WhyNot = Bind<Func<Player, ItemData, ItemData, bool, string>>("WhyNot");
        Apply = Bind<Func<Player, IList<(ItemData, ItemData, bool)>, string>>("Apply");
        Reset = Bind<Action<Player, ItemData>>("Reset");

        T Bind<T>(string name) where T : Delegate
        {
            MethodInfo signature = typeof(T).GetMethod("Invoke")!;
            Type[] args = Array.ConvertAll(signature.GetParameters(), p => p.ParameterType);
            MethodInfo? method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method == null || method.ReturnType != signature.ReturnType) throw new MissingMethodException(type.FullName, name);
            return (T)Delegate.CreateDelegate(typeof(T), method);
        }
    }
}

// PoV does not expose these two views through its public API. Validate the narrow
// instance contract once, bind closed delegates once per GUI, and own only our view.
internal sealed class PathOfValheimanCharacterPanelFactory
{
    private readonly ConstructorInfo _constructor;
    private readonly MethodInfo _show, _fit, _tick, _dispose;
    private readonly MethodInfo? _closed;
    private readonly bool _skills;
    private readonly FieldInfo? _current;
    private readonly PropertyInfo? _root;

    internal PathOfValheimanCharacterPanelFactory(Assembly assembly, bool skills)
    {
        _skills = skills;
        Type type = assembly.GetType(skills ? "PassiveTree.Gems.GemBoardPanel" : "PassiveTree.UI.CharacterSheet", true)!;
        if (skills)
        {
            _current = AccessTools.DeclaredField(type, "Current");
            _root = AccessTools.DeclaredProperty(type, "Root");
            if (_current == null || !_current.IsStatic || _current.FieldType != type || _root?.PropertyType != typeof(RectTransform))
                throw new MissingMemberException(type.FullName, "Current/Root");
        }
        Type[] args = skills ? new[] { typeof(RectTransform), typeof(TMP_FontAsset), typeof(InventoryGui) } :
            new[] { typeof(InventoryGui), typeof(RectTransform), typeof(TMP_FontAsset) };
        _constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)
            ?? throw new MissingMethodException(type.FullName, ".ctor");
        _show = Require("Show", typeof(bool));
        _fit = Require("Fit", typeof(Vector2), typeof(Vector2));
        _tick = Require("Tick", typeof(Player));
        _dispose = Require("Dispose");
        _closed = skills ? null : Require("Closed");

        MethodInfo Require(string name, params Type[] parameters)
        {
            MethodInfo? method = AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null || method.IsStatic || method.ReturnType != typeof(void)) throw new MissingMethodException(type.FullName, name);
            return method;
        }
    }

    internal Panel Create(InventoryGui gui, RectTransform parent, TMP_FontAsset font)
    {
        if (_current?.GetValue(null) != null) throw new InvalidOperationException("PoV already has a Skills view; refusing to replace its owner.");
        object? instance = null;
        try
        {
            instance = _constructor.Invoke(_skills ? new object[] { parent, font, gui } : new object[] { gui, parent, font });
            if (_skills && _root?.GetValue(instance) is RectTransform root && root != null && root.IsChildOf(parent))
            {
                // Our host already supplies the crafting wood. Hide only this owned Skills
                // instance's decorative backing; leave its board, header and input intact.
                foreach (string name in new[] { "Opaque underlay", "Crafting wood", "Wood" })
                {
                    Image? background = root.Find(name)?.GetComponent<Image>();
                    if (background == null) continue;
                    Color color = background.color;
                    color.a = 0f;
                    background.color = color;
                }
            }
            return new Panel(instance, _show, _fit, _tick, _dispose, _closed);
        }
        catch
        {
            // The native Skills constructor publishes Current before building UI.
            // A failed Build must not leave its partial singleton behind.
            object? candidate = instance ?? _current?.GetValue(null);
            if (candidate != null && (instance != null || _root?.GetValue(candidate) is RectTransform root && root != null && root.IsChildOf(parent)))
                _dispose.Invoke(candidate, null);
            for (int i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            throw;
        }
    }

    internal sealed class Panel : IDisposable
    {
        internal readonly Action<bool> Show;
        internal readonly Action<Vector2, Vector2> Fit;
        internal readonly Action<Player> Tick;
        private readonly Action _dispose;
        internal readonly Action? Closed;
        internal Panel(object instance, MethodInfo show, MethodInfo fit, MethodInfo tick, MethodInfo dispose, MethodInfo? closed)
        {
            Show = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, show);
            Fit = (Action<Vector2, Vector2>)Delegate.CreateDelegate(typeof(Action<Vector2, Vector2>), instance, fit);
            Tick = (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), instance, tick);
            _dispose = (Action)Delegate.CreateDelegate(typeof(Action), instance, dispose);
            Closed = closed == null ? null : (Action)Delegate.CreateDelegate(typeof(Action), instance, closed);
        }
        public void Dispose() => _dispose();
    }
}
