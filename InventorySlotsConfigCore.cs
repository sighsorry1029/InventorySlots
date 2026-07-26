using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
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

    public static Dictionary<string, int> ParseResourceMapYaml(string yaml)
    {
        Dictionary<string, int> result = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return result;
        }

        YamlStream stream = new();
        using (StringReader reader = new(yaml))
        {
            stream.Load(reader);
        }

        if (stream.Documents.Count != 1)
        {
            throw new InvalidDataException("ResourceMap.yml must contain exactly one YAML document.");
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException("ResourceMap.yml root must be a mapping of tier names to material lists.");
        }

        HashSet<string> tierNames = new(StringComparer.OrdinalIgnoreCase);
        int tierIndex = 0;
        foreach (KeyValuePair<YamlNode, YamlNode> entry in root.Children)
        {
            if (entry.Key is not YamlScalarNode tierNode || string.IsNullOrWhiteSpace(tierNode.Value))
            {
                throw new InvalidDataException("ResourceMap.yml contains an empty or structured tier name.");
            }

            string tierName = tierNode.Value!.Trim();
            if (!tierNames.Add(tierName))
            {
                throw new InvalidDataException($"ResourceMap.yml tier '{tierName}' is duplicated with different casing.");
            }

            if (entry.Value is not YamlSequenceNode materials)
            {
                throw new InvalidDataException($"ResourceMap.yml tier '{tierName}' must contain a YAML sequence.");
            }

            foreach (YamlNode materialNode in materials.Children)
            {
                if (materialNode is not YamlScalarNode materialScalar || string.IsNullOrWhiteSpace(materialScalar.Value))
                {
                    throw new InvalidDataException($"ResourceMap.yml tier '{tierName}' contains an empty or structured material.");
                }

                string token = NormalizeResourceToken(materialScalar.Value);
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidDataException($"ResourceMap.yml tier '{tierName}' contains a material with no usable token.");
                }

                if (!result.ContainsKey(token))
                {
                    result[token] = tierIndex;
                }
            }

            tierIndex++;
        }

        return result;
    }

    public static bool TryParseResourceMapYaml(string yaml, out Dictionary<string, int> resourceTiers, out Exception? error)
    {
        try
        {
            resourceTiers = ParseResourceMapYaml(yaml);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            resourceTiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            error = ex;
            return false;
        }
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
