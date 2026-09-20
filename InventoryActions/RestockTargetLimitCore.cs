using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventoryActions;

internal static class RestockTargetLimitCore
{
    public static Dictionary<string, int> Parse(string? raw) => Parse(raw, out _);

    public static Dictionary<string, int> Parse(string? raw, out List<string> refillEmptyKeys)
    {
        refillEmptyKeys = new List<string>();
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
            SplitRuleValue(trimmed.Substring(separator + 1), out string amountText, out bool refillEmpty);
            if (token.Length == 0 || !TryParseAmount(amountText, out int amount))
            {
                continue;
            }

            result[token] = amount;
            // Last duplicate wins for both quantity and opt-in, in config order.
            refillEmptyKeys.Remove(token);
            if (refillEmpty && amount > 0) refillEmptyKeys.Add(token);
        }

        return result;
    }

    internal static void SplitRuleValue(string value, out string amount, out bool refillEmpty)
    {
        int separator = value.IndexOf('|');
        refillEmpty = separator >= 0 &&
            string.Equals(value.Substring(separator + 1).Trim(), "refill", StringComparison.OrdinalIgnoreCase);
        // Unknown suffixes stay invalid rather than silently opting into a new policy.
        amount = (refillEmpty ? value.Substring(0, separator) : value).Trim();
    }

    public static int ResolveTargetStackLimit(Dictionary<string, int>? limits, IEnumerable<string?> lookupTokens, int itemMaxStack)
    {
        int fallback = Math.Max(0, itemMaxStack);
        if (fallback == 0 || limits == null || limits.Count == 0)
        {
            return fallback;
        }

        string? key = ResolveConfiguredKey(limits, lookupTokens);
        return key == null ? fallback : Math.Min(fallback, Math.Max(0, limits[key]));
    }

    internal static string? ResolveConfiguredKey(Dictionary<string, int> limits, IEnumerable<string?> lookupTokens)
    {
        foreach (string? lookupToken in lookupTokens)
        {
            string token = NormalizeResourceToken(lookupToken);
            if (token.Length > 0 && limits.ContainsKey(token))
            {
                return token;
            }
        }

        return null;
    }

    internal static string NormalizeResourceToken(string? token)
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

    internal static string NormalizeAmountForEditor(string? value)
    {
        return TryParseAmount(value, out int amount)
            ? amount.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    internal static string ClampAmountForEditor(string? value, int maximumAmount)
    {
        if (!long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return "";
        }

        long clamped = Math.Min(Math.Max(0L, parsed), Math.Max(0, maximumAmount));
        return clamped.ToString(CultureInfo.InvariantCulture);
    }

    internal static IEnumerable<string> SplitEntries(string raw)
    {
        return raw.Replace("\r", "\n").Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    internal static string StripInlineComment(string entry)
    {
        int commentIndex = entry.IndexOf('#');
        return commentIndex >= 0 ? entry.Substring(0, commentIndex) : entry;
    }

    internal static int FindSeparator(string entry)
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

    private static bool TryParseAmount(string? value, out int amount)
    {
        if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            amount = Math.Max(0, parsed);
            return true;
        }

        amount = 0;
        return false;
    }
}
