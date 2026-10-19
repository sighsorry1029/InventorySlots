using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

internal static class PathOfValheimanChecks
{
    private static bool PlayerId(ref string __result) { __result = "pov-smoke"; return false; }
    private static bool MucPresent(ref bool __result) { __result = true; return false; }
    private static string? storageProvider;
    private static bool storageSuspended;
    private static bool unsupportedApi;
    private static bool ApiVersionCheck(ref bool __result)
    {
        if (!unsupportedApi) return true;
        __result = false;
        return false;
    }
    // Exercise API-reported external states without compiling against PoV or
    // registering a real provider/claiming this process has a loaded character.
    private static bool StorageProvider(ref string? __result) { __result = storageProvider; return false; }
    private static bool StorageSuspended(ref bool __result) { __result = storageSuspended; return false; }
    private static IEnumerable<CodeInstruction> NoLocalization()
    {
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldnull);
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ret);
    }
    // Supply only engine services needed by managed static initialization. They
    // are not gameplay assertions; original game/DLL metadata is never rewritten.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeOffset();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeHash(IntPtr span);
    private static readonly NativeOffset OffsetStub = () => 0;
    private static readonly NativeHash HashStub = _ => 0;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);
    private static bool ObjectEquality(UnityEngine.Object lhs, UnityEngine.Object rhs, ref bool __result)
    { __result = ReferenceEquals(lhs, rhs); return false; }
    private static bool allowResponse;
    private static bool allowNativeRequest;
    private static bool responseBodySawOurs;
    private static int responseBodyCalls;
    private static FieldInfo ours = null!;
    private static bool ResponseGate() => allowResponse;
    private static bool NativeRequestGate() => allowNativeRequest;
    private static IEnumerable<CodeInstruction> EmptyBody()
    {
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ret);
    }
    private static void ObserveResponseBody() { ++responseBodyCalls; responseBodySawOurs = (bool)ours.GetValue(null)!; }
    private static IEnumerable<CodeInstruction> ResponseBody()
    {
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(PathOfValheimanChecks), nameof(ObserveResponseBody)));
        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ret);
    }

    internal static int Run(Type plugin, string path)
    {
        const BindingFlags statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var pov = Assembly.LoadFrom(path);
        var harmony = new Harmony("inventory-tests.pov");
        var povHarmony = new Harmony("treextr.pathofvalheiman");
        mono_add_internal_call("UnityEngine.Animator::StringToHash_Injected", Marshal.GetFunctionPointerForDelegate(HashStub));
        mono_add_internal_call("UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject", Marshal.GetFunctionPointerForDelegate(OffsetStub));
        harmony.Patch(AccessTools.Method(typeof(UnityEngine.Object), "CompareBaseObjects"),
            prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(ObjectEquality)));
        harmony.Patch(AccessTools.PropertyGetter(Assembly.Load("assembly_guiutils").GetType("Localization", true), "instance"),
            transpiler: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(NoLocalization)));
        MethodInfo Method(string name) => plugin.GetMethod(name, statics)!;
        object? Call(string name, params object?[] args) => Method(name).Invoke(null, args);
        void Write(string name, object value) => plugin.GetField(name, statics)!.SetValue(null, value);
        Type rowsType = pov.GetType("PassiveTree.Gear.EquipRows", true)!;
        Type moduleType = pov.GetType("PassiveTree.Gear.Module", true)!;
        Type quick = pov.GetType("PassiveTree.QuickStack", true)!;
        var owner = rowsType.GetField("inventory", statics)!;
        var start = rowsType.GetField("start", statics)!;
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "pov-smoke-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        var enabled = config.Bind("test", "equipment", true, "");
        moduleType.GetField("Enabled", statics)!.SetValue(null, enabled);
        int checks = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            ++checks;
            System.Console.WriteLine("PASS " + name);
        }
        Inventory bag = new Inventory("bag", null, 8, 10); // Four backpack + six PoV storage rows in 5.0.8.
        PoVTrashChecks.Run(plugin.Assembly, pov, Check);
        PoVTidyPackChecks.Run(plugin, pov, Check);
        Inventory chest = new Inventory("chest", null, 8, 8);
        // Only managed data is used. These sentinels have no native Unity objects;
        // no transform, component lookup, Player constructor or world logic is invoked.
        Player player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, bag);
        FieldInfo local = AccessTools.Field(typeof(Player), "m_localPlayer") ??
                          AccessTools.Field(typeof(Player), "<m_localPlayer>k__BackingField");
        object? oldPlayer = local.GetValue(null);
        local.SetValue(null, player);
        object state = plugin.GetField("Runtime", statics)!.GetValue(null)!;
        state.GetType().GetField("LoadedFavoritesPlayerId")!.SetValue(state, "pov-smoke");
        var favorites = (HashSet<Vector2i>)state.GetType().GetField("FavoriteSlots")!.GetValue(state)!;
        harmony.Patch(Method("GetPlayerId"), prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(PlayerId)));
        try
        {
            var response = AccessTools.DeclaredMethod(typeof(Container), "RPC_StackResponse", new[] { typeof(long), typeof(bool) });
            Type responseHooks = quick.GetNestedType("Response", BindingFlags.NonPublic)!;
            povHarmony.Patch(response, prefix: new HarmonyMethod(responseHooks.GetMethod("Prefix", statics)),
                finalizer: new HarmonyMethod(responseHooks.GetMethod("Finalizer", statics)));
            Type storageApi = pov.GetType("Valheiman.Api.Storage", true)!;
            harmony.Patch(AccessTools.PropertyGetter(storageApi, "ProviderId"),
                prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(StorageProvider)));
            harmony.Patch(AccessTools.PropertyGetter(storageApi, "Suspended"),
                prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(StorageSuspended)));
            Type api = pov.GetType("Valheiman.Api.Pov", true)!;
            // Report an unsupported major through the real public method wrapper.
            // Do not mutate its readonly ApiVersion field: Mono may inline it.
            harmony.Patch(AccessTools.Method(api, "AtLeast"),
                prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(ApiVersionCheck)));
            string[] features = (string[])api.GetField("Features")!.GetValue(null)!;
            int storageFeature = Array.IndexOf(features, "storage");
            bool RejectsApi()
            {
                try { Call("BindPathOfValheimanCompatibility", pov, harmony); return false; }
                catch (TargetInvocationException error) { return error.InnerException is InvalidOperationException; }
            }
            try
            {
                unsupportedApi = true;
                Check(RejectsApi(), "different public API major is rejected before installing hooks");
                unsupportedApi = false;
                features[storageFeature] = "test.missing-storage";
                Check(RejectsApi(), "missing public storage capability is rejected before installing hooks");
                Check(Harmony.GetPatchInfo(quick.GetMethod("Sort", statics))?.Owners.Contains(harmony.Id) != true &&
                      Harmony.GetPatchInfo(response)!.Owners.Contains(povHarmony.Id),
                    "rejected API leaves PoV action and response hooks untouched");
            }
            finally
            {
                unsupportedApi = false;
                features[storageFeature] = "storage";
            }
            Call("BindPathOfValheimanCompatibility", pov, harmony);
            PoVStackMetadataChecks.Run(plugin, pov, Check);
            Check(!rowsType.IsPublic, "original PoV equipment type remains internal");
            foreach (string name in new[] { "_povBackpackRows", "_povIsCellPosition", "_povEquipmentEnabled", "_povStorageProvider", "_povStorageSuspended" })
            {
                MethodInfo bound = ((Delegate)plugin.GetField(name, statics)!.GetValue(null)!).Method;
                Check(bound.IsPublic && bound.DeclaringType!.IsVisible && bound.DeclaringType.Namespace == "Valheiman.Api",
                    name + " binds the original public API");
            }
            foreach (string method in new[] { "Sort", "Run", "Movable", "ForeignCell" })
                Check(Harmony.GetPatchInfo(quick.GetMethod(method, statics))!.Owners.Contains(harmony.Id), "actual PoV " + method + " accepts compatibility hook");

            owner.SetValue(null, bag);
            start.SetValue(null, 4);
            int Rows(Inventory inventory) => (int)Call("GetRegularPlayerRowsOrInventoryHeight", inventory)!;
            List<Vector2i> Cells(Inventory inventory) => ((IEnumerable<Vector2i>)Call("GetInventorySlotsTopFirst", inventory)!).ToList();
            Check(Rows(bag) == 4 && Rows(chest) == 8, "PoV owns only the player boundary; chest capacity is unchanged");
            Check(Cells(bag).Count == 32 && Cells(chest).Count == 64, "top-first destinations exclude all six hidden rows");
            Check(!(bool)Call("CanFavoriteCell", bag, new Vector2i(0, 4))! &&
                  !(bool)Call("CanTrashCell", bag, new Vector2i(0, 4))!, "hidden cells cannot be favorited or trashed");
            enabled.Value = false;
            Check(Rows(bag) == 4, "Equipment Off still protects rows awaiting delayed release");
            AccessTools.Field(typeof(Inventory), "m_height").SetValue(bag, 12);
            Check(Rows(bag) == 4, "physical height alone does not move PoV's owned boundary");
            start.SetValue(null, 6);
            Check(Rows(bag) == 6 && Cells(bag).Count == 48, "live BigBag height changes update destinations without stale caching");
            AccessTools.Field(typeof(Inventory), "m_height").SetValue(bag, 10);
            start.SetValue(null, 4);
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, true);
            Check(Rows(bag) == 0 && Cells(bag).Count == 0 && Rows(chest) == 8 && !(bool)Call("PovSortStarting", player)!,
                "character loading blocks native and IA player actions even while rows are owned");
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, false);
            Check(Rows(bag) == 4, "loaded owned inventory resumes without a cached loading state");

            ItemDrop.ItemData Item(int x, int y, bool equipped = false) => new ItemDrop.ItemData
            {
                m_gridPos = new Vector2i(x, y), m_stack = 2, m_equipped = equipped,
                m_shared = new ItemDrop.ItemData.SharedData { m_name = "Wood", m_maxStackSize = 50 }
            };
            var hidden = Item(0, 4);
            var equipped = Item(1, 1, true);
            var favorite = Item(2, 1);
            var ordinary = Item(3, 1);
            bag.GetAllItems().AddRange(new[] { hidden, equipped, favorite, ordinary });
            favorites.Add(favorite.m_gridPos);
            favorites.Add(new Vector2i(0, 1)); // Empty favorite also reserves a sort destination.
            var movable = new List<ItemDrop.ItemData> { hidden, equipped, favorite, ordinary };
            Call("PovFilterMovableItems", player, movable);
            Check(movable.SequenceEqual(new[] { ordinary }), "native candidates retain only ordinary, nonfavorite, unequipped items");
            Check((bool)quick.GetMethod("ForeignCell", statics)!.Invoke(null, new object[] { new Vector2i(0, 1) })!, "patched PoV destination helper protects empty favorites");
            Check((bool)quick.GetMethod("ForeignCell", statics)!.Invoke(null, new object[] { new Vector2i(7, 7) })!, "patched PoV destination helper protects empty hidden cells");
            var actionCells = (List<Vector2i>)Call("GetPlayerActionSlots", player, bag)!;
            Check(!actionCells.Contains(equipped.m_gridPos) && !actionCells.Contains(hidden.m_gridPos) &&
                  !actionCells.Contains(favorite.m_gridPos) && !actionCells.Contains(new Vector2i(0, 1)),
                "IA sort/take-all destinations reserve equipment and full/empty favorites");
            Check(!(bool)Call("IsRegularActionItem", bag, hidden)! && !(bool)Call("IsRegularActionItem", bag, equipped)!,
                "quick-stack/store/take-stacks reject PoV-managed items");
            Check(!(bool)Call("CanTrashCell", bag, equipped.m_gridPos)!, "equipped item awaiting PoV settlement cannot be trashed");
            var earlier = Item(4, 2); earlier.m_shared.m_name = "A-test";
            bag.GetAllItems().Add(earlier);
            quick.GetMethod("Sort", statics)!.Invoke(null, new object[] { player });
            Check(earlier.m_gridPos == new Vector2i(3, 1) && ordinary.m_gridPos == new Vector2i(4, 1) &&
                  hidden.m_gridPos == new Vector2i(0, 4) && favorite.m_gridPos == new Vector2i(2, 1) &&
                  equipped.m_gridPos == new Vector2i(1, 1) && hidden.m_stack == 2 && favorite.m_stack == 2,
                "actual PoV Sort reaches patched helpers and preserves reserved cells and quantities");
            Check(!(bool)Call("CanMoveItemToInventoryTopFirst", bag, chest, hidden)! &&
                  (int)Call("MoveItemToInventoryTopFirst", bag, chest, hidden, null)! == 0,
                "protected source cannot be moved or unequipped through IA");

            // Fill ordinary cells: the only available space/partial stack is hidden.
            var source = Item(0, 0);
            chest.GetAllItems().Add(source);
            bag.GetAllItems().Clear();
            bag.GetAllItems().Add(hidden);
            for (int y = 0; y < 4; ++y)
            for (int x = 0; x < 8; ++x)
            {
                var full = Item(x, y); full.m_stack = 50; bag.GetAllItems().Add(full);
            }
            Check(!(bool)Call("CanMoveItemToInventoryTopFirst", chest, bag, source)! &&
                  (int)Call("MoveItemToInventoryTopFirst", chest, bag, source, null)! == 0 && hidden.m_stack == 2 && source.m_stack == 2,
                "Ctrl-move cannot merge into hidden partial stacks or occupy hidden empty cells");

            owner.SetValue(null, null);
            AccessTools.Field(typeof(Inventory), "m_height").SetValue(bag, 4);
            Check(Rows(bag) == 4, "released inventory with Equipment Off returns to ordinary actions");
            storageProvider = "test.external";
            Check(Rows(bag) == 0 && Cells(bag).Count == 0 && Rows(chest) == 8 && !(bool)Call("PovSortStarting", player)!,
                "external provider cannot expose empty reserved cells even with Equipment Off");
            enabled.Value = true;
            Check(Rows(bag) == 0, "registered external provider is not assumed ready with Equipment On");
            storageProvider = null;
            storageSuspended = true;
            Check(Rows(bag) == 0 && !(bool)Call("PovSortStarting", player)!, "suspended storage blocks actions independently of provider ID");
            storageSuspended = false;
            Check(Rows(bag) == 0 && Rows(chest) == 8, "enabled equipment awaiting Attach blocks only player actions");
            object?[] scope = { player, false };
            Check(!(bool)Method("PovQuickStackStarting").Invoke(null, scope)! && !(bool)scope[1]!,
                "native PoV quick stack cannot start before storage is ready");
            owner.SetValue(null, bag);
            AccessTools.Field(typeof(Inventory), "m_height").SetValue(bag, 10);
            Check(Rows(bag) == 4 && (bool)Call("PovSortStarting", player)!, "native storage resumes after attach without reinitializing the adapter");
            Check((bool)Method("PovQuickStackStarting").Invoke(null, scope)! && (bool)scope[1]!, "PoV native request scope begins");
            Check((int)plugin.GetField("_povQuickStackDepth", statics)!.GetValue(null)! == 1, "scope marks only synchronous PoV dispatch");
            Call("PovQuickStackFinished", true);
            Call("PovQuickStackFinished", false);
            Check((int)plugin.GetField("_povQuickStackDepth", statics)!.GetValue(null)! == 0, "finalizer balances entered scope and ignores skipped calls");
            bool runFailed = false;
            try { quick.GetMethod("Run", statics)!.Invoke(null, new object[] { player }); }
            catch (TargetInvocationException) { runFailed = true; } // No Game/world exists in this process.
            Check(runFailed && (int)plugin.GetField("_povQuickStackDepth", statics)!.GetValue(null)! == 0,
                "exception in original PoV Run propagates and restores dispatch scope");
            var hasMuc = AccessTools.PropertyGetter(plugin, "HasExternalMultiUserChestActive");
            harmony.Patch(hasMuc, prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(MucPresent)));
            scope[1] = false;
            Check(!(bool)Method("PovQuickStackStarting").Invoke(null, scope)! && !(bool)scope[1]!,
                "MUC blocks PoV's native transfer without entering its dispatch scope");
            harmony.Unpatch(hasMuc, AccessTools.Method(typeof(PathOfValheimanChecks), nameof(MucPresent)));

            // Exercise HarmonyX's real prefix ordering and __runOriginal. The RPC
            // body is an observer: this tests state handling, not network validation.
            harmony.Patch(response, prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(ResponseGate)) { priority = Priority.First },
                transpiler: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(ResponseBody)));
            var asked = (HashSet<Container>)quick.GetField("asked", statics)!.GetValue(null)!;
            ours = quick.GetField("ours", statics)!;
            var box = (Container)FormatterServices.GetUninitializedObject(typeof(Container));
            asked.Add(box);
            allowResponse = false;
            response.Invoke(box, new object[] { 1L, true });
            Check(asked.Contains(box) && !(bool)ours.GetValue(null)! && !responseBodySawOurs,
                "earlier rejected RPC does not consume PoV's pending request");
            allowResponse = true;
            response.Invoke(box, new object[] { 1L, true });
            Check(!asked.Contains(box) && responseBodySawOurs && !(bool)ours.GetValue(null)!,
                "later accepted RPC enters PoV protection and finalizer restores it");
            responseBodySawOurs = false;
            response.Invoke(box, new object[] { 1L, true });
            Check(!responseBodySawOurs && responseBodyCalls == 1, "duplicate response cannot reach unprotected native StackAll");
            var nativeRequest = AccessTools.DeclaredMethod(typeof(Container), nameof(Container.StackAll));
            harmony.Patch(nativeRequest,
                prefix: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(NativeRequestGate)) { priority = Priority.Last },
                transpiler: new HarmonyMethod(typeof(PathOfValheimanChecks), nameof(EmptyBody)));
            Check(Harmony.GetPatchInfo(nativeRequest)!.Prefixes.Single(p => p.PatchMethod == Method("PovNativeStackStarting")).priority < Priority.Last,
                "registered native request observer runs after the shared Last-priority fence");
            allowNativeRequest = false;
            nativeRequest.Invoke(box, null);
            response.Invoke(box, new object[] { 1L, true });
            Check(responseBodyCalls == 1, "rejected native request cannot reopen a completed response");
            allowNativeRequest = true;
            nativeRequest.Invoke(box, null);
            asked.Add(box);
            response.Invoke(box, new object[] { 1L, true });
            Check(responseBodyCalls == 2 && responseBodySawOurs, "new permitted request reopens PoV response processing");
            Call("PovNetworkStopped");
            response.Invoke(box, new object[] { 1L, true });
            Check(responseBodyCalls == 3, "network stop releases response fences from the old session");
            owner.SetValue(null, bag);
            var backpack = (Func<Player, int>)plugin.GetField("_povBackpackRows", statics)!.GetValue(null)!;
            foreach (int invalid in new[] { 0, bag.GetHeight(), bag.GetHeight() + 1 })
            {
                Write("_povBackpackRows", new Func<Player, int>(_ => invalid));
                Check(Rows(bag) == 0 && Cells(bag).Count == 0, "invalid public backpack boundary fails closed: " + invalid);
                Write("_povUnavailable", false); // Only fault injection resets this; production requires restart.
            }
            Write("_povBackpackRows", backpack);
            var isCell = (Func<Player, Vector2i, bool>)plugin.GetField("_povIsCellPosition", statics)!.GetValue(null)!;
            Write("_povIsCellPosition", new Func<Player, Vector2i, bool>((_, __) => throw new InvalidOperationException("cell probe failure")));
            Check((bool)Call("IsPathOfValheimanProtectedCell", bag, new Vector2i(0, 0))! && Rows(bag) == 0,
                "cell API failure protects the current cell and blocks subsequent player actions");
            Write("_povIsCellPosition", isCell);
            Write("_povUnavailable", false);
            Write("_povBackpackRows", new Func<Player, int>(_ => throw new InvalidOperationException("probe failure")));
            Check(Rows(bag) == 0 && Cells(bag).Count == 0 && Rows(chest) == 8, "lookup failure blocks player actions without blocking chest cells");
            Check(!(bool)Call("PovSortStarting", player)!, "native PoV sort also stops after integration failure");
            Write("_povPresent", false);
            Check(Rows(bag) == 10 && Cells(bag).Count == 80, "PoV absent leaves existing inventory behavior unchanged");
        }
        finally
        {
            local.SetValue(null, oldPlayer);
            owner.SetValue(null, null);
            favorites.Clear();
            harmony.UnpatchSelf();
            povHarmony.UnpatchSelf();
        }
        return checks;
    }
}
