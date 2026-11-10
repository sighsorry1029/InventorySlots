using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;

internal static class OverlayLayerChecks
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    private static int prefixes, postfixes, finalizers;
    private static void Before() => prefixes++;
    private static void After() => postfixes++;
    private static Exception? Finally(Exception? __exception) { finalizers++; return __exception; }
    private static bool Visible(ref bool __result) { __result = true; return false; }

    internal static void Run(Type plugin, Assembly pov, Action<bool, string> check)
    {
        MethodInfo target = pov.GetType("PassiveTree.UI.InventoryOverlayLayers", true)!.GetMethod("Refresh", Static)!;
        MethodInfo shape = plugin.GetMethod("PathOfValheimanOverlayShape", Static)!;
        MethodInfo transform = plugin.GetMethod("SkipIdlePathOfValheimanOverlayScan", Static)!;
        string expected = (string)plugin.GetField("PovOverlayRefreshShape", Static)!.GetRawConstantValue()!;
        var original = PatchProcessor.GetOriginalInstructions(target).ToList();
        string Shape(IEnumerable<CodeInstruction> code) => (string)shape.Invoke(null, new object[] { target, code })!;
        string actual = Shape(original);
        System.Console.WriteLine("PoV overlay Refresh shape=" + actual);
        check(actual == expected, "reviewed original PoV Refresh behavior shape");
        check(target.IsAssembly && target.IsStatic && target.ReturnType == typeof(void), "overlay uses original internal static method");
        check(AccessTools.DeclaredField(typeof(InventoryGui), "m_dragGo").IsPrivate, "drag field remains private in original game; injected code reuses native locals");
        var nop = original.Select(i => new CodeInstruction(i)).ToList(); nop.Insert(0, new CodeInstruction(OpCodes.Nop));
        check(Shape(nop) == actual, "nonsemantic NOP/label offset differences do not disable overlay guard");
        var changed = original.Select(i => new CodeInstruction(i)).ToList();
        changed.First(i => i.opcode == OpCodes.Ldc_I4_2).opcode = OpCodes.Ldc_I4_3;
        check(Shape(changed) != actual, "changed native layer behavior is rejected");
        var generator = new DynamicMethod("overlayFixture", typeof(void), new[] { typeof(InventoryGui) }).GetILGenerator();
        var rejected = ((IEnumerable<CodeInstruction>)transform.Invoke(null, new object[] { changed, generator, target })!).ToList();
        check(rejected.SequenceEqual(changed), "foreign changed transpiler input is left untouched");
        var patched = ((IEnumerable<CodeInstruction>)transform.Invoke(null, new object[] { original, generator, target })!).ToList();
        int at = original.FindIndex(i => i.opcode == OpCodes.Stloc_1) + 1;
        check(patched.Count == original.Count + 5 && patched.Take(at).SequenceEqual(original.Take(at)) &&
            patched.Skip(at + 5).SequenceEqual(original.Skip(at)), "all native instructions are retained around the five-instruction idle guard");
        check(patched[at].opcode == OpCodes.Ldloc_0 && patched[at + 1].opcode == OpCodes.Ldloc_1 &&
            patched[at + 3].opcode == OpCodes.Brtrue && patched[at + 4].opcode == OpCodes.Ret &&
            original[at].labels.Contains((Label)patched[at + 3].operand), "active overlay branches into the entire unchanged native scan");

        var own = new Harmony("smoke.pov.overlay-idle");
        var observer = new Harmony("smoke.pov.overlay-observer");
        var engine = new Harmony("smoke.pov.overlay-engine");
        try
        {
            own.Patch(target, transpiler: new HarmonyMethod(transform) { priority = Priority.Last });
            // This observer remains registered through Harmony's subsequent body
            // rebuild; an in-body return must not suppress its prefix/finalizer.
            observer.Patch(target, prefix: new HarmonyMethod(typeof(OverlayLayerChecks), nameof(Before)),
                postfix: new HarmonyMethod(typeof(OverlayLayerChecks), nameof(After)),
                finalizer: new HarmonyMethod(typeof(OverlayLayerChecks), nameof(Finally)));
            engine.Patch(AccessTools.Method(typeof(InventoryGui), "IsVisible"), prefix: new HarmonyMethod(typeof(OverlayLayerChecks), nameof(Visible)));
            var gui = (InventoryGui)FormatterServices.GetUninitializedObject(typeof(InventoryGui));
            target.Invoke(null, new object?[] { null });
            target.Invoke(null, new object[] { gui }); // split/drag are null; no native Canvas engine call is reachable.
            check(prefixes == 2 && postfixes == 2 && finalizers == 2, "actual original Mono method returns safely with no overlays and retains other hooks");
            own.UnpatchSelf();
            check(Harmony.GetPatchInfo(target)!.Prefixes.Any(p => p.owner == observer.Id) &&
                !Harmony.GetPatchInfo(target)!.Transpilers.Any(p => p.owner == own.Id), "removing optimization retains foreign observers");
        }
        finally { own.UnpatchSelf(); observer.UnpatchSelf(); engine.UnpatchSelf(); }
    }
}
