using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InventorySlots;
using ItemData = ItemDrop.ItemData;
using Plugin = InventorySlots.InventorySlotsPlugin;

int checks = 0;
void Check(bool value, string message)
{
    checks++;
    if (!value) throw new InvalidOperationException(message);
}

MethodInfo transpiler = typeof(InventoryGuiRefinementOutcomePatch).GetMethod("Transpiler", BindingFlags.NonPublic | BindingFlags.Static)!;
MethodInfo add = typeof(Inventory).GetMethod(nameof(Inventory.AddItem))!;
List<CodeInstruction> Template() => new()
{
    new(OpCodes.Ldstr, "$msg_upgrader_success"), new(OpCodes.Callvirt, add), new(OpCodes.Br, default(Label)),
    new(OpCodes.Ldstr, "$msg_upgrader_broke"), new(OpCodes.Br, default(Label)),
    new(OpCodes.Ldstr, "$msg_upgrader_failed"), new(OpCodes.Callvirt, add), new(OpCodes.Ret)
};
List<CodeInstruction> Patch(List<CodeInstruction> source, ILGenerator? generator = null)
{
    generator ??= new DynamicMethod("TestBody", typeof(void), Type.EmptyTypes).GetILGenerator();
    return ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { source, generator })!).ToList();
}

// Execute the actual emitted gate against the source-linked observer. This
// checks CLR stack/branch validity and the early-return cost boundary as well
// as calling the observer directly in the scenarios below.
Action<InventoryGui, ItemData?, Action> BuildCostGate()
{
    var method = new DynamicMethod("CostGate", typeof(void), new[] { typeof(InventoryGui), typeof(ItemData), typeof(Action) }, typeof(Plugin), true);
    var generator = method.GetILGenerator();
    var body = Patch(Template(), generator);
    int callback = body.FindIndex(i => i.operand is MethodInfo m && m.Name == "ObserveRefinementReplacement");
    generator.Emit(OpCodes.Ldarg_1);
    for (int i = callback - 2; i <= callback + 3; i++)
    {
        var instruction = body[i];
        if (instruction.operand is MethodInfo called) generator.Emit(instruction.opcode, called);
        else if (instruction.operand is Label label) generator.Emit(instruction.opcode, label);
        else generator.Emit(instruction.opcode);
    }
    generator.MarkLabel((Label)body[callback + 1].operand);
    generator.Emit(OpCodes.Pop);
    generator.Emit(OpCodes.Ldarg_2);
    generator.Emit(OpCodes.Callvirt, typeof(Action).GetMethod("Invoke")!);
    generator.Emit(OpCodes.Ret);
    return (Action<InventoryGui, ItemData?, Action>)method.CreateDelegate(typeof(Action<InventoryGui, ItemData?, Action>));
}

var costGate = BuildCostGate();

var source = Template();
var labelGenerator = new DynamicMethod("Labels", typeof(void), Type.EmptyTypes).GetILGenerator();
Label target = labelGenerator.DefineLabel();
source[0].labels.Add(target);
var patched = Patch(source);
Check(InventoryGuiRefinementOutcomePatch.Ready, "All three native outcomes recognized");
Check(patched.Count(i => i.operand is MethodInfo m && m.Name == "ObserveRefinementOutcome") == 3, "Every outcome observed once");
Check(patched.Count(i => i.operand is MethodInfo m && m.Name == "ObserveRefinementReplacement") == 2, "Only surviving results observed");
Check(patched[0].opcode == OpCodes.Ldarg_0 && patched[0].labels.Contains(target), "Branch labels precede observer");
foreach (Action<List<CodeInstruction>> corrupt in new Action<List<CodeInstruction>>[]
{
    code => code.RemoveAt(3),
    code => code.Add(new(OpCodes.Ldstr, "$msg_upgrader_failed")),
    code => code.RemoveAt(6),
    code => code[1] = new(OpCodes.Br, default(Label)),
    code => code[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock))
})
{
    source = Template();
    corrupt(source);
    patched = Patch(source);
    Check(!InventoryGuiRefinementOutcomePatch.Ready && patched.Count == source.Count, "Unknown branch layout stays untouched");
}
Patch(Template());

(Player player, InventoryGui gui, ItemData original) Fresh(int quality = 4, SlotKind kind = SlotKind.Quick, bool refinement = true)
{
    Plugin.ClearPendingUpgradeFavorite();
    Plugin.TestSlot = new() { Kind = kind, Id = kind.ToString() };
    Plugin.SlotAllowed = true;
    Plugin.SocketActive = false;
    Player player = new();
    Player.m_localPlayer = player;
    player.Station.m_upgrader = refinement;
    ItemData item = new() { m_quality = quality, m_equipped = kind != SlotKind.Quick };
    item.m_customData[Plugin.UpgradeFavoriteItemIdKey] = "original-id";
    if (kind == SlotKind.BuiltIn) player.m_chestItem = item;
    if (kind == SlotKind.CustomEquipment)
    {
        item.m_customData[Plugin.SlotIdKey] = Plugin.TestSlot.Id;
        item.m_customData[Plugin.EquippedByKey] = "test";
    }
    player.Inventory.m_inventory.Add(item);
    InventoryGui gui = new() { m_craftUpgradeItem = item };
    InventoryGui.instance = gui;
    return (player, gui, item);
}

EquipmentSlotUpgradeTransaction Begin(Player player, InventoryGui gui)
{
    Plugin.CaptureUpgradeFavoriteBeforeCrafting(gui);
    var transaction = Plugin.BeginEquipmentSlotUpgradeTransaction(gui, player, out bool abort);
    Check(!abort && transaction != null, "Validated special-slot upgrade starts");
    return transaction!;
}
void Remove(Player player, ItemData item)
{
    player.Inventory.m_inventory.Remove(item);
    item.m_equipped = false;
    player.m_chestItem = null;
}

foreach (SlotKind kind in Enum.GetValues<SlotKind>())
foreach (RefinementOutcome outcome in new[] { RefinementOutcome.Success, RefinementOutcome.Downgrade })
foreach (int quality in new[] { 1, 4 })
{
    var (player, gui, original) = Fresh(quality, kind);
    // Fill every ordinary cell: replacement may claim only its captured slot.
    for (int y = 0; y < 9; y++)
    for (int x = 0; x < 8; x++)
        player.Inventory.m_inventory.Add(new() { m_gridPos = new(x, y) });
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.ObserveRefinementOutcome(gui, outcome);
    int expected = quality + (outcome == RefinementOutcome.Success ? 1 : -1);
    var scope = Plugin.BeginEquipmentSlotUpgradeReplacementAdd(player.Inventory, "Shield", 1, expected, 0, original.m_gridPos);
    Check(scope != null, "Exact native downgrade supports quality zero too");
    Check(Plugin.TryUseEquipmentSlotUpgradeReplacementCell(player.Inventory, out var pos) && pos == original.m_gridPos,
        "Full inventory uses only emptied captured slot");
    ItemData? result = new() { m_quality = expected };
    if (kind == SlotKind.CustomEquipment)
    {
        result.m_customData[Plugin.SlotIdKey] = Plugin.TestSlot.Id;
        result.m_customData[Plugin.EquippedByKey] = "test";
    }
    Check(Plugin.TryValidateEquipmentSlotUpgradeReplacementInsert(player.Inventory, player, result, pos, out bool allow) && allow,
        "Actual replacement passes live slot validation");
    player.Inventory.m_inventory.Add(result);
    Plugin.FinalizeEquipmentSlotUpgradeReplacementAdd(scope, ref result, null);
    Check(Plugin.ObserveRefinementReplacement(result, gui), "Committed replacement proceeds to native costs");
    int nativeCosts = 0;
    costGate(gui, result, () => nativeCosts++);
    Check(nativeCosts == 1, "Emitted IL gate preserves the successful cost path");
    Plugin.RestoreUpgradeFavoriteAfterCrafting(gui, player);
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction, new Exception("post-cost effect failure"));
    Check(transaction.Committed && transaction.Closed && !player.Inventory.ContainsItem(original), "Committed result never resurrects original");
    Check(result != null && result.m_quality == expected && result.m_customData[Plugin.UpgradeFavoriteItemIdKey] == "original-id",
        "Favorite follows actual successful/downgraded replacement");
    Check(player.Inventory.m_inventory.Count == 73 && Plugin.Active == null, "Exactly one replacement and scope cleaned");
}

foreach (bool refund in new[] { false, true })
foreach (bool throwAfter in new[] { false, true })
{
    var (player, gui, original) = Fresh();
    ItemData lookalike = new() { m_quality = 5, m_gridPos = new(1, 0) };
    player.Inventory.m_inventory.Add(lookalike);
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.ObserveRefinementOutcome(gui, RefinementOutcome.Destroyed);
    if (refund) player.Inventory.m_inventory.Add(new() { m_gridPos = new(2, 0), m_stack = 10 });
    Plugin.RestoreUpgradeFavoriteAfterCrafting(gui, player);
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction, throwAfter ? new Exception("refund/effect failure") : null);
    Check(transaction.Committed && !transaction.RolledBack && !player.Inventory.ContainsItem(original), "Native destruction stays terminal with refunds/exceptions");
    Check(!lookalike.m_customData.ContainsKey(Plugin.UpgradeFavoriteItemIdKey), "Destruction never tags a pre-existing lookalike");
    Check(player.Inventory.m_inventory.Count == (refund ? 2 : 1) && Plugin.Active == null, "Refunds retained without resurrecting equipment");
}

foreach (bool interrupted in new[] { false, true })
{
    var (player, gui, original) = Fresh();
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction, interrupted ? new Exception("before decision") : null);
    Check(transaction.RolledBack && player.Inventory.ContainsItem(original), "Unverified removal/missing resource still restores original");
    Check(original.m_quality == 4 && original.m_gridPos == new Vector2i(0, 9), "Recovery preserves original quality and slot");
}

foreach (bool bypassAddScope in new[] { false, true })
{
    var (player, gui, original) = Fresh();
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.ObserveRefinementOutcome(gui, RefinementOutcome.Success);
    ItemData? failed = null;
    if (!bypassAddScope)
    {
        var scope = Plugin.BeginEquipmentSlotUpgradeReplacementAdd(player.Inventory, "Shield", 1, 5, 0, original.m_gridPos);
        Plugin.FinalizeEquipmentSlotUpgradeReplacementAdd(scope, ref failed, null);
    }
    int costsConsumed = 0;
    costGate(gui, failed, () => costsConsumed++);
    Plugin.RestoreUpgradeFavoriteAfterCrafting(gui, player);
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction);
    Check(costsConsumed == 0 && transaction.RolledBack && player.Inventory.ContainsItem(original), "Failed add stops native costs after rollback");
}

foreach (bool bypassAddScope in new[] { false, true })
foreach (bool preExisting in new[] { false, true })
foreach (string mismatch in new[] { "quality", "variant", "prefab" })
{
    var (player, gui, original) = Fresh();
    ItemData? unexpected = new() { m_quality = 5 };
    if (mismatch == "quality") unexpected.m_quality = 6;
    if (mismatch == "variant") unexpected.m_variant = 1;
    if (mismatch == "prefab") unexpected.m_dropPrefab.name = "OtherShield";
    if (preExisting)
    {
        unexpected.m_gridPos = new(1, 0);
        player.Inventory.m_inventory.Add(unexpected);
    }
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.ObserveRefinementOutcome(gui, RefinementOutcome.Success);
    if (!preExisting) player.Inventory.m_inventory.Add(unexpected);
    if (!bypassAddScope)
    {
        var scope = Plugin.BeginEquipmentSlotUpgradeReplacementAdd(player.Inventory, "Shield", 1, 5, 0, original.m_gridPos);
        Plugin.FinalizeEquipmentSlotUpgradeReplacementAdd(scope, ref unexpected, null);
    }
    Check(!Plugin.ObserveRefinementReplacement(unexpected, gui), "Invalid causal result cannot charge resources");
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction);
    Check(transaction.RolledBack && player.Inventory.ContainsItem(original), "Invalid result restores original");
    Check(player.Inventory.m_inventory.Count == (preExisting ? 2 : 1), "Rollback removes only newly owned causal result, preserving pre-existing items");
}

{
    var (player, gui, original) = Fresh();
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.ObserveRefinementOutcome(gui, RefinementOutcome.Success);
    transaction.ReplacementResult = new() { m_quality = 5 }; // external callback removed this provisional result
    ItemData substituted = new() { m_quality = 6 };
    player.Inventory.m_inventory.Add(substituted);
    Check(!Plugin.ObserveRefinementReplacement(substituted, gui), "Substituted invalid result cannot consume costs");
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction);
    Check(transaction.RolledBack && player.Inventory.ContainsItem(original), "Stale provisional result does not prevent recovery");
    Check(player.Inventory.m_inventory.Count == 1, "Returned invalid result is removed despite stale tracking reference");
}

{
    var (player, gui, original) = Fresh();
    var transaction = Begin(player, gui);
    Remove(player, original);
    Plugin.ObserveRefinementOutcome(gui, RefinementOutcome.Downgrade);
    ItemData externalResult = new() { m_quality = 3 };
    player.Inventory.m_inventory.Add(externalResult);
    Check(Plugin.ObserveRefinementReplacement(externalResult, gui), "Valid result from earlier Add prefix validated before costs");
    Plugin.RestoreUpgradeFavoriteAfterCrafting(gui, player);
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction);
    Check(transaction.Committed && externalResult.m_customData[Plugin.UpgradeFavoriteItemIdKey] == "original-id", "Unobserved add retains exact favorite result");
}

{
    var (player, gui, original) = Fresh(refinement: false);
    Check(Plugin.BeginEquipmentSlotUpgradeTransaction(gui, player, out bool abort) == null && abort, "Ordinary station cannot bypass safe cap");
    (player, gui, original) = Fresh(quality: 2, refinement: false);
    var transaction = Begin(player, gui);
    Remove(player, original);
    Check(Plugin.BeginEquipmentSlotUpgradeReplacementAdd(player.Inventory, "Shield", 1, 0, 0, original.m_gridPos) == null,
        "Quality-zero exception does not leak into ordinary upgrades");
    Plugin.CompleteEquipmentSlotUpgradeTransaction(transaction);
}

{
    Patch(new());
    var (player, gui, original) = Fresh();
    Check(Plugin.BeginEquipmentSlotUpgradeTransaction(gui, player, out bool abort) == null && abort && player.Inventory.ContainsItem(original),
        "Missing native observer blocks special-slot removal");
    original.m_gridPos = new(0, 0);
    Check(Plugin.BeginEquipmentSlotUpgradeTransaction(gui, player, out abort) == null && !abort, "Ordinary cells retain vanilla ownership");
    original.m_gridPos = new(0, 9);
    Plugin.SocketActive = true;
    Check(Plugin.BeginEquipmentSlotUpgradeTransaction(gui, player, out abort) == null && !abort, "Jewelcrafting retains its own mutation contract");
    Patch(Template());
}

Console.WriteLine($"{checks} refinement checks passed using source-linked production transactions and in-memory game boundaries. No game session executed.");
