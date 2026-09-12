using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InventoryActions;

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
            entries.Add(new Entry
            {
                Start = match.Index, Length = match.Length,
                Key = separator > 0 ? text.Substring(0, separator).Trim() : text,
                Amount = separator > 0 ? text.Substring(separator + 1).Trim() : "",
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
            if (!entry.Removed && previous != null && previous.Key == entry.Key && previous.Amount == entry.Amount) continue;
            result.Remove(entry.Start, entry.Length).Insert(entry.Start, replacement);
        }
        foreach (Entry entry in entries.Where(e => e.Start < 0 && !e.Removed))
        {
            if (result.Length > 0 && result[result.Length - 1] != '\n') result.Append('\n');
            result.Append(Format(entry, restock));
        }
        return result.ToString();
    }

    private static string Format(Entry entry, bool restock) =>
        restock ? entry.Key + ": " + entry.Amount : entry.Key;

    internal static string PrefabKey(string name) => name.Replace("(Clone)", "").Trim();

    internal static HashSet<string> ParseExclusions(string raw) =>
        new(Read(raw, false).Select(e => PrefabKey(e.Key)).Where(k => k.Length > 0), StringComparer.OrdinalIgnoreCase);
}
