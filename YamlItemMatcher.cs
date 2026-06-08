using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void RefreshItemNameTokens()
    {
        ObjectDB objectDb = ObjectDB.instance;
        if (IsUnityNull(objectDb) || objectDb.m_items == null)
        {
            return;
        }

        if (InventoryDefinitions.CachedObjectDbItemCount == objectDb.m_items.Count && ItemNameTokens.Count > 0)
        {
            return;
        }

        ItemNameTokens.Clear();
        InventoryDefinitions.CachedObjectDbItemCount = objectDb.m_items.Count;

        foreach (GameObject itemPrefab in objectDb.m_items)
        {
            if (IsUnityNull(itemPrefab))
            {
                continue;
            }

            ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
            string? sharedName = itemDrop?.m_itemData?.m_shared?.m_name;
            if (string.IsNullOrWhiteSpace(sharedName))
            {
                continue;
            }

            AddItemNameToken(itemPrefab.name, sharedName!);
            AddItemNameToken(sharedName!, sharedName!);
            AddItemNameToken(StripLocalizationToken(sharedName!), sharedName!);
            AddItemNameToken(NormalizeResourceToken(itemPrefab.name), sharedName!);
            AddItemNameToken(NormalizeResourceToken(sharedName!), sharedName!);
        }
    }

    private static void AddItemNameToken(string? token, string sharedName)
    {
        string clean = CleanPrefabName(token?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(clean) || ItemNameTokens.ContainsKey(clean))
        {
            return;
        }

        ItemNameTokens[clean] = sharedName;
    }

    private static bool ItemMatchesAnyToken(ItemData? item, HashSet<string> tokens)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        foreach (string token in tokens)
        {
            if (TryGetPredefinedGroupToken(token, out string groupId) && ItemMatchesPredefinedGroup(item, groupId))
            {
                return true;
            }
        }

        foreach (string token in tokens)
        {
            if (ItemMatchesExactPrefabOrName(item, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ItemMatchesSlotItems(ItemData? item, IEnumerable<string> items)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        foreach (string token in items)
        {
            if (ItemMatchesYamlReferenceToken(item, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPredefinedGroupToken(string token, out string groupId)
    {
        groupId = "";
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string trimmed = token.Trim();
        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            groupId = NormalizeGroupId(trimmed.Substring(1));
            return !string.IsNullOrWhiteSpace(groupId);
        }

        const string prefix = "group:";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            groupId = NormalizeGroupId(trimmed.Substring(prefix.Length));
            return !string.IsNullOrWhiteSpace(groupId);
        }

        return false;
    }

    private static bool ItemMatchesYamlReferenceToken(ItemData item, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (ItemMatchesExactPrefabOrName(item, token))
        {
            return true;
        }

        if (TryNormalizeGroupSectionId(token, out string sectionGroupId))
        {
            return ItemMatchesTopLevelGroup(item, sectionGroupId);
        }

        string groupId = NormalizeGroupId(token);
        return !string.IsNullOrWhiteSpace(groupId) && ItemMatchesPredefinedGroup(item, groupId);
    }

    private static bool ItemMatchesTopLevelGroup(ItemData item, string groupId)
    {
        string normalizedGroupId = NormalizeGroupId(groupId);
        return !string.IsNullOrWhiteSpace(normalizedGroupId) && ItemMatchesBuiltInPredefinedGroup(item, normalizedGroupId);
    }

    private static bool ItemMatchesPredefinedGroup(ItemData? item, string groupId)
    {
        return ItemMatchesPredefinedGroup(item, groupId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static bool ItemMatchesPredefinedGroup(ItemData? item, string groupId, HashSet<string> visiting)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        string id = NormalizeGroupId(groupId);
        if (string.IsNullOrWhiteSpace(id) || !visiting.Add(id))
        {
            return false;
        }

        try
        {
            if (PredefinedGroupDefinitions.TryGetValue(id, out YamlPredefinedGroup yamlGroup))
            {
                return ItemMatchesYamlPredefinedGroup(item, yamlGroup, visiting);
            }

            return ItemMatchesBuiltInPredefinedGroup(item, id);
        }
        finally
        {
            visiting.Remove(id);
        }
    }

    private static bool ItemMatchesYamlPredefinedGroup(ItemData item, YamlPredefinedGroup group, HashSet<string> visiting)
    {
        YamlGroupMatch? match = group.Match;
        if (match == null)
        {
            return false;
        }

        bool hasCondition = false;
        bool matched = true;

        void Require(bool condition)
        {
            hasCondition = true;
            matched &= condition;
        }

        if (HasConfiguredValues(match.Groups))
        {
            Require(match.Groups.Any(groupId => ItemMatchesPredefinedGroup(item, groupId, visiting)));
        }

        if (HasConfiguredValues(match.ItemTypes))
        {
            Require(match.ItemTypes.Any(token => ItemTypeTokenMatches(item.m_shared.m_itemType, token)));
        }

        if (HasConfiguredValues(match.SkillTypes))
        {
            Require(match.SkillTypes.Any(token => SkillTypeTokenMatches(item.m_shared.m_skillType, token)));
        }

        if (HasConfiguredValues(match.Prefabs))
        {
            Require(match.Prefabs.Any(token => ItemMatchesExactPrefabOrName(item, token)));
        }

        if (HasConfiguredValues(match.PrefabAny))
        {
            Require(match.PrefabAny.Any(pattern => ItemMatchesPrefabPattern(item, pattern)));
        }

        if (HasConfiguredValues(match.NameAny))
        {
            Require(match.NameAny.Any(pattern => ItemMatchesNamePattern(item, pattern)));
        }

        if (HasConfiguredValues(match.AmmoTypes))
        {
            string ammoType = GetAmmoType(item);
            Require(match.AmmoTypes.Any(token => string.Equals(ammoType, token, StringComparison.OrdinalIgnoreCase)));
        }

        if (match.MaxStackGreaterThan.HasValue)
        {
            Require(item.m_shared.m_maxStackSize > match.MaxStackGreaterThan.Value);
        }

        if (match.ValueGreaterThan.HasValue)
        {
            Require(item.m_shared.m_value > match.ValueGreaterThan.Value);
        }

        if (match.HasFood.HasValue)
        {
            Require(MatchFoodCategory(item) == match.HasFood.Value);
        }

        if (match.HasStatusEffect.HasValue)
        {
            Require((item.m_shared.m_consumeStatusEffect != null) == match.HasStatusEffect.Value);
        }

        return hasCondition && matched;
    }

    private static bool HasConfiguredValues(List<string>? values) =>
        values != null && values.Any(value => !string.IsNullOrWhiteSpace(value));

    private static bool ItemMatchesExactPrefabOrName(ItemData item, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string clean = CleanPrefabName(token);
        return ItemIdentityTokenMatches(GetItemPrefabName(item), clean) ||
               ItemIdentityTokenMatches(GetSharedName(item), clean) ||
               ItemIdentityTokenMatches(StripLocalizationToken(GetSharedName(item)), clean) ||
               ItemMatchesKnownItemNameToken(item, clean);
    }

    private static bool ItemMatchesKnownItemNameToken(ItemData item, string token)
    {
        return TryGetKnownItemSharedName(token, out string knownSharedName) &&
               ItemIdentityTokenMatches(GetSharedName(item), knownSharedName);
    }

    private static bool TryGetKnownItemSharedName(string token, out string sharedName)
    {
        if (ItemNameTokens.TryGetValue(CleanPrefabName(token), out sharedName))
        {
            return true;
        }

        string normalized = NormalizeResourceToken(token);
        if (!string.IsNullOrWhiteSpace(normalized) &&
            ItemNameTokens.TryGetValue(normalized, out sharedName))
        {
            return true;
        }

        sharedName = "";
        return false;
    }

    private static bool ItemIdentityTokenMatches(string identity, string token)
    {
        string cleanIdentity = CleanPrefabName(identity);
        string cleanToken = CleanPrefabName(token);
        if (string.IsNullOrWhiteSpace(cleanIdentity) || string.IsNullOrWhiteSpace(cleanToken))
        {
            return false;
        }

        if (string.Equals(cleanIdentity, cleanToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedIdentity = NormalizeResourceToken(cleanIdentity);
        string normalizedToken = NormalizeResourceToken(cleanToken);
        return !string.IsNullOrWhiteSpace(normalizedIdentity) &&
               !string.IsNullOrWhiteSpace(normalizedToken) &&
               string.Equals(normalizedIdentity, normalizedToken, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ItemMatchesPrefabPattern(ItemData item, string pattern)
    {
        string prefab = GetItemPrefabName(item);
        return PatternMatches(prefab, pattern) || ItemMatchesKnownPrefabPattern(item, pattern);
    }

    private static bool ItemMatchesKnownPrefabPattern(ItemData item, string pattern)
    {
        string sharedName = GetSharedName(item);
        if (string.IsNullOrWhiteSpace(sharedName))
        {
            return false;
        }

        foreach (KeyValuePair<string, string> entry in ItemNameTokens)
        {
            if (ItemIdentityTokenMatches(sharedName, entry.Value) &&
                PatternMatches(entry.Key, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ItemMatchesNamePattern(ItemData item, string pattern)
    {
        string sharedName = GetSharedName(item);
        string localizedName = Localization.instance != null ? Localization.instance.Localize(sharedName) : sharedName;
        return PatternMatches(sharedName, pattern) || PatternMatches(localizedName, pattern);
    }

    private static bool ItemMatchesAnyPattern(ItemData item, IEnumerable<string> patterns) =>
        patterns.Any(pattern => ItemMatchesPrefabPattern(item, pattern) || ItemMatchesNamePattern(item, pattern));

    private static bool PatternMatches(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        string text = CleanPrefabName(value);
        string needle = CleanPrefabName(pattern);
        if (needle.IndexOf("*", StringComparison.Ordinal) >= 0)
        {
            return WildcardMatches(text, needle);
        }

        return text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool WildcardMatches(string value, string pattern)
    {
        string[] parts = pattern.Split(new[] { '*' }, StringSplitOptions.None);
        int position = 0;
        bool anchoredStart = !pattern.StartsWith("*", StringComparison.Ordinal);
        bool anchoredEnd = !pattern.EndsWith("*", StringComparison.Ordinal);

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            int index = value.IndexOf(part, position, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || anchoredStart && i == 0 && index != 0)
            {
                return false;
            }

            position = index + part.Length;
        }

        return !anchoredEnd || parts.Length == 0 || value.EndsWith(parts[parts.Length - 1], StringComparison.OrdinalIgnoreCase);
    }
}
