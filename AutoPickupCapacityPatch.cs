using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace InventorySlots;

[HarmonyPatch(typeof(Player), "AutoPickup", new[] { typeof(float) })]
internal static class AutoPickupCapacityPatch
{
    private static bool Prepare() => !InventorySlotsPlugin.IsDedicatedServer;

    // Reorder the native checks before AdminQoL replaces the carry-limit call.
    // Its later replacement still controls whether weight limits apply.
    [HarmonyBefore("sighsorry.AdminQoL")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (!TryMoveWeightCheck(code))
            InventorySlotsPlugin.Log.LogWarning(
                "Auto pickup capacity optimization could not locate one unchanged capacity/weight check pair; leaving pickup checks unchanged.");
        return code;
    }

    private static bool TryMoveWeightCheck(List<CodeInstruction> code)
    {
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        MethodInfo Method(Type type, string name, params Type[] parameters) => type.GetMethod(name, members, null, parameters, null)!;
        MethodInfo load = Method(typeof(ItemDrop), "Load");
        MethodInfo capacity = Method(typeof(Inventory), "CanAddItem", typeof(ItemDrop.ItemData), typeof(int));
        MethodInfo weight = Method(typeof(ItemDrop.ItemData), "GetWeight", typeof(int));
        MethodInfo totalWeight = Method(typeof(Inventory), "GetTotalWeight");
        MethodInfo maxWeight = Method(typeof(Player), "GetMaxCarryWeight");
        FieldInfo inventory = typeof(Humanoid).GetField("m_inventory", members)!;
        FieldInfo item = typeof(ItemDrop).GetField("m_itemData", members)!;
        const int capacityLength = 7;
        const int weightLength = 11;
        const int length = capacityLength + weightLength;

        int BranchTarget(Label label)
        {
            int target = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].labels.Contains(label)) continue;
                if (target >= 0) return -1;
                target = i;
            }
            return target;
        }

        bool Matches(int start)
        {
            CodeInstruction drop = code[start + 2];
            bool SameDrop(int index) => code[index].opcode == drop.opcode && Equals(code[index].operand, drop.operand);
            bool LoadsField(int index, FieldInfo field) => code[index].opcode == OpCodes.Ldfld && Equals(code[index].operand, field);

            if (!drop.IsLdloc() || !SameDrop(start - 2) || !code[start - 1].Calls(load) ||
                code[start].opcode != OpCodes.Ldarg_0 || !LoadsField(start + 1, inventory) ||
                !LoadsField(start + 3, item) || code[start + 4].opcode != OpCodes.Ldc_I4_M1 ||
                !code[start + 5].Calls(capacity) || code[start + 6].opcode != OpCodes.Brfalse ||
                !SameDrop(start + 7) || !LoadsField(start + 8, item) ||
                code[start + 9].opcode != OpCodes.Ldc_I4_M1 || !code[start + 10].Calls(weight) ||
                code[start + 11].opcode != OpCodes.Ldarg_0 || !LoadsField(start + 12, inventory) ||
                !code[start + 13].Calls(totalWeight) || code[start + 14].opcode != OpCodes.Add ||
                code[start + 15].opcode != OpCodes.Ldarg_0 || !code[start + 16].Calls(maxWeight) ||
                code[start + 17].opcode != OpCodes.Bgt ||
                code[start + 6].operand is not Label capacityReject || code[start + 17].operand is not Label weightReject)
                return false;

            // Harmony can assign separate labels to branches targeting the same
            // instruction. Compare their destinations, not their label identities.
            int rejectTarget = BranchTarget(capacityReject);
            if (rejectTarget < start + length || BranchTarget(weightReject) != rejectTarget)
                return false;

            // Do not move instructions across foreign branch entries or exception
            // boundaries. An entry at the start of the pair can safely move with it.
            for (int i = start; i < start + length; i++)
                if (code[i].blocks.Count != 0 || (i != start && code[i].labels.Count != 0))
                    return false;
            return true;
        }

        int match = -1;
        int matches = 0;
        if (load != null && capacity != null && weight != null && totalWeight != null && maxWeight != null &&
            inventory != null && item != null)
        {
            for (int i = 2; i <= code.Count - length; i++)
            {
                if (!Matches(i)) continue;
                match = i;
                matches++;
            }
        }
        if (matches != 1) return false;

        // Keep the native weight expression and comparison (including whole-stack
        // weight and equality), but reject overweight drops before our slot scan.
        // Ownership, ItemDrop.Load, exclusions and manual pickup remain untouched.
        List<CodeInstruction> capacityBlock = code.GetRange(match, capacityLength);
        List<CodeInstruction> weightBlock = code.GetRange(match + capacityLength, weightLength);
        weightBlock[0].labels.AddRange(capacityBlock[0].labels);
        capacityBlock[0].labels.Clear();
        code.RemoveRange(match, length);
        code.InsertRange(match, weightBlock);
        code.InsertRange(match + weightLength, capacityBlock);
        return true;
    }
}
