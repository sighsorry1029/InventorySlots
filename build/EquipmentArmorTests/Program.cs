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
foreach (string id in new[] { "helmet", "chest", "legs", "cape" })
    Plugin.SlotDefinitions.Add(new SlotDefinition(id, SlotKind.BuiltIn, true));
player.m_helmetItem = new();
player.m_chestItem = new();
player.m_legItem = new();
player.m_shoulderItem = new();
player.Inventory.m_inventory.AddRange([player.m_helmetItem, player.m_chestItem, player.m_legItem, player.m_shoulderItem]);
Equal(0f, Plugin.GetProjectedEquipmentArmor(player), "native four armor slots are not added a second time");
Equal(0f, Plugin.GetProjectedEquipmentArmor(null!), "absent player does not grant armor");

Console.WriteLine($"PASS: {checks} equipment armor assertions using linked production projection code and a fake game host.");
