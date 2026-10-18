using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace InventoryCompatibility;

// UI-only lifetime, independent of the storage/action safety patches in either mod.
internal static class PathOfValheimanTrashMode
{
    private static Harmony? _patches;
    private static Func<bool>? _active, _popup;
    private static Func<int>? _count;
    private static Action? _exit;
    private static Func<RectTransform>? _plate;
    private static RectTransform? _hiddenPlate;

    internal static void Install(Assembly assembly, string owner)
    {
        if (_patches != null) return;
        Type type = assembly.GetType("PassiveTree.UI.TrashMode", true)!;
        MethodInfo Require(string name, Type result, params Type[] args)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, args);
            if (method == null || !method.IsStatic || method.ReturnType != result)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
        MethodInfo build = Require("Build", typeof(void), typeof(InventoryGui));
        MethodInfo enter = Require("Enter", typeof(void));
        MethodInfo press = Require("Press", typeof(void));
        _active = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), Require("get_Active", typeof(bool)));
        _popup = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), Require("get_PopupOpen", typeof(bool)));
        _count = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), Require("get_Count", typeof(int)));
        _exit = (Action)Delegate.CreateDelegate(typeof(Action), Require("Exit", typeof(void)));
        FieldInfo field = AccessTools.DeclaredField(type, "plate");
        if (field == null || !field.IsStatic || field.FieldType != typeof(RectTransform))
            throw new MissingFieldException(type.FullName, "plate");
        _plate = Expression.Lambda<Func<RectTransform>>(Expression.Field(null, field)).Compile();
        _patches = new Harmony(owner + ".pov.delete-ui");
        try
        {
            var prefix = new HarmonyMethod(typeof(PathOfValheimanTrashMode), nameof(Suppress));
            _patches.Patch(build, prefix: prefix);
            _patches.Patch(enter, prefix: prefix);
            _patches.Patch(press, prefix: prefix);
            Suppress();
        }
        catch { Dispose(); throw; }
    }

    private static bool Suppress()
    {
        if (_active!() || _popup!() || _count!() != 0) _exit!();
        RectTransform plate = _plate!();
        if (plate != null && plate.gameObject.activeSelf)
        {
            _hiddenPlate = plate;
            // Keep the instance: PoV's Paint still clears its old item-selection marks.
            plate.gameObject.SetActive(false);
        }
        return false;
    }

    internal static void Dispose()
    {
        _patches?.UnpatchSelf();
        _patches = null;
        if (_hiddenPlate != null) _hiddenPlate.gameObject.SetActive(true);
        _hiddenPlate = null;
        _active = _popup = null;
        _count = null;
        _exit = null;
        _plate = null;
    }
}
