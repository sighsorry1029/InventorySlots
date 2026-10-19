using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using HarmonyLib;

internal static class PoVTidyPackChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool unlocked;
    private static int quickCalls, restockCalls;
    private static bool NoProgress() => false;
    private static bool Available(ref bool __result) { __result = unlocked; return false; }
    private static bool Quick(ref int __result) { quickCalls++; __result = 0; return false; }
    private static bool Restock(ref int __result) { restockCalls++; __result = 0; return false; }
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private static bool EmptyTargets(ref List<ItemDrop.ItemData> __result) { __result = new(); return false; }
    private static IEnumerable<CodeInstruction> EmptyBody() { yield return new CodeInstruction(OpCodes.Ret); }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate float NativeTime();
    private static readonly NativeTime TimeStub = () => 1f;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);

    // Original public API binding + controlled progression and engine services.
    // Action bodies are the final merged mod; transfer observers below isolate
    // routing from Unity/network I/O, not a claim of live item/RPC validation.
    internal static void Run(Type plugin, Assembly pov, Action<bool, string> check)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Static)!.Invoke(null, args);
        void Write(string name, object? value) => plugin.GetField(name, Static)!.SetValue(null, value);
        bool Allowed() => (bool)Call("CanUseTidyPackActions")!;
        bool slots = plugin.Namespace == "InventorySlots";
        var fixture = new Harmony("smoke.pov.tidy." + plugin.Namespace);
        MethodInfo getter = pov.GetType("Valheiman.Api.QuickStack", true)!.GetProperty("Available")!.GetGetMethod()!;
        fixture.Patch(AccessTools.PropertyGetter(pov.GetType("PassiveTree.Progress", true), "Current"),
            prefix: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(NoProgress)));
        fixture.Patch(AccessTools.Method(typeof(Player), "Message"),
            transpiler: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(EmptyBody)));
        fixture.Patch(plugin.GetMethod("QuickStackItemsIntoContainer", Static),
            prefix: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(Quick)));
        fixture.Patch(plugin.GetMethod("RestockTargetsFromContainer", Static),
            prefix: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(Restock)));
        fixture.Patch(plugin.GetMethod("RestockMissingFavoriteItems", Static),
            prefix: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(Zero)));
        if (!slots) fixture.Patch(plugin.GetMethod("GetRestockTargets", Static),
            prefix: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(EmptyTargets)));
        try
        {
            Call("ResetPathOfValheimanActionGate");
            check(Allowed(), "Tidy Pack: absent PoV preserves ordinary actions");
            Call("BindPathOfValheimanActionGate", pov);
            var bound = (Delegate)plugin.GetField("_povTidyPackAvailable", Static)!.GetValue(null)!;
            check(bound.Method == getter && getter.IsPublic && getter.DeclaringType!.IsVisible,
                "Tidy Pack: binds the original visible public getter");
            check(!Allowed(), "Tidy Pack: original API with no loaded progression stays locked");
            fixture.Patch(getter, prefix: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(Available)));
            unlocked = true;
            check(Allowed(), "Tidy Pack: same bound getter observes unlock");
            unlocked = false;
            check(!Allowed(), "Tidy Pack: same bound getter observes respec without reload");
            check(((string)Call("AddTidyPackGuideHint", "Title\nBody")!).Contains("Tidy Pack"),
                "Tidy Pack: locked guide explains the required mastery");
            unlocked = true;
            check((string)Call("AddTidyPackGuideHint", "Title\nBody")! == "Title\nBody",
                "Tidy Pack: unlock removes the guide notice");
            unlocked = false;

            Player player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
            Inventory bag = new("tidy bag", null, 8, 4);
            Inventory chest = new("tidy chest", null, 8, 4);
            Container container = (Container)FormatterServices.GetUninitializedObject(typeof(Container));
            AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, bag);
            AccessTools.Field(typeof(Container), "m_inventory").SetValue(container, chest);
            if (slots) Call("SortPlayerInventory", player, false);
            else Call("SortPlayerInventory", player);
            check(bag.GetAllItems().Count == 0, "Tidy Pack: locked player sort returns before GUI/slot traversal");
            quickCalls = restockCalls = 0;
            if (slots)
            {
                Call("QuickStackIntoContainer", player, bag, container);
                Call("ExecuteContainerAreaTransfer", player, bag, container, false);
                object session = Activator.CreateInstance(plugin.GetNestedType("ContainerAreaSession", BindingFlags.NonPublic)!, true)!;
                session.GetType().GetField("QuickStack", Instance)!.SetValue(session, true);
                Call("ExecuteContainerAreaTargetAction", session, container);
                session.GetType().GetField("Targets", Instance)!.SetValue(session, new List<Container> { container });
                Write("_containerAreaSession", session);
                Call("ContinueContainerAreaTransfer");
                check(plugin.GetField("_containerAreaSession", Static)!.GetValue(null) == null,
                    "Tidy Pack: revoked area batch cancels before visiting the next chest");
            }
            else
            {
                Call("QuickStackIntoContainers", player, bag, container, false);
                Type sessionType = plugin.GetNestedType("AreaContainerTransferSession", BindingFlags.NonPublic)!;
                object session = Activator.CreateInstance(sessionType, true)!;
                sessionType.GetField("Player", Instance)!.SetValue(session, player);
                sessionType.GetField("PlayerInventory", Instance)!.SetValue(session, bag);
                FieldInfo action = sessionType.GetField("Action", Instance)!;
                action.SetValue(session, Enum.Parse(action.FieldType, "Restock"));
                Call("ExecuteAreaContainerTransfer", session, container);
                action.SetValue(session, Enum.Parse(action.FieldType, "QuickStack"));
                Call("ExecuteAreaContainerTransfer", session, container);
                sessionType.GetField("Targets", Instance)!.SetValue(session, new List<Container> { container });
                Write("_areaContainerTransfer", session);
                Call("ContinueAreaContainerTransfer");
                check(plugin.GetField("_areaContainerTransfer", Static)!.GetValue(null) == null,
                    "Tidy Pack: revoked area batch cancels before visiting the next chest");
                CheckMucCancellation(plugin, fixture, player, check);
            }
            check(quickCalls == 1, "Tidy Pack: locked nearby transfer cannot reach mutation; single-chest stack still can");
            check(restockCalls == 1, "Tidy Pack: restock still reaches its existing transfer path while locked");

            bool missing = false;
            try { Call("BindPathOfValheimanActionGate", typeof(PoVTidyPackChecks).Assembly); }
            catch (TargetInvocationException error) { missing = error.InnerException is MissingMethodException; }
            check(missing && !Allowed(), "Tidy Pack: installed PoV with missing API fails closed");
            int attempts = 0;
            Write("_povTidyPackAvailable", (Func<bool>)(() => { attempts++; throw new InvalidOperationException("fixture getter failure"); }));
            check(!Allowed() && !Allowed() && attempts == 1, "Tidy Pack: throwing getter locks actions without a per-frame exception loop");
            Call("ResetPathOfValheimanActionGate");
            check(Allowed(), "Tidy Pack: teardown releases the old plugin delegate/state");
        }
        finally { Call("ResetPathOfValheimanActionGate"); fixture.UnpatchSelf(); }
    }

    private static void CheckMucCancellation(Type plugin, Harmony fixture, Player player, Action<bool, string> check)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Static)!.Invoke(null, args);
        void Write(string name, object? value) => plugin.GetField(name, Static)!.SetValue(null, value);
        object? Read(string name) => plugin.GetField(name, Static)!.GetValue(null);
        object Create(string name) => Activator.CreateInstance(plugin.GetNestedType(name, BindingFlags.NonPublic)!, true)!;
        fixture.Patch(plugin.GetMethod("PruneMucSeedMemoryProtection", Static),
            transpiler: new HarmonyMethod(typeof(PoVTidyPackChecks), nameof(EmptyBody)));
        mono_add_internal_call("UnityEngine.Time::get_unscaledTime", Marshal.GetFunctionPointerForDelegate(TimeStub));
        object session = Create("MucTransferSession");
        FieldInfo action = session.GetType().GetField("Action", Instance)!;
        action.SetValue(session, Enum.Parse(action.FieldType, "QuickStack"));
        object pending = Create("MucTransferPending");
        object gate = pending.GetType().GetField("Gate", Instance)!.GetValue(pending)!;
        object? Gate(string name, params object[] args) => gate.GetType().GetMethod(name, Instance)!.Invoke(gate, args);
        bool State(string name) => (bool)gate.GetType().GetProperty(name, Instance)!.GetValue(gate)!;
        try
        {
            Gate("Begin", 1f); Gate("Register", -37);
            Write("_mucTransfer", session); Write("_mucTransferPending", pending);
            Call("TickMucTransfer", player);
            check(Read("_mucTransfer") == null && ReferenceEquals(Read("_mucTransferPending"), pending) && !State("ContinueBatch"),
                "Tidy Pack: node loss cancels MUC batch but retains its pending response");
            check((bool)Gate("BeginResponse", -37)!, "Tidy Pack: original late MUC response remains acceptable");
            Gate("FinishResponse", true);
            check(State("Completed") && !State("ContinueBatch"), "Tidy Pack: late completion cannot resume the revoked batch");
        }
        finally { Write("_mucTransfer", null); Write("_mucTransferPending", null); }
    }
}
