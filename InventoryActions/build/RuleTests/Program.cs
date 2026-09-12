using System;
using System.Linq;
#if INVENTORY_SLOTS
using InventorySlots;
#else
using InventoryActions;
#endif

int checks = 0;
void Check(string name, bool result) { if (!result) throw new Exception(name); checks++; }
const string raw = "# personal limits\r\nStone = 10 # keep note\r\nWood: 30; $item_stone: 8, UnknownModItem: 999\r\n";
var entries = ItemRuleConfigCore.Read(raw, true);
Check("four editable rows", entries.Count == 4);
Check("no-op preserves exact text", ItemRuleConfigCore.Write(raw, entries, true) == raw);
entries[0].Amount = "0";
string changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("edited zero and comment preserved", changed.Contains("Stone: 0 # keep note"));
Check("unrelated aliases and unknown mod item survive", changed.Contains("Wood: 30; $item_stone: 8, UnknownModItem: 999\r\n"));
Check("last normalized duplicate retains priority", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(changed), new[] { "Stone", "$item_stone" }, 50) == 8);
entries[2].Amount = "0";
Check("zero in effective entry blocks restock", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(ItemRuleConfigCore.Write(raw, entries, true)), new[] { "Stone" }, 50) == 0);
entries[2].Amount = "8";
entries[0].Removed = true;
changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("deletion preserves remaining alias policy", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(changed), new[] { "Stone", "$item_stone" }, 50) == 8);
entries.Add(new ItemRuleConfigCore.Entry { Start = -1, Key = "Resin", Amount = "5" });
changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("new row readable", RestockTargetLimitCore.Parse(changed).ContainsKey("resin"));
Check("unknown maximum clamped only during resolution", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(raw), new[] { "UnknownModItem" }, 50) == 50);
var excluded = ItemRuleConfigCore.ParseExclusions("# note\nResin; Stone(Clone), resin\nmod-item:variant");
Check("exclusions deduplicate", excluded.Count == 3);
Check("prefab matching case insensitive", excluded.Contains("RESIN"));
Check("clone suffix stripped", excluded.Contains("Stone"));
Check("exclusion identity keeps punctuation", excluded.Contains("mod-item:variant") && !excluded.Contains("moditemvariant"));
Check("empty config preserves pickup", ItemRuleConfigCore.ParseExclusions("").Count == 0);
var excludes = ItemRuleConfigCore.Read("Resin # comment\nStone", false);
excludes[0].Removed = true;
Check("remove exclusion", ItemRuleConfigCore.ParseExclusions(ItemRuleConfigCore.Write("Resin # comment\nStone", excludes, false)).SetEquals(new[] { "Stone" }));
var reloaded = ItemRuleConfigCore.Read(changed, true);
reloaded.Last(e => e.Key == "Resin").Removed = true;
string deleted = ItemRuleConfigCore.Write(changed, reloaded, true);
var registeredAgain = ItemRuleConfigCore.Read(deleted, true);
registeredAgain.Add(new ItemRuleConfigCore.Entry { Start = -1, Key = "Resin", Amount = "15" });
string final = ItemRuleConfigCore.Write(deleted, registeredAgain, true);
Check("save delete reload register has fresh spans", RestockTargetLimitCore.Parse(final)["resin"] == 15);
Check("multi-save preserves other rules", final.Contains("Wood: 30; $item_stone: 8, UnknownModItem: 999"));

// Live inputs retain their Entry object across saves. Exercise shifted spans,
// duplicate keys and a newly appended row without rebuilding the editor.
string live = "# limits\r\nStone = 9 # first\r\nStone: 8; UnknownModItem: invalid\r\n";
var liveEntries = ItemRuleConfigCore.Read(live, true);
var first = liveEntries[0];
var duplicate = liveEntries[1];
void CommitLive()
{
    live = ItemRuleConfigCore.Write(live, liveEntries, true);
    var saved = ItemRuleConfigCore.Read(live, true);
    ItemRuleConfigCore.AcceptSaved(liveEntries, saved);
    Check("rebased no-op preserves saved bytes", ItemRuleConfigCore.Write(live, liveEntries, true) == live);
}
foreach (string amount in new[] { "10", "1000", "2" })
{
    first.Amount = amount; CommitLive();
    Check("focused entry identity survives", ReferenceEquals(first, liveEntries[0]));
    Check("duplicate entry remains separate", ReferenceEquals(duplicate, liveEntries[1]) && duplicate.Amount == "8");
}
Check("comments CRLF and invalid untouched row survive", live == "# limits\r\nStone: 2 # first\r\nStone: 8; UnknownModItem: invalid\r\n");
first.Removed = true; CommitLive();
duplicate.Amount = "12"; CommitLive();
Check("delete then edit duplicate changes the surviving row", liveEntries.Count == 2 && RestockTargetLimitCore.Parse(live)["stone"] == 12);
var added = new ItemRuleConfigCore.Entry { Start = -1, Key = "Resin", Amount = "5" };
liveEntries.Add(added); CommitLive();
added.Amount = "50"; CommitLive();
Check("registered row updates without a second append", liveEntries.Count(e => e.Key == "Resin") == 1 && RestockTargetLimitCore.Parse(live)["resin"] == 50);
Check("registered entry identity and saved span retained", ReferenceEquals(added, liveEntries.Last()) && added.Start >= 0);
added.Removed = true; CommitLive();
liveEntries.Add(new ItemRuleConfigCore.Entry { Start = -1, Key = "Resin", Amount = "6" }); CommitLive();
Check("delete and re-register uses a new valid span", liveEntries.Count(e => e.Key == "Resin") == 1 && RestockTargetLimitCore.Parse(live)["resin"] == 6);
Console.WriteLine($"PASS: {checks} item rule config checks");
