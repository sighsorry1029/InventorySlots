using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] Classes = { "Player_Load_InventorySize_Patch", "Player_OnSpawned_InventorySize_Patch", "Player_SetInventorySize_Patch", "Inventory_MoveAll_Patch", "Inventory_StackAll_Patch", "Player_HaveRequirementItems_Transpiler", "Player_AutoPickup_Transpiler" };
    private static int checks;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeInt();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeHash(IntPtr span);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate float NativeTime();
    private static readonly NativeInt Zero = () => 0;
    private static readonly NativeHash Hash = _ => 0;
    private static float fixtureTime = 1f;
    private static readonly NativeTime Time = () => fixtureTime;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);
    private static bool Equality(UnityEngine.Object lhs, UnityEngine.Object rhs, ref bool __result)
    { __result = ReferenceEquals(lhs, rhs); return false; }
    private static bool NoLog() => false;
    private static int Main(string[] args)
    {
        if (args.Length != 5) throw new ArgumentException("InventorySlots.dll ValheimPlus.dll original-Managed BepInEx-core shapes|test|rebuilt|changed-rows|changed-merge|changed-stack|changed-crafting");
        string[] roots = { args[2], args[3], Path.GetDirectoryName(args[0])!, Path.GetDirectoryName(args[1])! };
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) => {
            string file = new AssemblyName(request.Name).Name + ".dll";
            foreach (string root in roots) { string path = Path.Combine(root, file); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        try { return Run(args); }
        catch (Exception error) { System.Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        string isolated = Path.Combine(Path.GetTempPath(), "vplus-smoke-" + Guid.NewGuid());
        typeof(BepInEx.Paths).GetMethod("SetExecutablePath", Statics)!.Invoke(null,
            new object[] { Path.Combine(isolated, "valheim.exe"), Path.Combine(isolated, "BepInEx"), args[2], new[] { args[2], args[3] } });
        Type threading = typeof(BepInEx.Paths).Assembly.GetType("BepInEx.ThreadingHelper", true)!;
        object queue = FormatterServices.GetUninitializedObject(threading);
        AccessTools.Field(threading, "_invokeLock").SetValue(queue, new object());
        AccessTools.Field(threading, "<Instance>k__BackingField").SetValue(null, queue);
        Assembly slots = Assembly.LoadFrom(args[0]);
        Check(!slots.GetReferencedAssemblies().Any(a => a.Name == "ValheimPlus"), "V+ remains optional");
        Type compat = slots.GetType("InventorySlots.ValheimPlusCompat", true)!;
        Assembly vplus = args[4] == "rebuilt" || args[4].StartsWith("changed-", StringComparison.Ordinal)
            ? FixtureVariants.Create(args, Classes) : Assembly.LoadFrom(args[1]);
        Type[] types = Classes.Select(n => vplus.GetType("ValheimPlus.GameClasses." + n, true)!).ToArray();
        if (args[4] == "shapes") {
            foreach (Type type in types) System.Console.WriteLine(type.Name + "=" + compat.GetMethod("BehaviorShape", Statics)!.Invoke(null, new object[] { type }));
            return 0;
        }
        mono_add_internal_call("UnityEngine.Animator::StringToHash_Injected", Marshal.GetFunctionPointerForDelegate(Hash));
        mono_add_internal_call("UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject", Marshal.GetFunctionPointerForDelegate(Zero));
        mono_add_internal_call("UnityEngine.Time::get_time", Marshal.GetFunctionPointerForDelegate(Time));
        mono_add_internal_call("UnityEngine.Time::get_unscaledTime", Marshal.GetFunctionPointerForDelegate(Time));
        mono_add_internal_call("UnityEngine.Time::get_frameCount", Marshal.GetFunctionPointerForDelegate(Zero));
        mono_add_internal_call("UnityEngine.SystemInfo::GetGraphicsDeviceType", Marshal.GetFunctionPointerForDelegate(Zero));
        mono_add_internal_call("UnityEngine.Application::get_isBatchMode", Marshal.GetFunctionPointerForDelegate(Zero));
        var seams = new Harmony("smoke.engine");
        seams.Patch(AccessTools.Method(typeof(UnityEngine.Object), "CompareBaseObjects"), prefix: new HarmonyMethod(typeof(Program), nameof(Equality)));
        seams.Patch(AccessTools.Method(typeof(ZLog), "Log", new[] { typeof(object) }), prefix: new HarmonyMethod(typeof(Program), nameof(NoLog)));
        seams.Patch(AccessTools.Method(typeof(UnityEngine.Debug).Assembly.GetType("UnityEngine.DebugLogHandler"), "LogFormat",
            new[] { typeof(UnityEngine.LogType), typeof(UnityEngine.Object), typeof(string), typeof(object[]) }), prefix: new HarmonyMethod(typeof(Program), nameof(NoLog)));
        var config = new ConfigFile(Path.Combine(isolated, "vplus.cfg"), false) { SaveOnConfigSet = false };
        Type inventoryConfig = vplus.GetType("ValheimPlus.Configurations.Sections.InventoryConfiguration", true)!;
        object inventorySettings = Activator.CreateInstance(inventoryConfig)!;
        inventoryConfig.GetMethod("Bind")!.Invoke(inventorySettings, new object[] { config });
        Type configuration = vplus.GetType("ValheimPlus.Configurations.Configuration", true)!;
        object current = Activator.CreateInstance(configuration)!;
        configuration.GetProperty("Inventory")!.SetValue(current, inventorySettings);
        configuration.GetProperty("Current")!.SetValue(null, current);
        foreach (string section in new[] { "AutoStack", "CraftFromChest", "Player" })
        {
            Type settingsType = vplus.GetType("ValheimPlus.Configurations.Sections." + section + "Configuration", true)!;
            object settings = Activator.CreateInstance(settingsType)!;
            settingsType.GetMethod("Bind")!.Invoke(settings, new object[] { config });
            configuration.GetProperty(section)!.SetValue(current, settings);
        }
        config[new ConfigDefinition("AutoStack", "enabled")].BoxedValue = true;
        config[new ConfigDefinition("CraftFromChest", "enabled")].BoxedValue = true;
        config[new ConfigDefinition("Inventory", "enabled")].BoxedValue = true;
        config[new ConfigDefinition("Inventory", "mergeWithExistingStacks")].BoxedValue = true;
        config[new ConfigDefinition("Inventory", "playerInventoryRows")].BoxedValue = 9;
        var native = new Harmony("mod.valheim_plus");
        foreach (Type type in types) native.CreateClassProcessor(type).Patch();
        MethodInfo resize = AccessTools.DeclaredMethod(typeof(Player), "SetInventorySize", new[] { typeof(int) });
        MethodInfo atLeast = types[2].GetMethod("AtLeastConfigured", Statics)!;
        int RowCalls() => PatchProcessor.GetCurrentInstructions(resize).Count(c => c.Calls(atLeast));
        bool Active(string name) => (bool)compat.GetProperty(name, Statics)!.GetValue(null)!;
        var warnings = new List<string>();
        void Install() => compat.GetMethod("Install", Statics)!.Invoke(null, new object[] { vplus, (Action<string>)(s => { warnings.Add(s); System.Console.WriteLine(s); }) });
        void Dispose() => compat.GetMethod("Dispose", Statics)!.Invoke(null, null);
        if (args[4].StartsWith("changed-", StringComparison.Ordinal))
        {
            Install();
            Check(Active("RowsActive") == (args[4] != "changed-rows") &&
                  Active("MergeActive") == (args[4] != "changed-merge") &&
                  Active("StackActive") == (args[4] != "changed-stack") &&
                  Active("CraftingAvailable") == (args[4] != "changed-crafting") && warnings.Count == 1,
                "changed relevant body refuses only the affected feature group");
            Check(warnings[0].Contains("Relevant patch behavior changed"), "rejection identifies behavior rather than assembly version");
            Dispose(); native.UnpatchSelf(); seams.UnpatchSelf();
            System.Console.WriteLine($"{checks} checks passed; isolated changed-contract fixture.");
            return 0;
        }
        Check(RowCalls() == 2, "baseline native resize has V+ minimum-row transforms");
        var player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        var bag = new Inventory("player", null, 8, 4);
        AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, bag);
        AccessTools.Field(typeof(Player), "m_localPlayer").SetValue(null, player);
        var gui = (InventoryGui)FormatterServices.GetUninitializedObject(typeof(InventoryGui));
        AccessTools.Field(typeof(InventoryGui), "m_instance").SetValue(null, gui);
        MethodInfo load = AccessTools.DeclaredMethod(typeof(Player), "Load", new[] { typeof(ZPackage) });
        MethodInfo spawn = AccessTools.DeclaredMethod(typeof(Player), "OnSpawned", new[] { typeof(bool) });
        // Skip only engine-dependent native lifecycle bodies. V+ hooks themselves
        // execute against real managed inventory and a captured UI size setter.
        seams.Patch(load, prefix: new HarmonyMethod(typeof(Program), nameof(NoLog)));
        seams.Patch(spawn, prefix: new HarmonyMethod(typeof(Program), nameof(NoLog)));
        seams.Patch(AccessTools.Method(typeof(InventoryGui), "SetInventorySize", new[] { typeof(int) }), prefix: new HarmonyMethod(typeof(Program), nameof(CapturePanelSize)));
        load.Invoke(player, new object[] { null! });
        Check(bag.GetHeight() == 9, "warmed native Load wrapper runs V+ row hook");
        bag.SetHeight(15); spawn.Invoke(player, new object[] { false });
        Check(panelRows == 15, "baseline spawn treats hidden storage rows as native visible rows");
        // Warm the compiled original wrapper before installing guards.
        var baseline = Pair("different", "identity");
        baseline.to.MoveAll(baseline.from);
        Check(baseline.destination.m_stack == 5 && baseline.from.NrOfItems() == 0, "baseline V+ premerge bypasses different metadata");
        Install();
        Check(warnings.Count == 0 && Active("RowsActive") && Active("MergeActive"), "both verified protections installed");
        bag.SetHeight(4); panelRows = -1;
        load.Invoke(player, new object[] { null! }); spawn.Invoke(player, new object[] { false });
        Check(bag.GetHeight() == 4 && panelRows == -1, "guards reach already-warmed Load and OnSpawned wrapper calls");
        AccessTools.Field(typeof(Player), "m_localPlayer").SetValue(null, null);
        AccessTools.Field(typeof(InventoryGui), "m_instance").SetValue(null, null);
        Check(RowCalls() == 0, "already-installed resize wrapper rebuilt without V+ row inflation");
        int nativePatchCount = Harmony.GetPatchInfo(resize).Transpilers.Count;
        Install();
        Check(Harmony.GetPatchInfo(resize).Transpilers.Count == nativePatchCount, "repeat install adds no native patch registrations");
        // Invoke the native original AddItem bodies with the real IS metadata hooks.
        var own = new Harmony("smoke.slots");
        foreach (string name in new[] { "InventoryAddItemDataPatch", "InventoryAddItemXyPatch", "InventoryAddItemPosPatch", "InventoryFindFreeStackItemPatch", "PlayerNativeInventorySizePatch" })
            own.CreateClassProcessor(slots.GetType("InventorySlots." + name, true)!).Patch();
        Check(RowCalls() == 0, "IS row transpiler coexists with guarded V+ transpiler");
        var separate = Pair("different", "identity");
        separate.to.MoveAll(separate.from);
        Check(separate.destination.m_stack == 2 && separate.to.NrOfItems() == 2 && separate.from.NrOfItems() == 0, "different metadata stays separate through native MoveAll");
        var same = Pair("same", "same");
        same.to.MoveAll(same.from);
        Check(same.destination.m_stack == 5 && same.to.NrOfItems() == 1 && same.from.NrOfItems() == 0, "identical metadata merges normally");
        var full = Pair("different", "identity", 1);
        full.to.MoveAll(full.from);
        Check(full.destination.m_stack == 2 && full.from.GetAllItems().Single().m_stack == 3, "full destination preserves rejected source");
        var partial = Pair("same", "same", 1); partial.destination.m_stack = 19;
        partial.to.MoveAll(partial.from);
        Check(partial.destination.m_stack == 20 && partial.from.GetAllItems().Single().m_stack == 2, "partial merge preserves total quantity at max stack");
        int ownerCalls = 0;
        Type policy = slots.GetType("InventorySlots.StackMetadataPolicy", true)!;
        policy.GetMethod("Register", Statics, null, new[] { typeof(string), typeof(Func<string,string,string>), typeof(Func<string,string,bool>) }, null)!.Invoke(null,
            new object[] { "expiry", (Func<string,string,string>)((a,b) => { ownerCalls++; return Math.Min(int.Parse(a), int.Parse(b)).ToString(); }), (Func<string,string,bool>)((a,b) => true) });
        var expiry = Pair("same", "same");
        expiry.destination.m_customData["expiry"] = "20";
        expiry.from.GetAllItems()[0].m_customData["expiry"] = "10";
        expiry.to.MoveAll(expiry.from);
        Check(expiry.destination.m_stack == 5 && expiry.destination.m_customData["expiry"] == "10" && ownerCalls > 0, "native transfer preserves owner metadata composition");
        var expiryPartial = Pair("same", "same", 1); expiryPartial.destination.m_stack = 18;
        expiryPartial.destination.m_customData["expiry"] = "20";
        expiryPartial.from.GetAllItems()[0].m_customData["expiry"] = "10";
        expiryPartial.to.MoveAll(expiryPartial.from);
        Check(expiryPartial.destination.m_stack == 20 && expiryPartial.destination.m_customData["expiry"] == "10" &&
              expiryPartial.from.GetAllItems().Single().m_stack == 1 && expiryPartial.from.GetAllItems()[0].m_customData["expiry"] == "10",
              "partial merge composes destination metadata and retains source quantity/metadata");
        config[new ConfigDefinition("Inventory", "enabled")].BoxedValue = false;
        var disabled = Pair("same", "same"); disabled.to.MoveAll(disabled.from);
        Check(disabled.destination.m_stack == 5, "disabled V+ Inventory retains normal merge");
        config[new ConfigDefinition("Inventory", "enabled")].BoxedValue = true;
        // Reproduce V+'s owner-only unpatch/repatch, including a new native wrapper.
        native.UnpatchSelf();
        foreach (Type type in types) native.CreateClassProcessor(type).Patch();
        Check(RowCalls() == 0, "V+ configuration repatch cannot restore row inflation");
        var repatched = Pair("different", "identity"); repatched.to.MoveAll(repatched.from);
        Check(repatched.destination.m_stack == 2 && repatched.to.NrOfItems() == 2, "V+ configuration repatch cannot bypass metadata");
        Check((int)config[new ConfigDefinition("Inventory", "playerInventoryRows")].BoxedValue == 9 &&
              (bool)config[new ConfigDefinition("Inventory", "mergeWithExistingStacks")].BoxedValue, "V+ configuration values remain unchanged");
        own.UnpatchSelf(); Dispose();
        Check(!Active("RowsActive") && !Active("MergeActive") && RowCalls() == 2, "teardown restores original row wrapper");
        var restored = Pair("different", "identity"); restored.to.MoveAll(restored.from);
        Check(restored.destination.m_stack == 5, "teardown restores original V+ premerge");
        // Missing native hook rejects only its own feature group.
        native.Unpatch(AccessTools.DeclaredMethod(typeof(Player), "Load", new[] { typeof(ZPackage) }), HarmonyPatchType.Prefix, native.Id);
        Install();
        Check(!Active("RowsActive") && Active("MergeActive") && warnings.Count == 1, "partial row registration refuses rows but keeps metadata protection");
        Dispose(); warnings.Clear(); native.CreateClassProcessor(types[0]).Patch();
        native.Unpatch(AccessTools.DeclaredMethod(typeof(Inventory), "MoveAll", new[] { typeof(Inventory) }), HarmonyPatchType.Prefix, native.Id);
        Install();
        Check(Active("RowsActive") && !Active("MergeActive") && warnings.Count == 1, "missing merge registration refuses merge but keeps row protection");
        Dispose(); native.UnpatchSelf();
        ExtendedChecks(slots, vplus, compat, types, native, seams, config, configuration, current);
        seams.UnpatchSelf();
        System.Console.WriteLine($"{checks} checks passed; original Mono/game assemblies, no Unity scene or networking.");
        return 0;
    }

    private static int panelRows;
    private static bool CapturePanelSize(int __0) { panelRows = __0; return false; }

    private static (Inventory to, Inventory from, ItemData destination) Pair(string a, string b, int width = 2)
    {
        var to = new Inventory("to", null, width, 1);
        var from = new Inventory("from", null, 2, 1);
        ItemData Item(int count, string identity) => new ItemData {
            m_shared = new ItemData.SharedData { m_name = "fixture", m_maxStackSize = 20 },
            m_stack = count, m_quality = 1, m_gridPos = new Vector2i(0,0),
            m_customData = new Dictionary<string,string> { ["identity"] = identity }
        };
        ItemData destination = Item(2, a);
        to.GetAllItems().Add(destination); from.GetAllItems().Add(Item(3, b));
        return (to, from, destination);
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        checks++; System.Console.WriteLine("PASS " + label);
    }
}

// Keep Cecil-dependent compiler-generated delegate fields out of Program's .cctor;
// the original Mono host installs AssemblyResolve inside Program.Main first.
internal static class FixtureVariants
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Assembly Create(string[] args, string[] classes)
    {
        // Mutate a private in-memory V+ test copy only. Never rewrite game or
        // installed mod binaries. Game access restrictions remain original.
        var resolver = new Mono.Cecil.DefaultAssemblyResolver();
        resolver.AddSearchDirectory(args[2]); resolver.AddSearchDirectory(args[3]);
        using (var module = Mono.Cecil.ModuleDefinition.ReadModule(args[1], new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver }))
        {
            module.Mvid = Guid.NewGuid();
            module.Assembly.Name.Version = new System.Version(99, 1, 2, 3);
            module.Assembly.Name.Name += ".Smoke." + args[4];
            foreach (string name in classes)
            {
                var type = module.Types.Single(t => t.Name == name);
                var method = type.Methods.First(m => m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Transpiler");
                method.Body.Instructions.Insert(0, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Nop));
                if ((args[4] == "changed-rows" && name == classes[0]) || (args[4] == "changed-merge" && name == classes[3]))
                    method.Body.Instructions.Insert(0, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
                string helper = args[4] == "changed-stack" && name == classes[4] ? "IsStackAllIntoOpenChest" :
                    args[4] == "changed-crafting" && name == classes[5] ? "ComputeItemQuantity" : "";
                if (helper.Length != 0)
                {
                    var body = type.Methods.Single(m => m.Name == helper).Body;
                    body.Instructions.Insert(0, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
                    body.Instructions.Insert(0, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0));
                }
            }
            using (var stream = new MemoryStream()) { module.Write(stream); return Assembly.Load(stream.ToArray()); }
        }
    }

}
