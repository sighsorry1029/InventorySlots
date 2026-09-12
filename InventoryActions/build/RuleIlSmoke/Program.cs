using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("<mod.dll> <original Managed> <BepInEx core>");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            foreach (string folder in new[] { args[1], args[2], Path.GetDirectoryName(Path.GetFullPath(args[0]))! })
            { string path = Path.Combine(folder, new AssemblyName(e.Name).Name + ".dll"); if (File.Exists(path)) return Assembly.LoadFrom(path); }
            return null;
        };
        Run(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string[] args)
    {
        Assembly game = Assembly.LoadFrom(Path.Combine(args[1], "assembly_valheim.dll"));
        Assembly mod = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        MethodInfo original = game.GetType("Player", true)!.GetMethod("AutoPickup", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo transpiler = mod.GetType(mod.GetName().Name + ".AutoPickupExclusionPatch", true)!.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
        var input = ReadBody(original);
        var field = game.GetType("ItemDrop", true)!.GetField("m_autoPickup")!;
        int at = input.FindIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field));
        int checks = 0;
        void Check(string name, bool result) { if (!result) throw new Exception(name); checks++; }
        Check("original AutoPickup is private", original.IsPrivate);
        Check("original pickup field is public", field.IsPublic);
        Check("one native pickup flag check", input.Count(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field)) == 1);
        var label = new DynamicMethod("label", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        input[at].labels.Add(label);
        var result = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { input })!).ToList();
        Check("exactly three inserted instructions", result.Count == input.Count + 3);
        Check("drop duplicated before flag load", result[at].opcode == OpCodes.Dup && result[at + 1].opcode == OpCodes.Ldfld);
        Check("local player argument and filter after flag", result[at + 2].opcode == OpCodes.Ldarg_0 && result[at + 3].operand is MethodInfo filter && filter.Name == "FilterAutoPickup");
        Check("branch labels enter before duplication", result[at].labels.Contains(label) && !result[at + 1].labels.Contains(label));
        Check("caller input labels unchanged", input[at].labels.Contains(label));
        var stripped = result.Where((_, i) => i != at && i != at + 2 && i != at + 3).ToList();
        Check("all original opcodes and operands retained", stripped.Zip(input).All(p => p.First.opcode == p.Second.opcode && Equals(p.First.operand, p.Second.operand)));
        Check("no item or world stores added", result.Count(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld) == input.Count(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld));
        Console.WriteLine($"PASS: {checks} compiled transpiler checks on original {original.Module.FullyQualifiedName}");
        Console.WriteLine("No Unity, manual pickup, game session or network execution performed.");
    }

    private static List<CodeInstruction> ReadBody(MethodInfo method)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
        byte[] bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var instructions = new List<(int Offset, CodeInstruction Code)>();
        var branchTargets = new List<(CodeInstruction Code, int[] Targets)>();
        int p = 0;
        int Int() { int n = BitConverter.ToInt32(bytes, p); p += 4; return n; }
        while (p < bytes.Length)
        {
            int offset = p; ushort value = bytes[p++]; if (value == 0xfe) value = (ushort)(0xfe00 | bytes[p++]);
            OpCode opcode = opcodes[value]; object? operand = null; int[]? targets = null;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.InlineField: operand = method.Module.ResolveField(Int()); break;
                case OperandType.InlineMethod: operand = method.Module.ResolveMethod(Int()); break;
                case OperandType.InlineType: operand = method.Module.ResolveType(Int()); break;
                case OperandType.InlineTok: operand = method.Module.ResolveMember(Int()); break;
                case OperandType.InlineString: operand = method.Module.ResolveString(Int()); break;
                case OperandType.InlineI: operand = Int(); break;
                case OperandType.ShortInlineI: operand = (sbyte)bytes[p++]; break;
                case OperandType.InlineI8: operand = BitConverter.ToInt64(bytes, p); p += 8; break;
                case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, p); p += 4; break;
                case OperandType.InlineR: operand = BitConverter.ToDouble(bytes, p); p += 8; break;
                case OperandType.InlineVar: operand = (int)BitConverter.ToUInt16(bytes, p); p += 2; break;
                case OperandType.ShortInlineVar: operand = (int)bytes[p++]; break;
                case OperandType.InlineBrTarget: { int delta = Int(); targets = new[] { p + delta }; break; }
                case OperandType.ShortInlineBrTarget: { int delta = (sbyte)bytes[p++]; targets = new[] { p + delta }; break; }
                case OperandType.InlineSwitch: { int count = Int(); var delta = new int[count]; for (int i = 0; i < count; i++) delta[i] = Int(); int start = p; targets = delta.Select(d => start + d).ToArray(); break; }
                default: throw new NotSupportedException(opcode.OperandType.ToString());
            }
            CodeInstruction code = new(opcode, operand); instructions.Add((offset, code));
            if (targets != null) branchTargets.Add((code, targets));
        }
        var generator = new DynamicMethod("branches", typeof(void), Type.EmptyTypes).GetILGenerator();
        var labels = branchTargets.SelectMany(b => b.Targets).Distinct().ToDictionary(t => t, _ => generator.DefineLabel());
        foreach (var instruction in instructions) if (labels.TryGetValue(instruction.Offset, out Label label)) instruction.Code.labels.Add(label);
        foreach (var branch in branchTargets) branch.Code.operand = branch.Code.opcode == OpCodes.Switch ? branch.Targets.Select(t => labels[t]).ToArray() : labels[branch.Targets[0]];
        return instructions.Select(i => i.Code).ToList();
    }
}
