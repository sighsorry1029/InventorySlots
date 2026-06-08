using System;
using System.Collections.Generic;

namespace InventorySlots;

internal static class UpgradeFavoriteCore
{
    public static string GetItemId(IDictionary<string, string>? customData, string key)
    {
        if (customData == null || string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        return customData.TryGetValue(key, out string? id) ? (id ?? "").Trim() : "";
    }

    public static string GetOrCreateItemId(
        IDictionary<string, string> customData,
        string key,
        ISet<string> existingIds,
        Func<string> createId)
    {
        string existing = GetItemId(customData, key);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        string id;
        do
        {
            id = createId().Trim();
        }
        while (string.IsNullOrWhiteSpace(id) || existingIds.Contains(id));

        customData[key] = id;
        return id;
    }

    public static void SetItemId(IDictionary<string, string> customData, string key, string id)
    {
        if (customData == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        customData[key] = id.Trim();
    }

    public static bool RemoveItemId(IDictionary<string, string>? customData, string key)
    {
        return customData != null && !string.IsNullOrWhiteSpace(key) && customData.Remove(key);
    }
}
