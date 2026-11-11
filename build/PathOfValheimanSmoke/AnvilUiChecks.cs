using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static partial class Program
{
    private static void CheckAnvilUiBindings(Type plugin, Assembly pov)
    {
        var patches = new Harmony("smoke.pov.anvil.ui");
        string[] names = { "_povIsAnvil", "_povIsAnvilCache", "_povViewingAnvil" };
        object?[] previous = names.Select(name => plugin.GetField(name, Statics)!.GetValue(null)).ToArray();
        try
        {
            plugin.GetMethod("InstallPathOfValheimanAnvilUi", Statics)!.Invoke(null, new object[] { patches, pov });
            Check(true, "runic anvil UI hooks compile and install against original game/PoV assemblies on Mono");
            foreach (string name in names)
            {
                var predicate = (Func<Container, bool>)plugin.GetField(name, Statics)!.GetValue(null)!;
                Check(predicate.Method.DeclaringType == pov.GetType("PassiveTree.RunicCraft.Anvil") && predicate.Method.IsPublic,
                    name + " binds the verified native predicate");
                Check(!predicate(null!), name + " executes original method safely for absent container");
            }
            MethodInfo refresh = pov.GetType("PassiveTree.UI.RunicAnvilWindow", true)!.GetMethod("Refresh", Statics)!;
            Check(!refresh.IsPublic && refresh.IsAssembly, "Refresh is internal on original PoV; hook does not assume publicized access");
            var hooks = Harmony.GetPatchInfo(refresh)!;
            Check(hooks.Prefixes.Any(p => p.owner == patches.Id) && hooks.Postfixes.Any(p => p.owner == patches.Id) &&
                !hooks.Transpilers.Any(p => p.owner == patches.Id), "native Refresh body retained between handoff hooks");
            MethodInfo show = AccessTools.DeclaredMethod(typeof(InventoryGui), "Show", new[] { typeof(Container), typeof(int) });
            var showHooks = Harmony.GetPatchInfo(show)!;
            Check(showHooks.Prefixes.Any(p => p.owner == patches.Id) && showHooks.Finalizers.Any(p => p.owner == patches.Id),
                "correct native Show overload has scoped prefix/finalizer");
            Type anvil = pov.GetType("PassiveTree.RunicCraft.Anvil", true)!;
            Check(anvil.GetMethods(Statics).Where(m => m.Name == "Do" || m.Name == "Open" || m.Name == "Writable")
                .All(m => Harmony.GetPatchInfo(m)?.Owners.Contains(patches.Id) != true), "UI adapter does not patch ownership or forging operations");
        }
        finally
        {
            patches.UnpatchSelf();
            for (int i = 0; i < names.Length; i++) plugin.GetField(names[i], Statics)!.SetValue(null, previous[i]);
        }
    }
}
