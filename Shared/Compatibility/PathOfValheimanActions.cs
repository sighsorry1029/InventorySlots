using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

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
    private delegate bool RuneResponseHandler(Container container, bool granted, out bool state);
    private static RuneResponseHandler? _runeResponse;
    private static Func<Container, bool>? _isRuneChest, _isAnvil;
    [ThreadStatic] private static bool _anvilBulk;
    private static int _generation;

    internal static void BindContainers(Assembly assembly)
    {
        Func<Container, bool>? Bind(string name)
        {
            Type? type = assembly.GetType(name);
            if (type == null) return null; // Earlier PoV has no specialized storage.
            MethodInfo? method = AccessTools.DeclaredMethod(type, "Is", new[] { typeof(Container) });
            if (method == null || !method.IsPublic || !method.IsStatic || method.ReturnType != typeof(bool))
            {
                // A missing query must not abort plugin/server startup. The
                // reviewed Is implementations identify these exact components.
                Type? store = type.GetNestedType("Store", BindingFlags.Public | BindingFlags.NonPublic);
                return store != null && typeof(UnityEngine.Component).IsAssignableFrom(store)
                    ? container => container.GetComponent(store) != null
                    : _ => true; // Unverifiable storage: disable generic container writes only.
            }
            return (Func<Container, bool>)Delegate.CreateDelegate(typeof(Func<Container, bool>), method);
        }
        _isRuneChest = Bind("PassiveTree.RuneChest");
        _isAnvil = Bind("PassiveTree.RunicCraft.Anvil");
    }

    // Query components live: PoV may attach Store after Container.Awake. These
    // stores own a separate ledger; m_inventory is only their working area.
    internal static bool IsSpecialContainer(Container? container)
    {
        if (container == null) return false;
        try { return _isRuneChest?.Invoke(container) == true || _isAnvil?.Invoke(container) == true; }
        catch { return true; } // No generic writes when a storage query fails.
    }

    internal static void Install(Assembly assembly, string owner)
    {
        if (_patches != null) return;
        BindContainers(assembly);
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
        // Ordinary chest replies belong exclusively to the inventory backend.
        Find(rpc, response, HarmonyPatchType.Prefix);
        Find(rpc, response, HarmonyPatchType.Finalizer);
        Find(update, keys, HarmonyPatchType.Postfix);
        MethodInfo responsePrefix = observers[0].Patch.PatchMethod;
        MethodInfo? runeStack = null, anvilStack = null, anvilDo = null, anvilDeposit = null;
        if (_isRuneChest != null)
        {
            Type rune = assembly.GetType("PassiveTree.RuneChest", true)!;
            runeStack = Require(rune, "StackFrom", typeof(int), typeof(Container), typeof(IEnumerable<ItemData>), typeof(bool));
            if (responsePrefix.ReturnType != typeof(bool) || !responsePrefix.IsStatic ||
                !responsePrefix.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[] { typeof(Container), typeof(bool), typeof(bool).MakeByRefType() }))
                throw new NotSupportedException("PoV RuneChest response contract changed");
            MethodInfo runeIs = Require(rune, "Is", typeof(bool), typeof(Container));
            MethodInfo dispatch = Require(native, "RuneStack", typeof(void), typeof(Container), typeof(bool));
            Type wait = native.GetNestedType("RuneWait", BindingFlags.NonPublic) ?? throw new TypeLoadException("PoV RuneWait");
            MethodInfo waitTick = Require(wait, "Postfix", typeof(void), typeof(Player));
            if (!PatchProcessor.GetOriginalInstructions(responsePrefix).Any(i => i.Calls(runeIs)) ||
                !PatchProcessor.GetOriginalInstructions(responsePrefix).Any(i => i.Calls(dispatch)) ||
                !PatchProcessor.GetOriginalInstructions(waitTick).Any(i => i.Calls(dispatch)) ||
                !PatchProcessor.GetOriginalInstructions(dispatch).Any(i => i.Calls(runeStack)))
                throw new NotSupportedException("PoV RuneChest response routing changed");
            _runeResponse = (RuneResponseHandler)Delegate.CreateDelegate(typeof(RuneResponseHandler), responsePrefix);
        }
        if (_isAnvil != null)
        {
            Type anvil = assembly.GetType("PassiveTree.RunicCraft.Anvil", true)!;
            anvilStack = Require(anvil.GetNestedType("Stack", BindingFlags.NonPublic)!, "Prefix", typeof(bool), typeof(Container));
            anvilDo = Require(anvil, "Do", typeof(void), typeof(Container), typeof(Action));
            anvilDeposit = Require(anvil, "Deposit", typeof(bool), typeof(Container), typeof(Inventory), typeof(ItemData), typeof(int));
            if (!PatchProcessor.GetOriginalInstructions(anvilStack).Any(i => i.Calls(anvilDo)))
                throw new NotSupportedException("PoV runic anvil bulk routing changed");
        }
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
            if (_runeResponse != null)
            {
                _patches.Patch(rpc, prefix: new HarmonyMethod(typeof(PathOfValheimanActions), nameof(RuneResponse)) { priority = Priority.Last });
                _patches.Patch(runeStack, prefix: new HarmonyMethod(typeof(PathOfValheimanActions), nameof(FilterRuneStack)));
            }
            if (anvilStack != null)
            {
                _patches.Patch(anvilStack, prefix: new HarmonyMethod(typeof(PathOfValheimanActions), nameof(BeginAnvilStack)),
                    finalizer: new HarmonyMethod(typeof(PathOfValheimanActions), nameof(EndAnvilStack)));
                _patches.Patch(anvilDo, prefix: new HarmonyMethod(typeof(PathOfValheimanActions), nameof(WrapAnvilStack)));
                _patches.Patch(anvilDeposit, prefix: new HarmonyMethod(typeof(PathOfValheimanActions), nameof(GuardAnvilDeposit)));
            }
        }
        catch { Dispose(); throw; }
    }

    private static bool Suppress() => false;

    private static bool RuneResponse(Container __instance, bool granted, bool __runOriginal)
    {
        // Our Priority.First handoff fence already consumes adapter/late replies.
        // Never consume it twice or forward a reply it has rejected.
        if (!__runOriginal) return true;
        try { if (_isRuneChest?.Invoke(__instance) != true) return true; }
        catch { return false; }
        return _runeResponse!(__instance, granted, out _);
    }

    private static void FilterRuneStack(ref IEnumerable<ItemData> __1)
    {
        Inventory? inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
        __1 = inventory == null ? Array.Empty<ItemData>() : __1.Where(item => Plugin.CanUsePathOfValheimanBulkSource(inventory, item));
    }

    private static void BeginAnvilStack(out bool __state) { __state = _anvilBulk; _anvilBulk = true; }
    private static void EndAnvilStack(bool __state) => _anvilBulk = __state;
    private static void WrapAnvilStack(ref Action __1)
    {
        if (!_anvilBulk || __1 == null) return;
        Action native = __1;
        int generation = _generation;
        // PoV may defer the action until its lease arrives. Carry only the scope,
        // never a stale eligibility result, and leave PoV's cancellation intact.
        __1 = () =>
        {
            if (_patches == null || generation != _generation) return;
            bool before = _anvilBulk;
            _anvilBulk = true;
            try { native(); } finally { _anvilBulk = before; }
        };
    }
    private static bool GuardAnvilDeposit(Inventory __1, ItemData __2, ref bool __result)
    {
        if (!_anvilBulk || Plugin.CanUsePathOfValheimanBulkSource(__1, __2)) return true;
        __result = false;
        return false;
    }

    internal static void Dispose()
    {
        ++_generation;
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
        _runeResponse = null;
        _anvilBulk = false;
    }
}
