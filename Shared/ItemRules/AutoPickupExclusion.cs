using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

#if INVENTORY_SLOTS
using RulePlugin = InventorySlots.InventorySlotsPlugin;
namespace InventorySlots;
#else
using RulePlugin = InventoryActions.InventoryActionsPlugin;
namespace InventoryActions;
#endif

#if INVENTORY_SLOTS
public sealed partial class InventorySlotsPlugin
#else
public sealed partial class InventoryActionsPlugin
#endif
{
    private static HashSet<string> _autoPickupExcludedItems = new(StringComparer.OrdinalIgnoreCase);

    private static void RefreshAutoPickupExclusions(object? sender, EventArgs args) =>
        _autoPickupExcludedItems = ItemRuleConfigCore.ParseExclusions(_autoPickupExcludedItemsConfig.Value);

    internal static bool FilterAutoPickup(ItemDrop drop, bool original, Player player)
    {
        if (!original || _autoPickupExcludedItems.Count == 0 || player != Player.m_localPlayer ||
            player == null || drop == null || drop.m_itemData == null)
            return original;

        // Only this player's automatic pickup decision changes. Never change the
        // shared world's m_autoPickup flag or the manual Interact/Pickup path.
        string name = drop.m_itemData.m_dropPrefab != null ? drop.m_itemData.m_dropPrefab.name : drop.gameObject.name;
        return !_autoPickupExcludedItems.Contains(ItemRuleConfigCore.PrefabKey(name));
    }
}

[HarmonyPatch(typeof(Player), "AutoPickup", new[] { typeof(float) })]
internal static class AutoPickupExclusionPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        FieldInfo field = typeof(ItemDrop).GetField(nameof(ItemDrop.m_autoPickup))!;
        int matches = code.Count(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field));
        if (matches != 1)
        {
            RulePlugin.Log.LogWarning($"Auto pickup exclusions disabled: expected one m_autoPickup check, found {matches}.");
            return code;
        }

        int index = code.FindIndex(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field));
        if (code[index].blocks.Count != 0)
        {
            RulePlugin.Log.LogWarning("Auto pickup exclusions disabled: another patch added an exception boundary at the pickup check.");
            return code;
        }
        CodeInstruction duplicate = new(OpCodes.Dup);
        duplicate.labels.AddRange(code[index].labels);
        code[index].labels.Clear();
        code.Insert(index, duplicate);
        code.InsertRange(index + 2, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, typeof(RulePlugin).GetMethod(nameof(RulePlugin.FilterAutoPickup), BindingFlags.Static | BindingFlags.NonPublic))
        });
        return code;
    }
}
