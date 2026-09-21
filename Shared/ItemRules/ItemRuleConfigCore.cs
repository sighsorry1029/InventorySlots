using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#if INVENTORY_SLOTS
namespace InventorySlots;
#else
namespace InventoryActions;
#endif

// Text spans belong to one config snapshot. Updating one row preserves other rows,
// comments and separators; callers must reject saves against a newer snapshot.
internal static class ItemRuleConfigCore
{
    internal sealed class Entry
    {
        public int Start;
        public int Length;
        public string Key = "";
        public string Amount = "";
        public RestockRuleMode Mode = RestockRuleMode.Existing;
        public bool Excluded = true;
        public string Comment = "";
        public bool Removed = false;
    }

    internal static List<Entry> Read(string raw, bool restock)
    {
        List<Entry> entries = new();
        foreach (Match match in Regex.Matches(raw, @"[^,;\r\n]+"))
        {
            string text = RestockTargetLimitCore.StripInlineComment(match.Value).Trim();
            if (text.Length == 0) continue;
            int separator = restock ? RestockTargetLimitCore.FindSeparator(text) : -1;
            int comment = match.Value.IndexOf('#');
            string amount = separator > 0 ? text.Substring(separator + 1).Trim() : "";
            RestockRuleMode mode = RestockRuleMode.Existing;
            if (restock && (separator <= 0 || !RestockTargetLimitCore.TryParseRuleValue(amount, out amount, out mode))) continue;
            string key = separator > 0 ? text.Substring(0, separator).Trim() : text;
            bool excluded = true;
            if (!restock && !TryReadExclusion(text, out key, out excluded)) continue;
            entries.Add(new Entry
            {
                Start = match.Index, Length = match.Length,
                Key = key,
                Amount = amount, Mode = mode, Excluded = excluded,
                Comment = comment < 0 ? "" : " " + match.Value.Substring(comment)
            });
        }
        return entries;
    }

    internal static string Write(string original, IReadOnlyList<Entry> entries, bool restock)
    {
        StringBuilder result = new(original);
        foreach (Entry entry in entries.Where(e => e.Start >= 0).OrderByDescending(e => e.Start))
        {
            string replacement = entry.Removed ? "" : Format(entry, restock) + entry.Comment;
            // Leave unedited entries byte-for-byte intact, including whitespace and '='.
            Entry? previous = Read(original.Substring(entry.Start, entry.Length), restock).FirstOrDefault();
            if (!entry.Removed && previous != null && previous.Key == entry.Key && previous.Amount == entry.Amount &&
                previous.Mode == entry.Mode && previous.Excluded == entry.Excluded) continue;
            result.Remove(entry.Start, entry.Length).Insert(entry.Start, replacement);
        }
        foreach (Entry entry in entries.Where(e => e.Start < 0 && !e.Removed))
        {
            if (result.Length > 0 && result[result.Length - 1] != '\n') result.Append('\n');
            result.Append(Format(entry, restock));
        }
        return result.ToString();
    }

    // Call only after saving and validating the surviving rows against Read(next).
    // Keep Entry identities held by focused inputs, but replace every text span:
    // digit counts, deletions and new rows can all shift the following entries.
    internal static void AcceptSaved(List<Entry> entries, IReadOnlyList<Entry> saved)
    {
        entries.RemoveAll(entry => entry.Removed);
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Start = saved[i].Start;
            entries[i].Length = saved[i].Length;
            entries[i].Comment = saved[i].Comment;
        }
    }

    private static string Format(Entry entry, bool restock) =>
        restock ? entry.Key + ": " + RestockTargetLimitCore.FormatRuleValue(entry.Amount, entry.Mode) :
        entry.Key + (entry.Excluded ? "" : " | Off");

    private static bool TryReadExclusion(string text, out string key, out bool excluded)
    {
        key = text;
        excluded = true;
        int separator = text.IndexOf('|');
        if (separator < 0) return key.Length > 0;

        key = text.Substring(0, separator).Trim();
        string state = text.Substring(separator + 1).Trim();
        if (key.Length == 0) return false;
        if (state.Equals("On", StringComparison.OrdinalIgnoreCase)) return true;
        if (!state.Equals("Off", StringComparison.OrdinalIgnoreCase)) return false;
        excluded = false;
        return true;
    }

    internal static string PrefabKey(string name) => name.Replace("(Clone)", "").Trim();

    internal static HashSet<string> ParseExclusions(string raw) =>
        new(Read(raw, false).Where(e => e.Excluded).Select(e => PrefabKey(e.Key)).Where(k => k.Length > 0), StringComparer.OrdinalIgnoreCase);
}
