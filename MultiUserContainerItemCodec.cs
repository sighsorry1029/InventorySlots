using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const byte MultiUserContainerItemCodecVersion = 1;
    private const int MultiUserContainerMaxPrefabNameBytes = 1024;
    private const int MultiUserContainerMaxCrafterNameBytes = 4 * 1024;
    private const int MultiUserContainerMaxCustomDataEntries = 1024;
    private const int MultiUserContainerMaxCustomDataKeyBytes = 4 * 1024;
    private const int MultiUserContainerMaxCustomDataValueBytes = 40 * 1024;
    private const int MultiUserContainerMaxCustomDataBytes = 40 * 1024;
    private const int MultiUserContainerMaxStack = 1_000_000;
    private const int MultiUserContainerMaxQuality = 10_000;
    private const int MultiUserContainerMaxVariant = 10_000;
    private const int MultiUserContainerMaxWorldLevel = 10_000;
    private const int MultiUserContainerMaxGridCoordinate = 4096;

    private static readonly Encoding MultiUserContainerUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static bool TryWriteMultiUserContainerItem(ZPackage? package, ItemData? item)
    {
        if (package == null)
        {
            return false;
        }

        if (item == null)
        {
            package.Write(false);
            return true;
        }

        ItemData serializedItem;
        try
        {
            // Clone gives ItemDataManager-style mods a chance to flush their runtime data
            // into m_customData and gives this write a stable dictionary snapshot.
            serializedItem = item.Clone();
        }
        catch
        {
            return false;
        }

        if (!TryGetMultiUserContainerPrefabName(serializedItem, out string prefabName) ||
            !IsValidMultiUserContainerItem(serializedItem) ||
            !TryGetUtf8ByteCount(prefabName, MultiUserContainerMaxPrefabNameBytes, out _) ||
            !TryGetUtf8ByteCount(serializedItem.m_crafterName, MultiUserContainerMaxCrafterNameBytes, out _) ||
            !IsValidMultiUserContainerCustomData(serializedItem.m_customData))
        {
            return false;
        }

        package.Write(true);
        package.Write(MultiUserContainerItemCodecVersion);
        WriteMultiUserContainerString(package, prefabName);
        package.Write(serializedItem.m_stack);
        package.Write(serializedItem.m_durability);
        package.Write(serializedItem.m_gridPos);
        package.Write(serializedItem.m_equipped);
        package.Write(serializedItem.m_quality);
        package.Write(serializedItem.m_variant);
        package.Write(serializedItem.m_crafterID);
        WriteMultiUserContainerString(package, serializedItem.m_crafterName);
        package.Write(serializedItem.m_worldLevel);
        package.Write(serializedItem.m_pickedUp);
        package.Write(serializedItem.m_customData.Count);

        foreach (KeyValuePair<string, string> entry in serializedItem.m_customData)
        {
            WriteMultiUserContainerString(package, entry.Key);
            WriteMultiUserContainerString(package, entry.Value);
        }

        return true;
    }

    internal static bool TryReadMultiUserContainerItem(ZPackage? package, out ItemData? item)
    {
        item = null;
        if (package == null)
        {
            return false;
        }

        int startPosition = package.GetPos();
        try
        {
            if (TryReadMultiUserContainerItemCore(package, out item))
            {
                return true;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is System.IO.EndOfStreamException ||
            exception is System.IO.IOException)
        {
            // Malformed or truncated network input is rejected below.
        }

        package.SetPos(startPosition);
        item = null;
        return false;
    }

    internal static MultiUserContainerItemSnapshot? CreateMultiUserContainerItemSnapshot(ItemData? item)
    {
        if (item == null)
        {
            return null;
        }

        ItemData snapshotItem;
        try
        {
            snapshotItem = item.Clone();
        }
        catch
        {
            return null;
        }

        _ = TryGetMultiUserContainerPrefabName(snapshotItem, out string prefabName);
        return new MultiUserContainerItemSnapshot(
            prefabName,
            snapshotItem.m_quality,
            snapshotItem.m_variant,
            snapshotItem.m_worldLevel,
            snapshotItem.m_crafterID,
            snapshotItem.m_crafterName,
            snapshotItem.m_durability,
            snapshotItem.m_pickedUp,
            snapshotItem.m_stack,
            snapshotItem.m_customData);
    }

    private static bool TryReadMultiUserContainerItemCore(ZPackage package, out ItemData? item)
    {
        item = null;
        if (package.Size() - package.GetPos() < sizeof(byte))
        {
            return false;
        }

        bool hasItem = package.ReadBool();
        if (!hasItem)
        {
            return true;
        }

        if (package.Size() - package.GetPos() < sizeof(byte) ||
            package.ReadByte() != MultiUserContainerItemCodecVersion ||
            !TryReadMultiUserContainerString(
                package,
                MultiUserContainerMaxPrefabNameBytes,
                out string prefabName,
                out _) ||
            string.IsNullOrWhiteSpace(prefabName))
        {
            return false;
        }

        int stack = package.ReadInt();
        float durability = package.ReadSingle();
        Vector2i gridPosition = package.ReadVector2i();
        bool equipped = package.ReadBool();
        int quality = package.ReadInt();
        int variant = package.ReadInt();
        long crafterId = package.ReadLong();

        if (!TryReadMultiUserContainerString(
                package,
                MultiUserContainerMaxCrafterNameBytes,
                out string crafterName,
                out _))
        {
            return false;
        }

        int worldLevel = package.ReadInt();
        bool pickedUp = package.ReadBool();
        int customDataCount = package.ReadInt();
        if (!IsValidMultiUserContainerPrimitiveData(
                stack,
                durability,
                gridPosition,
                quality,
                variant,
                worldLevel) ||
            customDataCount < 0 ||
            customDataCount > MultiUserContainerMaxCustomDataEntries)
        {
            return false;
        }

        Dictionary<string, string> customData = new(customDataCount, StringComparer.Ordinal);
        long customDataBytes = 0;
        for (int index = 0; index < customDataCount; index++)
        {
            if (!TryReadMultiUserContainerString(
                    package,
                    MultiUserContainerMaxCustomDataKeyBytes,
                    out string key,
                    out int keyBytes) ||
                !TryReadMultiUserContainerString(
                    package,
                    MultiUserContainerMaxCustomDataValueBytes,
                    out string value,
                    out int valueBytes) ||
                customData.ContainsKey(key))
            {
                return false;
            }

            customDataBytes += (long)keyBytes + valueBytes;
            if (customDataBytes > MultiUserContainerMaxCustomDataBytes)
            {
                return false;
            }

            customData.Add(key, value);
        }

        if (!TryCreateMultiUserContainerItem(prefabName, out ItemData decodedItem))
        {
            return false;
        }

        if (decodedItem.m_shared?.m_icons == null ||
            variant >= decodedItem.m_shared.m_icons.Length)
        {
            return false;
        }

        decodedItem.m_stack = stack;
        decodedItem.m_durability = durability;
        decodedItem.m_gridPos = gridPosition;
        decodedItem.m_equipped = equipped && decodedItem.IsEquipable();
        decodedItem.m_quality = quality;
        decodedItem.m_variant = variant;
        decodedItem.m_crafterID = crafterId;
        decodedItem.m_crafterName = crafterName;
        decodedItem.m_worldLevel = worldLevel;
        decodedItem.m_pickedUp = pickedUp;
        decodedItem.m_customData = customData;
        item = decodedItem;
        return true;
    }

    private static bool TryCreateMultiUserContainerItem(string prefabName, out ItemData item)
    {
        item = null!;
        try
        {
            GameObject? prefab = ObjectDB.instance != null
                ? ObjectDB.instance.GetItemPrefab(prefabName)
                : null;

            if (IsUnityNull(prefab) && ZNetScene.instance != null)
            {
                prefab = ZNetScene.instance.GetPrefab(prefabName);
            }

            if (IsUnityNull(prefab))
            {
                return false;
            }

            ItemDrop? itemDrop = prefab!.GetComponent<ItemDrop>();
            if (IsUnityNull(itemDrop) || itemDrop!.m_itemData == null)
            {
                return false;
            }

            item = itemDrop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            return true;
        }
        catch
        {
            item = null!;
            return false;
        }
    }

    private static bool TryGetMultiUserContainerPrefabName(ItemData item, out string prefabName)
    {
        prefabName = !IsUnityNull(item.m_dropPrefab)
            ? item.m_dropPrefab.name
            : "";
        return !string.IsNullOrWhiteSpace(prefabName);
    }

    private static bool IsValidMultiUserContainerItem(ItemData item)
    {
        return item.m_shared?.m_icons != null &&
               item.m_variant >= 0 &&
               item.m_variant < item.m_shared.m_icons.Length &&
               item.m_crafterName != null &&
               item.m_customData != null &&
               IsValidMultiUserContainerPrimitiveData(
                   item.m_stack,
                   item.m_durability,
                   item.m_gridPos,
                   item.m_quality,
                   item.m_variant,
                   item.m_worldLevel);
    }

    private static bool IsValidMultiUserContainerPrimitiveData(
        int stack,
        float durability,
        Vector2i gridPosition,
        int quality,
        int variant,
        int worldLevel)
    {
        return stack > 0 &&
               stack <= MultiUserContainerMaxStack &&
               !float.IsNaN(durability) &&
               !float.IsInfinity(durability) &&
               durability >= 0f &&
               quality > 0 &&
               quality <= MultiUserContainerMaxQuality &&
               variant >= 0 &&
               variant <= MultiUserContainerMaxVariant &&
               worldLevel >= 0 &&
               worldLevel <= MultiUserContainerMaxWorldLevel &&
               gridPosition.x >= -1 &&
               gridPosition.y >= -1 &&
               gridPosition.x <= MultiUserContainerMaxGridCoordinate &&
               gridPosition.y <= MultiUserContainerMaxGridCoordinate;
    }

    private static bool IsValidMultiUserContainerCustomData(Dictionary<string, string> customData)
    {
        if (customData.Count > MultiUserContainerMaxCustomDataEntries)
        {
            return false;
        }

        long totalBytes = 0;
        foreach (KeyValuePair<string, string> entry in customData)
        {
            if (!TryGetUtf8ByteCount(entry.Key, MultiUserContainerMaxCustomDataKeyBytes, out int keyBytes) ||
                !TryGetUtf8ByteCount(entry.Value, MultiUserContainerMaxCustomDataValueBytes, out int valueBytes))
            {
                return false;
            }

            totalBytes += (long)keyBytes + valueBytes;
            if (totalBytes > MultiUserContainerMaxCustomDataBytes)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetUtf8ByteCount(string? value, int maximumBytes, out int byteCount)
    {
        byteCount = 0;
        if (value == null)
        {
            return false;
        }

        try
        {
            byteCount = MultiUserContainerUtf8.GetByteCount(value);
            return byteCount <= maximumBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static void WriteMultiUserContainerString(ZPackage package, string value)
    {
        package.Write(MultiUserContainerUtf8.GetBytes(value));
    }

    private static bool TryReadMultiUserContainerString(
        ZPackage package,
        int maximumBytes,
        out string value,
        out int byteCount)
    {
        value = "";
        byteCount = 0;
        if (package.Size() - package.GetPos() < sizeof(int))
        {
            return false;
        }

        int length = package.ReadInt();
        int remaining = package.Size() - package.GetPos();
        if (length < 0 || length > maximumBytes || length > remaining)
        {
            return false;
        }

        byte[] bytes = package.ReadByteArray(length);
        if (bytes.Length != length)
        {
            return false;
        }

        value = MultiUserContainerUtf8.GetString(bytes);
        byteCount = length;
        return true;
    }
}
