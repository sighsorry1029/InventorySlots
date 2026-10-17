using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Real HarmonyX, annotations from the final mod/original PoV DLLs, instrumented
// bodies: verifies ordering/state/exception semantics, not game item transactions.
internal static class CraftingOrderProbe
{
    private const BindingFlags Methods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<string> Trace = new List<string>();
    private static string mode = "";
    private static bool safetyCaptured, favoriteCaptured, finishedWithSnapshot;
    private static readonly Exception Failure = new InvalidOperationException("simulated PoV failure");

    private sealed class Craft
    {
        public object? Item = new object();
        public bool InventoryHasItem = true;
        public bool OriginalRan;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DoCrafting() { Trace.Add("original"); OriginalRan = true; }
    }

    private static class Pov
    {
        public static bool Prefix(Craft __instance)
        {
            Trace.Add("pov");
            if (mode.StartsWith("upgrade")) return true;
            if (mode == "throw-before") throw Failure;
            if (mode != "salvage-rejected") __instance.InventoryHasItem = false;
            if (mode == "throw-after") throw Failure;
            __instance.Item = null;
            return false;
        }
    }

    private static class Safety
    {
        public static bool Prefix(Craft __instance, out object? __state)
        {
            Trace.Add("safety");
            __state = __instance.Item;
            safetyCaptured = __state != null;
            return mode != "upgrade-veto";
        }
        public static Exception? Finalizer(object? __state, Exception? __exception)
        {
            Trace.Add("safety-finalizer");
            finishedWithSnapshot = __state != null;
            return __exception;
        }
    }

    private static class Favorite
    {
        public static bool Prefix(Craft __instance, out bool __state)
        {
            Trace.Add("favorite");
            __state = true;
            favoriteCaptured = __instance.Item != null;
            return true;
        }
        public static void Postfix(bool __state) { Trace.Add(__state ? "favorite-post" : "favorite-post-empty"); }
        public static Exception? Finalizer(bool __state, Exception? __exception)
        {
            Trace.Add(__state ? "favorite-finalizer" : "favorite-finalizer-empty");
            return __exception;
        }
    }

    private static HarmonyMethod Redirect(Type sourceType, string name, Type probeType)
    {
        MethodInfo source = sourceType.GetMethod(name, Methods)!;
        HarmonyMethod container = HarmonyMethodExtensions.GetMergedFromType(sourceType);
        if (!container.methodType.HasValue) container.methodType = MethodType.Normal;
        HarmonyMethod method = HarmonyMethodExtensions.GetMergedFromMethod(source);
        HarmonyMethod merged = HarmonyMethodExtensions.Merge(container, method);
        merged.method = probeType.GetMethod(name, Methods)!;
        return merged;
    }

    public static void Run(Assembly slots, Assembly pov, Action<bool, string> check)
    {
        Type safety = slots.GetType("InventorySlots.InventoryGuiEquipmentSlotUpgradeSafetyPatch", true)!;
        Type favorite = slots.GetType("InventorySlots.InventoryGuiUpgradeFavoriteCraftingPatch", true)!;
        Type native = pov.GetType("PassiveTree.CraftSalvage+Gui_Do", true)!;
        var safetyPrefix = Redirect(safety, "Prefix", typeof(Safety));
        var favoritePrefix = Redirect(favorite, "Prefix", typeof(Favorite));
        var favoritePostfix = Redirect(favorite, "Postfix", typeof(Favorite));
        check(safetyPrefix.after.Contains("treextr.pathofvalheiman") && favoritePrefix.after.Contains("treextr.pathofvalheiman") &&
              !(favoritePostfix.after ?? Array.Empty<string>()).Contains("treextr.pathofvalheiman"),
            "PoV dependency applies to safety/favorite prefixes only");
        System.Console.WriteLine($"Effective prefix priorities: safety={safetyPrefix.priority}, favorite={favoritePrefix.priority}");
        MethodInfo target = typeof(Craft).GetMethod(nameof(Craft.DoCrafting))!;
        foreach (int registration in new[] { 0, 1, 2 })
        {
            var s = new Harmony("sighsorry.InventorySlots");
            var p = new Harmony("treextr.pathofvalheiman");
            void Slots() {
                s.Patch(target, prefix: safetyPrefix, finalizer: Redirect(safety, "Finalizer", typeof(Safety)));
                s.Patch(target, prefix: favoritePrefix, postfix: favoritePostfix, finalizer: Redirect(favorite, "Finalizer", typeof(Favorite)));
            }
            void PoV() => p.Patch(target, prefix: Redirect(native, "Prefix", typeof(Pov)));
            try
            {
                if (registration == 0) { Slots(); PoV(); }
                else if (registration == 1) { PoV(); Slots(); }
                else Slots(); // Optional dependency absent.
                foreach (string scenario in registration == 2 ? new[] { "upgrade", "upgrade-veto" } :
                    new[] { "salvage", "salvage-rejected", "throw-before", "throw-after", "upgrade", "upgrade-veto" })
                {
                    mode = scenario; Trace.Clear(); safetyCaptured = favoriteCaptured = finishedWithSnapshot = false;
                    var craft = new Craft(); Exception? error = null;
                    try { craft.DoCrafting(); } catch (Exception e) { error = e; }
                    if (scenario.StartsWith("throw"))
                        check(ReferenceEquals(error, Failure) && !craft.OriginalRan && !safetyCaptured && !favoriteCaptured &&
                              !finishedWithSnapshot && !Trace.Contains("safety") && !Trace.Contains("favorite") &&
                              craft.InventoryHasItem == (scenario == "throw-before"),
                            $"registration {registration}: {scenario}, no late protection snapshot or swallowed exception");
                    else if (scenario.StartsWith("salvage"))
                        check(error == null && !craft.OriginalRan && !safetyCaptured && !favoriteCaptured && !finishedWithSnapshot &&
                              Trace[0] == "pov" && Trace.Contains("safety") && Trace.Contains("favorite") &&
                              craft.InventoryHasItem == (scenario == "salvage-rejected"),
                            $"registration {registration}: {scenario}, false still runs prefixes with cleared item");
                    else
                        check(error == null && safetyCaptured && favoriteCaptured && finishedWithSnapshot &&
                              craft.OriginalRan == (scenario == "upgrade") && (registration == 2 || Trace[0] == "pov"),
                            $"registration {registration}: {scenario}, normal protection and veto retained");
                }
            }
            finally { s.UnpatchSelf(); p.UnpatchSelf(); }
        }
    }
}
