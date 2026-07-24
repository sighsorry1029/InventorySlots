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
        if (item?.m_shared == null)
        {
            return false;
        }

        string id = NormalizeGroupId(groupId);
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (PredefinedGroupDefinitions.TryGetValue(id, out List<string> items))
        {
            return items.Any(token => ItemMatchesExactPrefabOrName(item, token));
        }

        return ItemMatchesBuiltInPredefinedGroup(item, id);
    }

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

}
