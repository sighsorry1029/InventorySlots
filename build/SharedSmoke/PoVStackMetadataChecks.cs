using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

// Uses the supplied original PoV key list and, optionally, actual FineDining
// callbacks. No copy of either mod's parsing/composition algorithm lives here.
internal static class PoVStackMetadataChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Expiry = "sighsorry.FineDining.ExpiryWorldTicks";
    private const string Lifetime = "sighsorry.FineDining.AssignedLifetimeTicks";
    private static bool withPoV;

    internal static void Run(Type plugin, Assembly? pov, Action<bool, string> check)
    {
        // Standalone Mono has no Unity native logging service. Inventory's
        // original mutation bodies still run; only their diagnostic sink is off.
        UnityEngine.Debug.unityLogger.logEnabled = false;
        bool actions = plugin.Namespace == "InventoryActions";
        Type policy = plugin.Assembly.GetType(plugin.Namespace + ".StackMetadataPolicy", true)!;
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Static)!.Invoke(null, args);
        bool Compatible(ItemData a, ItemData b) => (bool)policy.GetMethod("AreCompatible", Static)!
            .Invoke(null, new object?[] { a.m_customData, b.m_customData })!;
        withPoV = pov != null;
        ItemData target, source;
        Dictionary<string, string> partialBefore;
        if (pov != null)
        {
            var keys = (IEnumerable<string>)pov.GetType("PassiveTree.InventorySlotsCompat", true)!
                .GetMethod("OurKeys", Static)!.Invoke(null, null)!;
            plugin.Assembly.GetType(plugin.Namespace + ".PathOfValheimanStackMetadata", true)!
                .GetMethod("Register", Static)!.Invoke(null, new object[] { keys });
            target = Item(5, "a", "100");
            source = Item(5, "b", "101");
            check(Compatible(target, source), "PoV separate craft batches are compatible");
            check(Compatible(source, source.Clone()), "PoV split clone remains compatible");
            target.m_customData["pt.work.origin"] = "brew:player:batch-a";
            target.m_customData.Remove("pt.bonus");
            source.m_customData["pt.work.origin"] = "brew:player:batch-b";
            source.m_customData["pt.bonus"] = "1";
            check(Compatible(target, source), "different brew batches and bonus drops are compatible");
            foreach (string key in new[] { "pt.brew", "pt.keep", "pt.fooddur", "pt.foodfresh",
                         "pt.age.t", "pt.aged", "pt.fish.caught", "pt.work.wind-used", "pt.born" })
            {
                source.m_customData[key] = "1";
                check(!Compatible(target, source), key + " remains protected");
                source.m_customData.Remove(key);
            }
            source.m_customData["pt.food"] = "1.2";
            check(!Compatible(target, source), "PoV food strength remains part of identity");
            source.m_customData["pt.food"] = "1.1";
            source.m_customData["unknown.mod"] = "x";
            check(!Compatible(target, source), "unknown metadata cannot be discarded");
            source.m_customData.Remove("unknown.mod");
            source.m_customData["pt.work.portal"] = "quest";
            check(!Compatible(target, source), "other PoV quest provenance stays exact");
            source.m_customData.Remove("pt.work.portal");
            source.m_customData[Expiry] = "900";
            check(!Compatible(target, source), "FineDining data stays exact before owner registration");
            source.m_customData.Remove(Expiry);
            partialBefore = new Dictionary<string, string>(source.m_customData);
            policy.GetMethod("MergeInto", Static)!.Invoke(null, new object[] { target.m_customData, source.m_customData });
            check(target.m_customData["pt.work.made"] == "100" && target.m_customData["pt.work.made.id"] == "a",
                "destination craft time and ID are retained together");
            check(target.m_customData["pt.work.origin"] == "brew:player:batch-a" &&
                  !target.m_customData.ContainsKey("pt.bonus"), "destination origin and absent bonus marker are retained");
            var noProvenance = Item(1, "c", "102");
            noProvenance.m_customData.Remove("pt.work.made");
            noProvenance.m_customData.Remove("pt.work.made.id");
            policy.GetMethod("MergeInto", Static)!.Invoke(null, new object[] { noProvenance.m_customData, source.m_customData });
            check(!noProvenance.m_customData.ContainsKey("pt.work.made") && !noProvenance.m_customData.ContainsKey("pt.work.made.id"),
                "unstamped destination does not acquire invented craft provenance");
            check(Equal(source.m_customData, partialBefore), "metadata merge never modifies remaining source");
        }
        CheckNativePolicies(plugin, check);

        string? fdPath = Environment.GetEnvironmentVariable("INVENTORY_TEST_FINEDINING_DLL");
        if (string.IsNullOrEmpty(fdPath))
        {
            System.Console.WriteLine("NOT RUN: optional real FineDining stack tests (set INVENTORY_TEST_FINEDINING_DLL).");
            return;
        }
        Assembly fd = Assembly.LoadFrom(fdPath);
        System.Console.WriteLine("Stack callbacks from " + fd.FullName);
        bool available = true;
        if (actions)
        {
            // Binding failure must not leave half the owner policies installed.
            try { Call("BindFineDiningStackMetadata", plugin.Assembly, new Func<bool>(() => available)); }
            catch (TargetInvocationException) { }
            target = Item(5, "a", "100"); source = Item(5, "b", "101");
            source.m_customData[Expiry] = "900";
            check(!Compatible(target, source), "unsupported FineDining binding leaves policy closed");
            Call("BindFineDiningStackMetadata", fd, new Func<bool>(() => available));
        }
        else
        {
            // Equivalent to FD's three guarded public API registrations, using
            // its actual methods and the final InventorySlots DLL's actual API.
            var register = plugin.Assembly.GetType("InventorySlots.InventorySlotsApi", true)!.GetMethod(
                "RegisterStackMetadataPolicy", new[] { typeof(string), typeof(Func<string,string,string>), typeof(Func<string,string,bool>) })!;
            void Register(string key, string owner, string merge, string guard)
            {
                Type type = fd.GetType("FineDining." + owner, true)!;
                register.Invoke(null, new object[] { key,
                    Delegate.CreateDelegate(typeof(Func<string,string,string>), type.GetMethod(merge, Static)!),
                    Delegate.CreateDelegate(typeof(Func<string,string,bool>), type.GetMethod(guard, Static)!) });
            }
            Register(Expiry, "DecayRuntime", "ComposeStackClockValues", "CanMergeStackClockValues");
            Register(Lifetime, "FreshnessRuntime", "ComposeAssignedLifetimeValues", "CanMergeAssignedLifetimeValues");
            Register("sighsorry.FineDining.Spoiled", "SpoilageClock", "ComposeSpoiledValues", "CanMergeSpoiledValues");
        }
        target = Food(18, "a", "100", "1000");
        source = Food(5, "b", "101", "900");
        check(Compatible(target, source), "actual FineDining callbacks allow different valid expiry clocks");
        source.m_customData[Expiry] = "broken";
        check(!Compatible(target, source), "malformed FineDining clock is rejected");
        source.m_customData[Expiry] = "900";
        source.m_customData[Lifetime] = "broken";
        check(!Compatible(target, source), "malformed FineDining lifetime is rejected");
        source.m_customData[Lifetime] = "800";
        source.m_customData[Expiry] = "-900";
        check(!Compatible(target, source), "mixed paused/running clocks need a world clock");
        source.m_customData[Expiry] = "900";
        partialBefore = new Dictionary<string, string>(source.m_customData);
        Inventory inventory = Bag(target);
        Inventory from = Bag(source);
        if (actions)
        {
            check((bool)Call("CanRestockFromContainerItem", target, source)!, "IA restock uses guarded metadata pair");
            check(!(bool)Call("CanUseMucActionStacking", source)!, "MUC remote metadata eligibility remains conservative");
            available = false;
            check(!(bool)Call("CanRestockFromContainerItem", target, source)!, "destroyed FineDining owner blocks subsequent transfers");
            available = true;
            // Actual original Inventory.MoveItemToThis/AddItem, deliberately no
            // FD native tracker: local-only chests need our confirmed-move merge.
            Call("MoveActionStack", inventory, from, source, 5, target);
        }
        else
        {
            object?[] args = { inventory, source, 5, 0, 0, false, null };
            check((bool)Call("TryPreparePositionalStackMetadataMerge", args)!, "IS manual drag accepts separate crafted food batches");
            inventory.MoveItemToThis(from, source, 5, 0, 0);
            Call("CompletePositionalStackMetadataMerge", inventory, args[6]);
        }
        check(target.m_stack == 20 && source.m_stack == 3 && from.ContainsItem(source), "partial native move conserves item counts");
        check(target.m_customData[Expiry] == "900", "partial native move preserves earliest actual FD expiry");
        check(Equal(source.m_customData, partialBefore), "partial native move keeps source freshness and craft identity");
        if (withPoV)
            check(target.m_customData["pt.work.made.id"] == "a", "partial native move retains target craft ID");

        if (actions)
        {
            var fullBefore = new Dictionary<string, string>(target.m_customData);
            check(!(bool)Call("MoveActionStack", inventory, from, source, 1, target)! && Equal(target.m_customData, fullBefore),
                "full target rejection never mutates metadata");
            target = Food(5, "a", "100", "1000"); source = Food(5, "b", "101", "900");
            source.m_gridPos = new Vector2i(1, 0);
            inventory = Bag(target, source);
            var items = new List<ItemData>(inventory.GetAllItems());
            Call("MergeSortableStacks", items, inventory);
            check(target.m_stack == 10 && items.Count == 1 && !inventory.ContainsItem(source) && target.m_customData[Expiry] == "900",
                "IA sort merges 5+5 and preserves earliest expiry");
            target = Food(18, "a", "100", "1000"); source = Food(5, "b", "101", "900");
            partialBefore = new Dictionary<string, string>(source.m_customData);
            Call("FillFavoriteStackAmounts", new List<ItemData> { target }, new List<ItemData> { source });
            check(target.m_stack == 20 && source.m_stack == 3 && target.m_customData[Expiry] == "900" && Equal(source.m_customData, partialBefore),
                "IA favorite fill composes freshness and keeps source remainder");
            target = Item(5, "a", "100"); target.m_customData = null!;
            source = Food(5, "b", "101", "900"); source.m_customData.Remove("pt.food");
            Call("FillFavoriteStackAmounts", new List<ItemData> { target }, new List<ItemData> { source });
            check(target.m_stack == 10 && target.m_customData[Expiry] == "900", "null destination metadata cannot lose source freshness");
            target = Food(5, "a", "100", "1000"); source = Food(5, "b", "101", "900");
            source.m_customData["pt.food"] = "1.2";
            inventory = Bag(target, source); items = new List<ItemData>(inventory.GetAllItems());
            Call("MergeSortableStacks", items, inventory);
            check(items.Count == 2 && target.m_stack == 5 && source.m_stack == 5, "IA sort keeps actual food strength differences separate");

            target = Item(18, "a", "100"); target.m_customData["sighsorry.FineDining.Spoiled"] = "1";
            source = Food(5, "b", "101", "900");
            inventory = Bag(target); from = Bag(source);
            Call("MoveActionStack", inventory, from, source, 2, target);
            check(target.m_stack == 20 && target.m_customData["sighsorry.FineDining.Spoiled"] == "1" &&
                !target.m_customData.ContainsKey(Expiry) && !target.m_customData.ContainsKey(Lifetime) && source.m_customData[Expiry] == "900",
                "native transfer keeps FD completed-spoilage invariant and fresh source remainder");
            target = Item(5, "a", "100"); target.m_customData["sighsorry.FineDining.Spoiled"] = "1";
            source = Food(5, "b", "101", "900");
            Call("FillFavoriteStackAmounts", new List<ItemData> { target }, new List<ItemData> { source });
            check(target.m_stack == 10 && !target.m_customData.ContainsKey(Expiry) && !target.m_customData.ContainsKey(Lifetime),
                "favorite fill also normalizes FD spoiled metadata");

            target = Food(18, "a", "100", "1000"); source = Food(5, "b", "101", "900");
            inventory = Bag(Item(1, "replacement", "100")); from = Bag(source);
            check(!(bool)Call("MoveActionStack", inventory, from, source, 2, target)! && source.m_stack == 5,
                "stale occupied target cannot redirect metadata merge into another item");
            inventory = Bag(target);
            var fault = new Harmony("inventory-tests.stack-post-mutation-error");
            try
            {
                fault.Patch(AccessTools.Method(typeof(Inventory), "MoveItemToThis", new[] {
                    typeof(Inventory), typeof(ItemData), typeof(int), typeof(int), typeof(int) }),
                    postfix: new HarmonyMethod(typeof(PoVStackMetadataChecks), nameof(ThrowAfterMove)));
                bool propagated = false;
                try { Call("MoveActionStack", inventory, from, source, 2, target); }
                catch (TargetInvocationException error) { propagated = error.GetBaseException().Message == "stack fixture fault"; }
                check(propagated && target.m_stack == 20 && source.m_stack == 3 && target.m_customData[Expiry] == "900",
                    "post-mutation exception propagates after preserving actually transferred freshness");
            }
            finally { fault.UnpatchSelf(); }
        }
    }

    private static void CheckNativePolicies(Type plugin, Action<bool, string> check)
    {
        bool actions = plugin.Namespace == "InventoryActions";
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Static)!.Invoke(null, args);
        bool Pair(ItemData a, ItemData b) => (bool)Call(actions
            ? "CanStackIntoTargetForTopFirstMove" : "CanShareInventoryStack", a, b)!;
        var target = Item(18, "a", "100");
        var source = Item(5, "b", "101");
        target.m_customData = new Dictionary<string, string> { ["smoke.owner"] = "2" };
        source.m_customData = new Dictionary<string, string> { ["smoke.owner"] = "2" };
        check(Pair(target, source), "matching unknown metadata permits native stacking");
        check((bool)Call("CanUseContainerActionStacking", source)!, "unknown metadata is a native transfer candidate");
        if (!actions)
        {
            var bag = Bag(target);
            check((int)Call("CountStackSpaceForIncomingItem", bag, source)! == 2,
                "matching unknown metadata contributes pickup capacity");
            Call("BeginInventoryAddItemDataStackLookup", source);
            try
            {
                object?[] lookup = { bag, source.m_shared.m_name, source.m_quality, (float)source.m_worldLevel, null };
                Call("TryOverrideFindFreeStackItem", lookup);
                check(ReferenceEquals(lookup[4], target), "automatic pickup selects matching unknown metadata");
                source.m_customData["smoke.owner"] = "different";
                Call("TryOverrideFindFreeStackItem", lookup);
                check(lookup[4] == null && (int)Call("CountStackSpaceForIncomingItem", bag, source)! == 0,
                    "automatic pickup rejects different unknown metadata");
            }
            finally { Call("EndInventoryAddItemDataStackLookup", true); }
        }
        source.m_customData["smoke.owner"] = "1";
        check(!Pair(target, source), "different unknown metadata blocks native stacking");
        source.m_customData.Clear();
        check(!Pair(target, source), "missing unknown metadata blocks native stacking");
        source.m_customData["smoke.owner"] = "2";
        Call("FillFavoriteStackAmounts", new List<ItemData> { target }, new List<ItemData> { source });
        check(target.m_stack == 18 && source.m_stack == 5, "unknown data does not authorize direct favorite fill");
        if (actions)
        {
            source.m_gridPos = new Vector2i(1, 0);
            var bag = Bag(target, source);
            Call("MergeSortableStacks", new List<ItemData> { target, source }, bag);
            check(target.m_stack == 18 && source.m_stack == 5, "unknown data does not authorize direct sort");
            Call("MoveActionStack", Bag(target), Bag(source), source, 2, target);
            check(target.m_stack == 20 && source.m_stack == 3 &&
                  target.m_customData["smoke.owner"] == "2" && source.m_customData["smoke.owner"] == "2",
                "matching unknown metadata survives partial native transfer");
            target.m_stack = 18; source.m_stack = 5;
        }

        Type api = plugin.Assembly.GetType(plugin.Namespace + "." + plugin.Namespace + "Api", true)!;
        var register = api.GetMethod("RegisterStackMetadataPolicy", new[] {
            typeof(string), typeof(Func<string,string,string>), typeof(Func<string,string,bool>) })!;
        bool ready = false;
        Func<string?, string?, string?> merge = (a, b) => a == null ? b : b == null ? a :
            Math.Min(int.Parse(a), int.Parse(b)).ToString();
        Func<string?, string?, bool> guard = (_, _) => ready;
        check((bool)register.Invoke(null, new object[] { "smoke.owner", merge, guard })!, "public API registers owner policy");
        check(!(bool)register.Invoke(null, new object[] { "smoke.owner", merge, guard })!, "public API preserves first registration");
        check(!Pair(target, source), "live owner guard veto is honored");
        ready = true; source.m_customData["smoke.owner"] = "1";
        check(Pair(target, source), "live owner guard can allow previously rejected pair");
        var before = new Dictionary<string, string>(source.m_customData);
        Call("FillFavoriteStackAmounts", new List<ItemData> { target }, new List<ItemData> { source });
        check(target.m_stack == 20 && source.m_stack == 3 && target.m_customData["smoke.owner"] == "1" &&
              Equal(source.m_customData, before), "registered favorite fill composes destination and preserves source remainder");
        if (actions)
        {
            target.m_stack = source.m_stack = 5;
            var bag = Bag(target, source);
            Call("MergeSortableStacks", new List<ItemData> { target, source }, bag);
            check(target.m_stack == 10 && !bag.ContainsItem(source), "registered owner sort works independently of PoV");
        }
        else
        {
            target.m_stack = 18;
            source.m_customData.Clear();
            var bag = Bag(target);
            ready = false;
            check(!Pair(target, source) && !(bool)Call("CanCacheCanAddItemFailure", bag, source)!,
                "destination-only live metadata veto cannot be cached");
            ready = true;
            check(Pair(target, source), "destination-only owner state change allows immediate retry");
            source.m_customData["smoke.owner"] = "1";
            check(!(bool)Call("CanCacheCanAddItemFailure", bag, source)!, "metadata source failure is never reused for another batch");
            target.m_customData.Clear(); source.m_customData.Clear();
            check((bool)Call("CanCacheCanAddItemFailure", bag, source)!, "ordinary metadata-free failures remain cacheable");
            int originalKey = (int)Call("ComputeCanAddItemCacheItemKey", source)!;
            source.m_cheated = true;
            check(originalKey != (int)Call("ComputeCanAddItemCacheItemKey", source)!, "failure cache distinguishes cheated identity");
        }
    }

    private static void ThrowAfterMove() => throw new InvalidOperationException("stack fixture fault");

    private static bool Equal(Dictionary<string, string> a, Dictionary<string, string> b) =>
        a.Count == b.Count && a.All(entry => b.TryGetValue(entry.Key, out string value) && value == entry.Value);

    private static ItemData Item(int count, string id, string time) => new ItemData
    {
        m_shared = new ItemData.SharedData { m_name = "smoke_food", m_maxStackSize = 20, m_itemType = ItemData.ItemType.Consumable },
        m_stack = count, m_quality = 1,
        m_customData = withPoV
            ? new Dictionary<string, string> { ["pt.food"] = "1.1", ["pt.work.made"] = time,
                ["pt.work.made.id"] = id, ["pt.work.origin"] = "brew:player:" + id, ["pt.bonus"] = "1" }
            : new Dictionary<string, string>()
    };
    private static ItemData Food(int count, string id, string time, string expiry)
    {
        var item = Item(count, id, time);
        item.m_customData[Expiry] = expiry;
        item.m_customData[Lifetime] = "800";
        return item;
    }
    private static Inventory Bag(params ItemData[] items)
    {
        var bag = new Inventory("metadata", null, 8, 4);
        bag.GetAllItems().AddRange(items);
        return bag;
    }
}
