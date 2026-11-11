using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using HarmonyLib;

internal static class Program
{
    private static string[] folders = null!;
    private static int Main(string[] args)
    {
        // mod, original Managed, core, mode, optional mod DLLs...
        folders = new[] { args[1], args[2], Path.GetDirectoryName(args[0])! }
            .Concat(args.Skip(4).Select(p => Path.GetDirectoryName(p)!)).Distinct().ToArray();
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string folder in folders) { string p = Path.Combine(folder, name); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        return ChecksRunner.Run(args);
    }
}
internal static class ChecksRunner
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr function);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int HashCallback(IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int IntCallback();
    private static readonly HashCallback Hash = _ => 0;
    private static readonly IntCallback Offset = () => 0;
    private static void Observer() { }
    private static bool Skip() => false;
    internal static int Run(string[] args)
    {
        // BepInEx normally supplies these services before plugin static fields.
        // Only those engine boundaries are stubbed; game/mod method IL is original.
        var bep = Assembly.LoadFrom(Path.Combine(args[2], "BepInEx.dll"));
        string isolated = Path.Combine(Path.GetTempPath(), "recipe-row-smoke-" + Guid.NewGuid());
        bep.GetType("BepInEx.Paths", true)!.GetMethod("SetExecutablePath", Static)!.Invoke(null,
            new object[] { Path.Combine(isolated, "valheim.exe"), Path.Combine(isolated, "BepInEx"), args[1], new[] { args[1], args[2] } });
        Type threading = bep.GetType("BepInEx.ThreadingHelper", true)!;
        object queue = FormatterServices.GetUninitializedObject(threading);
        threading.GetField("_invokeLock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(queue, new object());
        threading.GetField("<Instance>k__BackingField", Static)!.SetValue(null, queue);
        mono_add_internal_call("UnityEngine.Animator::StringToHash_Injected", Marshal.GetFunctionPointerForDelegate(Hash));
        mono_add_internal_call("UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject", Marshal.GetFunctionPointerForDelegate(Offset));
        var mod = Assembly.LoadFrom(args[0]);
        var game = Assembly.LoadFrom(Path.Combine(args[1], "assembly_valheim.dll"));
        Type gui = game.GetType("InventoryGui", true)!;
        Type plugin = mod.GetType("InventorySlots.InventorySlotsPlugin", true)!;
        var shape = plugin.GetMethod("PathOfValheimanOverlayShape", Static)!;
        if (args[3] == "shapes")
        {
            // Relevant hooks and their direct UI/pairing helpers, not whole DLL hashes.
            var specs = new Dictionary<string, string[]> {
                ["PassiveTree.Loot.ItemAscension+UpgradeRow"] = new[] { "Prefix" },
                ["PassiveTree.Loot.ItemAscension+UpgradeList"] = new[] { "Prefix" },
                ["PassiveTree.Loot.ItemAscension+UpgradeListClean"] = new[] { "Postfix" },
                ["PassiveTree.Loot.ItemAscension"] = new[] { "Pairs", "UpgradeRecipe", "IsUpgradeClone" },
                ["PassiveTree.CraftSalvage+Gui_List"] = new[] { "Prefix" },
                ["PassiveTree.CraftSmith+Gui_List"] = new[] { "Prefix" },
                ["PassiveTree.UI.CraftCategories+Gui_List"] = new[] { "Postfix" },
                ["PassiveTree.UI.CraftCategories"] = new[] { "get_On", "Active", "Build", "Clear" },
                ["VeiledRecipes.InventoryGuiAddRecipeToListPatch"] = new[] { "Prefix", "Postfix", "FindRecipeElement", "MaskRecipeListElement" },
                ["VeiledRecipes.InventoryGuiUpdateRecipeListPatch"] = new[] { "Postfix" },
                ["VeiledRecipes.VeiledRecipeAaaCraftingCompat"] = new[] { "MaskRecipeList" },
                ["ServerDevcommands.LimitRecipesToStationPatch"] = new[] { "Prefix" },
                ["WarfareTweaks.InventoryGuiUpdateRecipeListWarfareThrowableUpgradePatch"] = new[] { "Prefix" },
                ["InteropFixes.Modules.PathOfValheimanLookup.OptionalTypeLookup"] = new[] { "Resolve", "FieldIfPresent" }
            };
            foreach (var path in args.Skip(4))
            {
                var assembly = Assembly.LoadFrom(path);
                foreach (var spec in specs)
                {
                    var type = assembly.GetType(spec.Key); if (type == null) continue;
                    foreach (string name in spec.Value)
                    {
                        var method = type.GetMethod(name, Static) ?? throw new Exception(spec.Key + "::" + name);
                        string value = (string)shape.Invoke(null, new object[] { method, PatchProcessor.GetOriginalInstructions(method) })!;
                        Console.WriteLine("[\"" + spec.Key + "::" + name + "\"] = \"" + value + "\", // " + method);
                    }
                }
            }
            return 0;
        }
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        Type reuse = mod.GetType("InventorySlots.CraftingRecipeRowReuse", true)!;
        foreach (var entry in new[] { ("UpdateRecipeList", "InventoryGuiRecipeRowReuseListPatch", "ListReady"), ("AddRecipeToList", "InventoryGuiRecipeRowReuseAddPatch", "RowReady") })
        {
            var original = AccessTools.Method(gui, entry.Item1);
            var patchType = mod.GetType("InventorySlots." + entry.Item2, true)!;
            var transform = patchType.GetMethod("Transpiler", Static)!;
            List<CodeInstruction> Apply(List<CodeInstruction> input) => ((IEnumerable<CodeInstruction>)transform.Invoke(null, new object[] { input })!).ToList();
            var before = PatchProcessor.GetOriginalInstructions(original).ToList();
            var after = Apply(before.Select(i => new CodeInstruction(i)).ToList());
            Check((bool)reuse.GetField(entry.Item3, Static)!.GetValue(null)!, entry.Item1 + " original IL recognized");
            var restored = new List<CodeInstruction>();
            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].opcode == OpCodes.Ldarg_0)
                {
                    if (i + 3 < after.Count && after[i + 3].operand is MethodInfo rent && rent.DeclaringType == reuse && rent.Name == "Rent")
                    {
                        restored.Add(before.Single(c => c.operand is MethodInfo m && m.Name == "Instantiate")); i += 3; continue;
                    }
                    if (i + 1 < after.Count && after[i + 1].operand is MethodInfo m && m.DeclaringType == reuse)
                    {
                        if (m.Name == "Release") restored.Add(before.Single(c => c.operand is MethodInfo a && a.Name == "Destroy"));
                        else if (m.Name == "Bind") restored.Add(before.Single(c => c.operand is MethodInfo a && a.Name == "AddListener"));
                        else if (m.Name != "Begin" && m.Name != "End" && m.Name != "CompleteRow") throw new Exception(m.Name);
                        i++; continue;
                    }
                }
                restored.Add(after[i]);
            }
            Check(restored.Count == before.Count && restored.Zip(before, (a,b) => a.opcode == b.opcode && Equals(a.operand,b.operand)).All(x=>x),
                entry.Item1 + " all other native instructions/operands preserved");
            Check(before.Where(c => c.labels.Count != 0).SelectMany(c => c.labels).All(l => after.Any(c=>c.labels.Contains(l))), "branch destinations retained");
            var missing = before.Where(c => !(c.operand is MethodInfo m && m.Name == (entry.Item1 == "UpdateRecipeList" ? "Destroy" : "AddListener"))).Select(c=>new CodeInstruction(c)).ToList();
            var rejected = Apply(missing);
            Check(!(bool)reuse.GetField(entry.Item3, Static)!.GetValue(null)! && rejected.SequenceEqual(missing), "unknown native body falls back without partial injection");
            // Compile the real Harmony replacement using the game's bundled Mono
            // and unmodified game assemblies. Do not execute any Unity UI methods.
            var harmony = new Harmony("smoke.recipe-row-reuse");
            try { harmony.CreateClassProcessor(patchType).Patch(); Check(true, entry.Item1 + " Harmony replacement compiled on game Mono"); }
            finally { harmony.UnpatchSelf(); }
        }
        Check(gui.GetField("m_availableRecipes", BindingFlags.NonPublic | BindingFlags.Instance)!.IsPrivate,
            "original private recipe list retained; reuse never directly accesses it");
        if (args.Length > 4) CheckHooks(mod, gui, args.Skip(4).Select(Assembly.LoadFrom).ToArray(), Check);
        Console.WriteLine(checks + " original-IL / game-Mono patch compilation checks passed. No Unity rendering or game session executed.");
        return 0;
    }

    private static void CheckHooks(Assembly mod, Type gui, Assembly[] assemblies, Action<bool, string> check)
    {
        Type gate = mod.GetType("InventorySlots.CraftingRecipeRowReuseHooks", true)!;
        var permit = gate.GetMethod("Permit", Static)!;
        bool Permit() => (bool)permit.Invoke(null, null)!;
        var known = new Dictionary<string, string[]> {
            ["treextr.pathofvalheiman"] = new[] { "PassiveTree.Loot.ItemAscension+UpgradeRow", "PassiveTree.Loot.ItemAscension+UpgradeList", "PassiveTree.Loot.ItemAscension+UpgradeListClean", "PassiveTree.CraftSalvage+Gui_List", "PassiveTree.CraftSmith+Gui_List", "PassiveTree.UI.CraftCategories+Gui_List" },
            ["sighsorry.VeiledRecipes"] = new[] { "VeiledRecipes.InventoryGuiAddRecipeToListPatch", "VeiledRecipes.InventoryGuiUpdateRecipeListPatch" },
            ["server_devcommands"] = new[] { "ServerDevcommands.LimitRecipesToStationPatch" },
            ["sighsorry.WarfareTweaks"] = new[] { "WarfareTweaks.InventoryGuiUpdateRecipeListWarfareThrowableUpgradePatch" }
        };
        var installed = new List<Harmony>();
        var own = new Harmony("sighsorry.InventorySlots");
        void InstallOwn()
        {
            own.CreateClassProcessor(mod.GetType("InventorySlots.InventoryGuiRecipeRowReuseListPatch", true)!).Patch();
            own.CreateClassProcessor(mod.GetType("InventorySlots.InventoryGuiRecipeRowReuseAddPatch", true)!).Patch();
        }
        var extra = new Harmony("smoke.unknown-row-decorator");
        var observer = new HarmonyMethod(typeof(ChecksRunner), nameof(Observer));
        try
        {
            InstallOwn();
            check(Permit(), "no optional mods: native reuse allowed");
            foreach (var group in known)
            {
                var h = new Harmony(group.Key); installed.Add(h);
                foreach (string name in group.Value)
                {
                    Type type = assemblies.Select(a => a.GetType(name)).First(t => t != null)!;
                    h.CreateClassProcessor(type).Patch();
                }
                check(Permit(), "actual audited hooks accepted: " + group.Key);
            }
            own.UnpatchSelf(); InstallOwn();
            check(Permit(), "own replacements compile before and after optional hook registration");
            CheckEffectiveHooks(mod, gui, assemblies, installed[0], own, InstallOwn, Permit, check);
            var original = AccessTools.Method(gui, "UpdateRecipeList");
            extra.Patch(original, postfix: observer);
            check(!Permit(), "late unknown list decorator disables reuse");
            extra.UnpatchSelf();
            check(Permit(), "removing unknown decorator permits reuse again");
            var helper = assemblies.Select(a=>a.GetType("VeiledRecipes.InventoryGuiAddRecipeToListPatch")).First(t=>t!=null)!.GetMethod("MaskRecipeListElement", Static)!;
            extra.Patch(helper, prefix: observer);
            check(!Permit(), "patch on a known helper disables reuse");
            extra.UnpatchSelf();
            check(Permit(), "helper unpatch recovers without touching gameplay patches");
            // The same known type/body under a different patch kind is not trusted.
            var knownPrefix = assemblies.Select(a=>a.GetType("PassiveTree.CraftSmith+Gui_List")).First(t=>t!=null)!.GetMethod("Prefix", Static)!;
            var hPov = installed[0];
            hPov.Patch(original, postfix: new HarmonyMethod(knownPrefix));
            check(!Permit(), "known method registered as wrong patch kind is rejected");
            hPov.Unpatch(original, HarmonyPatchType.Postfix, hPov.Id);
            check(Permit(), "correct known hook arrangement recovers");
            var shapes = (Dictionary<string,string>)gate.GetField("Shapes", Static)!.GetValue(null)!;
            var shape = mod.GetType("InventorySlots.InventorySlotsPlugin", true)!.GetMethod("PathOfValheimanOverlayShape", Static)!;
            var code = PatchProcessor.GetOriginalInstructions(helper).ToList();
            string Shape(List<CodeInstruction> il) => (string)shape.Invoke(null, new object[] { helper, il })!;
            string before = Shape(code);
            code.Insert(0, new CodeInstruction(OpCodes.Nop));
            check(Shape(code) == before, "nonsemantic NOP does not change reviewed shape");
            code.First(i=>i.opcode == OpCodes.Ldc_I4_1).opcode = OpCodes.Ldc_I4_0;
            check(Shape(code) != before, "changed known UI body is distinguished");
            // Cached contract starts from the actual body, never version/MVID.
            check(shapes[helper.DeclaringType!.FullName + "::" + helper.Name] == before, "installed helper matches checked original contract");
        }
        finally { extra.UnpatchSelf(); foreach (var h in installed) h.UnpatchSelf(); own.UnpatchSelf(); }
    }

    private static int inspections;
    private static IEnumerable<CodeInstruction> NoOp(IEnumerable<CodeInstruction> instructions)
    { inspections++; return instructions; }
    private static IEnumerable<CodeInstruction> ChangedBody(IEnumerable<CodeInstruction> instructions)
    {
        var result = instructions.Select(c => new CodeInstruction(c)).ToList();
        result.Insert(0, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChecksRunner), nameof(Observer))));
        return result;
    }
    private static IEnumerable<CodeInstruction> ChangedCleanup(IEnumerable<CodeInstruction> instructions)
    {
        // Keep the stack balanced and the method executable, but remove destruction.
        // Lookup compatibility must not legitimize this lifecycle change.
        return instructions.Select(c => {
            var copy = new CodeInstruction(c);
            if (c.operand is MethodInfo m && m.DeclaringType?.FullName == "UnityEngine.Object" && m.Name == "Destroy")
            { copy.opcode = OpCodes.Pop; copy.operand = null; }
            return copy;
        }).ToList();
    }

    private static void CheckEffectiveHooks(Assembly mod, Type gui, Assembly[] assemblies, Harmony pov,
        Harmony own, Action installOwn, Func<bool> permit, Action<bool, string> check)
    {
        Type Find(string name) => assemblies.Select(a => a.GetType(name)).First(t => t != null)!;
        var cleanup = Find("PassiveTree.Loot.ItemAscension+UpgradeListClean").GetMethod("Postfix", Static)!;
        var categories = Find("PassiveTree.UI.CraftCategories+Gui_List");
        var build = Find("PassiveTree.UI.CraftCategories").GetMethod("Build", Static)!;
        var original = AccessTools.Method(gui, "UpdateRecipeList");
        var changes = new Harmony("smoke.contract-transform");
        var lookupPatches = new Harmony("sighsorry.InteropFixes");
        var observer = new HarmonyMethod(typeof(ChecksRunner), nameof(Observer));
        try
        {
            changes.Patch(cleanup, transpiler: new HarmonyMethod(typeof(ChecksRunner), nameof(NoOp)));
            check(permit(), "body-preserving transpiler from unlisted owner is accepted on reviewed helper");
            int cached = inspections;
            check(Enumerable.Range(0, 20).All(_ => permit()), "unchanged contract remains valid over repeated rebuilds");
            check(inspections == cached, "unchanged patch set does not rerun IL inspection/transpilers");
            changes.UnpatchSelf();
            check(permit(), "transpiler removal invalidates caller cache");

            pov.Unpatch(original, categories.GetMethod("Postfix", Static)!);
            changes.Patch(build, prefix: observer);
            check(permit(), "unregistered hook's unrelated helper no longer disables reuse");
            pov.CreateClassProcessor(categories).Patch();
            check(!permit(), "registering hook rechecks its affected helper");
            changes.UnpatchSelf();
            check(permit(), "registered helper recovers after wrapper removal");

            Type? interop = assemblies.Select(a => a.GetType("InteropFixes.Modules.PathOfValheimanLookup.OptionalLookupPatch")).FirstOrDefault(t => t != null);
            if (interop == null) return;
            var transpiler = new HarmonyMethod(interop.GetMethod("Transpiler", Static)!) { priority = Priority.Last };
            lookupPatches.Patch(cleanup, transpiler: transpiler);
            check((bool)interop.GetMethod("WasAccepted", Static)!.Invoke(null, new object[] { cleanup })!, "actual InteropFixes lookup patch accepts PoV cleanup");
            var calls = PatchProcessor.GetCurrentInstructions(cleanup).Where(c => c.operand is MethodInfo m &&
                m.DeclaringType?.FullName == "InteropFixes.Modules.PathOfValheimanLookup.OptionalTypeLookup").ToArray();
            check(calls.Length == 2, "actual effective cleanup contains both InteropFixes lookup substitutions");
            check(permit(), "InteropFixes lookup substitutions preserve recipe row reuse");
            own.UnpatchSelf(); installOwn();
            check(permit(), "reuse installed after InteropFixes is accepted");
            lookupPatches.UnpatchSelf();
            check(permit(), "InteropFixes removal recovers original cleanup contract");
            lookupPatches.Patch(cleanup, transpiler: transpiler);
            check(permit(), "InteropFixes installed after reuse is accepted");

            changes.Patch(cleanup, prefix: new HarmonyMethod(typeof(ChecksRunner), nameof(Skip)));
            check(!permit(), "execution wrapper rejected even when lookup-normalized body matches");
            changes.UnpatchSelf();
            check(permit(), "wrapper removal restores reuse alongside InteropFixes");
            changes.Patch(cleanup, transpiler: new HarmonyMethod(typeof(ChecksRunner), nameof(ChangedCleanup)) { priority = Priority.Last, after = new[] { lookupPatches.Id } });
            check(!permit(), "changed row destruction rejected alongside valid lookup substitutions");
            changes.UnpatchSelf();
            check(permit(), "restoring cleanup restores reuse");
            var resolver = Find("InteropFixes.Modules.PathOfValheimanLookup.OptionalTypeLookup").GetMethod("Resolve", Static)!;
            changes.Patch(resolver, prefix: observer);
            check(!permit(), "late lookup-helper wrapper rejected without caller patch changes");
            changes.UnpatchSelf();
            check(permit(), "lookup-helper unpatch restores cached caller eligibility");
            var fieldLookup = Find("InteropFixes.Modules.PathOfValheimanLookup.OptionalTypeLookup").GetMethod("FieldIfPresent", Static)!;
            changes.Patch(fieldLookup, transpiler: new HarmonyMethod(typeof(ChecksRunner), nameof(ChangedBody)));
            check(!permit(), "changed lookup helper body is rejected without caller patch changes");
            changes.UnpatchSelf();
            check(permit(), "lookup helper body restoration permits reuse");
            // InventorySlots' reviewed category suppression is the one accepted wrapper.
            var on = Find("PassiveTree.UI.CraftCategories").GetMethod("get_On", Static)!;
            changes.Patch(on, postfix: new HarmonyMethod(mod.GetType("InventorySlots.InventorySlotsPlugin", true)!.GetMethod("SuppressPathOfValheimanCraftCategories", Static)!));
            check(permit(), "exact existing InventorySlots category suppression remains supported");
        }
        finally { changes.UnpatchSelf(); lookupPatches.UnpatchSelf(); }
    }
}
