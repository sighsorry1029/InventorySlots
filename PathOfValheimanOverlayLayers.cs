using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // Reviewed Refresh body from PoV 6.0.8. Only this UI operation is gated;
    // assembly version/MVID/tokens and unrelated PoV code are not part of it.
    private const string PovOverlayRefreshShape = "4DC4541278291166E79AA52265117DE6EF34C640C2DF6C68B6C59F4DE65534E4";
    private static bool _povOverlayShapeWarned;

    private static void InstallPathOfValheimanOverlayLayers(Harmony harmony)
    {
        Type type = _pov!.Assembly.GetType("PassiveTree.UI.InventoryOverlayLayers", true)!;
        MethodInfo method = RequirePathOfValheimanMethod(type, "Refresh", typeof(void), typeof(InventoryGui));
        if (PathOfValheimanOverlayShape(method, PatchProcessor.GetOriginalInstructions(method)) != PovOverlayRefreshShape)
            throw new NotSupportedException("PoV overlay refresh behavior changed; native layering retained");
        harmony.Patch(method, transpiler: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(SkipIdlePathOfValheimanOverlayScan))
        { priority = Priority.Last });
    }

    private static IEnumerable<CodeInstruction> SkipIdlePathOfValheimanOverlayScan(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        // Check the actual input too: a preceding foreign transpiler or a later
        // Harmony rebuild may have added work that must not be skipped.
        bool matches;
        try { matches = PathOfValheimanOverlayShape(__originalMethod, code) == PovOverlayRefreshShape; }
        catch { matches = false; } // Unrecognizable incoming IL disables only this optimization.
        if (!matches)
        {
            if (!_povOverlayShapeWarned)
                Log.LogWarning("PoV overlay idle optimization skipped: another patch changed the refresh body; native layering retained.");
            _povOverlayShapeWarned = true;
            return code;
        }

        int start = code.FindIndex(i => i.opcode == OpCodes.Stloc_1) + 1;
        // The checked body has already read split/drag Transforms into locals
        // 0/1, after its native GUI/null/visibility guards. No private C# access.
        Label scan = generator.DefineLabel();
        code[start].labels.Add(scan);
        code.InsertRange(start, new[]
        {
            new CodeInstruction(OpCodes.Ldloc_0), new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(InventorySlotsPlugin), nameof(HasActivePathOfValheimanOverlay))),
            new CodeInstruction(OpCodes.Brtrue, scan), new CodeInstruction(OpCodes.Ret)
        });
        return code;
    }

    private static bool HasActivePathOfValheimanOverlay(Transform split, Transform drag) =>
        (split != null && split.gameObject.activeInHierarchy) || (drag != null && drag.gameObject.activeInHierarchy);

    // Canonicalize just this small method, including local types and branches.
    // Used only while installing/rebuilding patches, never during a UI frame.
    // Exact shape matching is conservative, not proof of semantic inequality.
    private static string PathOfValheimanOverlayShape(MethodBase method, IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var labels = new Dictionary<Label, int>();
        int index = 0;
        foreach (CodeInstruction instruction in code)
        {
            foreach (Label label in instruction.labels) labels[label] = index;
            if (instruction.opcode != OpCodes.Nop) index++;
        }
        string Operand(object? operand) => operand switch
        {
            null => "",
            MemberInfo member => member.DeclaringType?.FullName + "::" + member,
            Label label => "@" + labels[label],
            Label[] targets => string.Join(",", targets.Select(l => "@" + labels[l])),
            LocalVariableInfo local => "local:" + local.LocalIndex,
            _ => Convert.ToString(operand, CultureInfo.InvariantCulture) ?? ""
        };
        var text = new StringBuilder();
        MethodBody body = method.GetMethodBody() ?? throw new NotSupportedException("PoV overlay refresh body missing");
        foreach (LocalVariableInfo local in body.LocalVariables)
            text.AppendLine("local:" + local.LocalType.FullName + ":" + local.IsPinned);
        foreach (CodeInstruction instruction in code)
        {
            foreach (ExceptionBlock block in instruction.blocks)
                text.AppendLine("block:" + block.blockType + ":" + block.catchType?.FullName);
            if (instruction.opcode != OpCodes.Nop)
                text.AppendLine(instruction.opcode.Name + " " + Operand(instruction.operand));
        }
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString().Replace("\r\n", "\n")))).Replace("-", "");
    }
}
