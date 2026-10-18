using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using HarmonyLib;

internal static class PoVTrashChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type trash = null!;
    private static int exits, builds;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void NativeCursor(IntPtr texture, IntPtr hotspot, int mode);
    private static readonly NativeCursor CursorStub = (_, __, ___) => { };
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);
    private static bool Visible(ref bool __result) { __result = true; return false; }
    private static void ObserveBuild() => ++builds;
    // Only replace engine-dependent cleanup. Assertions verify that production
    // suppression requests PoV's cleanup, not real cursor/popup rendering.
    private static void Exit()
    {
        ++exits;
        trash.GetField("<Active>k__BackingField", Static)!.SetValue(null, false);
        trash.GetField("popupOpen", Static)!.SetValue(null, false);
        object marked = trash.GetField("marked", Static)!.GetValue(null)!;
        marked.GetType().GetMethod("Clear")!.Invoke(marked, null);
    }
    private static IEnumerable<CodeInstruction> ExitBody()
    {
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PoVTrashChecks), nameof(Exit)));
        yield return new CodeInstruction(OpCodes.Ret);
    }

    internal static void Run(Assembly mod, Assembly pov, Action<bool, string> check)
    {
        // JIT of the guarded original Enter still resolves this Unity service.
        // The standalone harness has no native cursor; production uses PoV Exit.
        mono_add_internal_call("UnityEngine.Cursor::SetCursor_Injected", Marshal.GetFunctionPointerForDelegate(CursorStub));
        Type adapter = mod.GetType("InventoryCompatibility.PathOfValheimanTrashMode", true)!;
        trash = pov.GetType("PassiveTree.UI.TrashMode", true)!;
        var fixture = new Harmony("smoke.pov.delete.engine");
        string owner = mod.GetName().Name + ".smoke.delete";
        MethodInfo[] targets = { trash.GetMethod("Build", Static)!, trash.GetMethod("Enter", Static)!, trash.GetMethod("Press", Static)! };
        exits = builds = 0;
        fixture.Patch(trash.GetMethod("Exit", Static), transpiler: new HarmonyMethod(typeof(PoVTrashChecks), nameof(ExitBody)));
        fixture.Patch(typeof(InventoryGui).GetMethod("IsVisible", Static), prefix: new HarmonyMethod(typeof(PoVTrashChecks), nameof(Visible)));
        fixture.Patch(targets[0], prefix: new HarmonyMethod(typeof(PoVTrashChecks), nameof(ObserveBuild)));
        try
        {
            trash.GetField("<Active>k__BackingField", Static)!.SetValue(null, true);
            trash.GetField("popupOpen", Static)!.SetValue(null, true);
            adapter.GetMethod("Install", Static)!.Invoke(null, new object[] { pov, owner });
            check(exits == 1 && !(bool)trash.GetProperty("Active")!.GetValue(null)!, "Delete adapter exits existing mode/popup through PoV cleanup");
            foreach (MethodInfo method in targets)
                check(Harmony.GetPatchInfo(method).Owners.Contains(owner + ".pov.delete-ui"), "Delete guard installed on original " + method.Name);
            targets[1].Invoke(null, null); targets[2].Invoke(null, null);
            check(!(bool)trash.GetProperty("Active")!.GetValue(null)! && exits == 1, "Enter/Press are consumed without reactivating Delete mode");
            // Exercise the real PoV call site, not just its Harmony metadata.
            trash.GetNestedType("InventoryGui_Update", BindingFlags.NonPublic)!.GetMethod("Postfix", Static)!.Invoke(null, new object?[] { null });
            check(builds == 1 && trash.GetField("plate", Static)!.GetValue(null) == null, "original PoV Postfix dispatches to blocked Build without creating a plate");
            adapter.GetMethod("Dispose", Static)!.Invoke(null, null);
            check(targets.All(m => Harmony.GetPatchInfo(m)?.Owners.Contains(owner + ".pov.delete-ui") != true), "Delete teardown removes all and only adapter guards");
            check(Harmony.GetPatchInfo(targets[0]).Owners.Contains(fixture.Id), "Delete teardown retains another owner's patch");
        }
        finally
        {
            adapter.GetMethod("Dispose", Static)!.Invoke(null, null);
            fixture.UnpatchSelf();
        }
    }
}
