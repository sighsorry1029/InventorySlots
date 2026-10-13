using System;
using System.Collections.Generic;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // Syntax only: no item, group membership or ObjectDB alias results are cached.
    // Ordinal keys preserve case-sensitive section names and the literal (Clone) suffix.
    private sealed class YamlMatchToken
    {
        public readonly string Clean;
        public readonly string Normalized;
        public readonly string OnceCleaned;
        public readonly string OnceNormalized;
        public readonly string GroupId;
        public readonly string? SectionId;

        public YamlMatchToken(string token)
        {
            // Explicit references historically clean twice; alias values once.
            // Keep both forms because removing a nested (Clone) is not idempotent.
            OnceCleaned = CleanPrefabName(token);
            OnceNormalized = NormalizeResourceToken(OnceCleaned);
            Clean = CleanPrefabName(OnceCleaned);
            Normalized = NormalizeResourceToken(Clean);
            GroupId = NormalizeGroupId(token);
            SectionId = TryNormalizeGroupSectionId(token, out string section) ? section : null;
        }
    }

    private static YamlMatchToken GetYamlMatchToken(string token)
    {
        if (!InventoryDefinitions.YamlMatchTokens.TryGetValue(token, out YamlMatchToken prepared))
        {
            prepared = new YamlMatchToken(token);
            InventoryDefinitions.YamlMatchTokens[token] = prepared;
        }
        return prepared;
    }

    // Item identities are read afresh for each query, then normalized at most once
    // while comparing that item with a whole slot/group list.
    private struct YamlItemIdentity
    {
        private readonly string _prefab;
        private readonly string _shared;
        private readonly string _stripped;
        private string? _normalizedPrefab;
        private string? _normalizedShared;
        private string? _normalizedStripped;

        public YamlItemIdentity(ItemData item)
        {
            _prefab = CleanPrefabName(GetItemPrefabName(item));
            string shared = GetSharedName(item);
            _shared = CleanPrefabName(shared);
            _stripped = CleanPrefabName(StripLocalizationToken(shared));
            _normalizedPrefab = _normalizedShared = _normalizedStripped = null;
        }

        public bool Matches(YamlMatchToken token)
        {
            if (MatchesIdentity(_prefab, ref _normalizedPrefab, token.Clean, token.Normalized) ||
                MatchesIdentity(_shared, ref _normalizedShared, token.Clean, token.Normalized) ||
                MatchesIdentity(_stripped, ref _normalizedStripped, token.Clean, token.Normalized))
            {
                return true;
            }

            // Keep aliases live: mods can register item names after YAML is loaded.
            if (!TryGetKnownItemSharedName(token, out string sharedName)) return false;
            YamlMatchToken alias = GetYamlMatchToken(sharedName);
            return MatchesIdentity(_shared, ref _normalizedShared, alias.OnceCleaned, alias.OnceNormalized);
        }

        private static bool MatchesIdentity(string clean, ref string? normalized, string tokenClean, string tokenNormalized)
        {
            if (string.IsNullOrWhiteSpace(clean) || string.IsNullOrWhiteSpace(tokenClean)) return false;
            if (string.Equals(clean, tokenClean, StringComparison.OrdinalIgnoreCase)) return true;
            if (tokenNormalized.Length == 0) return false;
            normalized ??= NormalizeResourceToken(clean);
            return normalized.Length > 0 && string.Equals(normalized, tokenNormalized, StringComparison.OrdinalIgnoreCase);
        }
    }

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
        InventoryDefinitions.YamlMatchTokens.Clear();
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

    private static bool ItemMatchesSlotItems(ItemData? item, IEnumerable<string> items)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        YamlItemIdentity identity = new(item);
        foreach (string token in items)
        {
            if (!string.IsNullOrWhiteSpace(token) && ItemMatchesPreparedYamlReference(item, ref identity, GetYamlMatchToken(token)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ItemMatchesYamlReferenceToken(ItemData item, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        YamlItemIdentity identity = new(item);
        return ItemMatchesPreparedYamlReference(item, ref identity, GetYamlMatchToken(token));
    }

    private static bool ItemMatchesPreparedYamlReference(ItemData item, ref YamlItemIdentity identity, YamlMatchToken token)
    {
        if (identity.Matches(token))
        {
            return true;
        }

        if (token.SectionId != null)
        {
            return ItemMatchesBuiltInPredefinedGroup(item, token.SectionId);
        }

        return ItemMatchesPreparedGroup(item, ref identity, token.GroupId);
    }

    private static bool ItemMatchesTopLevelGroup(ItemData item, string groupId)
    {
        string normalizedGroupId = NormalizeGroupId(groupId);
        return !string.IsNullOrWhiteSpace(normalizedGroupId) && ItemMatchesBuiltInPredefinedGroup(item, normalizedGroupId);
    }

    private static bool ItemMatchesPredefinedGroup(ItemData? item, string groupId)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        YamlItemIdentity identity = new(item);
        return ItemMatchesPreparedGroup(item, ref identity, NormalizeGroupId(groupId));
    }

    private static bool ItemMatchesPreparedGroup(ItemData item, ref YamlItemIdentity identity, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (PredefinedGroupDefinitions.TryGetValue(id, out List<string> items))
        {
            // Group members are exact identities, not recursive group references.
            foreach (string token in items)
            {
                if (!string.IsNullOrWhiteSpace(token) && identity.Matches(GetYamlMatchToken(token))) return true;
            }
            return false;
        }

        return BuiltInGroupMatchers.ContainsKey(id) && ItemMatchesBuiltInPredefinedGroup(item, id);
    }

    private static bool ItemMatchesExactPrefabOrName(ItemData item, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        YamlItemIdentity identity = new(item);
        return identity.Matches(GetYamlMatchToken(token));
    }

    private static bool TryGetKnownItemSharedName(YamlMatchToken token, out string sharedName)
    {
        if (ItemNameTokens.TryGetValue(token.Clean, out sharedName))
        {
            return true;
        }

        string normalized = token.OnceNormalized;
        if (!string.IsNullOrWhiteSpace(normalized) &&
            ItemNameTokens.TryGetValue(normalized, out sharedName))
        {
            return true;
        }

        sharedName = "";
        return false;
    }

}
