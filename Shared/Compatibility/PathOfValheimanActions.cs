using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace InventoryCompatibility;

// PoV supplies progression; the inventory mod supplies inputs and transfers.
// Keep PoV's Inventory.StackAll storage-cell protection, but detach its
// request/ownership/save observers from the game's shared response channel.
internal static class PathOfValheimanActions
{
    private const string PovOwner = "treextr.pathofvalheiman";
    private static Harmony? _patches;
    private static readonly List<(MethodBase Target, Patch Patch, HarmonyPatchType Kind)> Removed = new();
    private static Action<string>? _hostButtons;
    private static Func<string>? _buttonsHost;
    private static string? _previousHost, _owner;

    internal static void Install(Assembly assembly, string owner)
    {
        if (_patches != null) return;
        Type native = assembly.GetType("PassiveTree.QuickStack", true)!;
        MethodInfo Require(Type type, string name, Type result, params Type[] args)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, args);
            if (method == null || !method.IsStatic || method.ReturnType != result)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
        Type api = assembly.GetType("Valheiman.Api.QuickStack", true)!;
        MethodInfo host = Require(api, "HostButtons", typeof(void), typeof(string));
        MethodInfo hosted = Require(api, "get_ButtonsHostedBy", typeof(string));
        if (!api.IsVisible || !host.IsPublic || !hosted.IsPublic)
            throw new MissingMethodException(api.FullName, "HostButtons");
        MethodInfo run = Require(native, "Run", typeof(void), typeof(Player));
        MethodInfo sort = Require(native, "Sort", typeof(void), typeof(Player));
        MethodInfo make = Require(native, "Make", typeof(void), typeof(InventoryGui));
        Type response = native.GetNestedType("Response", BindingFlags.NonPublic) ??
                        throw new TypeLoadException("PoV QuickStack.Response");
        Type keys = native.GetNestedType("Keys", BindingFlags.NonPublic) ??
                    throw new TypeLoadException("PoV QuickStack.Keys");
        MethodInfo rpc = AccessTools.DeclaredMethod(typeof(Container), "RPC_StackResponse", new[] { typeof(long), typeof(bool) });
        MethodInfo update = AccessTools.DeclaredMethod(typeof(Player), "Update", Type.EmptyTypes);
        var observers = new List<(MethodBase Target, Patch Patch, HarmonyPatchType Kind)>();
        void Find(MethodBase target, Type declaring, HarmonyPatchType kind)
        {
            Patches? info = Harmony.GetPatchInfo(target);
            var candidates = kind == HarmonyPatchType.Prefix ? info?.Prefixes :
                kind == HarmonyPatchType.Postfix ? info?.Postfixes : info?.Finalizers;
            Patch[] found = candidates?.Where(p => p.owner == PovOwner && p.PatchMethod.DeclaringType == declaring).ToArray()
                            ?? Array.Empty<Patch>();
            if (found.Length != 1) throw new NotSupportedException("PoV action observer changed: " + declaring.FullName + "/" + kind);
            observers.Add((target, found[0], kind));
        }
        // Identify installed observers, not their private argument lists. In 5.1.1
        // Finalizer gained Container; we no longer invoke either observer at all.
        Find(rpc, response, HarmonyPatchType.Prefix);
        Find(rpc, response, HarmonyPatchType.Finalizer);
        Find(update, keys, HarmonyPatchType.Postfix);
        _hostButtons = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), host);
        _buttonsHost = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), hosted);
        _previousHost = _buttonsHost();
        _owner = owner;
        _patches = new Harmony(owner + ".pov.actions");
        try
        {
            var suppress = new HarmonyMethod(typeof(PathOfValheimanActions), nameof(Suppress));
            _patches.Patch(run, prefix: suppress);
            _patches.Patch(sort, prefix: suppress);
            _patches.Patch(make, prefix: suppress);
            _hostButtons(owner);
            foreach (var observer in observers)
            {
                Removed.Add(observer);
                _patches.Unpatch(observer.Target, observer.Patch.PatchMethod);
            }
        }
        catch { Dispose(); throw; }
    }

    private static bool Suppress() => false;

    internal static void Dispose()
    {
        // Rollback/test teardown only. Both plugins retain their main action
        // patches on OnDestroy, so this safety boundary has the same process lifetime.
        // Restore only the exact observers removed by us, with their ordering.
        // Other PoV features and other mods' response guards are untouched.
        foreach (var observer in Removed)
        {
            Patches? current = Harmony.GetPatchInfo(observer.Target);
            var present = observer.Kind == HarmonyPatchType.Prefix ? current?.Prefixes :
                observer.Kind == HarmonyPatchType.Postfix ? current?.Postfixes : current?.Finalizers;
            if (present?.Any(p => p.owner == observer.Patch.owner && p.PatchMethod == observer.Patch.PatchMethod) == true)
                continue;
            var method = new HarmonyMethod(observer.Patch.PatchMethod)
            {
                priority = observer.Patch.priority, before = observer.Patch.before, after = observer.Patch.after,
                debug = observer.Patch.debug
            };
            new Harmony(observer.Patch.owner).Patch(observer.Target,
                prefix: observer.Kind == HarmonyPatchType.Prefix ? method : null,
                postfix: observer.Kind == HarmonyPatchType.Postfix ? method : null,
                finalizer: observer.Kind == HarmonyPatchType.Finalizer ? method : null);
        }
        Removed.Clear();
        if (_buttonsHost != null && _buttonsHost() == _owner) _hostButtons!(_previousHost!);
        _patches?.UnpatchSelf();
        _patches = null;
        _hostButtons = null;
        _buttonsHost = null;
        _previousHost = _owner = null;
    }
}
