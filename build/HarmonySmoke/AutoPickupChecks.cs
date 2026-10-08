using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static partial class Program
{
    private static int CheckAutoPickup(Assembly game, Assembly mod, Assembly? adminQoL)
    {
        var original = game.GetType("Player", true)!.GetMethod("AutoPickup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var transpiler = mod.GetType("InventorySlots.AutoPickupCapacityPatch", true)!
            .GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
        var tryMove = transpiler.DeclaringType!.GetMethod("TryMoveWeightCheck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var exclusions = mod.GetType("InventorySlots.AutoPickupExclusionPatch", true)!
            .GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
        var generator = new DynamicMethod("PickupLabels", typeof(void), Type.EmptyTypes).GetILGenerator();
        var input = ReadStraightLineBody(original, generator);
        List<CodeInstruction> Patch(MethodInfo patch, List<CodeInstruction> code) =>
            ((IEnumerable<CodeInstruction>)patch.Invoke(null, new object[] { code })!).ToList();
        List<CodeInstruction> Copy() => input.Select(i => new CodeInstruction(i)).ToList();
        bool Same(IEnumerable<CodeInstruction> a, IEnumerable<CodeInstruction> b) =>
            a.Count() == b.Count() && a.Zip(b).All(p => p.First.opcode == p.Second.opcode &&
                Equals(p.First.operand, p.Second.operand) && p.First.labels.SequenceEqual(p.Second.labels) &&
                p.First.blocks.SequenceEqual(p.Second.blocks));
        int CallAt(List<CodeInstruction> code, string name) => code.FindIndex(i => i.operand is MethodInfo m && m.Name == name);
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
            Console.WriteLine("PASS pickup: " + message);
        }

        // Use the installed Harmony reader too: a test-only IL reader previously
        // hid this regression by merging labels that share a destination.
        using (var module = Mono.Cecil.ModuleDefinition.CreateModule("BranchLabels", Mono.Cecil.ModuleKind.Dll))
        {
            var type = new Mono.Cecil.TypeDefinition("Probe", "Labels", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(type);
            var method = new Mono.Cecil.MethodDefinition("Branches", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
            type.Methods.Add(method);
            var il = method.Body.GetILProcessor();
            var target = il.Create(Mono.Cecil.Cil.OpCodes.Ret);
            il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Br, target));
            il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Br, target));
            il.Append(target);
            var readerType = typeof(CodeInstruction).Assembly.GetType("HarmonyLib.Internal.Patching.ILManipulator", true)!;
            var reader = Activator.CreateInstance(readerType, new object[] { method.Body, false })!;
            var getter = readerType.GetMethods().Single(m => m.Name == "GetInstructions" && m.GetParameters().Length == 2);
            var body = (List<CodeInstruction>)getter.Invoke(reader, new object?[] { generator, null })!;
            Check(!Equals(body[0].operand, body[1].operand) && body[2].labels.Contains((Label)body[0].operand) &&
                  body[2].labels.Contains((Label)body[1].operand),
                "installed Harmony produces separate labels on the same branch destination");
        }

        int start = CallAt(input, "CanAddItem") - 5;
        var capacityReject = (Label)input[start + 6].operand;
        var weightReject = (Label)input[start + 17].operand;
        Check(!capacityReject.Equals(weightReject) && input.Any(i => i.labels.Contains(capacityReject) && i.labels.Contains(weightReject)),
            "reader preserves distinct labels on the shared native rejection destination");
        var snapshot = Copy();
        var result = Patch(transpiler, input);
        Check(original.IsPrivate, "original private AutoPickup is the patch target");
        Check(Same(input, snapshot), "transpiler does not mutate caller instructions or labels");
        Check(result.Count == input.Count && Same(input.Take(start), result.Take(start)) &&
            Same(input.Skip(start + 18), result.Skip(start + 18)),
            "ownership, load, exclusion, distance and pickup instructions outside the gates stay unchanged");
        Check(CallAt(result, "Load") < CallAt(result, "GetWeight") && CallAt(result, "GetMaxCarryWeight") < CallAt(result, "CanAddItem"),
            "loaded drop weight is checked before capacity");
        Check(Same(result.Skip(start).Take(11), input.Skip(start + 7).Take(11)) &&
            Same(result.Skip(start + 11).Take(7), input.Skip(start).Take(7)),
            "native predicates, arguments and common continue target are reused exactly once");
        var sharedLabel = Copy();
        sharedLabel[start + 17].operand = capacityReject;
        Check((bool)tryMove.Invoke(null, new object[] { sharedLabel })! && CallAt(sharedLabel, "GetMaxCarryWeight") < CallAt(sharedLabel, "CanAddItem"),
            "shared label identities also remain supported");

        var entry = Copy();
        Label entryLabel = generator.DefineLabel();
        entry[start].labels.Add(entryLabel);
        var entryResult = Patch(transpiler, entry);
        Check(entryResult[start].labels.Contains(entryLabel) && !entryResult[start + 11].labels.Contains(entryLabel) && entry[start].labels.Contains(entryLabel),
            "incoming branches enter the weight gate without changing source labels");

        void Reject(string name, Action<List<CodeInstruction>> alter)
        {
            var changed = Copy();
            alter(changed);
            var saved = changed.Select(i => new CodeInstruction(i)).ToList();
            // Test the failure result before logging: the plugin's logger field
            // initializes unrelated Unity/Mono-only static field accessors.
            Check(!(bool)tryMove.Invoke(null, new object[] { changed })! && Same(changed, saved),
                name + " leaves all instructions unchanged");
        }
        Reject("foreign internal branch entry", code => code[start + 7].labels.Add(generator.DefineLabel()));
        Reject("foreign exception boundary", code => code[start + 11].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock)));
        Reject("undefined rejection target", code => code[start + 17].operand = generator.DefineLabel());
        Reject("different rejection targets", code =>
        {
            code.Single(i => i.labels.Contains(weightReject)).labels.Remove(weightReject);
            code[^1].labels.Add(weightReject);
        });
        Reject("ambiguous label destination", code => code[^1].labels.Add(capacityReject));
        Reject("different drop local", code => code[start + 7] = new CodeInstruction(OpCodes.Ldloc_3));
        Reject("different weight stack argument", code => code[start + 9].opcode = OpCodes.Ldc_I4_1);
        Reject("missing load call", code => code[start - 1].opcode = OpCodes.Nop);
        Reject("short branch with uncertain reach", code => code[start + 17].opcode = OpCodes.Bgt_S);
        Reject("ambiguous duplicate pair", code => code.InsertRange(start + 18,
            input.Skip(start - 2).Take(20).Select(i => new CodeInstruction(i))));
        var reordered = result.Select(i => new CodeInstruction(i)).ToList();
        Check(!(bool)tryMove.Invoke(null, new object[] { reordered })! && Same(reordered, result),
            "already reordered gates are not modified again");
        var exclusionFirst = Patch(transpiler, Patch(exclusions, input));
        var weightFirst = Patch(exclusions, result);
        Check(Same(exclusionFirst, weightFirst) && exclusionFirst.Count(i => i.operand is MethodInfo m && m.Name == "FilterAutoPickup") == 1,
            "automatic exclusions and weight ordering compose identically in either patch order");

        if (adminQoL != null)
        {
            var adminType = adminQoL.GetType("AdminQoL.IgnoreCarryWeightAutoPickupPatch", true)!;
            var adminTranspiler = adminType.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
            var adminLimit = adminType.GetMethod("GetAutoPickupCarryLimit", BindingFlags.Static | BindingFlags.NonPublic)!;
            var adminOnly = Patch(adminTranspiler, Copy());
            var wrongOrder = adminOnly.Select(i => new CodeInstruction(i)).ToList();
            Check(!(bool)tryMove.Invoke(null, new object[] { wrongOrder })! && Same(wrongOrder, adminOnly),
                "actual AdminQoL applied first reproduces the skipped optimization without changing its helper");
            var beforeOwners = transpiler.GetCustomAttribute<HarmonyBefore>()?.info.before ?? Array.Empty<string>();
            Check(beforeOwners.Contains("sighsorry.AdminQoL"), "compiled patch declares ordering before AdminQoL");

            var combined = Patch(adminTranspiler, result.Select(i => new CodeInstruction(i)).ToList());
            Check(combined.Count(i => Equals(i.operand, adminLimit)) == 1 &&
                CallAt(combined, "GetAutoPickupCarryLimit") < CallAt(combined, "CanAddItem"),
                "actual AdminQoL helper remains the carry-limit authority after weight-first optimization");

            var expected = Patch(exclusions, combined);
            var sorterType = typeof(Patch).Assembly.GetType("HarmonyLib.PatchSorter", true)!;
            const BindingFlags sorterFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var constructor = sorterType.GetConstructor(sorterFlags, null, new[] { typeof(Patch[]), typeof(bool) }, null)!;
            var sort = sorterType.GetMethod("Sort", sorterFlags)!;
            var patches = new[] { transpiler, exclusions, adminTranspiler };
            foreach (int first in new[] { 0, 1, 2 })
            foreach (int second in new[] { 0, 1, 2 }.Where(i => i != first))
            {
                // Real Harmony sorting, with indexes matching each registration
                // order. The exclusion transpiler also triggers wrapper rebuilds.
                var registration = new[] { patches[first], patches[second], patches[3 - first - second] };
                var descriptors = registration.Select((method, index) => new Patch(method, index,
                    method == adminTranspiler ? "sighsorry.AdminQoL" : "sighsorry.InventorySlots", Priority.Normal,
                    method.GetCustomAttribute<HarmonyBefore>()?.info.before ?? Array.Empty<string>(),
                    Array.Empty<string>(), false)).ToArray();
                var sorter = constructor.Invoke(new object[] { descriptors, false });
                var sorted = (List<MethodInfo>)sort.Invoke(sorter, new object[] { original })!;
                bool orderCorrect = sorted.IndexOf(transpiler) < sorted.IndexOf(adminTranspiler);
                var composed = Copy();
                if (orderCorrect) foreach (var patch in sorted) composed = Patch(patch, composed);
                Check(orderCorrect && Same(composed, expected),
                    $"Harmony registration order {first}/{second}/{3 - first - second} retains both mods and automatic exclusions");
            }

            var adminBefore = CompilePickupGate(adminOnly.GetRange(start, 18), adminLimit);
            var adminAfter = CompilePickupGate(combined.GetRange(start, 18), adminLimit);
            foreach (bool unlimited in new[] { false, true })
            foreach (bool room in new[] { false, true })
            {
                // Substitute only the helper's result; do not initialize AdminQoL
                // permissions/settings or Unity. Its real helper call is checked above.
                var probe = new PickupProbe(10, 456, unlimited ? float.PositiveInfinity : 300, room);
                bool native = adminBefore(probe);
                probe.Inventory.CapacityCalls = 0;
                Check(adminAfter(probe) == native && native == (unlimited && room) &&
                    probe.Inventory.CapacityCalls == (unlimited ? 1 : 0),
                    $"AdminQoL limit {(unlimited ? "unlimited" : "normal")}, room={room}: same result and expected capacity calls");
            }
        }

        // Execute the actual original and transformed gate IL, replacing only game
        // member operands with isolated probes. Unity cannot be initialized here.
        var before = CompilePickupGate(input.GetRange(start, 18));
        var after = CompilePickupGate(result.GetRange(start, 18));
        var cases = new (string Name, float Item, float Carried, float Max, bool Capacity, bool Allowed, int CapacityCalls)[]
        {
            ("overweight with room", 10, 456, 300, true, false, 0),
            ("incoming stack exceeds remaining weight", 51, 250, 300, true, false, 0),
            ("exact weight limit", 50, 250, 300, true, true, 1),
            ("under limit", 10, 250, 300, true, true, 1),
            ("overweight without room", 10, 456, 300, false, false, 0),
            ("full inventory below weight limit", 10, 250, 300, false, false, 1),
            ("zero weight at limit", 0, 300, 300, true, true, 1),
            ("zero weight above limit", 0, 301, 300, true, false, 0),
            ("negative weight lowers carried weight", -20, 310, 300, true, true, 1),
            ("changed carry limit", 10, 456, 500, true, true, 1),
            ("NaN item weight", float.NaN, 250, 300, true, true, 1),
            ("NaN carry limit", 10, 250, float.NaN, true, true, 1),
            ("infinite weight", float.PositiveInfinity, 250, 300, true, false, 0)
        };
        foreach (var c in cases)
        {
            var probe = new PickupProbe(c.Item, c.Carried, c.Max, c.Capacity);
            bool native = before(probe);
            int nativeCalls = probe.Inventory.CapacityCalls;
            probe.Inventory.CapacityCalls = 0;
            bool optimized = after(probe);
            Check(native == c.Allowed && optimized == native && nativeCalls == 1 && probe.Inventory.CapacityCalls == c.CapacityCalls,
                c.Name + ": same decision, expected capacity call count");
        }
        var live = new PickupProbe(10, 456, 300, true);
        bool blocked = !after(live);
        live.Inventory.TotalWeight = 250;
        Check(blocked && after(live) && live.Inventory.CapacityCalls == 1,
            "capacity is immediately rechecked when carried weight changes; no cached rejection");
        return checks;
    }

    private static Func<PickupProbe, bool> CompilePickupGate(List<CodeInstruction> gate, MethodInfo? alternateCarryLimit = null)
    {
        var method = new DynamicMethod("PickupGate", typeof(bool), new[] { typeof(PickupProbe) }, typeof(Program), true);
        var il = method.GetILGenerator();
        int dropIndex = gate.Where(i => i.opcode == OpCodes.Ldloc || i.opcode == OpCodes.Ldloc_S)
            .Select(i => Convert.ToInt32(i.operand)).Distinct().Single();
        LocalBuilder? drop = null;
        for (int i = 0; i <= dropIndex; i++) drop = il.DeclareLocal(i == dropIndex ? typeof(PickupDrop) : typeof(int));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, typeof(PickupProbe).GetField(nameof(PickupProbe.Drop))!);
        il.Emit(OpCodes.Stloc, drop!);
        Label rejected = il.DefineLabel();
        foreach (var instruction in gate)
        {
            if (instruction.operand is FieldInfo field)
            {
                FieldInfo replacement = field.Name switch
                {
                    "m_inventory" => typeof(PickupProbe).GetField(nameof(PickupProbe.Inventory))!,
                    "m_itemData" => typeof(PickupDrop).GetField(nameof(PickupDrop.Item))!,
                    _ => throw new InvalidOperationException("Unexpected gate field: " + field)
                };
                il.Emit(instruction.opcode, replacement);
            }
            else if (instruction.operand is MethodInfo called)
            {
                if (Equals(called, alternateCarryLimit))
                {
                    il.Emit(instruction.opcode, typeof(PickupProbe).GetMethod(nameof(PickupProbe.GetAlternateCarryLimit))!);
                    continue;
                }
                Type receiver = called.Name switch
                {
                    "CanAddItem" or "GetTotalWeight" => typeof(PickupInventory),
                    "GetWeight" => typeof(PickupItem),
                    "GetMaxCarryWeight" => typeof(PickupProbe),
                    _ => throw new InvalidOperationException("Unexpected gate call: " + called)
                };
                il.Emit(instruction.opcode, receiver.GetMethod(called.Name)!);
            }
            else if (instruction.operand is Label) il.Emit(instruction.opcode, rejected);
            else if (instruction.opcode == OpCodes.Ldloc || instruction.opcode == OpCodes.Ldloc_S) il.Emit(instruction.opcode, drop!);
            else if (instruction.operand == null) il.Emit(instruction.opcode);
            else throw new InvalidOperationException("Unexpected gate operand: " + instruction);
        }
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(rejected);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<PickupProbe, bool>>();
    }

    private sealed class PickupProbe(float item, float carried, float max, bool capacity)
    {
        public PickupInventory Inventory = new() { TotalWeight = carried, Capacity = capacity };
        public PickupDrop Drop = new() { Item = new PickupItem { Weight = item } };
        public float GetMaxCarryWeight() => max;
        public static float GetAlternateCarryLimit(PickupProbe player) => player.GetMaxCarryWeight();
    }
    private sealed class PickupDrop { public PickupItem Item = null!; }
    private sealed class PickupItem
    {
        public float Weight;
        public float GetWeight(int stack)
        {
            if (stack != -1) throw new InvalidOperationException("Whole-stack weight argument changed");
            return Weight;
        }
    }
    private sealed class PickupInventory
    {
        public int CapacityCalls;
        public bool Capacity;
        public float TotalWeight;
        public float GetTotalWeight() => TotalWeight;
        public bool CanAddItem(PickupItem item, int stack)
        {
            if (item == null || stack != -1) throw new InvalidOperationException("Capacity arguments changed");
            CapacityCalls++;
            return Capacity;
        }
    }
}
