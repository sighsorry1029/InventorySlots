using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;

internal static class Program
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;

    private static int Main(string[] args)
    {
        if (args.Length != 4 && (args.Length != 5 || args[4] != "--engine-hooks"))
            throw new ArgumentException("<final Slots DLL> <original Managed> <BepInEx core> <original PoV DLL> [--engine-hooks]");
        if (Type.GetType("Mono.Runtime") == null || IntPtr.Size != 8) throw new InvalidOperationException("Use original game x64 Mono.");
        string[] roots = { args[1], args[2], Path.GetDirectoryName(args[0])!, Path.GetDirectoryName(args[3])! };
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            string filename = new AssemblyName(request.Name).Name + ".dll";
            foreach (string root in roots)
                if (File.Exists(Path.Combine(root, filename))) return Assembly.LoadFrom(Path.Combine(root, filename));
            return null;
        };
        try { return Run(args, roots); }
        catch (Exception error) { System.Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args, string[] roots)
    {
        Assembly bep = Assembly.LoadFrom(Path.Combine(args[2], "BepInEx.dll"));
        string isolated = Path.Combine(Path.GetTempPath(), "InventorySlots-PoV-smoke-" + Guid.NewGuid());
        bep.GetType("BepInEx.Paths", true)!.GetMethod("SetExecutablePath", Static)!
            .Invoke(null, new object[] { Path.Combine(isolated, "valheim.exe"), Path.Combine(isolated, "BepInEx"), args[1], roots });
        Type threading = bep.GetType("BepInEx.ThreadingHelper", true)!;
        object queue = FormatterServices.GetUninitializedObject(threading);
        threading.GetField("_invokeLock", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(queue, new object());
        threading.GetField("<Instance>k__BackingField", Static)!.SetValue(null, queue);
        Assembly mod = Assembly.LoadFrom(args[0]);
        Type plugin = mod.GetType("InventorySlots.InventorySlotsPlugin", true)!;
        Type policy = mod.GetType("InventorySlots.StackMetadataPolicy", true)!;
        object? Call(string method, params object?[] values) => policy.GetMethod(method, Static)!.Invoke(null, values);
        var a = new Dictionary<string, string> { ["pt.potionPower"] = "1.1" };
        var b = new Dictionary<string, string> { ["pt.potionPower"] = "1.5" };
        Check(!(bool)Call("CanParticipateInAutomaticStacking", a)!, "PoV absent: no metadata registration");
        Check((int)plugin.GetMethod("GetPathOfValheimanRegularRows", Static)!.Invoke(null, new object?[] { null, 7 })! == 7,
            "PoV absent: original row count unchanged without touching a player");

        Assembly pov = Assembly.LoadFrom(args[3]);
        Type compat = pov.GetType("PassiveTree.InventorySlotsCompat", true)!;
        MethodInfo original = compat.GetMethod("Run", Static)!;
        MethodInfo keys = compat.GetMethod("OurKeys", Static)!;
        var getKeys = (Func<IEnumerable<string>>)Delegate.CreateDelegate(typeof(Func<IEnumerable<string>>), keys);
        plugin.GetField("_pathOfValheimanMetadataKeys", Static)!.SetValue(null, getKeys);
        var harmony = new Harmony("InventorySlots.tests.pov");
        harmony.Patch(original, prefix: new HarmonyMethod(plugin.GetMethod("RegisterPathOfValheimanMetadata", Static)!));
        original.Invoke(null, null);
        Check((bool)Call("CanParticipateInAutomaticStacking", a)!, "actual PoV-declared marks participate after patched Run");
        foreach (string key in new[] { "pt.potionPower", "pt.food", "pt.fooddur", "pt.work.made.id" })
        {
            a = new Dictionary<string, string> { [key] = "1.1" };
            b = new Dictionary<string, string> { [key] = "1.1" };
            Check((bool)Call("AreCompatible", a, b)!, key + " equal values");
            b[key] = "1.5";
            Check(!(bool)Call("AreCompatible", a, b)! && !(bool)Call("AreCompatible", b, a)!, key + " differing values rejected both ways");
            Check(!(bool)Call("AreCompatible", a, new Dictionary<string, string>())!, key + " absent value stays distinct");
            a[key] = b[key] = "";
            Call("MergeInto", a, b);
            Check(a[key] == "" && b[key] == "", key + " empty values and partial source preserved");
        }
        Check(!(bool)Call("CanParticipateInAutomaticStacking", new Dictionary<string, string> { ["pt.future.test"] = "x" })!, "undeclared future metadata stays conservative");
        Check(!(bool)Call("CanParticipateInAutomaticStacking", new Dictionary<string, string> { ["another.mod"] = "x" })!, "other mod metadata stays conservative");
        original.Invoke(null, null);
        Check(!(bool)Call("AreCompatible", new Dictionary<string, string> { ["pt.potionPower"] = "1.1" },
            new Dictionary<string, string> { ["pt.potionPower"] = "1.5" })!, "repeat PoV registration cannot relax identity");

        // Validate all external targets against original assemblies without starting Unity UI.
        Assembly game = Assembly.LoadFrom(Path.Combine(args[1], "assembly_valheim.dll"));
        Type player = game.GetType("Player", true)!;
        Type gui = game.GetType("InventoryGui", true)!;
        Type item = game.GetType("ItemDrop+ItemData", true)!;
        MethodInfo require = plugin.GetMethod("RequirePathOfValheimanMethod", Static)!;
        MethodInfo Require(string type, string name, Type result, params Type[] parameters) =>
            (MethodInfo)require.Invoke(null, new object[] { pov.GetType(type, true)!, name, result, parameters })!;
        var tick = Require("PassiveTree.TradeMechanics", "TickRows", typeof(void), player);
        Require("PassiveTree.TradeMechanics", "Rows", typeof(int), pov.GetType("PassiveTree.StatSet", true)!);
        Require("PassiveTree.Progress", "Peek", pov.GetType("PassiveTree.Progress", true)!, player);
        var panel = Require("PassiveTree.InventoryRowsPanel+Gui_Update", "Postfix", typeof(void), gui);
        var sort = Require("PassiveTree.QuickStack", "Sort", typeof(void), player);
        var movable = Require("PassiveTree.QuickStack", "Movable", typeof(List<>).MakeGenericType(item), player);
        Require("PassiveTree.UI.ItemTooltip", "get_Wanted", typeof(bool));
        Require("PassiveTree.UI.ItemTooltip", "RefreshCurrent", typeof(void));
        Require("PassiveTree.UI.TreeWindow", "get_IsOpen", typeof(bool));
        Check(true, "external method contracts match original PoV DLL");
        // These wrappers JIT Player methods whose static initialization needs Unity's native
        // Animator icalls. Keep the optional probe explicit; standalone Mono cannot supply them.
        if (args.Length == 5)
        {
            foreach (var pair in new[] { (tick, "UpdatePathOfValheimanRows"), (panel, "SkipPathOfValheimanRowsPanel"), (sort, "SortPathOfValheimanInventory") })
            {
                harmony.Patch(pair.Item1, prefix: new HarmonyMethod(plugin.GetMethod(pair.Item2, Static)!));
                Check(Harmony.GetPatchInfo(pair.Item1).Owners.Contains(harmony.Id), "installed actual Harmony prefix " + pair.Item1.Name);
            }
            harmony.Patch(movable, postfix: new HarmonyMethod(plugin.GetMethod("FilterPathOfValheimanMovableItems", Static)!));
            Check(Harmony.GetPatchInfo(movable).Owners.Contains(harmony.Id), "installed actual Movable postfix");
            panel.Invoke(null, new object?[] { null });
            Check(true, "patched PoV panel callback skips original Unity layout access");
        }
        else System.Console.WriteLine("NOT RUN engine-dependent row/sort/UI hooks (require Unity); signatures only.");

        // New private game-member reads use explicit cached accessors, not publicized visibility.
        Assembly guiutils = Assembly.LoadFrom(Path.Combine(args[1], "assembly_guiutils.dll"));
        Type tooltip = guiutils.GetType("UITooltip", true)!;
        MethodInfo fieldReader = plugin.GetMethod("PathOfValheimanStaticField", Static)!;
        foreach (string fieldName in new[] { "m_current", "m_tooltip" })
        {
            FieldInfo field = tooltip.GetField(fieldName, Static)!;
            Check(field.IsPrivate, fieldName + " is private in original game");
            var reader = (Delegate)fieldReader.MakeGenericMethod(field.FieldType).Invoke(null, new object[] { tooltip, fieldName })!;
            Check(reader.DynamicInvoke() == null, "compiled accessor reads original " + fieldName);
        }
        harmony.UnpatchSelf();
        Check(Harmony.GetPatchInfo(original)?.Owners.Contains(harmony.Id) != true, "hooks cleanly unpatch");
        System.Console.WriteLine($"PASS {checks} original-DLL Mono checks; no game/UI execution.");
        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
        System.Console.WriteLine("PASS " + message);
    }
}
