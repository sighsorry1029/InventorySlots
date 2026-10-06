using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal enum RefinementOutcome { Success, Downgrade, Destroyed }

// Observe vanilla's decisions, not its random roll. A missing item alone cannot
// distinguish deliberate destruction from an interrupted remove-first upgrade.
[HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
internal static class InventoryGuiRefinementOutcomePatch
{
    internal static bool Ready { get; private set; }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        List<CodeInstruction> code = instructions.ToList();
        Ready = false;
        int FindUnique(string token)
        {
            int found = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldstr || !Equals(code[i].operand, token)) continue;
                if (found >= 0) return -1;
                found = i;
            }
            return found;
        }

        int success = FindUnique("$msg_upgrader_success");
        int destroyed = FindUnique("$msg_upgrader_broke");
        int downgrade = FindUnique("$msg_upgrader_failed");
        MethodInfo? add = typeof(Inventory).GetMethod(nameof(Inventory.AddItem), new[]
        {
            typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string),
            typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool)
        });
        int FindBranchAdd(int start)
        {
            if (start < 0 || add == null) return -1;
            for (int i = start; i < code.Count; i++)
            {
                if (code[i].Calls(add)) return i;
                FlowControl flow = code[i].opcode.FlowControl;
                if (flow == FlowControl.Branch || flow == FlowControl.Cond_Branch ||
                    flow == FlowControl.Return || flow == FlowControl.Throw) return -1;
            }
            return -1;
        }
        int successAdd = FindBranchAdd(success);
        int downgradeAdd = FindBranchAdd(downgrade);
        if (success < 0 || destroyed <= success || downgrade <= destroyed ||
            successAdd <= success || successAdd >= destroyed || downgradeAdd <= downgrade ||
            downgradeAdd + 1 >= code.Count || code.Any(instruction => instruction.blocks.Count != 0))
        {
            // Leave foreign/changed IL untouched. Special-slot refinement will be
            // refused before removal until all three native outcomes can be observed.
            return code;
        }

        MethodInfo observeOutcome = typeof(InventorySlotsPlugin).GetMethod(nameof(InventorySlotsPlugin.ObserveRefinementOutcome), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo observeResult = typeof(InventorySlotsPlugin).GetMethod(nameof(InventorySlotsPlugin.ObserveRefinementReplacement), BindingFlags.Static | BindingFlags.NonPublic)!;
        List<CodeInstruction> patched = new(code.Count + 21);
        for (int i = 0; i < code.Count; i++)
        {
            if (i == success || i == destroyed || i == downgrade)
            {
                CodeInstruction loadGui = new(OpCodes.Ldarg_0);
                loadGui.labels.AddRange(code[i].labels);
                code[i].labels.Clear();
                loadGui.blocks.AddRange(code[i].blocks);
                code[i].blocks.Clear();
                patched.Add(loadGui);
                patched.Add(new CodeInstruction(OpCodes.Ldc_I4, (int)(i == success ? RefinementOutcome.Success :
                    i == destroyed ? RefinementOutcome.Destroyed : RefinementOutcome.Downgrade)));
                patched.Add(new CodeInstruction(OpCodes.Call, observeOutcome));
            }
            patched.Add(code[i]);
            if (i == successAdd || i == downgradeAdd)
            {
                // Keep vanilla's returned reference on the stack and observe that
                // exact result, rather than guessing among identical owned items.
                patched.Add(new CodeInstruction(OpCodes.Dup));
                patched.Add(new CodeInstruction(OpCodes.Ldarg_0));
                patched.Add(new CodeInstruction(OpCodes.Call, observeResult));
                Label continueCrafting = generator.DefineLabel();
                code[i + 1].labels.Add(continueCrafting);
                patched.Add(new CodeInstruction(OpCodes.Brtrue, continueCrafting));
                patched.Add(new CodeInstruction(OpCodes.Pop));
                patched.Add(new CodeInstruction(OpCodes.Ret));
            }
        }
        Ready = true;
        return patched;
    }
}

public sealed partial class InventorySlotsPlugin
{
    private static bool IsRefinementStationActive()
    {
        CraftingStation? station = Player.m_localPlayer != null ? Player.m_localPlayer.GetCurrentCraftingStation() : null;
        return station != null && station.m_upgrader;
    }

    internal static void ObserveRefinementOutcome(InventoryGui gui, RefinementOutcome outcome)
    {
        ItemData? original = gui.m_craftUpgradeItem;
        EquipmentSlotUpgradeTransaction? transaction = _activeEquipmentSlotUpgradeTransaction;
        if (transaction != null && transaction.IsRefinement && !transaction.Closed &&
            !transaction.Committed && !transaction.RolledBack &&
            !transaction.ReplacementAddAttempted && transaction.RefinementOutcome == null &&
            ReferenceEquals(original, transaction.OriginalItem) && !transaction.Inventory.ContainsItem(original))
        {
            transaction.RefinementOutcome = outcome;
            if (outcome == RefinementOutcome.Destroyed)
            {
                // This is terminal before refunds or other mod callbacks can run.
                // A later exception must not resurrect the item alongside its refunds.
                transaction.Committed = true;
            }
            else
            {
                transaction.ExpectedQuality = transaction.OriginalSnapshot.m_quality +
                    (outcome == RefinementOutcome.Success ? 1 : -1);
                transaction.ExpectedSlotItem.m_quality = transaction.ExpectedQuality;
            }
        }

        if (_pendingUpgradeFavoriteIsRefinement && ReferenceEquals(original, _pendingUpgradeFavoriteOriginal))
        {
            if (outcome == RefinementOutcome.Destroyed)
                ClearPendingUpgradeFavorite();
            else if (original != null)
                _pendingUpgradeFavoriteQuality = original.m_quality + (outcome == RefinementOutcome.Success ? 1 : -1);
        }
    }

    internal static bool ObserveRefinementReplacement(ItemData? result, InventoryGui gui)
    {
        EquipmentSlotUpgradeTransaction? transaction = _activeEquipmentSlotUpgradeTransaction;
        if (transaction != null && transaction.IsRefinement && !transaction.Closed &&
            ReferenceEquals(gui.m_craftUpgradeItem, transaction.OriginalItem))
        {
            if (!transaction.Committed && !transaction.RolledBack)
            {
                // A higher-priority AddItem prefix may have bypassed our add scope.
                // Validate its actual result here while costs are still unconsumed.
                transaction.ReplacementAddAttempted = true;
                transaction.ReplacementFinalized = true;
                FinalizeEquipmentSlotUpgradeResult(transaction, ref result, exception: null);
            }
            if (!transaction.Committed)
            {
                NotifyUnsafeEquipmentSlotUpgradeCanceled(transaction.Player, "the refinement replacement could not be preserved");
                // Native refinement consumes resources even when AddItem returns
                // null. Return from DoCrafting before that cost boundary on rollback.
                return false;
            }
        }
        if (_pendingUpgradeFavoriteIsRefinement &&
            ReferenceEquals(gui.m_craftUpgradeItem, _pendingUpgradeFavoriteOriginal))
            _pendingUpgradeFavoriteRefinementResult = result;
        return true;
    }
}
