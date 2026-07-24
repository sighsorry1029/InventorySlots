using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace InventorySlots;

internal static class InventorySlotsConfigCore
{
    public static YamlRoot ParseYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new YamlRoot();
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        YamlRoot config = deserializer.Deserialize<YamlRoot>(yaml) ?? new YamlRoot();
        if (config.Slots?.Any(slot => slot == null) == true)
        {
            throw new InvalidDataException("Slots cannot contain null entries.");
        }

        BuildInventoryLimits(config);
        return config;
    }

    public static bool TryParseYaml(string yaml, out YamlRoot config, out Exception? error)
    {
        try
        {
            config = ParseYaml(yaml);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            config = new YamlRoot();
            error = ex;
            return false;
        }
    }

    public static Dictionary<string, int> BuildResourceTierMap(YamlRoot config)
    {
        Dictionary<string, int> result = new(StringComparer.OrdinalIgnoreCase);
        List<YamlResourceTier> tiers = config.ResourceMap ?? new List<YamlResourceTier>();
        for (int index = 0; index < tiers.Count; index++)
        {
            YamlResourceTier tier = tiers[index];
            if (tier == null)
            {
                continue;
            }

            foreach (string material in tier.Materials ?? new List<string>())
            {
                string token = NormalizeResourceToken(material);
                if (!string.IsNullOrWhiteSpace(token) && !result.ContainsKey(token))
                {
                    result[token] = index;
                }
            }
        }

        return result;
    }

    public static Dictionary<string, int> BuildInventoryLimits(YamlRoot config)
    {
        Dictionary<string, int> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> entry in config?.InventoryLimits ?? new Dictionary<string, int>())
        {
            string target = entry.Key?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidDataException("InventoryLimits contains an empty target.");
            }

            if (entry.Value < 0)
            {
                throw new InvalidDataException($"Inventory limit '{target}' cannot be negative.");
            }

            if (result.ContainsKey(target))
            {
                throw new InvalidDataException($"Inventory limit '{target}' is duplicated with different casing.");
            }

            result[target] = entry.Value;
        }

        return result;
    }

    public static string CleanPrefabName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? "" : name.Replace("(Clone)", "").Trim();
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

    public static string NormalizeSlotId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        string trimmed = id!.Trim();
        return new string(trimmed.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.').ToArray()).ToLowerInvariant();
    }

    public static string NormalizeGroupId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        return new string(id!.Trim().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static string NormalizeResourceToken(string? token)
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
}
