using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

// Shared nested adapter exists in both final DLLs. No plugin Awake, Unity
// objects, fake-publicized game references or third-party bridge is used.
internal static class EpicLootStackingChecks
{
    private const string MagicKey = "randyknapp.mods.epicloot#EpicLoot.MagicItemComponent";
    private static int guardedLookupCalls;

    internal static int Run(Type plugin, string epicPath)
    {
        int checks = 0;
        void Check(string name, bool result)
        {
            if (!result) throw new InvalidOperationException(name);
            checks++;
            System.Console.WriteLine("PASS " + name);
        }

        Assembly epic = Assembly.LoadFrom(epicPath);
        Type modern = epic.GetType("EpicLoot.MagicItemData", true)!;
        MethodInfo endpoint = modern.GetMethod("StacksWith", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check("original EpicLoot predicate remains internal", endpoint != null && endpoint.IsAssembly);
        Type adapterType = plugin.GetNestedType("EpicLootStackingApi", BindingFlags.NonPublic)!;
        object adapter = Activator.CreateInstance(adapterType, new object[] { epic })!;
        MethodInfo merge = adapterType.GetMethod("GetMergedData")!;

        ItemData Item(Dictionary<string, string>? data)
        {
            var item = (ItemData)FormatterServices.GetUninitializedObject(typeof(ItemData));
            item.m_customData = data!;
            return item;
        }
        void Pair(string name, Dictionary<string, string>? sourceData, Dictionary<string, string>? targetData, bool approved)
        {
            var source = Item(sourceData);
            var target = Item(targetData);
            var sourceBefore = sourceData?.ToArray();
            var targetBefore = targetData?.ToArray();
            var result = (Dictionary<string, string>?)merge.Invoke(adapter, new object[] { source, target });
            Check(name, (result != null) == approved && (result == null || result.Count == 0));
            Check(name + " leaves metadata unchanged",
                (sourceData == null || sourceData.SequenceEqual(sourceBefore!)) &&
                (targetData == null || targetData.SequenceEqual(targetBefore!)));
        }

        Pair("empty raw data approved", new(), new(), true);
        Pair("identical magic JSON approved", new() { [MagicKey] = "{\"Effects\":[]}" }, new() { [MagicKey] = "{\"Effects\":[]}" }, true);
        Pair("different magic JSON rejected", new() { [MagicKey] = "A" }, new() { [MagicKey] = "B" }, false);
        Pair("blank versus inscribed rejected", new(), new() { [MagicKey] = "A" }, false);
        Pair("empty/null magic representations delegated without rewriting", new() { [MagicKey] = "" }, new() { [MagicKey] = "null" }, true);
        // Bool approval does not claim these keys. Subsequent Slots/Actions pair
        // guards are tested by the shared-source policy fixtures.
        Pair("foreign metadata remains for caller policy", new() { ["foreign"] = "A" }, new() { ["foreign"] = "B" }, true);
        Pair("null dictionaries approved", null, null, true);

        // Actual modern EpicLoot prefix + a rejecting boundary stand-in prove
        // HarmonyX runs the guarded lookup regardless of registration order.
        // This is not a simulation of the full Slots placement implementation.
        var inventory = (Inventory)FormatterServices.GetUninitializedObject(typeof(Inventory));
        var existing = Item(new());
        existing.m_shared = new ItemDrop.ItemData.SharedData { m_name = "Wood", m_maxStackSize = 50 };
        existing.m_quality = 1;
        existing.m_stack = 1;
        var sourceItem = Item(new());
        sourceItem.m_shared = existing.m_shared;
        sourceItem.m_quality = 1;
        AccessTools.Field(typeof(Inventory), "m_inventory").SetValue(inventory, new List<ItemData> { existing });
        FieldInfo adding = AccessTools.Field(modern, "_adding");
        MethodInfo lookup = AccessTools.Method(typeof(Inventory), "FindFreeStackItem", new[] { typeof(string), typeof(int), typeof(float) });
        MethodInfo prefix = AccessTools.Method(modern.GetNestedType("Inventory_FindFreeStackItem_Patch", BindingFlags.NonPublic), "Prefix");
        var epicHarmony = new Harmony("inventory-tests.epic-native-lookup");
        var guardHarmony = new Harmony("inventory-tests.epic-guarded-lookup");
        object? oldAdding = adding.GetValue(null);
        try
        {
            adding.SetValue(null, sourceItem);
            epicHarmony.Patch(lookup, prefix: new HarmonyMethod(prefix));
            Check("actual EpicLoot lookup accepts fixture destination", ReferenceEquals(lookup.Invoke(inventory, new object[] { "Wood", 1, 0f }), existing));
            epicHarmony.UnpatchSelf();
            foreach (bool epicFirst in new[] { true, false })
            {
                guardedLookupCalls = 0;
                if (epicFirst) epicHarmony.Patch(lookup, prefix: new HarmonyMethod(prefix));
                guardHarmony.Patch(lookup, prefix: new HarmonyMethod(typeof(EpicLootStackingChecks), nameof(RejectLookup)));
                if (!epicFirst) epicHarmony.Patch(lookup, prefix: new HarmonyMethod(prefix));
                Check("guarded lookup veto retained; EpicLoot first=" + epicFirst,
                    lookup.Invoke(inventory, new object[] { "Wood", 1, 0f }) == null && guardedLookupCalls == 1);
                guardHarmony.UnpatchSelf();
                epicHarmony.UnpatchSelf();
            }
        }
        finally
        {
            guardHarmony.UnpatchSelf();
            epicHarmony.UnpatchSelf();
            adding.SetValue(null, oldAdding);
        }
        return checks;
    }

    private static bool RejectLookup(ref ItemData? __result)
    {
        guardedLookupCalls++;
        __result = null;
        return false;
    }
}
