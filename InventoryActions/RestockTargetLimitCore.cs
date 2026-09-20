using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InventoryActions;

internal enum RestockRuleMode { Off, Existing, IncludeEmpty }

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
            if (token.Length == 0 || !TryParseRuleValue(trimmed.Substring(separator + 1), out string amountText, out RestockRuleMode mode))
            {
                continue;
            }

            result[token] = mode == RestockRuleMode.Off ? 0 : int.Parse(amountText, CultureInfo.InvariantCulture);
            // Last valid duplicate wins for both quantity and mode.
            refillEmptyKeys.Remove(token);
            if (mode == RestockRuleMode.IncludeEmpty) refillEmptyKeys.Add(token);
        }

        return result;
    }

    internal static bool TryParseRuleValue(string value, out string amount, out RestockRuleMode mode)
    {
        int separator = value.IndexOf('|');
        amount = "";
        mode = RestockRuleMode.Existing;
        // A rule always declares a positive target and one of the three modes.
        // No numeric-only, zero-as-Off, or old checkbox-suffix compatibility.
        if (separator <= 0 || !TryParseAmount(value.Substring(0, separator), out int parsed)) return false;
        string name = value.Substring(separator + 1).Trim();
        if (string.Equals(name, "Off", StringComparison.OrdinalIgnoreCase)) mode = RestockRuleMode.Off;
        else if (string.Equals(name, "Existing", StringComparison.OrdinalIgnoreCase)) mode = RestockRuleMode.Existing;
        else if (string.Equals(name, "IncludeEmpty", StringComparison.OrdinalIgnoreCase)) mode = RestockRuleMode.IncludeEmpty;
        else return false;
        amount = parsed.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    internal static string FormatRuleValue(string amount, RestockRuleMode mode) => amount + " | " + mode;

    internal static RestockRuleMode NextMode(RestockRuleMode mode) => mode switch
    {
        RestockRuleMode.Off => RestockRuleMode.Existing,
        RestockRuleMode.Existing => RestockRuleMode.IncludeEmpty,
        _ => RestockRuleMode.Off
    };

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
        return ClampAmountForEditor(value, int.MaxValue);
    }

    internal static string ClampAmountForEditor(string? value, int maximumAmount)
    {
        if (!long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return "";
        }

        long clamped = Math.Min(Math.Max(1L, parsed), Math.Max(1, maximumAmount));
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
        if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
        {
            amount = parsed;
            return true;
        }

        amount = 0;
        return false;
    }
}
