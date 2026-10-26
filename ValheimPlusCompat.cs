using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ValheimPlusGuid = "org.bepinex.plugins.valheim_plus";

    private static void InitializeValheimPlusCompatibility()
    {
        if (!Chainloader.PluginInfos.TryGetValue(ValheimPlusGuid, out var plugin) || plugin.Instance == null) return;
        ValheimPlusCompat.Install(plugin.Instance.GetType().Assembly, message => Log.LogWarning(message));
        Log.LogInfo($"ValheimPlus compatibility: player rows={ValheimPlusCompat.RowsActive}, stack metadata={ValheimPlusCompat.MergeActive}, separate AutoStack sweep blocked={ValheimPlusCompat.StackActive}, crafting stock adapter={ValheimPlusCompat.CraftingAvailable}.");
    }
}

// These guards have the same process lifetime as InventorySlots' native storage
// hooks, which remain installed on plugin OnDestroy. Do not re-enable V+ rows
// underneath those hooks, including after partially failed plugin initialization.
// Dispose is for isolated test teardown after native InventorySlots hooks are removed.
internal static class ValheimPlusCompat
{
    private const string NativeOwner = "mod.valheim_plus";
    private const string Prefix = "ValheimPlus.GameClasses.";
    private static readonly Harmony Rows = new(InventorySlotsPlugin.ModGUID + ".vplus.rows");
    private static readonly Harmony Merge = new(InventorySlotsPlugin.ModGUID + ".vplus.merge");
    private static readonly Harmony Stack = new(InventorySlotsPlugin.ModGUID + ".vplus.autostack");
    private static readonly Harmony Crafting = new(InventorySlotsPlugin.ModGUID + ".vplus.crafting");
    private static readonly MethodInfo NativeResize = AccessTools.DeclaredMethod(typeof(Player), "SetInventorySize", new[] { typeof(int) });
    private static readonly MethodInfo NativeRequirements = AccessTools.DeclaredMethod(typeof(Player), "HaveRequirementItems", new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) });
    private static Func<bool>? _craftingEnabled;
    private static Func<int, Piece.Requirement, int, Player, int>? _craftingStock;
    private static MethodInfo? _craftingStockMethod;
    private static bool _craftingQueryApplied;
    private static bool _craftingFaulted;
    private static float _craftingRetryAfter;
    private static bool _craftingQueryWarned;
    private static Action<string>? _warn;
    internal static bool RowsActive { get; private set; }
    internal static bool MergeActive { get; private set; }
    internal static bool StackActive { get; private set; }
    internal static bool CraftingAvailable => _craftingStock != null && !_craftingFaulted;
    internal static bool CraftingActive
    {
        get
        {
            try { return CraftingAvailable && _craftingQueryApplied && _craftingEnabled?.Invoke() == true; }
            catch (Exception error) { FailCraftingStock(error); return false; }
        }
    }

    internal static void Install(Assembly assembly, Action<string> warn)
    {
        _warn = warn;
        // Validate/install independently: changed row hooks must not disable
        // metadata protection, and a changed merge hook must not stop loading.
        if (!RowsActive)
        {
            try
            {
                MethodInfo load = Require(assembly, "Player_Load_InventorySize_Patch", "Prefix", typeof(void),
                    new[] { typeof(Player) }, AccessTools.DeclaredMethod(typeof(Player), "Load", new[] { typeof(ZPackage) }), HarmonyPatchType.Prefix,
                    "531C2D05A0D34BB38914E37F40D2788DD808A9D1BC08E85B843E62E5E2B693B1");
                MethodInfo spawn = Require(assembly, "Player_OnSpawned_InventorySize_Patch", "Postfix", typeof(void),
                    new[] { typeof(Player) }, AccessTools.DeclaredMethod(typeof(Player), "OnSpawned", new[] { typeof(bool) }), HarmonyPatchType.Postfix,
                    "38433C70AD7603C828A8DA8A10BE51581D9C72DE6E6A043FE76AFF52016774C1");
                MethodInfo resize = Require(assembly, "Player_SetInventorySize_Patch", "Transpiler", typeof(IEnumerable<CodeInstruction>),
                    new[] { typeof(IEnumerable<CodeInstruction>) }, NativeResize, HarmonyPatchType.Transpiler,
                    "D62A7EBE2B0FCEE04F2C622E3319DB4DC417A65E96B72BE72E2874C2C57C7971");
                var skip = new HarmonyMethod(typeof(ValheimPlusCompat), nameof(SkipRows));
                Rows.Patch(load, prefix: skip);
                Rows.Patch(spawn, prefix: skip);
                Rows.Patch(resize, prefix: new HarmonyMethod(typeof(ValheimPlusCompat), nameof(KeepInput)));
                RowsActive = true;
                // Recompile only this target, preserving every installed patch.
                // Needed even if V+ baked its row transform before our startup.
                Rows.CreateProcessor(NativeResize).Patch();
            }
            catch (Exception error)
            {
                Rollback(RestoreRows, warn);
                warn("ValheimPlus player-row compatibility unavailable: " + error.GetBaseException().Message);
            }
        }
        if (!MergeActive)
        {
            try
            {
                MethodInfo merge = Require(assembly, "Inventory_MoveAll_Patch", "Prefix", typeof(void),
                    new[] { typeof(Inventory).MakeByRefType(), typeof(Inventory).MakeByRefType() },
                    AccessTools.DeclaredMethod(typeof(Inventory), "MoveAll", new[] { typeof(Inventory) }), HarmonyPatchType.Prefix,
                    "FF9B31EDCC82995BEADB992BCE5D19DEA6A41BD70DDFA2CEE0CCAA5DCFC6ABF1");
                Merge.Patch(merge, prefix: new HarmonyMethod(typeof(ValheimPlusCompat), nameof(SkipMerge)));
                MergeActive = true;
            }
            catch (Exception error)
            {
                Rollback(RestoreMerge, warn);
                warn("ValheimPlus stack-merge compatibility unavailable: " + error.GetBaseException().Message);
            }
        }
        if (!StackActive)
        {
            try
            {
                Type type = assembly.GetType(Prefix + "Inventory_StackAll_Patch", true)!;
                MethodInfo native = AccessTools.DeclaredMethod(typeof(Inventory), "StackAll", new[] { typeof(Inventory), typeof(bool) });
                Type state = type.GetNestedType("SweepStart", BindingFlags.Public)!;
                Require(assembly, type.Name, "Prefix", typeof(bool), new[] { typeof(Inventory), typeof(Inventory), typeof(bool).MakeByRefType(), state.MakeByRefType() },
                    native, HarmonyPatchType.Prefix, "6A43878E0C3C722EA25108431322B17548753363CA4225FC60E4FE95C5478051");
                Require(assembly, type.Name, "Postfix", typeof(void), new[] { typeof(Inventory), typeof(int).MakeByRefType(), state },
                    native, HarmonyPatchType.Postfix, "6A43878E0C3C722EA25108431322B17548753363CA4225FC60E4FE95C5478051");
                MethodInfo gate = RequireStatic(type, "IsStackAllIntoOpenChest", typeof(bool), typeof(Inventory), typeof(Inventory));
                RequireIdleSweep(assembly.GetType(Prefix + "AutoStackSweep", true)!);
                Stack.Patch(gate, prefix: new HarmonyMethod(typeof(ValheimPlusCompat), nameof(PreventSweep)));
                StackActive = true;
            }
            catch (Exception error)
            {
                Rollback(RestoreStack, warn);
                warn("ValheimPlus AutoStack compatibility unavailable; its existing requests and replies remain unchanged: " + error.GetBaseException().Message);
            }
        }
        if (!CraftingAvailable)
        {
            try
            {
                MethodInfo transpiler = Require(assembly, "Player_HaveRequirementItems_Transpiler", "Transpiler", typeof(IEnumerable<CodeInstruction>),
                    new[] { typeof(IEnumerable<CodeInstruction>) }, NativeRequirements, HarmonyPatchType.Transpiler, "BBA7882529836E27EF8C38516C7F7EA11C0895D63A51C67ABBA07E6C03441532");
                _craftingStockMethod = RequireStatic(transpiler.DeclaringType!, "ComputeItemQuantity", typeof(int), typeof(int), typeof(Piece.Requirement), typeof(int), typeof(Player));
                _craftingStock = AccessTools.MethodDelegate<Func<int, Piece.Requirement, int, Player, int>>(_craftingStockMethod);
                _craftingEnabled = BindCraftingEnabled(assembly);
                _craftingFaulted = false;
                Crafting.Patch(transpiler, postfix: new HarmonyMethod(typeof(ValheimPlusCompat), nameof(ObserveCraftingQuery)));
                // Registration alone does not prove the conditional transpiler
                // emitted its helper. Track the output only at patch/repatch time.
                Crafting.CreateProcessor(NativeRequirements).Patch();
            }
            catch (Exception error)
            {
                Rollback(RestoreCrafting, warn);
                warn("ValheimPlus crafting stock display unavailable: " + error.GetBaseException().Message);
            }
        }
    }

    private static bool PreventSweep(ref bool __result)
    {
        if (!StackActive) return true;
        __result = false;
        return false;
    }

    private static void RequireIdleSweep(Type type)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (string name in new[] { "running", "stacking" })
        {
            FieldInfo field = type.GetField(name, flags) ?? throw new MissingFieldException(type.FullName, name);
            if (field.FieldType != typeof(bool) || (bool)field.GetValue(null))
                throw new NotSupportedException("AutoStack is active or has an unknown state; restart to apply compatibility.");
        }
        foreach (string name in new[] { "Pending", "Granted", "CutOff" })
        {
            FieldInfo field = type.GetField(name, flags) ?? throw new MissingFieldException(type.FullName, name);
            if (field.FieldType != typeof(HashSet<Container>) || field.GetValue(null) is not HashSet<Container> set || set.Count != 0)
                throw new NotSupportedException("AutoStack has outstanding replies or an unknown state; restart to apply compatibility.");
        }
    }

    private static MethodInfo RequireStatic(Type type, string name, Type result, params Type[] arguments)
    {
        MethodInfo? method = AccessTools.DeclaredMethod(type, name, arguments);
        if (method == null || !method.IsStatic || method.ReturnType != result) throw new MissingMethodException(type.FullName, name);
        return method;
    }

    private static Func<bool> BindCraftingEnabled(Assembly assembly)
    {
        Type configuration = assembly.GetType("ValheimPlus.Configurations.Configuration", true)!;
        Type section = assembly.GetType("ValheimPlus.Configurations.Sections.CraftFromChestConfiguration", true)!;
        PropertyInfo current = configuration.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!;
        PropertyInfo crafting = configuration.GetProperty("CraftFromChest", BindingFlags.Public | BindingFlags.Instance)!;
        PropertyInfo enabled = section.GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance)!;
        if (current?.PropertyType != configuration || crafting?.PropertyType != section || enabled?.PropertyType != typeof(bool))
            throw new NotSupportedException("CraftFromChest configuration getters changed.");
        var value = Expression.Variable(configuration);
        var settings = Expression.Variable(section);
        return Expression.Lambda<Func<bool>>(Expression.Block(new[] { value, settings },
            Expression.Assign(value, Expression.Property(null, current)),
            Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, configuration)),
                Expression.Block(Expression.Assign(settings, Expression.Property(value, crafting)),
                    Expression.AndAlso(Expression.NotEqual(settings, Expression.Constant(null, section)), Expression.Property(settings, enabled)))))).Compile();
    }

    private static void ObserveCraftingQuery(ref IEnumerable<CodeInstruction> __result)
    {
        var code = __result.ToList();
        _craftingQueryApplied = code.Count(c => c.Calls(_craftingStockMethod!)) == 1;
        __result = code;
    }

    internal static bool TryGetCraftingStock(int inventoryCount, Piece.Requirement requirement, Player player, out int total)
    {
        total = inventoryCount;
        if (!CraftingActive || UnityEngine.Time.unscaledTime < _craftingRetryAfter) return false;
        try
        {
            total = Math.Max(inventoryCount, _craftingStock!(inventoryCount, requirement, -1, player));
            return true;
        }
        catch (Exception error)
        {
            // V+'s cached chest list can briefly include destroyed/unloaded
            // containers. Let its next normal scan recover; do not edit its cache.
            _craftingRetryAfter = UnityEngine.Time.unscaledTime + 1f;
            if (!_craftingQueryWarned)
            {
                _craftingQueryWarned = true;
                _warn?.Invoke("ValheimPlus crafting stock query failed; temporarily using inventory-only quantities: " + error.GetBaseException().Message);
            }
            return false;
        }
    }

    private static void FailCraftingStock(Exception error)
    {
        if (_craftingFaulted) return;
        _craftingFaulted = true;
        _warn?.Invoke("ValheimPlus crafting stock display disabled for this session; using normal inventory display: " + error.GetBaseException().Message);
    }

    private static bool SkipRows() => !RowsActive;
    private static bool SkipMerge() => !MergeActive;
    private static bool KeepInput(IEnumerable<CodeInstruction> __0, ref IEnumerable<CodeInstruction> __result)
    {
        if (!RowsActive) return true;
        __result = __0;
        return false;
    }

    internal static void Dispose()
    {
        try { RestoreCrafting(); }
        finally
        {
            try { RestoreStack(); }
            finally { try { RestoreMerge(); } finally { RestoreRows(); } }
        }
    }

    private static void RestoreStack()
    {
        Stack.UnpatchSelf();
        StackActive = false;
    }

    private static void RestoreCrafting()
    {
        // Disable display first, even if removing the observer fails. Keep its
        // MethodInfo alive until removal succeeds so later V+ repatches stay safe.
        _craftingStock = null;
        Crafting.UnpatchSelf();
        _craftingEnabled = null;
        _craftingStockMethod = null;
        _craftingQueryApplied = false;
        _craftingFaulted = false;
        _craftingRetryAfter = 0f;
        _craftingQueryWarned = false;
    }

    private static void Rollback(Action restore, Action<string> warn)
    {
        try { restore(); }
        catch (Exception error)
        {
            warn("ValheimPlus compatibility rollback failed; restart after correcting the reported patch conflict: " + error.GetBaseException().Message);
        }
    }

    private static void RestoreMerge()
    {
        Merge.UnpatchSelf();
        MergeActive = false;
    }

    private static void RestoreRows()
    {
        bool rebuild = RowsActive;
        Rows.UnpatchSelf();
        RowsActive = false;
        if (rebuild) Rows.CreateProcessor(NativeResize).Patch();
    }

    private static MethodInfo Require(Assembly assembly, string typeName, string methodName, Type result,
        Type[] arguments, MethodBase native, HarmonyPatchType kind, string shape)
    {
        Type type = assembly.GetType(Prefix + typeName, true)!;
        MethodInfo? method = AccessTools.DeclaredMethod(type, methodName, arguments);
        if (method == null || !method.IsStatic || method.ReturnType != result)
            throw new MissingMethodException(type.FullName, methodName);
        if (native == null) throw new MissingMethodException("Native target unavailable: " + typeName);
        Patches? info = Harmony.GetPatchInfo(native);
        IEnumerable<Patch>? patches = kind == HarmonyPatchType.Prefix ? info?.Prefixes :
            kind == HarmonyPatchType.Postfix ? info?.Postfixes : info?.Transpilers;
        if (patches?.Count(p => p.owner == NativeOwner && p.PatchMethod == method) != 1)
            throw new NotSupportedException("Native patch registration changed: " + typeName);
        if (BehaviorShape(type) != shape)
            throw new NotSupportedException("Relevant patch behavior changed: " + typeName);
        return method;
    }

    // Reviewed V+ 10.4.0 patch classes only, including their private helpers,
    // generated predicates and static method bindings. Ignore assembly versions,
    // MVID/tokens, raw branch offsets and NOPs. This detects relevant IL changes;
    // it is not a proof that differently implemented code is semantically unequal.
    // No reflection/IL inspection runs in inventory or per-frame callbacks.
    internal static string BehaviorShape(Type type)
    {
        var text = new StringBuilder();
        IEnumerable<Type> Types(Type root) => new[] { root }.Concat(root.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(Types));
        foreach (Type current in Types(type).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var methods = current.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>();
            methods = methods.Concat(current.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            if (current.TypeInitializer != null) methods = methods.Concat(new[] { current.TypeInitializer });
            foreach (MethodBase method in methods.OrderBy(Member, StringComparer.Ordinal))
            {
                text.AppendLine(Member(method));
                MethodBody? body = method.GetMethodBody();
                if (body == null) throw new NotSupportedException("Missing body: " + method.Name);
                foreach (LocalVariableInfo local in body.LocalVariables) text.AppendLine("local:" + TypeName(local.LocalType) + ":" + local.IsPinned);
                var code = PatchProcessor.GetOriginalInstructions(method).ToList();
                var labels = new Dictionary<Label, int>();
                int index = 0;
                foreach (CodeInstruction instruction in code)
                {
                    foreach (Label label in instruction.labels) labels[label] = index;
                    if (instruction.opcode != OpCodes.Nop) index++;
                }
                string Operand(object? value) => value switch
                {
                    null => "",
                    MemberInfo member => Member(member),
                    Label label => "@" + labels[label],
                    Label[] targets => string.Join(",", targets.Select(t => "@" + labels[t])),
                    LocalVariableInfo local => "local:" + local.LocalIndex,
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                };
                foreach (CodeInstruction instruction in code)
                {
                    foreach (ExceptionBlock block in instruction.blocks) text.AppendLine("block:" + block.blockType + ":" + (block.catchType == null ? "" : TypeName(block.catchType)));
                    if (instruction.opcode != OpCodes.Nop) text.AppendLine(instruction.opcode.Name + " " + Operand(instruction.operand));
                }
            }
        }
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString().Replace("\r\n", "\n")))).Replace("-", "");
    }

    private static string Member(MemberInfo member) => member switch
    {
        Type type => TypeName(type),
        MethodBase method => TypeName(method.DeclaringType!) + "::" + method.Name + "(" +
            string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + "):" + (method is MethodInfo info ? TypeName(info.ReturnType) : ""),
        FieldInfo field => TypeName(field.DeclaringType!) + "::" + field.Name + ":" + TypeName(field.FieldType),
        _ => member.DeclaringType?.FullName + "::" + member.Name
    };

    private static string TypeName(Type type) => type.HasElementType
        ? TypeName(type.GetElementType()!) + (type.IsByRef ? "&" : type.IsPointer ? "*" : "[" + new string(',', type.GetArrayRank() - 1) + "]")
        : type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">"
        : type.FullName ?? type.Name;
}
