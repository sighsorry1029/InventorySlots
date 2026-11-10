using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

#if INVENTORY_ACTIONS
namespace InventoryActions;
#else
namespace InventorySlots;
#endif

// PoV's metadata comparison is narrower than an owner's guarded composition.
// Only authorize the pair while our caller also owns the resulting metadata merge.
internal static class PathOfValheimanStackCompatibility
{
    internal static bool Active { get; private set; }
    [ThreadStatic] private static ItemData? source, target;
    private static Harmony? patches;
    private static Func<ItemData?>? readIncoming;
    private static Func<ItemData, ItemData, bool>? nativeComparison;
    private static bool canCompose;

    internal readonly struct Scope : IDisposable
    {
        private readonly ItemData? previousSource, previousTarget;
        internal Scope(ItemData incoming, ItemData? destination)
        {
            previousSource = source; previousTarget = target;
            source = incoming; target = destination;
        }
        public void Dispose() { source = previousSource; target = previousTarget; }
    }

    internal static Scope Begin(ItemData incoming, ItemData? destination = null) => new(incoming, destination);

    internal static string? Install(Assembly assembly, string owner)
    {
        if (Active) return null;
        Type? type = assembly.GetType("PassiveTree.StackRules");
        MethodInfo? same = AccessTools.DeclaredMethod(type, "SameData", new[] { typeof(ItemData), typeof(ItemData) });
        if (same == null) return null; // Earlier PoV did not add this metadata gate.
        if (!same.IsStatic || same.ReturnType != typeof(bool)) throw new MissingMethodException(type?.FullName, "SameData");
        string? warning = null;
        try { ValidateMetadataComparison(same); }
        catch (Exception error) { warning = error.GetBaseException().Message; }
        nativeComparison = (Func<ItemData, ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemData, ItemData, bool>), same);
        FieldInfo incoming = AccessTools.DeclaredField(type, "incoming");
        if (incoming?.FieldType != typeof(ItemData) || !incoming.IsStatic)
            throw new MissingFieldException(type!.FullName, "incoming");
        var read = AccessTools.StaticFieldRefAccess<ItemData>(incoming);
        readIncoming = () => read();
#if INVENTORY_SLOTS
        MethodInfo RequirePostfix(string nested, Type result)
        {
            MethodInfo? method = AccessTools.DeclaredMethod(type!.GetNestedType(nested, BindingFlags.NonPublic),
                "Postfix", new[] { typeof(Inventory), result.MakeByRefType() });
            if (method == null || !method.IsStatic || method.ReturnType != typeof(void) ||
                !PatchProcessor.GetOriginalInstructions(method).Any(i => i.Calls(same)))
                throw new MissingMethodException(type.FullName + "+" + nested, "Postfix");
            return method;
        }
        var match = RequirePostfix("MatchMetadata", typeof(ItemData));
        var space = RequirePostfix("MetadataSpace", typeof(int));
#endif
        var harmony = new Harmony(owner + ".pov.stack-metadata");
        try
        {
            if (warning == null)
                harmony.Patch(same, postfix: new HarmonyMethod(typeof(PathOfValheimanStackCompatibility), nameof(Compare)));
#if INVENTORY_SLOTS
            // IS already decides destinations, including locked cells, cheat identity
            // and owner policies. PoV must not replace a rejected result by rescanning.
            harmony.Patch(match, prefix: new HarmonyMethod(typeof(PathOfValheimanStackCompatibility), nameof(KeepStackSelection)));
            harmony.Patch(space, prefix: new HarmonyMethod(typeof(PathOfValheimanStackCompatibility), nameof(StackSpace)));
#endif
            patches = harmony;
            canCompose = warning == null;
            Active = true;
        }
        catch { harmony.UnpatchSelf(); throw; }
        return warning;
    }

    internal static void Dispose()
    {
        patches?.UnpatchSelf(); patches = null;
        Active = false; source = target = null;
        readIncoming = null;
        nativeComparison = null; canCompose = false;
    }

    internal static bool MatchesIdentity(ItemData left, ItemData right)
    {
        if (!Active) return true;
        if (left.m_variant != right.m_variant) return false;
        // Run PoV first, including its normalization of obsolete implicit rolls,
        // before callers inspect or snapshot metadata. Unknown comparison behavior
        // retains its native gate in addition to our slot/owner policy.
        try { bool matches = nativeComparison!(left, right); return canCompose || matches; }
        catch { return false; }
    }

    private static void Compare(ItemData __0, ItemData __1, ref bool __result)
    {
        if (!Active || source == null || __0 == null || __1 == null ||
            !(ReferenceEquals(__0, source) && (target == null || ReferenceEquals(__1, target)) ||
              ReferenceEquals(__1, source) && (target == null || ReferenceEquals(__0, target))) ||
            !StackMetadataPolicy.HasMergeMetadata(__0.m_customData) &&
            !StackMetadataPolicy.HasMergeMetadata(__1.m_customData)) return;
        // An untracked nested AddItem must not borrow its caller's permission.
        ItemData? incoming = readIncoming?.Invoke();
        if (incoming != null && !ReferenceEquals(incoming, source)) return;
        __result = __0.m_variant == __1.m_variant && __0.m_cheated == __1.m_cheated &&
                   StackMetadataPolicy.AreCompatible(__0.m_customData, __1.m_customData);
    }

#if INVENTORY_SLOTS
    private static bool KeepStackSelection() => !Active;
    private static bool StackSpace(Inventory __0, ref int __1)
    {
        ItemData? incoming = readIncoming?.Invoke();
        if (!Active || incoming == null) return true;
        __1 = InventorySlotsPlugin.CountStackSpaceForIncomingItem(__0, incoming);
        return false;
    }
#endif

    private static void ValidateMetadataComparison(MethodInfo method)
    {
        // Check the relevant behavior, not a DLL version/MVID. A future comparison
        // using additional item state must not silently lose those checks here.
        // This bounds the inputs/calls; it is not proof of semantic equivalence.
        var code = PatchProcessor.GetOriginalInstructions(method).ToList();
        var normalizedArguments = new HashSet<OpCode>();
        var fields = code.Select(i => i.operand).OfType<FieldInfo>().ToArray();
        if (!fields.Any(f => f.Name == "m_variant") || !fields.Any(f => f.Name == "m_customData") ||
            fields.Any(f => f.DeclaringType != typeof(ItemData) || f.Name != "m_variant" && f.Name != "m_customData"))
            throw new NotSupportedException("PoV metadata comparison uses additional item state");
        foreach (var call in code.Select(i => i.operand).OfType<MethodInfo>().Distinct())
        {
            // SameData still executes this complete native implementation. Do not
            // emulate it or remove pt.implicit ourselves: corruption and prefab
            // preview exclusions belong to PoV.
            if (call.DeclaringType == method.DeclaringType!.Assembly.GetType("PassiveTree.Implicits") &&
                call.Name == "Settle" && call.IsPublic && call.IsStatic && call.ReturnType == typeof(bool) &&
                call.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(ItemData) }))
            {
                foreach (int index in Enumerable.Range(0, code.Count).Where(i => code[i].Calls(call)))
                {
                    if (index == 0 || index + 1 >= code.Count || code[index + 1].opcode != OpCodes.Pop ||
                        code[index - 1].opcode != OpCodes.Ldarg_0 && code[index - 1].opcode != OpCodes.Ldarg_1 ||
                        !normalizedArguments.Add(code[index - 1].opcode))
                        throw new NotSupportedException("PoV metadata normalization flow changed");
                }
                continue;
            }
            if (call.DeclaringType == method.DeclaringType && call.Name == "Bookkeeping")
            {
                var helper = PatchProcessor.GetOriginalInstructions(call).ToList();
                if (helper.Any(i => i.operand is FieldInfo) ||
                    helper.Select(i => i.operand).OfType<MethodInfo>().Any(m => m.DeclaringType != typeof(string)) ||
                    helper.Select(i => i.operand).OfType<string>().Any(s => s != "pt.work." && s != "pt.born" && s != "pt.bonus" &&
                        s != "valheiman.equipment.v1.cell" && s != "pt.fish.caught"))
                    throw new NotSupportedException("PoV metadata exclusions changed");
                continue;
            }
            Type? declaring = call.DeclaringType;
            bool collection = declaring != null && declaring.Namespace == "System.Collections.Generic" &&
                              (declaring == typeof(Dictionary<string, string>) || declaring == typeof(KeyValuePair<string, string>) ||
                               declaring == typeof(Dictionary<string, string>.Enumerator));
            if (!(declaring == typeof(string) && (call.Name == "op_Equality" || call.Name == "op_Inequality")) &&
                !(collection && new[] { "TryGetValue", "ContainsKey", "GetEnumerator", "MoveNext", "get_Current", "get_Key", "get_Value", "Dispose" }.Contains(call.Name)) &&
                !(declaring == typeof(IDisposable) && call.Name == "Dispose"))
                throw new NotSupportedException("PoV metadata comparison logic changed: " + call.Name);
        }
        if (normalizedArguments.Count == 1)
            throw new NotSupportedException("PoV metadata normalization is asymmetric");
    }
}
