using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static int sweepStarts;
    private static bool effectiveStackMessage;
    private static bool fakeAzu;
    private static int fakeAzuStock;
    private static bool brokenStock;
    private static bool FakeStart() { sweepStarts++; return false; }
    private static void CaptureStackMessage(ref bool message) { effectiveStackMessage = message; message = false; }
    private static bool AzuAvailable(ref bool __result) { __result = fakeAzu; return false; }
    private static bool AzuStock(ref int __result) { __result = fakeAzuStock; return false; }
    private static bool CacheKey(ref string __result) { __result = "fixture"; return false; }
    private static bool BreakStock() { if (brokenStock) throw new InvalidOperationException("fixture unloaded chest"); return true; }
    private static UnityEngine.GameObject? fixtureObject;
    private static bool GameObject(ref UnityEngine.GameObject __result) { __result = fixtureObject!; return false; }

    private static void ExtendedChecks(Assembly slots, Assembly vplus, Type compat, Type[] types, Harmony native,
        Harmony seams, ConfigFile config, Type configuration, object current)
    {
        var warnings = new List<string>();
        void Install() => compat.GetMethod("Install", Statics)!.Invoke(null, new object[] { vplus, (Action<string>)(s => { warnings.Add(s); System.Console.WriteLine(s); }) });
        void Dispose() => compat.GetMethod("Dispose", Statics)!.Invoke(null, null);
        bool Active(string name) => (bool)compat.GetProperty(name, Statics)!.GetValue(null)!;
        void RegisterAll() { foreach (Type t in types) native.CreateClassProcessor(t).Patch(); }
        RegisterAll();
        var own = new Harmony("smoke.extended");
        AccessTools.Field(typeof(Game), "<instance>k__BackingField").SetValue(null, FormatterServices.GetUninitializedObject(typeof(Game)));
        own.Patch(AccessTools.Method(typeof(Game), "IncrementPlayerStat"), prefix: new HarmonyMethod(typeof(Program), nameof(NoLog)));
        Type sweep = vplus.GetType("ValheimPlus.GameClasses.AutoStackSweep", true)!;
        MethodInfo stackAll = AccessTools.Method(typeof(Inventory), "StackAll", new[] { typeof(Inventory), typeof(bool) });
        own.Patch(AccessTools.Method(sweep, "Start"), prefix: new HarmonyMethod(typeof(Program), nameof(FakeStart)));
        own.Patch(stackAll, prefix: new HarmonyMethod(typeof(Program), nameof(CaptureStackMessage)) { priority = Priority.Last });
        var player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        var gui = (InventoryGui)FormatterServices.GetUninitializedObject(typeof(InventoryGui));
        var chest = (Container)FormatterServices.GetUninitializedObject(typeof(Container));
        AccessTools.Field(typeof(Player), "m_localPlayer").SetValue(null, player);
        AccessTools.Field(typeof(InventoryGui), "m_instance").SetValue(null, gui);
        AccessTools.Field(typeof(InventoryGui), "m_currentContainer").SetValue(gui, chest);
        (Inventory to, Inventory from, ItemData destination) StackFixture()
        {
            var pair = Pair("same", "same");
            AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, pair.from);
            AccessTools.Field(typeof(Container), "m_inventory").SetValue(chest, pair.to);
            sweepStarts = 0; effectiveStackMessage = true;
            return pair;
        }
        var before = StackFixture(); before.to.StackAll(before.from, true);
        Check(before.destination.m_stack == 5 && sweepStarts == 1 && !effectiveStackMessage,
            "warmed native StackAll baseline starts V+ sweep and takes over message");
        Install();
        Check(warnings.Count == 0 && Active("StackActive") && Active("CraftingAvailable"), "extended contracts installed");
        var after = StackFixture(); after.to.StackAll(after.from, true);
        Check(after.destination.m_stack == 5 && after.from.NrOfItems() == 0 && sweepStarts == 0 && effectiveStackMessage,
            "AutoStack gate preserves initial native transfer/message without starting nearby work");
        native.UnpatchSelf(); RegisterAll();
        var repatch = StackFixture(); repatch.to.StackAll(repatch.from, true);
        Check(sweepStarts == 0 && effectiveStackMessage && repatch.destination.m_stack == 5, "AutoStack gate survives native owner repatch");

        // Real V+ stock helper, original Container inventories and counting code.
        // Seed its normal cached chest list, avoiding Unity physics/world queries.
        fixtureObject = (UnityEngine.GameObject)FormatterServices.GetUninitializedObject(typeof(UnityEngine.GameObject));
        own.Patch(AccessTools.PropertyGetter(typeof(UnityEngine.Component), "gameObject"), prefix: new HarmonyMethod(typeof(Program), nameof(GameObject)));
        Type cache = vplus.GetType("ValheimPlus.GameClasses.Inventory_NearbyChests_Cache", true)!;
        var watch = (Stopwatch)AccessTools.Field(cache, "delta").GetValue(null);
        AccessTools.Field(cache, "chests").SetValue(null, new List<Container> { chest });
        config[new ConfigDefinition("CraftFromChest", "checkFromWorkbench")].BoxedValue = false;
        config[new ConfigDefinition("CraftFromChest", "lookupInterval")].BoxedValue = 10;
        var stockPair = Pair("same", "same");
        stockPair.destination.m_stack = 5; stockPair.from.GetAllItems()[0].m_stack = 2;
        AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, stockPair.from);
        AccessTools.Field(typeof(Container), "m_inventory").SetValue(chest, stockPair.to);
        var resource = (ItemDrop)FormatterServices.GetUninitializedObject(typeof(ItemDrop));
        resource.m_itemData = stockPair.destination;
        var requirement = new Piece.Requirement { m_resItem = resource, m_amount = 1 };
        MethodInfo query = compat.GetMethod("TryGetCraftingStock", Statics)!;
        (bool ok, int count) Query()
        {
            watch.Restart(); object[] a = { 2, requirement, player, 0 };
            bool ok = (bool)query.Invoke(null, a)!; return (ok, (int)a[3]);
        }
        Check(Active("CraftingActive") && Query() == (true, 7), "private V+ stock helper returns inventory plus cached chest once");
        Type plugin = slots.GetType("InventorySlots.InventorySlotsPlugin", true)!;
        own.Patch(plugin.GetMethod("HasAzuCraftyBoxesCraftingSource", Statics), prefix: new HarmonyMethod(typeof(Program), nameof(AzuAvailable)));
        own.Patch(plugin.GetMethod("GetAzuCraftyBoxesAvailableCraftingRequirementAmount", Statics), prefix: new HarmonyMethod(typeof(Program), nameof(AzuStock)));
        own.Patch(plugin.GetMethod("GetCraftingRequirementAvailabilityCacheKey", Statics), prefix: new HarmonyMethod(typeof(Program), nameof(CacheKey)));
        MethodInfo display = plugin.GetMethod("GetAvailableCraftingRequirementAmount", Statics)!;
        void Clear() => plugin.GetMethod("ClearCraftingRequirementAvailabilityCache", Statics)!.Invoke(null, null);
        int Display() { watch.Restart(); return (int)display.Invoke(null, new object[] { requirement, null! })!; }
        fakeAzu = false; Clear(); Check(Display() == 7, "production display uses V+ stock");
        fakeAzu = true; fakeAzuStock = 0;
        Check(Display() == 2, "valid Azu zero wins and does not reuse V+ cache");
        fakeAzuStock = 5; Clear(); Check(Display() == 7, "overlapping providers do not double-count chest stock");
        fakeAzu = false; Clear(); Check(Display() == 7, "V+ resumes when Azu unavailable");
        config[new ConfigDefinition("CraftFromChest", "enabled")].BoxedValue = false;
        Check(!Active("CraftingActive") && Display() == 2, "disable switches cache to inventory-only immediately");
        configuration.GetProperty("Current")!.SetValue(null, null);
        Check(!Active("CraftingActive"), "null current configuration safely disables stock display");
        configuration.GetProperty("Current")!.SetValue(null, current);
        MethodInfo requirements = AccessTools.Method(typeof(Player), "HaveRequirementItems", new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) });
        native.CreateProcessor(requirements).Patch();
        config[new ConfigDefinition("CraftFromChest", "enabled")].BoxedValue = true;
        Check(!Active("CraftingActive"), "enabled config alone cannot claim a helper absent from emitted query");
        native.CreateProcessor(requirements).Patch();
        Check(Active("CraftingActive") && Query() == (true, 7), "repatch emission reactivates real stock helper");
        var replacementConfig = new ConfigFile(config.ConfigFilePath + ".replacement", false) { SaveOnConfigSet = false };
        object replacement = Activator.CreateInstance(configuration)!;
        Type craftingSettings = vplus.GetType("ValheimPlus.Configurations.Sections.CraftFromChestConfiguration", true)!;
        object replacementSection = Activator.CreateInstance(craftingSettings)!;
        craftingSettings.GetMethod("Bind")!.Invoke(replacementSection, new object[] { replacementConfig });
        configuration.GetProperty("CraftFromChest")!.SetValue(replacement, replacementSection);
        configuration.GetProperty("Current")!.SetValue(null, replacement);
        Check(!Active("CraftingActive"), "replacement configuration does not reuse enabled old object");
        replacementConfig[new ConfigDefinition("CraftFromChest", "enabled")].BoxedValue = true;
        Check(Active("CraftingActive"), "replacement configuration live value is read");
        configuration.GetProperty("Current")!.SetValue(null, current);
        own.Patch(AccessTools.Method(types[5], "ComputeItemQuantity"), prefix: new HarmonyMethod(typeof(Program), nameof(BreakStock)));
        brokenStock = true;
        int warningCount = warnings.Count;
        Check(Query() == (false, 2) && Query() == (false, 2) && warnings.Count == warningCount + 1, "transient chest failure falls back without repeated warnings");
        brokenStock = false; fixtureTime += 2f;
        Check(Active("CraftingActive") && Query() == (true, 7), "stock display recovers after transient failure cooldown");

        Dispose();
        var restored = StackFixture(); restored.to.StackAll(restored.from, true);
        Check(sweepStarts == 1 && !effectiveStackMessage, "teardown restores native V+ AutoStack entry");
        foreach (string field in new[] { "running", "stacking", "Pending", "Granted", "CutOff" })
        {
            FieldInfo f = AccessTools.Field(sweep, field);
            if (f.FieldType == typeof(bool)) f.SetValue(null, true);
            else ((HashSet<Container>)f.GetValue(null)).Add(chest);
            warnings.Clear(); Install();
            Check(!Active("StackActive") && Active("RowsActive") && Active("CraftingAvailable") && warnings.Count == 1,
                "busy " + field + " refuses only new sweep guard");
            Check(f.FieldType == typeof(bool) ? (bool)f.GetValue(null) : ((HashSet<Container>)f.GetValue(null)).Contains(chest), "busy " + field + " state retained");
            Dispose();
            if (f.FieldType == typeof(bool)) f.SetValue(null, false); else ((HashSet<Container>)f.GetValue(null)).Clear();
        }
        warnings.Clear(); native.Unpatch(stackAll, HarmonyPatchType.Postfix, native.Id); Install();
        Check(!Active("StackActive") && warnings.Count == 1 && Active("CraftingAvailable"), "partial AutoStack registration rejected independently");
        Dispose(); warnings.Clear(); native.Unpatch(stackAll, HarmonyPatchType.All, native.Id); native.CreateClassProcessor(types[4]).Patch();
        native.Unpatch(requirements, HarmonyPatchType.Transpiler, native.Id); Install();
        Check(!Active("CraftingAvailable") && Active("StackActive") && Active("RowsActive") && Active("MergeActive") && warnings.Count == 1,
            "missing crafting query registration rejects only stock display");
        Dispose(); own.UnpatchSelf(); native.UnpatchSelf();
        AccessTools.Field(typeof(Player), "m_localPlayer").SetValue(null, null);
        AccessTools.Field(typeof(InventoryGui), "m_instance").SetValue(null, null);
        AccessTools.Field(typeof(Game), "<instance>k__BackingField").SetValue(null, null);
        AccessTools.Field(cache, "chests").SetValue(null, new List<Container>());
        CheckPickupOrder(slots, types[6], native, config);
    }

    private static void CheckPickupOrder(Assembly slots, Type vplusPatch, Harmony native, ConfigFile config)
    {
        var own = new Harmony("sighsorry.InventorySlots");
        Type ours = slots.GetType("InventorySlots.AutoPickupCapacityPatch", true)!;
        MethodInfo target = AccessTools.Method(typeof(Player), "AutoPickup", new[] { typeof(float) });
        MethodInfo capacity = AccessTools.Method(typeof(Inventory), "CanAddItem", new[] { typeof(ItemData), typeof(int) });
        MethodInfo originalLimit = AccessTools.Method(typeof(Player), "GetMaxCarryWeight", Type.EmptyTypes);
        MethodInfo vplusLimit = AccessTools.Method(vplusPatch, "GetMaxCarryWeight", Type.EmptyTypes);
        config[new ConfigDefinition("Player", "enabled")].BoxedValue = true;
        foreach (bool enabled in new[] { false, true })
        foreach (bool vplusFirst in new[] { false, true })
        {
            config[new ConfigDefinition("Player", "autoPickUpWhenEncumbered")].BoxedValue = enabled;
            if (vplusFirst) { native.CreateClassProcessor(vplusPatch).Patch(); own.CreateClassProcessor(ours).Patch(); }
            else { own.CreateClassProcessor(ours).Patch(); native.CreateClassProcessor(vplusPatch).Patch(); }
            var code = PatchProcessor.GetCurrentInstructions(target).ToList();
            MethodInfo limit = enabled ? vplusLimit : originalLimit;
            int at = code.FindIndex(c => c.Calls(limit));
            Check(code.Count(c => c.Calls(limit)) == 1 && at < code.FindIndex(c => c.Calls(capacity)) &&
                  code.Count(c => c.Calls(enabled ? originalLimit : vplusLimit)) == 0,
                  $"actual pickup IL keeps weight before capacity: V+ enabled={enabled}, registered first={vplusFirst}");
            Check(code[at - 1].opcode == (enabled ? OpCodes.Nop : OpCodes.Ldarg_0), "pickup replacement preserves static/instance call stack shape");
            own.UnpatchSelf(); native.UnpatchSelf();
        }
    }
}
