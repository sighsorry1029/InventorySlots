using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using HarmonyLib;

internal static class PoVActionsChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool allowResponse, throwStack;
    private static int responseCalls, otherObservers, keyCalls;
    private static ItemDrop.ItemData protectedItem = null!;
    private static Inventory source = null!;
    private static bool stackSawProtected;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate float NativeTime();
    private static readonly NativeTime Delta = () => 0f;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);
    private static bool Gate() => allowResponse;
    private static void OtherObserver() => ++otherObservers;
    private static void ResponseBody() => ++responseCalls;
    private static void KeysBody() => ++keyCalls;
    private static void StackBody()
    {
        stackSawProtected = source.GetAllItems().Contains(protectedItem);
        if (throwStack) throw new InvalidOperationException("injected StackAll failure");
    }
    private static bool Foreign(ItemDrop.ItemData __0, ref bool __result)
    { __result = ReferenceEquals(__0, protectedItem); return false; }
    private static IEnumerable<CodeInstruction> Empty() { yield return new CodeInstruction(OpCodes.Ret); }
    private static IEnumerable<CodeInstruction> RpcBody() => Body(nameof(ResponseBody));
    private static IEnumerable<CodeInstruction> InputBody() => Body(nameof(KeysBody));
    private static IEnumerable<CodeInstruction> InventoryBody()
    {
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PoVActionsChecks), nameof(StackBody)));
        yield return new CodeInstruction(OpCodes.Ldc_I4_0);
        yield return new CodeInstruction(OpCodes.Ret);
    }
    private static IEnumerable<CodeInstruction> Body(string name)
    {
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PoVActionsChecks), name));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    internal static void InstallNativeHooks(Assembly pov, Harmony harmony)
    {
        Type quick = pov.GetType("PassiveTree.QuickStack", true)!;
        foreach (string name in new[] { "Response", "Keys", "Stack" })
            harmony.CreateClassProcessor(quick.GetNestedType(name, BindingFlags.NonPublic)!).Patch();
    }

    internal static void Run(Assembly mod, Assembly pov, Action<bool, string> check)
    {
        mono_add_internal_call("UnityEngine.Time::get_deltaTime", Marshal.GetFunctionPointerForDelegate(Delta));
        Type adapter = mod.GetType("InventoryCompatibility.PathOfValheimanActions", true)!;
        Type quick = pov.GetType("PassiveTree.QuickStack", true)!;
        Type api = pov.GetType("Valheiman.Api.QuickStack", true)!;
        var native = new Harmony("treextr.pathofvalheiman");
        var fixture = new Harmony("smoke.pov.actions.fixture");
        string owner = mod.GetName().Name + ".smoke.actions";
        MethodInfo rpc = AccessTools.Method(typeof(Container), "RPC_StackResponse");
        MethodInfo update = AccessTools.Method(typeof(Player), "Update");
        MethodInfo stack = AccessTools.Method(typeof(Inventory), "StackAll");
        FieldInfo local = AccessTools.Field(typeof(Player), "m_localPlayer");
        object? previousPlayer = local.GetValue(null);
        Player player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        source = new Inventory("source", null, 8, 4);
        var chest = new Inventory("chest", null, 8, 4);
        protectedItem = new ItemDrop.ItemData { m_stack = 2, m_shared = new ItemDrop.ItemData.SharedData { m_name = "protected", m_maxStackSize = 50 } };
        var ordinary = new ItemDrop.ItemData { m_stack = 3, m_shared = new ItemDrop.ItemData.SharedData { m_name = "ordinary", m_maxStackSize = 50 } };
        source.GetAllItems().AddRange(new[] { protectedItem, ordinary });
        AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, source);
        local.SetValue(null, player);
        var asked = (HashSet<Container>)quick.GetField("asked", Static)!.GetValue(null)!;
        var box = (Container)FormatterServices.GetUninitializedObject(typeof(Container));
        FieldInfo ours = quick.GetField("ours", Static)!;
        FieldInfo stackChest = quick.GetField("stackChest", Static)!;
        try
        {
            fixture.Patch(rpc, prefix: new HarmonyMethod(typeof(PoVActionsChecks), nameof(Gate)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(PoVActionsChecks), nameof(OtherObserver)),
                transpiler: new HarmonyMethod(typeof(PoVActionsChecks), nameof(RpcBody)));
            fixture.Patch(update, transpiler: new HarmonyMethod(typeof(PoVActionsChecks), nameof(Empty)));
            fixture.Patch(quick.GetNestedType("Keys", BindingFlags.NonPublic)!.GetMethod("Postfix", Static),
                transpiler: new HarmonyMethod(typeof(PoVActionsChecks), nameof(InputBody)));
            fixture.Patch(stack, transpiler: new HarmonyMethod(typeof(PoVActionsChecks), nameof(InventoryBody)));
            fixture.Patch(quick.GetMethod("InForeignSlot", Static), prefix: new HarmonyMethod(typeof(PoVActionsChecks), nameof(Foreign)));
            InstallNativeHooks(pov, native);
            api.GetMethod("HostButtons")!.Invoke(null, new object[] { "prior.host" });
            adapter.GetMethod("Install", Static)!.Invoke(null, new object[] { pov, owner });
            check((string)api.GetProperty("ButtonsHostedBy")!.GetValue(null)! == owner, "inventory mod hosts PoV action buttons through the public API");
            check(Harmony.GetPatchInfo(rpc)!.Prefixes.All(p => p.owner != native.Id) &&
                  Harmony.GetPatchInfo(rpc)!.Finalizers.All(p => p.owner != native.Id), "PoV ownership/save observers are detached from the real game RPC");
            check(Harmony.GetPatchInfo(stack)!.Prefixes.Any(p => p.owner == native.Id) &&
                  Harmony.GetPatchInfo(stack)!.Finalizers.Any(p => p.owner == native.Id), "native storage-cell protection remains installed");
            responseCalls = otherObservers = keyCalls = 0;
            asked.Add(box);
            foreach (bool allowed in new[] { false, true })
            foreach (bool granted in new[] { false, true })
            {
                allowResponse = allowed;
                rpc.Invoke(box, new object[] { 77L, granted });
                check(asked.Contains(box) && !(bool)ours.GetValue(null)! && stackChest.GetValue(null) == null,
                    "PoV ignores shared RPC, allowed=" + allowed + ", granted=" + granted);
            }
            check(responseCalls == 2 && otherObservers == 4, "vanilla body and other observers retain normal gate behavior");
            update.Invoke(player, null);
            check(keyCalls == 0, "real Player.Update no longer invokes PoV action key observer");
            foreach (string name in new[] { "Run", "Sort" })
                quick.GetMethod(name, Static)!.Invoke(null, new object[] { player });
            quick.GetMethod("Make", Static)!.Invoke(null, new object?[] { null });
            check(source.GetAllItems().SequenceEqual(new[] { protectedItem, ordinary }) && asked.Count == 1,
                "native Run/Sort/Make are blocked without moving items or issuing requests");
            foreach (bool fail in new[] { false, true })
            {
                throwStack = fail;
                bool failed = false;
                try { stack.Invoke(chest, new object[] { source, true }); }
                catch (TargetInvocationException e) { failed = e.InnerException is InvalidOperationException; }
                check(failed == fail && !stackSawProtected && source.GetAllItems().Count == 2 &&
                      source.GetAllItems().Contains(protectedItem) && source.GetAllItems().Contains(ordinary),
                    "original storage protection restores exact items after StackAll, exception=" + fail);
            }
            asked.Clear();
            adapter.GetMethod("Dispose", Static)!.Invoke(null, null);
            check((string)api.GetProperty("ButtonsHostedBy")!.GetValue(null)! == "prior.host" &&
                  Harmony.GetPatchInfo(rpc)!.Finalizers.Any(p => p.owner == native.Id), "rollback restores prior host and exact native observers");
            adapter.GetMethod("Install", Static)!.Invoke(null, new object[] { pov, owner });
            api.GetMethod("HostButtons")!.Invoke(null, new object[] { "later.host" });
            adapter.GetMethod("Dispose", Static)!.Invoke(null, null);
            check((string)api.GetProperty("ButtonsHostedBy")!.GetValue(null)! == "later.host", "rollback preserves a later button host");
        }
        catch (Exception error) { System.Console.WriteLine("Action fixture failure: " + error); throw; }
        finally
        {
            asked.Clear();
            adapter.GetMethod("Dispose", Static)!.Invoke(null, null);
            api.GetMethod("HostButtons")!.Invoke(null, new object?[] { null });
            native.UnpatchSelf();
            fixture.UnpatchSelf();
            local.SetValue(null, previousPlayer);
        }
    }
}
