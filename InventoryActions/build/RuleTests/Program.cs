using System;
using System.Linq;
using System.Collections.Generic;
#if INVENTORY_SLOTS
using InventorySlots;
#else
using InventoryActions;
#endif

int checks = 0;
void Check(string name, bool result) { if (!result) throw new Exception(name); checks++; }
const string raw = "# personal limits\r\nStone = 10 | Existing # keep note\r\nWood: 30 | Off; $item_stone: 8 | IncludeEmpty, UnknownModItem: 999 | Existing\r\n";
var entries = ItemRuleConfigCore.Read(raw, true);
Check("four valid editable rows", entries.Count == 4);
Check("no-op preserves exact text", ItemRuleConfigCore.Write(raw, entries, true) == raw);
Check("mode and amount are independent", entries[1].Mode == RestockRuleMode.Off && entries[1].Amount == "30");
entries[0].Amount = "12";
string changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("edited amount and comment preserved", changed.Contains("Stone: 12 | Existing # keep note"));
Check("unrelated aliases and unknown mod item survive", changed.Contains("Wood: 30 | Off; $item_stone: 8 | IncludeEmpty, UnknownModItem: 999 | Existing\r\n"));
Check("last normalized duplicate retains priority", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(changed), new[] { "Stone", "$item_stone" }, 50) == 8);
entries[2].Mode = RestockRuleMode.Off;
changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("Off preserves configured amount", changed.Contains("$item_stone: 8 | Off"));
Check("Off disables runtime target", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(changed), new[] { "Stone" }, 50) == 0);
entries[2].Amount = "25";
Check("editing amount cannot enable Off", RestockTargetLimitCore.Parse(ItemRuleConfigCore.Write(raw, entries, true))["stone"] == 0);
entries[2].Mode = RestockRuleMode.Existing;
Check("re-enabling uses saved amount", RestockTargetLimitCore.Parse(ItemRuleConfigCore.Write(raw, entries, true))["stone"] == 25);
entries[0].Removed = true;
changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("deletion preserves remaining alias policy", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(changed), new[] { "Stone" }, 50) == 25);
entries.Add(new ItemRuleConfigCore.Entry { Start = -1, Key = "Resin", Amount = "5" });
changed = ItemRuleConfigCore.Write(raw, entries, true);
Check("new rules default to existing stacks", changed.Contains("Resin: 5 | Existing"));
Check("unresolved items keep target until runtime max is known", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(raw), new[] { "UnknownModItem" }, 50) == 50);
Check("unlisted items keep normal maximum", RestockTargetLimitCore.ResolveTargetStackLimit(RestockTargetLimitCore.Parse(raw), new[] { "Coal" }, 50) == 50);

var mode = RestockRuleMode.Off;
foreach (var expected in new[] { RestockRuleMode.Existing, RestockRuleMode.IncludeEmpty, RestockRuleMode.Off })
{
    mode = RestockTargetLimitCore.NextMode(mode);
    Check("mode cycle " + expected, mode == expected);
    string value = RestockTargetLimitCore.FormatRuleValue("30", mode);
    Check("mode round trip retains amount " + expected, RestockTargetLimitCore.TryParseRuleValue(value, out string amount, out var readMode) && amount == "30" && readMode == expected);
    var limits = RestockTargetLimitCore.Parse("Wood: " + value, out var enabled);
    Check("runtime target " + expected, limits["wood"] == (mode == RestockRuleMode.Off ? 0 : 30));
    Check("empty refill membership " + expected, enabled.Contains("wood") == (mode == RestockRuleMode.IncludeEmpty));
}
foreach (string invalid in new[] { "30", "0", "30 | refill", "0 | Off", "-1 | Existing", "30 | unknown", "30 | IncludeEmpty | Off", "2147483648 | Existing" })
{
    Check("unsupported rule rejected: " + invalid, RestockTargetLimitCore.Parse("Wood: " + invalid).Count == 0);
    Check("unsupported rule has no editor conversion: " + invalid, ItemRuleConfigCore.Read("Wood: " + invalid, true).Count == 0);
}
var rules = RestockTargetLimitCore.Parse("Wood: 30 | includeempty; $item_wood: 20 | off", out var refillKeys);
Check("last valid duplicate disables empty refill", rules["wood"] == 0 && refillKeys.Count == 0);
rules = RestockTargetLimitCore.Parse("Wood: 30 | Off; $item_wood: 20 | IncludeEmpty", out refillKeys);
Check("last valid duplicate enables empty refill", rules["wood"] == 20 && refillKeys.SequenceEqual(new[] { "wood" }));
rules = RestockTargetLimitCore.Parse("Wood: 30 | IncludeEmpty; Wood: invalid", out refillKeys);
Check("invalid later row cannot override valid rule", rules["wood"] == 30 && refillKeys.SequenceEqual(new[] { "wood" }));
rules = RestockTargetLimitCore.Parse("WoodPrefab: 30 | Off; $item_wood: 40 | IncludeEmpty", out refillKeys);
string? effectiveKey = RestockTargetLimitCore.ResolveConfiguredKey(rules, new[] { "WoodPrefab", "$item_wood" });
Check("prefab Off overrides lower priority alias", effectiveKey == "woodprefab" && !refillKeys.Contains(effectiveKey) && RestockTargetLimitCore.ResolveTargetStackLimit(rules, new[] { "WoodPrefab", "$item_wood" }, 50) == 0);
Check("positive editor clamps zero", RestockTargetLimitCore.ClampAmountForEditor("0", 30) == "1");
Check("positive editor clamps negatives", RestockTargetLimitCore.ClampAmountForEditor("-5", 30) == "1");
Check("editor clamps complete overshoot", RestockTargetLimitCore.ClampAmountForEditor("230", 30) == "30");
Check("editor supports long numeric input before clamp", RestockTargetLimitCore.ClampAmountForEditor("9999999999", 30) == "30");
Check("incomplete edit is not a target", RestockTargetLimitCore.ClampAmountForEditor("", 30) == "");

// Repeated saves keep focused Entry identities and all text spans valid.
string live = "# targets\r\nWood = 30 | Existing # keep\r\nStone: 12 | Off\r\nBad: invalid\r\n";
var liveEntries = ItemRuleConfigCore.Read(live, true);
var first = liveEntries[0];
void CommitLive()
{
    live = ItemRuleConfigCore.Write(live, liveEntries, true);
    ItemRuleConfigCore.AcceptSaved(liveEntries, ItemRuleConfigCore.Read(live, true));
    Check("rebased no-op preserves saved bytes", ItemRuleConfigCore.Write(live, liveEntries, true) == live);
}
foreach (var targetMode in new[] { RestockRuleMode.IncludeEmpty, RestockRuleMode.Off, RestockRuleMode.Existing })
{
    first.Mode = targetMode; CommitLive();
    Check("mode saves retain Entry identity and quantity", ReferenceEquals(first, liveEntries[0]) && first.Amount == "30");
}
foreach (string amount in new[] { "10", "1000", "2" })
{
    first.Amount = amount; CommitLive();
    Check("quantity saves keep mode and later rows", first.Mode == RestockRuleMode.Existing && liveEntries[1].Mode == RestockRuleMode.Off && liveEntries[1].Amount == "12");
}
Check("comments CRLF and invalid raw rows survive unrelated edits", live.Contains("# keep\r\n") && live.Contains("Bad: invalid\r\n"));
first.Removed = true; CommitLive();
liveEntries[0].Amount = "25"; CommitLive();
Check("delete then edit keeps correct row", RestockTargetLimitCore.Parse(live)["stone"] == 0 && live.Contains("Stone: 25 | Off"));
liveEntries.Add(new ItemRuleConfigCore.Entry { Start = -1, Key = "Wood", Amount = "15", Mode = RestockRuleMode.IncludeEmpty }); CommitLive();
liveEntries.Last().Amount = "30"; CommitLive();
Check("new row edit never duplicates append", ItemRuleConfigCore.Read(live, true).Count(e => e.Key == "Wood") == 1);
Check("new row serializes mode and quantity", RestockTargetLimitCore.Parse(live, out refillKeys)["wood"] == 30 && refillKeys.SequenceEqual(new[] { "wood" }));

var excluded = ItemRuleConfigCore.ParseExclusions("# note\nResin; Stone(Clone), resin\nmod-item:variant");
Check("exclusions still deduplicate", excluded.Count == 3);
Check("exclusions remain case insensitive", excluded.Contains("RESIN"));
Check("clone suffix stripped", excluded.Contains("Stone"));
Check("exclusion identity keeps punctuation", excluded.Contains("mod-item:variant") && !excluded.Contains("moditemvariant"));
var excludes = ItemRuleConfigCore.Read("Resin # comment\nStone", false);
excludes[0].Removed = true;
Check("exclusion removal unchanged", ItemRuleConfigCore.ParseExclusions(ItemRuleConfigCore.Write("Resin # comment\nStone", excludes, false)).SetEquals(new[] { "Stone" }));

const string exclusionRaw = "# pickup rules\r\n  Resin  # keep resin note\r\nStone(Clone) | Off; mod-item:variant | On, Wood | off\r\nBad | invalid\r\n";
var exclusionEntries = ItemRuleConfigCore.Read(exclusionRaw, false);
Check("disabled exclusions stay editable", exclusionEntries.Count == 4 && exclusionEntries[1].Key == "Stone(Clone)" && !exclusionEntries[1].Excluded);
Check("bare exclusions are enabled", exclusionEntries[0].Excluded);
Check("explicit exclusion states are case insensitive", exclusionEntries[2].Excluded && !exclusionEntries[3].Excluded);
Check("no-op retains explicit On whitespace comments and invalid lines", ItemRuleConfigCore.Write(exclusionRaw, exclusionEntries, false) == exclusionRaw);
Check("runtime excludes enabled rows only", ItemRuleConfigCore.ParseExclusions(exclusionRaw).SetEquals(new[] { "Resin", "mod-item:variant" }));
foreach (string invalid in new[] { "Wood |", "Wood | Disabled", "Wood | Off | On", "Wood | 0", "| Off" })
{
    Check("malformed exclusion state is not a prefab: " + invalid, ItemRuleConfigCore.Read(invalid, false).Count == 0);
    Check("malformed exclusion state is not active: " + invalid, ItemRuleConfigCore.ParseExclusions(invalid).Count == 0);
}

string exclusionLive = exclusionRaw;
var retainedEntry = exclusionEntries[0];
void CommitExclusions()
{
    exclusionLive = ItemRuleConfigCore.Write(exclusionLive, exclusionEntries, false);
    ItemRuleConfigCore.AcceptSaved(exclusionEntries, ItemRuleConfigCore.Read(exclusionLive, false));
    Check("exclusion rebase keeps saved bytes", ItemRuleConfigCore.Write(exclusionLive, exclusionEntries, false) == exclusionLive);
}
retainedEntry.Excluded = false;
CommitExclusions();
Check("unchecked row persists with Off and comment", exclusionLive.Contains("Resin | Off # keep resin note\r\n"));
Check("unchecked row no longer excludes pickup", !ItemRuleConfigCore.ParseExclusions(exclusionLive).Contains("Resin"));
Check("toggling retains Entry identity", ReferenceEquals(retainedEntry, exclusionEntries[0]));
Check("toggling leaves later raw rows untouched", exclusionLive.Contains("Stone(Clone) | Off; mod-item:variant | On, Wood | off\r\nBad | invalid\r\n"));
retainedEntry.Excluded = true;
CommitExclusions();
Check("checked row resumes exclusion and uses natural bare format", exclusionLive.Contains("Resin # keep resin note\r\n") && ItemRuleConfigCore.ParseExclusions(exclusionLive).Contains("Resin"));
exclusionEntries[1].Removed = true;
CommitExclusions();
Check("disabled row can be deleted completely", !ItemRuleConfigCore.Read(exclusionLive, false).Any(entry => entry.Key == "Stone(Clone)"));
exclusionEntries[1].Excluded = false;
CommitExclusions();
Check("editing after disabled row deletion uses rebased span", exclusionLive.Contains("mod-item:variant | Off") && !ItemRuleConfigCore.ParseExclusions(exclusionLive).Contains("mod-item:variant"));
exclusionEntries.Add(new ItemRuleConfigCore.Entry { Start = -1, Key = "Stone" });
CommitExclusions();
Check("new or re-added exclusion starts enabled", exclusionEntries.Last().Excluded && ItemRuleConfigCore.ParseExclusions(exclusionLive).Contains("Stone"));
exclusionEntries.Last().Excluded = false;
CommitExclusions();
Check("new exclusion subsequent toggle does not append duplicate", ItemRuleConfigCore.Read(exclusionLive, false).Count(entry => entry.Key == "Stone") == 1 && !ItemRuleConfigCore.ParseExclusions(exclusionLive).Contains("Stone"));

// A failed config write leaves snapshot/spans untouched. The UI restores only
// the edited value, so its next save must serialize the same pre-edit snapshot.
string beforeFailure = exclusionLive;
int beforeFailureStart = retainedEntry.Start;
int beforeFailureLength = retainedEntry.Length;
bool beforeFailureExcluded = retainedEntry.Excluded;
retainedEntry.Excluded = !beforeFailureExcluded;
string failedCandidate = ItemRuleConfigCore.Write(exclusionLive, exclusionEntries, false);
Check("toggle produces distinct pending save", failedCandidate != beforeFailure);
retainedEntry.Excluded = beforeFailureExcluded;
Check("failed toggle rollback restores no-op serialization", ItemRuleConfigCore.Write(exclusionLive, exclusionEntries, false) == beforeFailure);
Check("failed toggle never shifts accepted spans", retainedEntry.Start == beforeFailureStart && retainedEntry.Length == beforeFailureLength);

// The actual shared memory/selection policy; cells use integers here so this
// regression scenario does not need a running Unity player.
var memory = new Dictionary<int, string>();
var favoriteCells = new HashSet<int> { 0, 1, 2, 3 };
var occupied = new Dictionary<int, string> { [1] = "Wood", [2] = "Stone" };
bool Observe() => FavoriteSlotMemoryCore.Observe(memory, favoriteCells,
    cell => occupied.TryGetValue(cell, out string? prefab) ? prefab : null);
bool Select(string prefab, bool existing, out int cell, int blocked = -1) =>
    FavoriteSlotMemoryCore.TrySelect(favoriteCells.OrderBy(x => x).ToArray(), memory, prefab, existing,
        candidate => candidate != blocked && !occupied.ContainsKey(candidate), out cell);
Check("occupied favorites record prefab", Observe() && memory[1] == "Wood" && memory[2] == "Stone");
Check("quantity-only changes need no memory save", !Observe());
occupied.Clear();
Check("consuming all stacks retains associations", !Observe() && memory.Count == 2);
Check("stone from first chest keeps original later slot", Select("Stone", false, out int destination) && destination == 2);
occupied[destination] = "Stone";
Check("wood from later chest keeps original slot ahead of anonymous cells", Select("Wood", false, out destination) && destination == 1);
occupied[destination] = "Wood";
Check("full remembered slot cannot fall back", !Select("Wood", true, out destination));
occupied.Remove(1);
Check("locked or incompatible original slot cannot fall back", !Select("Wood", false, out destination, blocked: 1));
Check("unremembered item can use anonymous empty favorite", Select("Resin", false, out destination) && destination == 0);
Check("existing favorite suppresses anonymous duplicate", !Select("Resin", true, out destination));
memory[0] = "Wood";
Check("second remembered slot restores even if same item remains elsewhere", Select("Wood", true, out destination) && destination == 0);
occupied[destination] = "Wood";
Check("multiple remembered empty slots restore in grid order", Select("Wood", true, out destination) && destination == 1);
occupied[destination] = "Wood";
Check("restored remembered slots cannot create extra anonymous stacks", !Select("Wood", true, out destination));
occupied[1] = "Coal";
Check("manual replacement changes remembered prefab", Observe() && memory[1] == "Coal");
occupied.Remove(1);
Check("replacement remains remembered after consumption", !Observe() && Select("Coal", false, out destination) && destination == 1);
favoriteCells.Remove(1);
Check("unfavorite removes binding", Observe() && !memory.ContainsKey(1));
favoriteCells.Add(1);
Check("refavorite empty slot does not resurrect binding", !Observe() && !memory.ContainsKey(1));
favoriteCells.Clear(); favoriteCells.UnionWith(new[] { 0, 2 }); occupied.Clear(); Observe();
Check("unavailable remembered items reserve every remaining favorite cell", !Select("Resin", false, out destination));

foreach (string prefab in new[] { "", "Wood", "Mod:木,材%Special" })
{
    string line = FavoriteSlotMemoryCore.WriteLine(2, 3, prefab);
    Check("Actions favorite persistence round trip: " + prefab,
        FavoriteSlotMemoryCore.TryReadLine(line, out int x, out int y, out string saved) && x == 2 && y == 3 && saved == prefab);
}
Check("old coordinate-only favorites remain readable", FavoriteSlotMemoryCore.TryReadLine(" 2, 3 ", out _, out _, out string oldPrefab) && oldPrefab == "");
foreach (string invalid in new[] { "# comment", "-1,2,Wood", "a,1", "1,2,Wood,Stone", "2" })
    Check("invalid favorite record ignored: " + invalid, !FavoriteSlotMemoryCore.TryReadLine(invalid, out _, out _, out _));
Console.WriteLine($"PASS: {checks} item rule and favorite memory checks");
