using System;
using System.Collections.Generic;
using System.Linq;

namespace InventoryActions;

internal static class RestockTargetLimitCore
{
    public static Dictionary<string, int> Parse(string? raw)
    {
        Dictionary<string, int> result = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        foreach (string entry in SplitEntries(raw!))
        {
            string trimmed = StripInlineComment(entry).Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            int separator = FindSeparator(trimmed);
            if (separator <= 0 || separator >= trimmed.Length - 1)
            {
                continue;
            }

            string token = NormalizeResourceToken(trimmed.Substring(0, separator));
            string amountText = trimmed.Substring(separator + 1).Trim();
            if (token.Length == 0 || !int.TryParse(amountText, out int amount))
            {
                continue;
            }

            result[token] = Math.Max(0, amount);
        }

        return result;
    }

    public static int ResolveTargetStackLimit(Dictionary<string, int>? limits, IEnumerable<string?> lookupTokens, int itemMaxStack)
    {
        int fallback = Math.Max(0, itemMaxStack);
        if (fallback == 0 || limits == null || limits.Count == 0)
        {
            return fallback;
        }

        foreach (string? lookupToken in lookupTokens)
        {
            string token = NormalizeResourceToken(lookupToken);
            if (token.Length > 0 && limits.TryGetValue(token, out int configuredLimit))
            {
                return Math.Min(fallback, Math.Max(0, configuredLimit));
            }
        }

        return fallback;
    }

    public static string StripLocalizationToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string trimmed = value.Trim();
        return trimmed.StartsWith("$item_", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring("$item_".Length)
            : trimmed.TrimStart('$');
    }

    public static string CleanPrefabNameForLookup(string name)
    {
        return CleanPrefabName(name);
    }

    private static string NormalizeResourceToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "";
        }

        string text = CleanPrefabName(token!.Trim());
        if (text.StartsWith("$item_", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring("$item_".Length);
        }
        else if (text.StartsWith("$", StringComparison.Ordinal))
        {
            text = text.Substring(1);
        }

        return new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string CleanPrefabName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "" : name.Replace("(Clone)", "").Trim();
    }

    private static IEnumerable<string> SplitEntries(string raw)
    {
        return raw.Replace("\r", "\n").Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string StripInlineComment(string entry)
    {
        int commentIndex = entry.IndexOf('#');
        return commentIndex >= 0 ? entry.Substring(0, commentIndex) : entry;
    }

    private static int FindSeparator(string entry)
    {
        int colon = entry.IndexOf(':');
        int equals = entry.IndexOf('=');
        if (colon < 0)
        {
            return equals;
        }

        if (equals < 0)
        {
            return colon;
        }

        return Math.Min(colon, equals);
    }
}
