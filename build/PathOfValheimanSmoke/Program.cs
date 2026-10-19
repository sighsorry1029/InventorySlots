using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instances = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeOffset();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeHash(IntPtr span);
    private static readonly NativeOffset OffsetStub = () => 0;
    private static readonly NativeHash HashStub = _ => 0;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate float NativeTime();
    private static readonly NativeTime TimeStub = () => 1f;
    private static ItemData? quiverFixtureItem, coinFixtureItem;
    private static bool QuiverPrefab(ItemData item, ref string __result)
    { if (ReferenceEquals(item, coinFixtureItem) && item != null) { __result = "Coins"; return false; } if (!ReferenceEquals(item, quiverFixtureItem)) return true; __result = "SmokeQuiver"; return false; }
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);
    private static bool ObjectEquality(UnityEngine.Object lhs, UnityEngine.Object rhs, ref bool __result)
    { __result = ReferenceEquals(lhs, rhs); return false; }
    private static bool ParseColor(string htmlString, ref Color color, ref bool __result)
    {
        string hex = (htmlString ?? "").TrimStart('#');
        uint value;
        __result = (hex.Length == 6 || hex.Length == 8) && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
        if (__result) { value = Convert.ToUInt32(hex, 16); if (hex.Length == 6) value = (value << 8) | 255; color = new Color((value >> 24) / 255f, ((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f); }
        return false;
    }
    private static bool NoProgress() => false;
    private static bool FixturePlayerId(ref long __result) { __result = 1; return false; }
    private static int ordinaryTrinkets = 3, totalTrinkets = 4;
    private static bool queueTrinket;
    private static ItemData? queuedTrinket;
    private static bool OrdinaryTrinkets(ref int __result) { __result = ordinaryTrinkets; return false; }
    private static bool TotalTrinkets(ref int __result) { __result = totalTrinkets; return false; }
    private static bool ZeroAdrenaline(ref float __result) { __result = 0f; return false; }
    private static bool QueuedTrinket(ItemData item, ref bool __result) { __result = ReferenceEquals(item, queuedTrinket); return false; }
    private static bool TrinketUse(Humanoid __instance, ItemData item)
    {
        if (queueTrinket) queuedTrinket = item;
        else if (__instance.IsItemEquiped(item)) __instance.UnequipItem(item, true);
        else __instance.EquipItem(item, true);
        return false;
    }
    private static bool VanillaTrinketEquip(Humanoid humanoid, ItemData item)
    { AccessTools.Field(typeof(Humanoid), "m_trinketItem").SetValue(humanoid, item); item.m_equipped = true; return true; }
    private static void VanillaTrinketUnequip(Humanoid humanoid, ItemData item)
    {
        FieldInfo field = AccessTools.Field(typeof(Humanoid), "m_trinketItem");
        if (ReferenceEquals(field.GetValue(humanoid), item)) field.SetValue(humanoid, null);
        item.m_equipped = false;
    }
    private static IEnumerable<CodeInstruction> VanillaTrinketBody(MethodBase __originalMethod)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0); yield return new CodeInstruction(OpCodes.Ldarg_1);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Program), __originalMethod.Name == "EquipItem" ? nameof(VanillaTrinketEquip) : nameof(VanillaTrinketUnequip)));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    private static IEnumerable<CodeInstruction> NoLocalization()
    { yield return new CodeInstruction(OpCodes.Ldnull); yield return new CodeInstruction(OpCodes.Ret); }

    private static int Main(string[] args)
    {
        if (args.Length != 5 && args.Length != 6) throw new ArgumentException("final InventorySlots.dll original PoV.dll original Managed BepInEx/core cold|warm [optional dependency directory]");
        string[] roots = { args[2], args[3], Path.GetDirectoryName(args[0])!,
            args.Length == 6 ? args[5] : Path.Combine(Directory.GetParent(Path.GetDirectoryName(args[1])!)!.Parent!.FullName,"ValheimModding-Jotunn") };
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) =>
        {
            string file = new AssemblyName(request.Name).Name + ".dll";
            foreach (string root in roots) { string path = Path.Combine(root, file); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        try { return Run(args, roots); }
        catch (Exception error) { System.Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args, string[] roots)
    {
        Assembly bep = Assembly.LoadFrom(Path.Combine(roots[1], "BepInEx.dll"));
        string isolated = Path.Combine(Path.GetTempPath(), "pov-storage-smoke-" + Guid.NewGuid());
        bep.GetType("BepInEx.Paths", true)!.GetMethod("SetExecutablePath", Statics)!.Invoke(null,
            new object[] { Path.Combine(isolated, "valheim.exe"), Path.Combine(isolated, "BepInEx"), roots[0], roots });
        Type threading = bep.GetType("BepInEx.ThreadingHelper", true)!;
        object queue = FormatterServices.GetUninitializedObject(threading);
        threading.GetField("_invokeLock", Instances)!.SetValue(queue, new object());
        threading.GetField("<Instance>k__BackingField", Statics)!.SetValue(null, queue);
        mono_add_internal_call("UnityEngine.Animator::StringToHash_Injected", Marshal.GetFunctionPointerForDelegate(HashStub));
        mono_add_internal_call("UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject", Marshal.GetFunctionPointerForDelegate(OffsetStub));
        mono_add_internal_call("UnityEngine.Time::get_time", Marshal.GetFunctionPointerForDelegate(TimeStub));
        mono_add_internal_call("UnityEngine.Time::get_unscaledTime", Marshal.GetFunctionPointerForDelegate(TimeStub));
        mono_add_internal_call("UnityEngine.Time::get_frameCount", Marshal.GetFunctionPointerForDelegate(OffsetStub));
        var engine = new Harmony("smoke.pov.storage.engine-services");
        engine.Patch(AccessTools.Method(typeof(UnityEngine.Object), "CompareBaseObjects"),
            prefix: new HarmonyMethod(typeof(Program), nameof(ObjectEquality)));
        engine.Patch(AccessTools.PropertyGetter(Assembly.Load("assembly_guiutils").GetType("Localization", true), "instance"),
            transpiler: new HarmonyMethod(typeof(Program), nameof(NoLocalization)));
        engine.Patch(AccessTools.Method(typeof(ColorUtility), "TryParseHtmlString"),
            prefix: new HarmonyMethod(typeof(Program), nameof(ParseColor)));
        Assembly mod = Assembly.LoadFrom(args[0]);
        Check(!mod.GetReferencedAssemblies().Any(a => a.Name == "PathOfValheiman"), "final IS has no mandatory PoV assembly reference");
        Check(mod.GetTypes().Length > 0, "final IS types enumerate before original PoV assembly loads");
        Assembly pov = Assembly.LoadFrom(args[1]);
        PoVTrashChecks.Run(mod, pov, Check);
        Type bridgeType = mod.GetType("InventorySlots.PathOfValheimanStorageBridge", true)!;
        Type contract = pov.GetType("Valheiman.Api.IStorageProvider", true)!;
        Type equipment = pov.GetType("Valheiman.Api.Equipment", true)!;
        Type storage = pov.GetType("Valheiman.Api.Storage", true)!;
        Type state = pov.GetType("PassiveTree.Gear.State", true)!;
        Type slot = pov.GetType("PassiveTree.Gear.Slot", true)!;
        Type cells = pov.GetType("PassiveTree.Gear.Cells", true)!;
        MethodInfo isStorage = state.GetMethod("IsStorage", Statics)!;
        int nativeCount = Enum.GetValues(slot).Length;
        bool[] baseline = Enumerable.Range(0, nativeCount).Select(i => (bool)isStorage.Invoke(null, new[] { Enum.ToObject(slot, i) })!).ToArray();
        Check(!baseline.Skip(24).Take(8).Any(x => x), "original wallet classification omission present");
        object? oldEquipment = null, oldStorage = null;
        if (args[4] == "warm")
        {
            oldEquipment = equipment.GetProperty("Cells")!.GetValue(null);
            oldStorage = storage.GetProperty("Cells")!.GetValue(null);
            Check(((IEnumerable<string>)oldStorage!).Count() == 28, "warm original cache has 28 storage cells");
        }
        var patches = new Harmony("smoke.pov.storage.bridge");
        object bridge;
        ConstructorInfo? newer = bridgeType.GetConstructor(Instances, null, new[] { typeof(Assembly) }, null);
        if (newer != null)
        {
            bridge = newer.Invoke(new object[] { pov });
            bridgeType.GetMethod("InstallFixes", Instances)!.Invoke(bridge, new object[] { patches });
        }
        else bridge = bridgeType.GetConstructor(Instances, null, new[] { typeof(Assembly), typeof(Harmony) }, null)!.Invoke(new object[] { pov, patches });
        Check(true, "actual bridge reflection bindings and Harmony patch installation succeed on original Mono");
        Type plugin = mod.GetType("InventorySlots.InventorySlotsPlugin", true)!;
        foreach (MethodInfo method in new[] { AccessTools.Method(state, "Assign"), AccessTools.Method(pov.GetType("PassiveTree.Gear.UtilitySlots"), "Tick") })
        {
            var original = PatchProcessor.GetOriginalInstructions(method).ToList();
            var patched = ((IEnumerable<CodeInstruction>)plugin.GetMethod("DeferPathOfValheimanUtilityUnequip", Statics)!.Invoke(null, new object[] { original })!).ToList();
            MethodInfo unequip = AccessTools.Method(typeof(Humanoid), "UnequipItem", new[] { typeof(ItemData), typeof(bool) });
            int deferredCalls = 0; bool effectsPreserved = true;
            for (int i = 0; i < patched.Count; i++)
            {
                if (patched[i].operand is MethodInfo m && m.Name == "PathOfValheimanUtilityUnequip") deferredCalls++;
                if (patched[i].Calls(unequip) && (i == 0 || patched[i - 1].opcode != OpCodes.Ldc_I4_1)) effectsPreserved = false;
            }
            Check(deferredCalls == 1 && effectsPreserved,
                  method.Name + " changes only utility hand-off; trinket replacement unequip remains intact");
        }
        plugin.GetMethod("BindPathOfValheimanCrafting", Statics)!.Invoke(null, new object[] { pov });
        Check(true, "actual crafting binding succeeds against original triple Returns and float ReturnChance");
        Delegate returns = (Delegate)plugin.GetField("_povSalvageReturns", Statics)!.GetValue(null)!;
        Check(returns.Method.IsPrivate && returns.Method.ReturnType == typeof(List<ValueTuple<ItemDrop, int, int>>), "salvage Returns uses explicit cached private triple delegate");
        Check(((ICollection)returns.DynamicInvoke(new object?[] { null, null })!).Count == 0, "private original Returns delegate executes on original Mono for empty recipe");
        plugin.GetMethod("BindPathOfValheimanCraftingChests", Statics)!.Invoke(null, new object[] { pov });
        Check(true, "actual crafting chest method/field bindings succeed");
        plugin.GetField("_pov", Statics)!.SetValue(null, bridge);
        plugin.GetMethod("BindPathOfValheimanItemPresentation", Statics)!.Invoke(null, new object[] { pov });
        Check(true, "public Tooltip Describe out-list and item rarity expressions compile on original Mono");
        var affixes = (Func<ItemData, IEnumerable>)plugin.GetField("_povAffixRows", Statics)!.GetValue(null)!;
        Check(!affixes(null!).Cast<object>().Any(), "actual public Describe expression invokes out-list and reads empty result");
        object affix = FormatterServices.GetUninitializedObject(pov.GetType("Valheiman.Api.AffixRow", true)!);
        foreach (string property in new[] { "_povAffixName", "_povAffixEffect", "_povAffixColor" })
        {
            ((Delegate)plugin.GetField(property, Statics)!.GetValue(null)!).DynamicInvoke(affix);
            Check(true, property + " compiled expression accesses original public property");
        }
        var rarityColor = (Func<ItemData, Color>)plugin.GetField("_povNameColor", Statics)!.GetValue(null)!;
        Color commonColor = rarityColor(null!);
        Check(!float.IsNaN(commonColor.r) && !float.IsNaN(commonColor.g) && !float.IsNaN(commonColor.b), "actual public rarity expression invokes original API and returns finite color");
        string[] presentationFields = { "_povAffixRows", "_povAffixName", "_povAffixEffect", "_povAffixColor" };
        object?[] oldPresentation = presentationFields.Select(f => plugin.GetField(f, Statics)!.GetValue(null)).ToArray();
        plugin.GetField("_povAffixRows", Statics)!.SetValue(null, new Func<ItemData, IEnumerable>(_ => new object[] { "First", "Second" }));
        plugin.GetField("_povAffixName", Statics)!.SetValue(null, new Func<object, string>(o => (string)o));
        plugin.GetField("_povAffixEffect", Statics)!.SetValue(null, new Func<object, string>(_ => "+5% Damage"));
        plugin.GetField("_povAffixColor", Statics)!.SetValue(null, new Func<object, Color>(_ => Color.white));
        var append = plugin.GetMethod("AppendPathOfValheimanAffixes", Statics)!;
        string distinct = (string)append.Invoke(null, new object?[] { null, "Description" })!;
        Check(distinct.Contains("First: +5% Damage") && distinct.Contains("Second: +5% Damage"), "separate affixes with identical value text remain visible");
        string already = (string)append.Invoke(null, new object?[] { null, "Description\n+5% Damage" })!;
        Check(already == "Description\n+5% Damage", "affix effect already present in original body is not repeated");
        for (int i = 0; i < presentationFields.Length; i++) plugin.GetField(presentationFields[i], Statics)!.SetValue(null, oldPresentation[i]);
        plugin.GetMethod("InitializePathOfValheimanNativeHud", Statics)!.Invoke(null, new object[] { pov });
        MethodInfo late = AccessTools.DeclaredMethod(pov.GetType("PassiveTree.Gear.Module", true), "LateUpdate");
        Check(Harmony.GetPatchInfo(late)?.Owners.Contains("sighsorry.InventorySlots.pov.native-hud") == true, "guarded native HUD transpiler applies to original Module.LateUpdate");
        plugin.GetField("_pov", Statics)!.SetValue(null, null);
        foreach (string field in new[] { "Enabled", "Suspended", "ProviderId", "Open", "Accepts", "KeyLabel", "KeptOnDeath", "Weightless", "ExtraRows", "Equipped", "Equip", "Unequip", "Changed" })
        {
            MethodInfo method = ((Delegate)bridgeType.GetField(field, Instances)!.GetValue(bridge)!).Method;
            Check(method.IsPublic && method.DeclaringType!.IsVisible && method.DeclaringType.Namespace == "Valheiman.Api", field + " binds original public API");
        }
        object currentEquipment = equipment.GetProperty("Cells")!.GetValue(null)!;
        object currentStorage = storage.GetProperty("Cells")!.GetValue(null)!;
        if (oldEquipment != null) Check(ReferenceEquals(oldEquipment, currentEquipment) && ReferenceEquals(oldStorage, currentStorage), "warm API cache object identities preserved");
        Check(((IEnumerable<string>)currentStorage).Distinct().Count() == 28, "original storage cache stays unchanged at 28 unique cells");
        for (int i = 0; i < nativeCount; i++)
        {
            bool actual = (bool)isStorage.Invoke(null, new[] { Enum.ToObject(slot, i) })!;
            Check(actual == baseline[i], "IsStorage preserves role " + i);
        }
        foreach (object info in (IEnumerable)currentEquipment)
        {
            Type t = info.GetType(); string id = (string)t.GetProperty("Id")!.GetValue(info)!;
            if (id.StartsWith("purse.")) Check((bool)t.GetProperty("Worn")!.GetValue(info)!, id + " cached Worn unchanged");
        }
        Player sentinel = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        ItemData item = new ItemData();
        int reads = 0, puts = 0, takes = 0;
        Func<Player, string, ItemData> read = (p, id) => { Check(ReferenceEquals(p, sentinel) && id == "tool.0", "emitted ItemIn argument forwarding"); reads++; return item; };
        Func<Player, string, ItemData, bool> put = (p, id, it) => { Check(ReferenceEquals(p, sentinel) && id == "tool.1" && ReferenceEquals(it, item), "emitted Put argument forwarding"); puts++; return false; };
        Func<Player, string, bool> take = (p, id) => { Check(ReferenceEquals(p, sentinel) && id == "tool.2", "emitted TakeOut argument forwarding"); takes++; return true; };
        object provider = bridgeType.GetMethod("CreateProvider", Statics)!.Invoke(null, new object[] { contract, read, put, take })!;
        Check(contract.IsInstanceOfType(provider), "dynamic provider implements original public interface");
        Check((string)contract.GetProperty("Id")!.GetValue(provider)! == "sighsorry.InventorySlots" && (int)contract.GetProperty("Contract")!.GetValue(provider)! == 1, "provider constants");
        Check(ReferenceEquals(contract.GetMethod("ItemIn")!.Invoke(provider, new object[] { sentinel, "tool.0" }), item), "emitted ItemIn return");
        Check(!(bool)contract.GetMethod("Put")!.Invoke(provider, new object[] { sentinel, "tool.1", item })!, "emitted Put false return");
        Check((bool)contract.GetMethod("TakeOut")!.Invoke(provider, new object[] { sentinel, "tool.2" })!, "emitted TakeOut true return");
        Check(reads == 1 && puts == 1 && takes == 1, "each emitted callback invoked exactly once");
        bridgeType.GetMethod("Register", Instances)!.Invoke(bridge, new object[] { read, put, take });
        Check((string)storage.GetProperty("ProviderId")!.GetValue(null)! == "sighsorry.InventorySlots", "register reports provider before activation");
        Check(!(bool)cells.GetProperty("External", Statics)!.GetValue(null)!, "registration does not activate backend");
        cells.GetMethod("Choose", Statics)!.Invoke(null, null);
        Check((bool)cells.GetProperty("External", Statics)!.GetValue(null)!, "load Choose activates external backend");
        bridgeType.GetMethod("Unregister", Instances)!.Invoke(bridge, null);
        Check((bool)cells.GetProperty("External", Statics)!.GetValue(null)!, "unregister preserves current character backend");
        cells.GetMethod("Forget", Statics)!.Invoke(null, null);
        Check(!(bool)cells.GetProperty("External", Statics)!.GetValue(null)!, "Forget releases active backend");
        CheckInventoryBackend(mod, pov, plugin, bridgeType, bridge, cells, contract);
        patches.UnpatchSelf(); engine.UnpatchSelf(); new Harmony("sighsorry.InventorySlots.pov.native-hud").UnpatchSelf();
        new Harmony("sighsorry.InventorySlots.pov.unified-hud").UnpatchSelf();
        Check(((IEnumerable<string>)currentStorage).Count() == 28, "unpatch leaves original 28-cell storage cache unchanged");
        foreach (object info in (IEnumerable)currentEquipment)
        {
            Type t = info.GetType(); string id = (string)t.GetProperty("Id")!.GetValue(info)!;
            if (id.StartsWith("purse.")) Check((bool)t.GetProperty("Worn")!.GetValue(info)!, id + " cached Worn unchanged after unpatch");
        }
        System.Console.WriteLine("PASS " + checks + " original Mono managed bridge checks; no Unity/world/game execution; cache mode=" + args[4]);
        return 0;
    }

    private static void CheckInventoryBackend(Assembly mod, Assembly pov, Type plugin, Type bridgeType, object bridge, Type cells, Type contract)
    {
        object? Call(string name, params object?[] values) => plugin.GetMethod(name, Statics)!.Invoke(null, values);
        Type definition = mod.GetType("InventorySlots.SlotDefinition", true)!;
        Type kind = mod.GetType("InventorySlots.SlotKind", true)!;
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        definitions.Clear(); mapped.Clear();
        Func<ItemData, bool> accepts = it => it != null;
        object Make(string role, bool worn = false)
        {
            object value = Activator.CreateInstance(definition, new object?[] { "pov." + role, role, Enum.Parse(kind, worn ? "BuiltIn" : "Quick"), accepts, -1, role })!;
            definitions.Add(value); mapped.Add(role, value); return value;
        }
        Make("arrow.0"); Make("potion.0");
        plugin.GetField("_pov", Statics)!.SetValue(null, bridge);
        int fixedRows = (int)Call("GetFixedRegularRows")!;
        Type module = pov.GetType("PassiveTree.Gear.Module", true)!;
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "pov-storage-test-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        InitializeProgressionConfig(plugin, config);
        var enabled = config.Bind("test", "equipment", true, "");
        module.GetField("Enabled", Statics)!.SetValue(null, enabled);
        // No world/character lifecycle runs here. Return no allocated stat tree,
        // avoiding PoV's normal disk/load path for the unmanaged Player sentinel.
        var fixture = new Harmony("smoke.pov.storage.no-world-progress");
        fixture.Patch(AccessTools.PropertyGetter(pov.GetType("PassiveTree.Progress", true), "Current"),
            prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
        // The uninitialized player has no network view; only its stable identity
        // is supplied here, while the real discovery/cache methods still run.
        fixture.Patch(AccessTools.Method(typeof(Player), "GetPlayerID"),
            prefix: new HarmonyMethod(typeof(Program), nameof(FixturePlayerId)));
        // Model a not-yet-allocated Magic Purse through PoV's actual public gate.
        pov.GetType("PassiveTree.Gear.Wallet", true)!.GetField("Gate", Statics)!.SetValue(null, new Func<Player, bool>(_ => false));
        Player player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
        Inventory bag = new Inventory("Inventory", null, 8, fixedRows + 6);
        AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, bag);
        AccessTools.Field(typeof(Player), "m_uniques").SetValue(player, new HashSet<string>());
        AccessTools.Field(typeof(Player), "m_knownMaterial").SetValue(player, new HashSet<string>());
        var custom = new Dictionary<string, string>();
        AccessTools.Field(typeof(Player), "m_customData").SetValue(player, custom);
        FieldInfo queue = AccessTools.Field(typeof(Player), "m_actionQueue");
        queue.SetValue(player, Activator.CreateInstance(queue.FieldType));
        FieldInfo local = AccessTools.Field(typeof(Player), "m_localPlayer");
        List<ItemData> Items() => (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        ItemData Item(int x, int y, int stack = 1) => new ItemData { m_gridPos = new Vector2i(x, y), m_stack = stack, m_shared = new ItemData.SharedData { m_name = "smoke-item" } };
        ItemData tool = Item(5, 5, 7), potion = Item(0, 8, 9), regular = Item(0, 0, 3), utility = Item(1, 5, 2);
        Items().AddRange(new[] { tool, potion, regular, utility });
        custom["valheiman.equipment.v1.rowStart"] = "4";
        custom["InventorySlots.PovLayout.v1"] = "stale.provider.layout";
        Call("CompletePathOfValheimanLoad", player);
        Check(tool.m_gridPos == new Vector2i(0, fixedRows) && potion.m_gridPos == new Vector2i(1, fixedRows), "actual native ordinal13/32 map to own arrow/potion cells");
        Check(regular.m_gridPos == new Vector2i(0, 0) && Items().Count == 4 && ReferenceEquals(Items()[0], tool) && tool.m_stack == 7 && potion.m_stack == 9, "native map preserves ordinary coordinate, exact item identity and stack");
        Check(utility.m_gridPos == new Vector2i(1, 0) && ReferenceEquals(Items()[3], utility) && utility.m_stack == 2,
            "removed native Utility ordinal9 recovers the exact item into ordinary storage");
        Check(!custom.ContainsKey("valheiman.equipment.v1.rowStart") && custom.ContainsKey("InventorySlots.PovLayout.v1"), "native row key wins stale provider key and is replaced only after conversion");
        utility.m_gridPos = new Vector2i(0, fixedRows);
        tool.m_gridPos = new Vector2i(1, fixedRows);
        potion.m_gridPos = new Vector2i(2, fixedRows);
        custom["InventorySlots.PovLayout.v1"] = "pov.utility\npov.arrow.0\npov.potion.0";
        Call("CompletePathOfValheimanLoad", player);
        Check(utility.m_gridPos == new Vector2i(1, 0) && tool.m_gridPos == new Vector2i(0, fixedRows) &&
            potion.m_gridPos == new Vector2i(1, fixedRows) && Items().Count == 4 && ReferenceEquals(Items()[3], utility),
            "saved provider Utility recovers without overwriting subsequent shifted slots or cloning items");
        Items().Clear();
        for (int y = 0; y < fixedRows; y++) for (int x = 0; x < 8; x++) Items().Add(Item(x, y));
        ItemData orphan = Item(0, fixedRows + 3, 5); Items().Add(orphan);
        var prior = Items().ToDictionary(it => it, it => it.m_gridPos);
        bool failed = false;
        try { Call("ReprojectPathOfValheimanLayout", bag, new Dictionary<Vector2i, string> { { orphan.m_gridPos, "pov.utility" } }); }
        catch (TargetInvocationException e) { failed = e.InnerException is InvalidOperationException; }
        Check(failed && Items().All(it => it.m_gridPos == prior[it]), "full recovery grid rejects entire layout plan before changing any item");
        Items().Clear();
        tool = Item(2, 0, 7); Items().Add(tool);
        var read = (Func<Player, string, ItemData>)Delegate.CreateDelegate(typeof(Func<Player, string, ItemData>), plugin.GetMethod("ReadPathOfValheimanItem", Statics)!);
        var put = (Func<Player, string, ItemData, bool>)Delegate.CreateDelegate(typeof(Func<Player, string, ItemData, bool>), plugin.GetMethod("TryPutPathOfValheimanItem", Statics)!);
        var take = (Func<Player, string, bool>)Delegate.CreateDelegate(typeof(Func<Player, string, bool>), plugin.GetMethod("TakeOutPathOfValheimanItem", Statics)!);
        bridgeType.GetMethod("Register", Instances)!.Invoke(bridge, new object[] { read, put, take });
        cells.GetMethod("Choose", Statics)!.Invoke(null, null);
        local.SetValue(null, player);
        try
        {
            CheckProgression(mod, pov, plugin, player, bag, enabled);
            CheckPanelLayout(mod, pov, plugin, player, bag, config);
            object provider = bridgeType.GetField("_provider", Instances)!.GetValue(bridge)!;
            bool Put(ItemData it, string role) => (bool)contract.GetMethod("Put")!.Invoke(provider, new object[] { player, role, it })!;
            bool Take(string role) => (bool)contract.GetMethod("TakeOut")!.Invoke(provider, new object[] { player, role })!;
            Check(!Put(tool, "utility") && read(player, "utility") == null && tool.m_gridPos == new Vector2i(2, 0) && Items().Count == 1,
                "unsupported Utility provider rejects insertion without changing or hiding the item");
            Check((bool)Call("CanRouteEquipToDedicatedSlot", player, bag, tool)!, "ordinary inventory candidate is routable before load scope");
            Call("BeginPathOfValheimanLoad", player);
            Check(!(bool)Call("CanRouteEquipToDedicatedSlot", player, bag, tool)! && !Put(tool, "arrow.0") && tool.m_gridPos == new Vector2i(2, 0), "load scope blocks both equip routing and provider insertion without coordinate changes");
            Call("EndPathOfValheimanLoad", player, null);
            Call("BeginPathOfValheimanLoad", player);
            Call("EndPathOfValheimanLoad", player, new InvalidOperationException("simulated native load failure"));
            Check(!(bool)Call("CanRouteEquipToDedicatedSlot", player, bag, tool)! && !Put(tool, "arrow.0") && tool.m_gridPos == new Vector2i(2, 0), "failed native load stays protected after finalizer clears active loading");
            Call("BeginPathOfValheimanLoad", player);
            Call("EndPathOfValheimanLoad", player, null);
            Check((bool)Call("CanRouteEquipToDedicatedSlot", player, bag, tool)!, "next successful load scope releases prior failure guard");
            Check(Put(tool, "arrow.0") && tool.m_gridPos == new Vector2i(0, fixedRows), "actual provider Put relocates same inventory item to target cell");
            Check(Items().Count == 1 && ReferenceEquals(Items()[0], tool) && tool.m_stack == 7, "provider Put makes no clone or stack change");
            ItemData other = Item(3, 0); Items().Add(other); Vector2i priorOther = other.m_gridPos;
            Check(!Put(other, "arrow.0") && other.m_gridPos == priorOther && tool.m_gridPos == new Vector2i(0, fixedRows), "occupied provider target rejects without swapping or moving either item");
            ItemData foreign = Item(0, 0);
            Check(!Put(foreign, "arrow.0") && !Items().Contains(foreign), "foreign item rejected without inserting a clone");
            ItemData locked = Item(4, 0); Items().Add(locked);
            Check(!Put(locked, "potion.0") && locked.m_gridPos == new Vector2i(4, 0), "locked potion rejects new insertion");
            tool.m_gridPos = new Vector2i(1, fixedRows);
            Check(Take("potion.0") && tool.m_gridPos.y < 4, "locked potion still permits safe withdrawal of existing item");
            Items().Clear(); for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++) Items().Add(Item(x, y));
            tool.m_gridPos = new Vector2i(1, fixedRows); Items().Add(tool);
            Check(!Take("potion.0") && tool.m_gridPos == new Vector2i(1, fixedRows) && Items().Count == 33, "full usable backpack rejects withdrawal without loss into locked rows");
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, true);
            Check(!Put(tool, "arrow.0") && !Take("potion.0"), "actual provider rejects all writes while player loading");
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, false);
            Items().Clear(); Items().Add(tool); tool.m_gridPos = new Vector2i(1, fixedRows);
            fixture.Patch(pov.GetType("PassiveTree.Gear.EquipmentApi", true)!.GetMethod("Prefab", Statics)!, prefix: new HarmonyMethod(typeof(Program), nameof(QuiverPrefab)));
            CheckCoinBenefits(pov, plugin, player, bag, enabled, config);
            enabled.Value = false;
            Check(!Put(tool, "arrow.0") && tool.m_gridPos == new Vector2i(1, fixedRows), "Equipment Off rejects insertion with existing coordinate preserved");
            Check(Take("potion.0") && tool.m_gridPos.y < 4, "Equipment Off permits existing storage item withdrawal");
            enabled.Value = true;
            Make("quiver", true);
            quiverFixtureItem = Item(2, 0); Items().Clear(); Items().Add(quiverFixtureItem);
            Type equipmentApi = pov.GetType("PassiveTree.Gear.EquipmentApi", true)!;
            // Managed callbacks stand in for the external quiver's own effects.
            bool quiverEquipped = false, throwEquip = false, throwUnequip = false; int equipCalls = 0, unequipCalls = 0;
            Func<Player, ItemData, bool> equip = (p, it) => { equipCalls++; quiverEquipped = true; if (throwEquip) throw new InvalidOperationException("simulated external equip failure"); return true; };
            Action<Player, ItemData> unequip = (p, it) => { unequipCalls++; if (throwUnequip) throw new InvalidOperationException("simulated external unequip failure"); quiverEquipped = false; };
            Func<Player, ItemData, bool> isEquipped = (p, it) => quiverEquipped && ReferenceEquals(it, quiverFixtureItem);
            equipmentApi.GetMethod("RegisterQuiver", Statics)!.Invoke(null, new object[] { "SmokeQuiver", 3, equip, unequip, isEquipped });

            Call("BeginPathOfValheimanLoad", player);
            Call("OnPlayerInventoryItemPlaced", bag, quiverFixtureItem, new Vector2i(2, fixedRows), true);
            object?[] overrideEquip = { player, quiverFixtureItem, true };
            bool overridden = (bool)plugin.GetMethod("TryOverrideHumanoidEquipItem", Statics)!.Invoke(null, overrideEquip)!;
            Check(!overridden && (bool)overrideEquip[2]! && equipCalls == 0 && quiverFixtureItem.m_gridPos == new Vector2i(2, 0), "load scope placement and equip hooks leave existing state and original result untouched");
            Call("EndPathOfValheimanLoad", player, null);
            Check(Put(quiverFixtureItem, "quiver") && quiverEquipped && equipCalls == 1 && quiverFixtureItem.m_gridPos == new Vector2i(2, fixedRows), "quiver Put uses original registered equipment callback exactly once then commits coordinate");
            Items().Clear(); for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++) Items().Add(Item(x, y)); Items().Add(quiverFixtureItem);
            fixture.Patch(AccessTools.DeclaredMethod(typeof(Character), "Message"), prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
            object?[] use = { player, bag, quiverFixtureItem, true };
            bool handled = (bool)plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use)!;
            Check(handled && !(bool)use[3]! && quiverEquipped && unequipCalls == 0 && quiverFixtureItem.m_gridPos == new Vector2i(2, fixedRows), "right-click quiver with full backpack is consumed without removing its effects or item");
            Items().RemoveAt(0);
            use = new object?[] { player, bag, quiverFixtureItem, true };
            handled = (bool)plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use)!;
            Check(handled && !(bool)use[3]! && !quiverEquipped && unequipCalls == 1 && quiverFixtureItem.m_gridPos == new Vector2i(0, 0) && Items().Count == 32,
                "right-click quiver with a free cell removes effects once and moves exact item into backpack");
            Check(Put(quiverFixtureItem, "quiver") && quiverEquipped && equipCalls == 2, "quiver can be equipped again after completed UI withdrawal");
            pov.GetType("Valheiman.Api.Equipment", true)!.GetMethod("TakeOut")!.Invoke(null, new object[] { player, "quiver" });
            Check(!quiverEquipped && unequipCalls == 2 && Items().Contains(quiverFixtureItem), "original quiver TakeOut invokes external callback once and retains item identity");
            Check(Put(quiverFixtureItem, "quiver") && quiverEquipped && equipCalls == 3, "quiver setup before Equipment Off");
            enabled.Value = false;
            Check(pov.GetType("Valheiman.Api.Equipment", true)!.GetMethod("ItemIn")!.Invoke(null, new object[] { player, "quiver" }) == null &&
                ReferenceEquals(bridgeType.GetMethod("QuiverItem", Instances)!.Invoke(bridge, new object[] { player }), quiverFixtureItem), "Off hides public equipped item but bridge safely reads original private state");
            ItemData fill = Item(0, 0); Items().Add(fill);
            use = new object?[] { player, bag, quiverFixtureItem, true };
            handled = (bool)plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use)!;
            Check(handled && !(bool)use[3]! && quiverEquipped && unequipCalls == 2 && quiverFixtureItem.m_gridPos == new Vector2i(2, fixedRows), "closed quiver with full backpack preserves callback state and coordinate");
            Items().Remove(fill);
            use = new object?[] { player, bag, quiverFixtureItem, true };
            handled = (bool)plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use)!;
            Check(handled && !(bool)use[3]! && !quiverEquipped && unequipCalls == 3 && quiverFixtureItem.m_gridPos == new Vector2i(0, 0) && Items().Count == 32,
                "closed quiver withdrawal invokes original cleanup once and moves exact item into free backpack cell");
            enabled.Value = true;
            Check(Put(quiverFixtureItem, "quiver") && quiverEquipped, "quiver setup for external removal exception");
            throwUnequip = true; int priorUnequip = unequipCalls;
            use = new object?[] { player, bag, quiverFixtureItem, true };
            handled = (bool)plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use)!;
            Check(handled && !(bool)use[3]! && quiverEquipped && unequipCalls == priorUnequip + 1 && quiverFixtureItem.m_gridPos == new Vector2i(2, fixedRows) &&
                !(bool)Call("TryCompletePendingSlotUnequip", player, quiverFixtureItem)!, "throwing external quiver cleanup preserves coordinate and clears deferred move");
            throwUnequip = false;
            use = new object?[] { player, bag, quiverFixtureItem, true };
            plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use);
            Check(!quiverEquipped && quiverFixtureItem.m_gridPos == new Vector2i(0, 0), "quiver can be withdrawn normally after callback failure");
            throwEquip = true; int beforeItems = Items().Count;
            Check(!Put(quiverFixtureItem, "quiver") && !quiverEquipped && quiverFixtureItem.m_gridPos == new Vector2i(0, 0) && Items().Count == beforeItems && Items().Contains(quiverFixtureItem),
                "throwing external equip callback restores original item coordinate and removes partial callback state");
            throwEquip = false;
            if ((bool)bridgeType.GetProperty("HasExtraTrinkets", Instances)!.GetValue(bridge)!)
                CheckTrinkets(mod, pov, plugin, bridgeType, bridge, player, bag, enabled, fixture);
        }
        finally
        {
            local.SetValue(null, null);
            bridgeType.GetMethod("Unregister", Instances)!.Invoke(bridge, null);
            cells.GetMethod("Forget", Statics)!.Invoke(null, null);
            plugin.GetField("_pov", Statics)!.SetValue(null, null);
            definitions.Clear(); mapped.Clear();
            fixture.UnpatchSelf();
            quiverFixtureItem = coinFixtureItem = null;
        }
    }
    private static void CheckTrinkets(Assembly mod, Assembly pov, Type plugin, Type bridgeType, object bridge,
        Player player, Inventory bag, ConfigEntry<bool> enabled, Harmony fixture)
    {
        object? Call(string name, params object?[] values) => plugin.GetMethod(name, Statics)!.Invoke(null, values);
        Type definition = mod.GetType("InventorySlots.SlotDefinition", true)!;
        Type kind = mod.GetType("InventorySlots.SlotKind", true)!;
        Type trinkets = pov.GetType("PassiveTree.Trinkets", true)!;
        Type api = pov.GetType("Valheiman.Api.Equipment", true)!;
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        var mapped = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        var custom = (Dictionary<string, string>)AccessTools.Field(typeof(Player), "m_customData").GetValue(player);
        var extra = (IList)trinkets.GetField("extra", Statics)!.GetValue(null)!;
        FieldInfo primary = AccessTools.Field(typeof(Humanoid), "m_trinketItem");
        int rows = (int)Call("GetFixedRegularRows")!;
        string[] roles = { "trinket", "trinket.1", "trinket.2", "trinket.hand" };
        Vector2i Pos(string role) { int index = definitions.IndexOf(mapped[role]); return new Vector2i(index % 8, rows + index / 8); }
        ItemData Item(int x, int y, bool worn = true) => new ItemData {
            m_gridPos = new Vector2i(x, y), m_equipped = worn,
            m_shared = new ItemData.SharedData { m_name = "smoke-trinket", m_itemType = (ItemData.ItemType)24, m_maxStackSize = 1 } };
        void Refresh() => trinkets.GetMethod("Refresh", Statics)!.Invoke(null, null);
        bool Project() => (bool)Call("ProjectPathOfValheimanTrinkets", player, bag)!;
        ItemData? Actual(string role) => (ItemData?)bridgeType.GetMethod("TrinketItem", Instances)!.Invoke(bridge, new object[] { player, role });
        void Conserved(ItemData[] expected, string context) => Check(expected.All(items.Contains) &&
            items.Distinct().Count() == items.Count && items.Select(it => it.m_gridPos).Distinct().Count() == items.Count, context);
        var hooks = new Harmony("smoke.pov.trinkets");
        hooks.Patch(AccessTools.PropertyGetter(trinkets, "OrdinarySlots"), prefix: new HarmonyMethod(typeof(Program), nameof(OrdinaryTrinkets)));
        hooks.Patch(AccessTools.PropertyGetter(trinkets, "Slots"), prefix: new HarmonyMethod(typeof(Program), nameof(TotalTrinkets)));
        hooks.Patch(AccessTools.Method(trinkets, "Changed"), prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
        hooks.Patch(AccessTools.Method(typeof(Player), "GetAdrenaline"), prefix: new HarmonyMethod(typeof(Program), nameof(ZeroAdrenaline)));
        hooks.Patch(AccessTools.Method(typeof(Player), "IsEquipActionQueued"), prefix: new HarmonyMethod(typeof(Program), nameof(QueuedTrinket)));
        hooks.Patch(AccessTools.Method(typeof(Humanoid), "UseItem"), prefix: new HarmonyMethod(typeof(Program), nameof(TrinketUse)));
        foreach (string name in new[] { "EquipItem", "UnequipItem" })
            hooks.Patch(AccessTools.Method(typeof(Humanoid), name), transpiler: new HarmonyMethod(typeof(Program), nameof(VanillaTrinketBody)));
        foreach (string name in new[] { "Equip", "Unequip", "Equipped" })
            hooks.CreateClassProcessor(pov.GetType("PassiveTree.Trinkets+" + name, true)!).Patch();
        // Include the real IS routing/unequip patches to expose ordering conflicts
        // with PoV's priority-800 trinket hooks. Only Unity's vanilla body is mocked.
        foreach (string name in new[] { "HumanoidUseItemRouteToDedicatedSlotPatch", "HumanoidEquipItemRouteToDedicatedSlotPatch", "HumanoidUnequipItemPatch" })
            hooks.CreateClassProcessor(mod.GetType("InventorySlots." + name, true)!).Patch();
        try
        {
            enabled.Value = true; ordinaryTrinkets = 3; totalTrinkets = 4; extra.Clear();
            definitions.Clear(); mapped.Clear(); items.Clear();
            definitions.Add(Activator.CreateInstance(definition, new object?[] { "trinket", "Trinket", Enum.Parse(kind, "BuiltIn"), new Func<ItemData, bool>(_ => true), -1, null }));
            Call("AppendPathOfValheimanSlots");
            Check(roles.All(mapped.Contains) && roles.All(r => definition.GetProperty("Kind")!.GetValue(mapped[r])!.ToString() == "BuiltIn"), "actual slot builder maps all four trinket roles as equipment");
            ItemData a = Item(0, 5), b = Item(5, 9), c = Item(6, 9), d = Item(7, 9);
            ItemData[] all = { a, b, c, d };
            for (int i = 1; i < all.Length; i++) all[i].m_customData["pt.trinket.slot"] = i.ToString();
            items.AddRange(all); primary.SetValue(player, a); custom["valheiman.equipment.v1.rowStart"] = "4";
            Call("BeginPathOfValheimanLoad", player); Call("CompletePathOfValheimanLoad", player); Call("EndPathOfValheimanLoad", player, null);
            Check(all.Select((it, i) => it.m_gridPos == Pos(roles[i])).All(v => v) && b.m_customData["pt.trinket.slot"] == "1" && d.m_customData["pt.trinket.slot"] == "3",
                "native ordinals8/45/46/47 convert by ID without erasing PoV reconnect marks");
            Check(!Project(), "projection waits until PoV restores extra trinkets at spawn");
            pov.GetType("PassiveTree.Trinkets+Spawn", true)!.GetMethod("Postfix", Statics)!.Invoke(null, new object[] { player });
            Check(roles.Select((r, i) => ReferenceEquals(Actual(r), all[i])).All(v => v), "original PoV spawn reconstructs four worn identities and activates projection");
            for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++) items.Add(Item(x, y, false));
            player.UnequipItem(a, true); Project();
            Check(Actual("trinket") == b && b.m_gridPos == Pos("trinket") && c.m_gridPos == Pos("trinket.1") && d.m_gridPos == Pos("trinket.2") && !a.m_equipped,
                "base removal promotes and reprojects remaining items with a full backpack");
            Conserved(all, "full-backpack promotion conserves all references and unique coordinates");
            ordinaryTrinkets = 1; totalTrinkets = 1; Refresh(); Project();
            Check(!c.m_equipped && !d.m_equipped && !c.m_customData.ContainsKey("pt.trinket.slot") && !d.m_customData.ContainsKey("pt.trinket.slot") && b.m_equipped,
                "original node reset removes extra effects/marks without re-equipping displaced items");
            Conserved(all, "full-backpack node reset retains displaced items in withdrawable reserved cells");
            object?[] use = { player, bag, d, true };
            Check((bool)plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use)! && d.m_gridPos.y >= rows,
                "closed displaced trinket with full backpack is retained");
            items.RemoveAll(it => it.m_gridPos == new Vector2i(0, 0));
            use = new object?[] { player, bag, d, true };
            plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use);
            Check(d.m_gridPos == new Vector2i(0, 0) && !d.m_equipped, "closed displaced trinket can be withdrawn after freeing space");
            items.RemoveAll(it => !all.Contains(it)); Project();
            ordinaryTrinkets = 3; totalTrinkets = 4;
            Check((bool)Call("TryEquipPathOfValheimanTrinket", player, bag, c, mapped["trinket.hand"])! && Actual("trinket.1") == c && c.m_gridPos == Pos("trinket.1"),
                "Put into hand with earlier seats empty follows actual PoV allocation, not requested coordinate");
            Check((bool)Call("TryEquipPathOfValheimanTrinket", player, bag, d, mapped["trinket.1"])! && !c.m_equipped && Actual("trinket.1") == d,
                "original Assign removes occupied trinket before replacement; utility transpiler does not suppress it");
            ordinaryTrinkets = 1; totalTrinkets = 2; Refresh(); Project();
            Check(Actual("trinket.hand") == d && d.m_gridPos == Pos("trinket.hand"), "node change moves dynamic hand seat to its new role");
            Check(!(bool)Call("TryEquipPathOfValheimanTrinket", player, bag, c, mapped["trinket.2"])! && !c.m_equipped, "locked trinket role rejects new equip");
            ordinaryTrinkets = 3; totalTrinkets = 4; Refresh(); Project();
            Vector2i before = c.m_gridPos; queueTrinket = true;
            Check((bool)Call("TryEquipPathOfValheimanTrinket", player, bag, c, mapped["trinket.2"])! && c.m_gridPos == before && Actual("trinket.2") == null,
                "queued Put keeps source coordinate until actual equipment completion");
            queueTrinket = false; queuedTrinket = null; player.EquipItem(c, true); Project();
            Check(Actual("trinket.2") == c && c.m_gridPos == Pos("trinket.2"), "completed native equip projects all actual roles without IS rerouting");
            enabled.Value = false;
            Check(api.GetMethod("ItemIn")!.Invoke(null, new object[] { player, "trinket.2" }) == null && Actual("trinket.2") == c,
                "Off hides public trinkets but checked native read preserves active equipment identity");
            use = new object?[] { player, bag, c, true }; plugin.GetMethod("TryHandleDedicatedSlotUse", Statics)!.Invoke(null, use);
            Check(!c.m_equipped && !extra.Contains(c) && c.m_gridPos.y < 4, "Off trinket withdrawal removes exact PoV equipment and returns same item");
            enabled.Value = true; queueTrinket = true;
            Call("UnequipInventorySlotsItem", player, d);
            Check(!d.m_equipped && !extra.Contains(d) && queuedTrinket == null, "external trinket removal is synchronous even when UseItem would queue");
            Conserved(all, "all equipment operations conserve the original four item references");
            queueTrinket = false;
            Call("TryEquipPathOfValheimanTrinket", player, bag, d, mapped["trinket.1"]);
            Call("TryEquipPathOfValheimanTrinket", player, bag, c, mapped["trinket.2"]);
            ItemData replacement = d.Clone(); replacement.m_quality++;
            Type transactionType = mod.GetType("InventorySlots.EquipmentSlotUpgradeTransaction", true)!;
            object transaction = Activator.CreateInstance(transactionType, new object?[] { player, bag, d, d.Clone(), replacement,
                mapped["trinket.1"], (string)Call("GetItemPrefabName", replacement)!, replacement.m_quality, replacement.m_variant, new HashSet<ItemData>(items), null })!;
            transactionType.GetProperty("ReplacementAddAttempted")!.SetValue(transaction, true);
            player.UnequipItem(d, true); items.Remove(d); items.Add(replacement);
            Call("TryReturnEquipmentSlotUpgradeResultToOriginalSlot", transaction, replacement);
            Check(Actual("trinket.1") == c && c.m_equipped && Actual("trinket.2") == replacement && replacement.m_gridPos == Pos("trinket.2"),
                "upgrade restoration uses empty actual role without evicting promoted peer");
            Check(Call("SelectEquipmentSlotUpgradeCompletionPlan", transaction, replacement)!.ToString() == "Commit",
                "upgrade commit accepts sole actual PoV role instead of stale captured role");
            Check(!(bool)Call("TryRestoreEquipmentSlotUpgradeBuiltInReplacementState", transaction, replacement)!,
                "upgrade fallback cannot fabricate PoV equipment through a Humanoid snapshot");
            Call("UnequipInventorySlotsItem", player, replacement); items.Remove(replacement); items.Add(d);
            Check((bool)Call("TryRestoreExistingEquipmentSlotUpgradeOriginalState", transaction)! && c.m_equipped && Actual("trinket.1") == c && Actual("trinket.2") == d,
                "upgrade rollback original restoration also preserves promoted peer");
            Conserved(all, "upgrade/rollback fixtures conserve original identities after replacement removal");

            Call("UnequipInventorySlotsItem", player, a);
            string marker = (string)plugin.GetField("SlotIdKey", Statics)!.GetRawConstantValue()!;
            a.m_customData[marker] = "custom.trinket"; a.m_equipped = true;
            Check(!(bool)Call("TryEquipPathOfValheimanTrinket", player, bag, a, mapped["trinket.hand"])! && a.m_customData.ContainsKey(marker),
                "custom-owned trinket must be unequipped before transfer to PoV; failed request leaves ownership intact");
            a.m_customData.Remove(marker); a.m_equipped = false;
        }
        finally
        {
            hooks.UnpatchSelf(); queueTrinket = false; queuedTrinket = null; extra.Clear(); primary.SetValue(player, null);
            trinkets.GetField("owner", Statics)!.SetValue(null, null);
            plugin.GetField("_povTrinketsReadyPlayer", Statics)!.SetValue(null, null);
        }
    }

    private static void Check(bool ok, string description)
    { if (!ok) throw new InvalidOperationException(description); checks++; System.Console.WriteLine("PASS " + description); }
}
