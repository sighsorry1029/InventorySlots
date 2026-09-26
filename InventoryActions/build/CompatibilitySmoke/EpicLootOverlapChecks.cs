using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

internal static class EpicLootOverlapChecks
{
    internal static int Run(Type plugin)
    {
        if (plugin.Namespace != "InventoryActions") throw new ArgumentException("InventoryActions only");
        const string owner = "pumpli.epicloot.adventuretools";
        var addonHarmony = new Harmony(owner);
        var otherHarmony = new Harmony("inventory-tests.unrelated");
        var info = new PluginInfo();
        var metadata = typeof(PluginInfo).GetProperty("Metadata")!;
        metadata.SetValue(info, new BepInPlugin(owner, "Fixture", "0.8.5"));
        Chainloader.PluginInfos.Add(owner, info);
        FieldInfo api = plugin.GetField("_epicLootStackingApi", BindingFlags.Static | BindingFlags.NonPublic)!;
        api.SetValue(null, FormatterServices.GetUninitializedObject(api.FieldType));
        object instance = FormatterServices.GetUninitializedObject(plugin);
        plugin.GetField("_harmony", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, new Harmony("sighsorry.InventoryActions"));
        var start = plugin.GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var classifier = AccessTools.Method(plugin, "CanStackForTopFirstMove");
        var pair = AccessTools.Method(plugin, "CanStackIntoTargetForTopFirstMove");
        var sort = AccessTools.Method(plugin, "MergeSortableStacks");
        var tracker = AccessTools.Method(typeof(EpicLootOverlapChecks), nameof(TrackerFixture));
        var fixture = typeof(EpicLootAdventureTools.EpicLootStackCompatibility);
        int checks = 0;
        void Check(string name, bool condition)
        {
            if (!condition) throw new InvalidOperationException(name);
            ++checks;
            System.Console.WriteLine("PASS " + name);
        }
        int Count(MethodBase target, string id) => Harmony.GetPatchInfo(target)?.Owners.Count(value => value == id) ?? 0;
        try
        {
            addonHarmony.Patch(classifier, postfix: new HarmonyMethod(fixture, "CanStackPostfix"));
            addonHarmony.Patch(pair, prefix: new HarmonyMethod(fixture, "PairPrefix"));
            addonHarmony.Patch(sort, prefix: new HarmonyMethod(fixture, "SortPrefix"), postfix: new HarmonyMethod(fixture, "SortPostfix"));
            addonHarmony.Patch(tracker, prefix: new HarmonyMethod(typeof(EpicLootOverlapChecks), nameof(UnrelatedPrefix)));
            otherHarmony.Patch(pair, prefix: new HarmonyMethod(typeof(EpicLootOverlapChecks), nameof(UnrelatedPrefix)));
            Check("real Harmony fixtures installed", Count(classifier, owner) == 1 && Count(pair, owner) == 1 && Count(sort, owner) == 1);
            start.Invoke(instance, null);
            Check("unreviewed AdventureTools version left untouched", Count(pair, owner) == 1);
            metadata.SetValue(info, new BepInPlugin(owner, "Fixture", "0.8.4"));
            start.Invoke(instance, null);
            Check("only known redundant stacking patches removed", Count(classifier, owner) == 0 && Count(pair, owner) == 0 && Count(sort, owner) == 0);
            Check("AdventureTools tracker patch preserved", Count(tracker, owner) == 1);
            Check("other mod patch preserved", Count(pair, otherHarmony.Id) == 1);
            start.Invoke(instance, null);
            Check("overlap cleanup is idempotent", Count(tracker, owner) == 1 && Count(pair, otherHarmony.Id) == 1);
        }
        finally
        {
            addonHarmony.UnpatchSelf(); otherHarmony.UnpatchSelf();
            Chainloader.PluginInfos.Remove(owner); api.SetValue(null, null);
        }
        return checks;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static int TrackerFixture(int value) => value + 1;
    private static void UnrelatedPrefix() { }
}

// Only the external adapter's patch identity is reproduced; its merge logic is
// not copied. The test invokes the actual compiled plugin lifecycle method.
namespace EpicLootAdventureTools
{
    internal static class EpicLootStackCompatibility
    {
        private static void CanStackPostfix() { }
        private static void PairPrefix() { }
        private static void SortPrefix() { }
        private static void SortPostfix() { }
    }
}
