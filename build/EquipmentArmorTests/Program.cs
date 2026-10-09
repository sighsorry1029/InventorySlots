using InventorySlots;
using ItemData = ItemDrop.ItemData;
using Plugin = InventorySlots.InventorySlotsPlugin;

int checks = 0;
void Equal(float expected, float actual, string message)
{
    checks++;
    if (Math.Abs(expected - actual) > 0.0001f)
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
}

Player Fresh()
{
    Plugin.Reset();
    return new Player();
}

ItemData Custom(Player player, string id, bool applyArmor = false, SlotKind kind = SlotKind.CustomEquipment)
{
    Plugin.SlotDefinitions.Add(new SlotDefinition(id, kind, applyArmor));
    ItemData item = new();
    Plugin.MarkCustom(player, item, id);
    player.Inventory.m_inventory.Add(item);
    return item;
}

Player player = Fresh();
ItemData wishbone = Custom(player, "wishbone");
ItemData demister = Custom(player, "demister");
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "accessories without opt-in do not expose hidden armor");
Equal(0f, wishbone.ArmorCalls + demister.ArmorCalls, "excluded armor does not call item armor providers");
Equal(4f, Plugin.Weight(player), "armor-off preserves custom weight projection");
Equal(0.3f, Plugin.Eitr(player), "armor-off preserves custom eitr projection");
Equal(2f, Plugin.SetCount(player), "armor-off preserves set participation");
Equal(0.4f, Plugin.Modifier(player), "armor-off preserves equipment modifier projection");
Equal(2f, Plugin.EquippedCount(player), "armor-off keeps items available to status-effect projection");

player = Fresh();
ItemData circlet = Custom(player, "circlet", true);
circlet.m_shared.m_armor = 4f;
circlet.m_shared.m_armorPerLevel = 2f;
circlet.m_quality = 3;
Equal(8f, Plugin.GetProjectedEquipmentArmor(player), "opt-in uses quality-aware GetArmor result");
Equal(8f, Plugin.GetProjectedEquipmentArmor(player), "repeated query returns cache");
Equal(1f, circlet.ArmorCalls, "repeated custom armor query does not recalculate item armor");
Plugin.SlotDefinitions[0].ApplyArmor = false;
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "live disable applies with same player and inventory");
Equal(2f, Plugin.Weight(player), "live disable preserves non-armor effects");
Plugin.SlotDefinitions[0].ApplyArmor = true;
Plugin.Invalidate();
Equal(8f, Plugin.GetProjectedEquipmentArmor(player), "live enable restores armor without re-equipping");
Plugin.SlotDefinitions[0] = new SlotDefinition("circlet", SlotKind.CustomEquipment, false);
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "definition replacement does not retain the previous flag");

player = Fresh();
ItemData missing = Custom(player, "deleted-slot", true);
Plugin.SlotDefinitions.Clear();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "unknown marker cannot grant armor");
Plugin.SlotDefinitions.Add(new SlotDefinition("deleted-slot", SlotKind.Quick, true));
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "quick slot marker cannot grant armor");
Plugin.SlotDefinitions[0] = new SlotDefinition("deleted-slot", SlotKind.BuiltIn, true);
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "stale native marker is not custom armor");

player = Fresh();
ItemData foreign = Custom(player, "custom", true);
foreign.m_customData["owner"] = "different-player";
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "another player's marker cannot grant armor");
Equal(0f, Plugin.EquippedCount(player), "foreign marker cannot grant custom effects");
Plugin.MarkCustom(player, foreign, "custom");
foreign.m_equipped = false;
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "unequipped marked item cannot grant armor");
foreign.m_equipped = true;
foreign.m_shared.m_armor = 0f;
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "zero base armor remains excluded");
foreign.m_shared.m_armor = -1f;
Plugin.Invalidate();
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "negative base armor remains excluded");

player = Fresh();
Plugin.SlotDefinitions.Add(new SlotDefinition("utility", SlotKind.BuiltIn));
Plugin.SlotDefinitions.Add(new SlotDefinition("trinket", SlotKind.BuiltIn));
ItemData utility = new();
ItemData trinket = new();
player.m_utilityItem = utility;
player.m_trinketItem = trinket;
player.Inventory.m_inventory.AddRange([utility, trinket]);
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "native accessories also default to no extra armor");
Plugin.SlotDefinitions[0].ApplyArmor = true;
Equal(10f, Plugin.GetProjectedEquipmentArmor(player), "utility opt-in adds actual equipped item armor");
Plugin.SlotDefinitions[1].ApplyArmor = true;
Equal(20f, Plugin.GetProjectedEquipmentArmor(player), "utility and trinket independently opt in");
utility.m_equipped = false;
Equal(10f, Plugin.GetProjectedEquipmentArmor(player), "stale unequipped native reference is excluded");
utility.m_equipped = true;
player.Inventory.m_inventory.Remove(utility);
Equal(10f, Plugin.GetProjectedEquipmentArmor(player), "native item outside player inventory is excluded");
player.Inventory.m_inventory.Remove(trinket);
player.Inventory.m_inventory.Add(new ItemData());
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "similar replacement is not the equipped item instance");

player = Fresh();
ItemData alias = Custom(player, "custom", true);
Plugin.SlotDefinitions.Add(new SlotDefinition("utility", SlotKind.BuiltIn, true));
player.m_utilityItem = alias;
Equal(10f, Plugin.GetProjectedEquipmentArmor(player), "custom item temporarily aliased as utility is not counted twice");

player = Fresh();
ItemData tome = Custom(player, "utility", false, SlotKind.BuiltIn);
tome.m_customData["tome"] = "true";
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "custom-owned Utility Tome respects armor opt-out");
Plugin.SlotDefinitions[0].ApplyArmor = true;
Plugin.Invalidate();
Equal(10f, Plugin.GetProjectedEquipmentArmor(player), "Utility Tome armor opt-in follows its selected slot");
player.m_utilityItem = tome;
Equal(10f, Plugin.GetProjectedEquipmentArmor(player), "Utility Tome with a stale native alias is counted once");

player = Fresh();
foreach (string id in new[] { "helmet", "chest", "legs", "cape" })
    Plugin.SlotDefinitions.Add(new SlotDefinition(id, SlotKind.BuiltIn, true));
player.m_helmetItem = new();
player.m_chestItem = new();
player.m_legItem = new();
player.m_shoulderItem = new();
player.Inventory.m_inventory.AddRange([player.m_helmetItem, player.m_chestItem, player.m_legItem, player.m_shoulderItem]);
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "native four armor slots are not added a second time");
Equal(0f, Plugin.GetProjectedEquipmentArmor(null!), "absent player does not grant armor");

// A menu character can have equipped items while a patched armor provider
// cannot run yet. Finding those items must not invoke the provider at all.
player = Fresh();
ItemData previewA = Custom(player, "preview-a", true);
ItemData previewB = Custom(player, "preview-b", true);
var unavailable = new NullReferenceException("simulated provider without live world");
previewA.ArmorError = previewB.ArmorError = unavailable;
Equal(2, Plugin.EquippedCount(player), "preview equipment remains available without a live armor provider");
Equal(4, Plugin.Weight(player), "preview weight does not request armor");
Equal(0.3f, Plugin.Eitr(player), "preview eitr does not request armor");
Equal(2, Plugin.SetCount(player), "preview set lookup does not request armor");
Equal(0.4f, Plugin.Modifier(player), "preview modifiers do not request armor");
Equal(0, previewA.ArmorCalls + previewB.ArmorCalls, "non-armor getters never call GetArmor");

void ThrowsSame(Exception expected, Action action, string message)
{
    checks++;
    try { action(); }
    catch (Exception actual) when (ReferenceEquals(actual, expected)) { return; }
    throw new InvalidOperationException(message);
}

// Fail after one item has contributed, then retry without a new inventory event.
previewA.ArmorError = null;
ThrowsSame(unavailable, () => Plugin.GetProjectedEquipmentArmor(player), "actual armor failures remain observable");
Equal(2, Plugin.EquippedCount(player), "armor failure cannot truncate the equipment list");
Equal(4, Plugin.Weight(player), "armor failure cannot truncate other projections");
previewB.ArmorError = null;
Equal(20, Plugin.GetProjectedEquipmentArmor(player), "armor retries the entire sum after a provider failure");
Equal(2, previewA.ArmorCalls, "the successful first item is recalculated after failed aggregate");
Equal(2, previewB.ArmorCalls, "the failed item is retried");
Equal(20, Plugin.GetProjectedEquipmentArmor(player), "completed armor result is cached");
Equal(4, previewA.ArmorCalls + previewB.ArmorCalls, "repeated completed query avoids providers");

Plugin.Invalidate();
previewA.BeforeArmor = () =>
{
    Equal(2, Plugin.EquippedCount(player), "provider reentry sees the complete equipment list");
    Equal(4, Plugin.Weight(player), "provider reentry reads weight without recursively calculating armor");
    Equal(2, Plugin.SetCount(player), "provider reentry reads a complete set count");
};
Equal(20, Plugin.GetProjectedEquipmentArmor(player), "ordinary getter reentry preserves the armor sum");

// Rebuild the shared list inside GetArmor. The active sum must enumerate its
// snapshot and must not mark that invalidated result as reusable.
Plugin.Invalidate();
previewA.BeforeArmor = () =>
{
    previewA.BeforeArmor = null;
    Plugin.Invalidate();
    Equal(2, Plugin.EquippedCount(player), "provider can rebuild equipment during armor calculation");
};
Equal(20, Plugin.GetProjectedEquipmentArmor(player), "rebuild during calculation does not invalidate the iterator");
previewA.m_shared.m_armor = 15;
Equal(25, Plugin.GetProjectedEquipmentArmor(player), "invalidated in-flight sum is recalculated on next query");

// Query a different character during the callback; never publish the first
// character's result into the second character's cache.
var other = new Player { Id = "other-player" };
ItemData otherItem = Custom(other, "other-item", true);
otherItem.m_shared.m_armor = 30;
Plugin.Invalidate();
previewA.BeforeArmor = () =>
{
    previewA.BeforeArmor = null;
    Equal(1, Plugin.EquippedCount(other), "provider can query another character's equipment");
};
Equal(25, Plugin.GetProjectedEquipmentArmor(player), "character switch does not truncate in-flight snapshot");
Equal(30, Plugin.GetProjectedEquipmentArmor(other), "character switch cannot cache the previous character's armor");

// An inventory instance replacement with the same player/id/version is also a
// distinct cache context, even if a foreign callback omitted invalidation.
Plugin.Invalidate();
previewA.BeforeArmor = () =>
{
    previewA.BeforeArmor = null;
    player.Inventory = new Inventory();
    ItemData replacement = Custom(player, "replacement", true);
    replacement.m_shared.m_armor = 40;
    Equal(1, Plugin.EquippedCount(player), "provider can replace the inventory instance");
};
Equal(25, Plugin.GetProjectedEquipmentArmor(player), "old inventory snapshot completes independently");
Equal(40, Plugin.GetProjectedEquipmentArmor(player), "old inventory total is not published into replacement cache");

// Failed non-armor projection must also clear partial data and retry. Keep an
// existing view to verify that the failed builder does not leave it truncated.
player = Fresh();
ItemData normalA = Custom(player, "normal-a", true);
ItemData normalB = Custom(player, "normal-b", true);
var view = Plugin.EquippedItems(player);
Plugin.Invalidate();
Plugin.InspectItem = item => { if (ReferenceEquals(item, normalB)) throw unavailable; };
ThrowsSame(unavailable, () => Plugin.EquippedCount(player), "projection failure remains observable");
Equal(0, view.Count, "failed projection clears the partially populated list");
Plugin.InspectItem = null;
Equal(2, Plugin.EquippedCount(player), "failed projection retries without another invalidation");
Equal(4, Plugin.Weight(player), "retry sums weight exactly once");
Equal(2, Plugin.SetCount(player), "retry sums sets exactly once");
Equal(0.4f, Plugin.Modifier(player), "retry sums modifiers exactly once");

Plugin.Invalidate();
int inspections = 0;
Plugin.InspectItem = _ => { if (++inspections == 1) Plugin.Invalidate(); };
Equal(2, Plugin.EquippedCount(player), "projection can finish after an in-flight invalidation");
Equal(2, Plugin.EquippedCount(player), "in-flight invalidation forces next projection rebuild");
Equal(4, inspections, "builder does not commit the newer invalidation version");
Equal(2, Plugin.EquippedCount(player), "complete retried projection is reused");
Equal(4, inspections, "valid projection avoids item inspection");
Plugin.InspectItem = null;

normalB.m_shared = null!;
Plugin.Invalidate();
Equal(2, Plugin.EquippedCount(player), "missing shared data does not truncate remaining equipment");
Equal(10, Plugin.GetProjectedEquipmentArmor(player), "missing shared data is excluded from armor");
Equal(0, normalB.ArmorCalls, "invalid shared data does not reach armor providers");

Console.WriteLine($"PASS: {checks} equipment armor assertions using linked production projection code and a fake game host.");
