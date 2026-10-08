using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static partial class Program
{
    private static string[] folders = Array.Empty<string>();
    private static int Main(string[] args)
    {
        if (args.Length < 3 || args.Length > 4) throw new ArgumentException("Usage: <mod.dll> <original Managed> <BepInEx core> [AdminQoL.dll]");
        folders = new[] { Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetDirectoryName(Path.GetFullPath(args[0]))! }
            .Concat(args.Length == 4 ? new[] { Path.GetDirectoryName(Path.GetFullPath(args[3]))! } : Array.Empty<string>()).ToArray();
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
        var crafting = game.GetType("InventoryGui", true)!.GetMethod("DoCrafting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var refinementPatch = mod.GetType("InventorySlots.InventoryGuiRefinementOutcomePatch", true)!;
        var refinementTranspiler = refinementPatch.GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
        var ready = refinementPatch.GetProperty("Ready", BindingFlags.Static | BindingFlags.NonPublic)!;
        List<CodeInstruction> PatchRefinement(List<CodeInstruction> code)
        {
            var generator = new DynamicMethod("RefinementSmoke", typeof(void), Type.EmptyTypes).GetILGenerator();
            return ((IEnumerable<CodeInstruction>)refinementTranspiler.Invoke(null, new object[] { code, generator })!).ToList();
        }
        var craftingInput = ReadStraightLineBody(crafting);
        var craftingResult = PatchRefinement(craftingInput);
        Check((bool)ready.GetValue(null)!, "original refinement branch layout is recognized");
        Check(Calls(craftingResult, "ObserveRefinementOutcome") == 3, "success, downgrade and destruction are observed once each");
        Check(Calls(craftingResult, "ObserveRefinementReplacement") == 2, "only success and downgrade observe replacement results");
        Check(Calls(craftingResult, "AddItem") == Calls(craftingInput, "AddItem"), "native result and refund additions remain");
        Check(Calls(craftingResult, "Range") == Calls(craftingInput, "Range"), "native random decision is unchanged");
        Check(Calls(craftingResult, "ConsumeResources") == Calls(craftingInput, "ConsumeResources"), "native resource consumption remains");
        // Remove only the inserted observer sequences. All original IL operands
        // and order (including native branches and chance comparisons) must match.
        var native = new List<CodeInstruction>();
        for (int i = 0; i < craftingResult.Count; i++)
        {
            if (i + 2 < craftingResult.Count && craftingResult[i + 2].operand is MethodInfo callback)
            {
                if (callback.Name == "ObserveRefinementOutcome") { i += 2; continue; }
                if (callback.Name == "ObserveRefinementReplacement") { i += 5; continue; }
            }
            native.Add(craftingResult[i]);
        }
        Check(native.Count == craftingInput.Count && native.Zip(craftingInput, (a, b) => a.opcode == b.opcode && Equals(a.operand, b.operand)).All(same => same),
            "every native instruction and operand is preserved");
        var missingBreak = ReadStraightLineBody(crafting).Where(i => !Equals(i.operand, "$msg_upgrader_broke")).ToList();
        var rejectedRefinement = PatchRefinement(missingBreak);
        Check(!(bool)ready.GetValue(null)! && Calls(rejectedRefinement, "ObserveRefinementOutcome") == 0, "unknown refinement IL fails closed without partial injection");
        var capture = game.GetType("GameCamera", true)!.GetMethod("UpdateMouseCapture", Type.EmptyTypes)!;
        var cursorTranspiler = mod.GetType("InventorySlots.InventorySlotsPlugin+ItemLinkChatCursorPatch", true)!
            .GetMethod("Transpiler", BindingFlags.Static | BindingFlags.NonPublic)!;
        var captureInput = ReadStraightLineBody(capture);
        var captureResult = ((IEnumerable<CodeInstruction>)cursorTranspiler.Invoke(null, new object[] { captureInput })!).ToList();
        int cursorAt = captureResult.FindIndex(i => i.operand is MethodInfo m && m.Name == "KeepItemLinkChatCursorFree");
        Check(cursorAt > 0 && Calls(captureResult, "KeepItemLinkChatCursorFree") == 1,
            "chat cursor predicate is inserted once");
        Check(captureResult[cursorAt - 1].operand is MethodInfo visibility && visibility.DeclaringType?.Name == "TextInput" && visibility.Name == "IsVisible",
            "predicate wraps the native text-input result only inside camera capture");
        var unchangedCapture = captureResult.Where(i => !(i.operand is MethodInfo m && m.Name == "KeepItemLinkChatCursorFree")).ToList();
        Check(unchangedCapture.Count == captureInput.Count && unchangedCapture.Zip(captureInput,
            (a, b) => a.opcode == b.opcode && Equals(a.operand, b.operand)).All(same => same),
            "native Ctrl+F1 toggle, capture field, menu/radial branches and cursor API calls are preserved");
        int autoPickupChecks = CheckAutoPickup(game, mod, args.Length == 4 ? Assembly.LoadFrom(Path.GetFullPath(args[3])) : null);
        Console.WriteLine($"{17 + autoPickupChecks} checks passed using original game IL and isolated pickup predicates. No Unity initialization or game session was executed.");
        return 0;
    }

    private static List<CodeInstruction> ReadStraightLineBody(MethodInfo method, ILGenerator? generator = null)
    {
        // HarmonyX's older MonoMod IL copier does not support this verifier's
        // CoreCLR runtime. Read actual bytes without installing patches. Native
        // branch offsets are retained as data unless a label generator is supplied.
        var codes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(c => unchecked((ushort)c.Value));
        var body = method.GetMethodBody()!;
        if (body.ExceptionHandlingClauses.Count != 0) throw new InvalidOperationException("Unexpected protected region in original method");
        using var reader = new BinaryReader(new MemoryStream(body.GetILAsByteArray()!));
        var result = new List<CodeInstruction>();
        var offsets = new Dictionary<int, CodeInstruction>();
        var branches = new List<(CodeInstruction Code, int[] Targets)>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            int offset = (int)reader.BaseStream.Position;
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
                OperandType.InlineBrTarget => reader.ReadInt32(),
                OperandType.ShortInlineBrTarget => reader.ReadSByte(),
                OperandType.InlineR => reader.ReadDouble(),
                OperandType.ShortInlineR => reader.ReadSingle(),
                OperandType.InlineSwitch => Enumerable.Range(0, reader.ReadInt32()).Select(_ => reader.ReadInt32()).ToArray(),
                _ => throw new InvalidOperationException("Unsupported original IL operand: " + opcode)
            };
            var instruction = new CodeInstruction(opcode, operand);
            result.Add(instruction);
            offsets.Add(offset, instruction);
            int end = (int)reader.BaseStream.Position;
            if (opcode.OperandType == OperandType.InlineBrTarget || opcode.OperandType == OperandType.ShortInlineBrTarget)
                branches.Add((instruction, new[] { end + Convert.ToInt32(operand) }));
            else if (opcode.OperandType == OperandType.InlineSwitch)
                branches.Add((instruction, ((int[])operand!).Select(delta => end + delta).ToArray()));
        }
        if (generator != null)
        {
            foreach (var branch in branches)
            {
                // Harmony ILManipulator creates a fresh label per branch, even
                // when multiple branches share the same destination instruction.
                var labels = branch.Targets.Select(target =>
                {
                    Label label = generator.DefineLabel();
                    offsets[target].labels.Add(label);
                    return label;
                }).ToArray();
                branch.Code.operand = branch.Code.opcode == OpCodes.Switch
                    ? labels
                    : labels[0];
            }
        }
        return result;
    }
}
