using ItemData = ItemDrop.ItemData;
#if INVENTORY_SLOTS
using Plugin = InventorySlots.InventorySlotsPlugin;
#else
using Plugin = InventoryActions.InventoryActionsPlugin;
#endif

int checks = 0;
void Check(string name, bool passed)
{
    if (!passed) throw new Exception(name);
    ++checks;
    Console.WriteLine("PASS " + name);
}
ItemData Item(int amount, int column, string? effect = "fire", string kind = "material", string prefab = "Dust")
{
    var item = new ItemData { m_stack = amount, m_gridPos = new(column, 1), m_dropPrefab = new(prefab), Kind = kind };
    item.m_shared.m_name = prefab;
    if (effect != null) item.m_customData["epic.effect"] = effect;
    return item;
}
Inventory Inventory(params ItemData[] items) { var inv = new Inventory(); inv.Items.AddRange(items); return inv; }

Plugin.Setup(false);
var a = Item(20, 0); var b = Item(35, 1);
Check("optional dependency absent", !Plugin.Eligible(a) && !Plugin.Pair(a, b));
Plugin.Setup();
Check("public API binding and matching metadata", Plugin.Eligible(a) && Plugin.Pair(a, b));
foreach (string kind in new[] { "rune", "shard", "material" }) Check(kind + " eligible", Plugin.Eligible(Item(1, 0, kind: kind)));
foreach (string token in new[] { "ForestToken", "IronBountyToken", "GoldBountyToken" })
    Check(token + " eligible", Plugin.Eligible(Item(1, 0, null, "token", token)));
Check("enchanted equipment excluded", !Plugin.Eligible(Item(1, 0, kind: "equipment")));
a.m_shared.m_questItem = true; Check("quest items excluded", !Plugin.Eligible(a)); a.m_shared.m_questItem = false;
a.m_shared.m_useDurability = true; Check("durability items excluded", !Plugin.Eligible(a)); a.m_shared.m_useDurability = false;
a.m_shared.m_maxStackSize = 1; Check("nonstackable items excluded", !Plugin.Eligible(a)); a.m_shared.m_maxStackSize = 50;
Check("same object cannot donate to itself", !Plugin.Pair(a, a));
Check("different effects stay separate", !Plugin.Pair(a, Item(20, 1, "frost")));
Check("blank and inscribed runestones stay separate", !Plugin.Pair(Item(1, 0, null, "rune", "Runestone"), Item(1, 1, "fire", "rune", "Runestone")));
foreach (Action<ItemData> mutate in new Action<ItemData>[] {
    item => item.m_quality++, item => item.m_worldLevel++, item => item.m_variant++, item => item.m_cheated = true,
    item => item.m_shared.m_name = "Other", item => item.m_dropPrefab = new("Other"), item => item.m_dropPrefab = null })
{
    b = Item(35, 1); mutate(b); Check("identity mismatch rejected " + checks, !Plugin.Pair(a, b));
}
b = Item(35, 1);
a.m_customData["other.expiry"] = "10";
Check("one-sided foreign data rejected", !Plugin.Pair(a, b));
b.m_customData["other.expiry"] = "20";
Check("different foreign data rejected", !Plugin.Pair(a, b));
b.m_customData["other.expiry"] = "10";
Check("identical foreign data preserved", Plugin.Pair(a, b));
a.m_customData["randyknapp.mods.epicloot#UnknownType"] = "A";
b.m_customData["randyknapp.mods.epicloot#UnknownType"] = "B";
Check("unresolved EpicLoot data is not blindly trusted", !Plugin.Pair(a, b));
a = Item(20, 0); b = Item(40, 1);
a.m_customData["epic.value"] = "1"; b.m_customData["epic.value"] = "9";
var inv = Inventory(a, b); var list = inv.Items.ToList();
Check("sort merges through native path", Plugin.Sort(inv, list) && inv.Moves == 1 && a.m_stack == 50 && b.m_stack == 10);
Check("native metadata result applied with donor retained", a.m_customData["epic.value"] == "9" && b.m_customData["epic.value"] == "9");
Check("repeat sort does not move items again", !Plugin.Sort(inv, list) && inv.Moves == 1);

a = Item(20, 0); b = Item(30, 1); inv = Inventory(a, b); list = inv.Items.ToList();
Check("depleted donor removed from inventory and sort list", Plugin.Sort(inv, list) && a.m_stack == 50 && inv.Items.Count == 1 && list.Count == 1);
a = Item(20, 0); b = Item(30, 1); inv = Inventory(a, b); inv.Veto = true;
Check("native veto never falls back to direct addition", !Plugin.Sort(inv, inv.Items.ToList()) && a.m_stack == 20 && b.m_stack == 30);
inv.Veto = false; inv.MoveCap = 7; inv.ReturnFalse = true;
Check("observed native progress wins over boolean result", Plugin.Sort(inv, inv.Items.ToList()) && a.m_stack == 27 && b.m_stack == 23);

// Targets deliberately supplied out of order. Neither may donate to the other.
var first = Item(20, 0); var second = Item(35, 1); var donor = Item(40, 2);
inv = Inventory(first, second, donor);
Check("favorites fill in grid order from normal donors", Plugin.Fill(inv, new() { second, first }, new() { donor }) && first.m_stack == 50 && second.m_stack == 45);
Check("favorite locations and identities preserved", first.m_gridPos == new Vector2i(0, 1) && second.m_gridPos == new Vector2i(1, 1) && inv.Items.SequenceEqual(new[] { first, second }));
Check("favorites never consolidate each other", !Plugin.Fill(inv, new() { first, second }, new()) && second.m_stack == 45);
first = Item(20, 0); donor = Item(40, 1, "frost"); inv = Inventory(first, donor);
Check("incompatible favorite effect untouched", !Plugin.Fill(inv, new() { first }, new() { donor }) && first.m_stack == 20 && donor.m_stack == 40 && inv.Moves == 0);
first = Item(20, 0); donor = Item(40, 1); inv = Inventory(first, donor); inv.Veto = true;
Check("favorite native veto has no direct fallback", !Plugin.Fill(inv, new() { first }, new() { donor }) && first.m_stack == 20);
first = Item(20, 0, null, "vanilla", "Wood"); donor = Item(40, 1, null, "vanilla", "Wood"); inv = Inventory(first, donor);
Check("ordinary favorite filling unchanged", Plugin.Fill(inv, new() { first }, new() { donor }) && first.m_stack == 50 && donor.m_stack == 10 && inv.Moves == 0);

#if INVENTORY_SLOTS
InventorySlots.StackMetadataPolicy.Register("freshness", (left, right) => Math.Min(int.Parse(left!), int.Parse(right!)).ToString(), (_, _) => true);
a = Item(20, 0); b = Item(35, 1); a.m_customData["freshness"] = "10"; b.m_customData["freshness"] = "7";
inv = Inventory(a, b);
Check("Slots registered metadata policy remains authoritative", Plugin.Sort(inv, inv.Items.ToList()) && a.m_customData["freshness"] == "7" && b.m_customData["freshness"] == "7");
a = Item(20, 0); b = Item(35, 1); a.m_customData["epic.value"] = "1"; b.m_customData["epic.value"] = "9";
Check("capacity probe does not write EpicLoot merge output", Plugin.Pair(a, b) && a.m_customData["epic.value"] == "1");
Check("overridden automatic lookup preserves EpicLoot merge output", Plugin.PrepareAuto(a, b) && a.m_customData["epic.value"] == "9" && a.m_stack == 20 && b.m_stack == 35);
#endif

int warnings = Plugin.Log.Warnings;
EpicLoot.Data.ItemInfo.Fail = true;
Check("API exception refuses merge", !Plugin.Pair(Item(20, 0), Item(30, 1)));
Check("failed endpoint stays disabled without warning spam", !Plugin.Pair(Item(20, 0), Item(30, 1)) && Plugin.Log.Warnings == warnings + 1);
first = Item(20, 0, null, "rune", "Runestone"); donor = Item(30, 1, null, "rune", "Runestone"); inv = Inventory(first, donor);
Check("failed pair API never falls back to direct favorite filling", !Plugin.Fill(inv, new() { first }, new() { donor }) && first.m_stack == 20 && donor.m_stack == 30);
Plugin.Setup();
Check("new plugin lifetime rebinds API", Plugin.Pair(Item(20, 0), Item(30, 1)));
EpicLoot.API.FailKind = "rune";
first = Item(20, 0, null, "rune", "Runestone"); donor = Item(30, 1, null, "rune", "Runestone"); inv = Inventory(first, donor);
Check("classification error quarantines item from direct fill", !Plugin.Fill(inv, new() { first }, new() { donor }) && first.m_stack == 20 && donor.m_stack == 30);
first = Item(20, 0, null, "vanilla", "Wood"); donor = Item(30, 1, null, "vanilla", "Wood"); inv = Inventory(first, donor);
Check("classification error does not block ordinary materials", Plugin.Fill(inv, new() { first }, new() { donor }) && first.m_stack == 50);

if (args.Length > 0)
{
    using var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[0]);
    var module = assembly.MainModule;
    var info = module.GetType("EpicLoot.Data.ItemInfo");
    var ext = module.GetType("EpicLoot.Data.ItemExtensions");
    var api = module.GetType("EpicLoot.API");
    Check("original DLL exposes public ItemInfo.Data contract", ext.Methods.Any(m => m.Name == "Data" && m.IsPublic && m.IsStatic && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "ItemDrop/ItemData" && m.ReturnType.FullName == info.FullName));
    Check("original DLL exposes public pair metadata contract", info.Methods.Any(m => m.Name == "IsStackableWithOtherInfo" && m.IsPublic && !m.IsStatic && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == info.FullName && m.ReturnType.FullName == "System.Collections.Generic.Dictionary`2<System.String,System.String>"));
    foreach (string name in new[] { "IsShardStone", "IsRunestone", "IsMagicItem", "IsMagicCraftingMaterial" })
        Check("original DLL classifier " + name, api.Methods.Any(m => m.Name == name && m.IsPublic && m.IsStatic && m.ReturnType.FullName == "System.Boolean" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "ItemDrop/ItemData"));
}
Console.WriteLine($"PASS {checks} {typeof(Plugin).Namespace} stacking checks; simulated native host, no game execution.");
