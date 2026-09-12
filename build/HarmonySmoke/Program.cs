using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class Program
{
    private static string[] folders = Array.Empty<string>();
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: <mod.dll> <original Managed> <BepInEx core>");
        folders = new[] { Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetDirectoryName(Path.GetFullPath(args[0]))! };
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
        {
            string fileName = new AssemblyName(eventArgs.Name).Name + ".dll";
            foreach (string folder in folders)
            {
                string path = Path.Combine(folder, fileName);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        return Run(args);
    }

    // Run separately so Harmony resolves only after installing the original-DLL resolver.
    private static int Run(string[] args)
    {
        var game = Assembly.LoadFrom(Path.Combine(args[1], "assembly_valheim.dll"));
        var mod = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var player = game.GetType("Player", true)!;
        var original = player.GetMethod("SetInventorySize", new[] { typeof(int) })!;
        Console.WriteLine($"Original: {original.Module.FullyQualifiedName}; IL bytes: {original.GetMethodBody()?.GetILAsByteArray()?.Length}");
        var transpiler = mod.GetType("InventorySlots.PlayerNativeInventorySizePatch", true)!
            .GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
        var input = ReadStraightLineBody(original);
        var result = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { input })!).ToList();
        int Calls(List<CodeInstruction> body, string name) => body.Count(i => i.operand is MethodInfo method && method.Name == name);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        Check(Calls(result, "SetNativeInventoryStorageHeight") == 1, "physical storage is intercepted once before drops");
        Check(Calls(result, "SetNativeInventoryPanelSize") == 1, "display size is intercepted once");
        Check(Calls(result, "AddUniqueKeyValue") == Calls(input, "AddUniqueKeyValue"), "vanilla purchase-state write remains");
        Check(Calls(result, "DropInvalidItems") == 1, "invalid-coordinate cleanup remains");
        Check(result.FindIndex(i => i.operand is MethodInfo m && m.Name == "SetNativeInventoryStorageHeight") <
              result.FindIndex(i => i.operand is MethodInfo m && m.Name == "DropInvalidItems"), "storage protection precedes cleanup");
        bool rejected = false;
        try
        {
            var missingStorage = ReadStraightLineBody(original)
                .Where(i => !(i.operand is MethodInfo m && m.Name == "SetHeight")).ToList();
            _ = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { missingStorage })!).ToList();
        }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "changed/foreign IL without storage interception is rejected");
        Console.WriteLine("6 checks passed using original game IL. No Unity initialization or game session was executed.");
        return 0;
    }

    private static List<CodeInstruction> ReadStraightLineBody(MethodInfo method)
    {
        // HarmonyX's older MonoMod IL copier does not support this verifier's
        // CoreCLR runtime. Read the actual method bytes without installing patches.
        var codes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(c => unchecked((ushort)c.Value));
        var body = method.GetMethodBody()!;
        if (body.ExceptionHandlingClauses.Count != 0) throw new InvalidOperationException("Unexpected protected region in original resize method");
        using var reader = new BinaryReader(new MemoryStream(body.GetILAsByteArray()!));
        var result = new List<CodeInstruction>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            ushort code = reader.ReadByte();
            if (code == 0xfe) code = (ushort)(0xfe00 | reader.ReadByte());
            OpCode opcode = codes[code];
            object? operand = opcode.OperandType switch
            {
                OperandType.InlineNone => null,
                OperandType.InlineMethod => method.Module.ResolveMethod(reader.ReadInt32()),
                OperandType.InlineField => method.Module.ResolveField(reader.ReadInt32()),
                OperandType.InlineType => method.Module.ResolveType(reader.ReadInt32()),
                OperandType.InlineTok => method.Module.ResolveMember(reader.ReadInt32()),
                OperandType.InlineString => method.Module.ResolveString(reader.ReadInt32()),
                OperandType.InlineI => reader.ReadInt32(),
                OperandType.InlineI8 => reader.ReadInt64(),
                OperandType.ShortInlineI => reader.ReadSByte(),
                OperandType.ShortInlineVar => reader.ReadByte(),
                OperandType.InlineVar => reader.ReadUInt16(),
                _ => throw new InvalidOperationException("Original resize method is no longer straight-line IL: " + opcode)
            };
            result.Add(new CodeInstruction(opcode, operand));
        }
        return result;
    }
}
