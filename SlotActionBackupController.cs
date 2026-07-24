using System;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

[Serializable]
internal sealed class InventorySlotsBackup
{
    public int version { get; set; }
    public string date { get; set; } = "";
    public string worldName { get; set; } = "";
    public int nrOfItems { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public int fixedRegularRows { get; set; }
    public int fullHeight { get; set; }
    public string inventoryBase64 { get; set; } = "";
}

public sealed partial class InventorySlotsPlugin
{
    internal static void PrunePendingSlotActions(Player? player = null)
    {
        player ??= Player.m_localPlayer;
        if (player == null)
        {
            ClearPendingSlotActions();
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            ClearPendingSlotActions();
            return;
        }

        foreach (var pair in InventorySafety.PendingSlotEquips.ToArray())
        {
            ItemData item = pair.Key;
            PendingSlotEquip pending = pair.Value;
            if (pending == null ||
                IsPendingSlotActionExpired(pending.CreatedAt) ||
                !inventory.ContainsItem(item) ||
                !pending.Slot.Accepts(item) ||
                !player.IsEquipActionQueued(item) && !item.m_equipped)
            {
                InventorySafety.PendingSlotEquips.Remove(item);
            }
        }

        foreach (var pair in InventorySafety.PendingSlotUnequips.ToArray())
        {
            ItemData item = pair.Key;
            PendingSlotUnequip pending = pair.Value;
            if (pending == null ||
                IsPendingSlotActionExpired(pending.CreatedAt) ||
                !inventory.ContainsItem(item) ||
                !pending.SourceSlot.Accepts(item) ||
                !player.IsEquipActionQueued(item))
            {
                InventorySafety.PendingSlotUnequips.Remove(item);
            }
        }

        foreach (var pair in InventorySafety.SlotUnequipToInventoryRequests.ToArray())
        {
            ItemData item = pair.Key;
            if (IsPendingSlotActionExpired(pair.Value) || !inventory.ContainsItem(item) || !item.m_equipped)
            {
                InventorySafety.SlotUnequipToInventoryRequests.Remove(item);
            }
        }
    }

    internal static void ClearPendingSlotActions()
    {
        InventorySafety.PendingSlotEquips.Clear();
        InventorySafety.PendingSlotUnequips.Clear();
        InventorySafety.SlotUnequipToInventoryRequests.Clear();
    }

    private static bool IsPendingSlotActionExpired(float createdAt)
    {
        float timeout = Mathf.Max(5f, PendingSlotActionTimeout);
        return Time.time - createdAt > timeout;
    }

    internal static void SaveSlotBackup(Player player)
    {
        if (IsUnityNull(player) || player.m_isLoading || player != Player.m_localPlayer)
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return;
        }

        if (HasServerCharactersActive)
        {
            return;
        }

        try
        {
            InventorySlotsBackup backup = CreateSlotBackup(inventory);
            ISerializer serializer = new SerializerBuilder().Build();
            player.m_customData[BackupKey] = serializer.Serialize(backup);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save slot backup: {ex}");
        }
    }

    internal static void TryRestoreSlotBackup(Player player)
    {
        if (IsUnityNull(player) || HasServerCharactersActive || InventorySafety.RestoringSlotBackup)
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null || HasAnySlotTailItem(inventory) || !TryGetSlotBackup(player, out InventorySlotsBackup? backup) || backup!.nrOfItems <= 0)
        {
            return;
        }

        InventorySafety.RestoringSlotBackup = true;
        try
        {
            Inventory backupInventory = new("InventorySlotsBackup", null, backup.width, backup.height);
            backupInventory.Load(new ZPackage(backup.inventoryBase64).ReadCompressedPackage());
            if (backupInventory.NrOfItems() == 0)
            {
                return;
            }

            int fullHeight = GetFullHeightForWidth(inventory.GetWidth());
            if (inventory.m_height < fullHeight)
            {
                inventory.m_height = fullHeight;
            }

            int restored = 0;
            foreach (ItemData backupItem in backupInventory.GetAllItemsInGridOrder().AsEnumerable().Reverse())
            {
                Vector2i target = new(backupItem.m_gridPos.x, backupItem.m_gridPos.y + GetFixedRegularRows());
                if (TryRestoreBackupItem(player, inventory, backupItem, target))
                {
                    restored++;
                }
            }

            if (restored > 0)
            {
                inventory.Changed();
                EnsureInventoryState(player, InventoryStateEnsureReason.BackupRestore);
                Log.LogInfo($"Slot backup restored: {restored}/{backupInventory.NrOfItems()} item(s). Backup date {backup.date}, world {backup.worldName}.");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to restore slot backup: {ex}");
        }
        finally
        {
            InventorySafety.RestoringSlotBackup = false;
        }
    }

    private static InventorySlotsBackup CreateSlotBackup(Inventory inventory)
    {
        int width = inventory.GetWidth();
        int tailRows = GetSlotTailRows(width);
        Inventory backupInventory = new("InventorySlotsBackup", null, width, tailRows);
        foreach (ItemData item in inventory.GetAllItemsInGridOrder().Where(item => item != null && item.m_gridPos.y >= GetFixedRegularRows()))
        {
            ItemData clone = item.Clone();
            clone.m_gridPos = new Vector2i(clone.m_gridPos.x, clone.m_gridPos.y - GetFixedRegularRows());
            backupInventory.m_inventory.Add(clone);
        }

        ZPackage raw = new();
        backupInventory.Save(raw);
        ZPackage compressed = new();
        compressed.WriteCompressed(raw);

        return new InventorySlotsBackup
        {
            version = 1,
            date = DateTime.Now.ToString("s"),
            worldName = ZNet.instance != null ? ZNet.instance.GetWorldName() : "",
            nrOfItems = backupInventory.NrOfItems(),
            width = width,
            height = tailRows,
            fixedRegularRows = GetFixedRegularRows(),
            fullHeight = GetFullHeightForWidth(width),
            inventoryBase64 = compressed.GetBase64()
        };
    }

    private static bool TryGetSlotBackup(Player player, out InventorySlotsBackup? backup)
    {
        backup = null;
        if (!player.m_customData.TryGetValue(BackupKey, out string value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            IDeserializer deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            backup = deserializer.Deserialize<InventorySlotsBackup>(value);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to parse slot backup: {ex}");
            return false;
        }

        return backup != null && !string.IsNullOrWhiteSpace(backup.inventoryBase64);
    }

    private static bool TryRestoreBackupItem(Player player, Inventory inventory, ItemData backupItem, Vector2i target)
    {
        ItemData item = backupItem.Clone();
        if (!IsOutOfBounds(inventory, target) &&
            TryGetEmptyUsableSpecialSlotAtCell(player, inventory, item, target, out SlotDefinition? slot))
        {
            return TryEquipIntoSlot(player, inventory, item, slot!);
        }

        if (TryAutoPlaceItemInSpecialSlot(player, inventory, item))
        {
            return true;
        }

        item.m_equipped = false;
        ClearItemSlot(item);
        if (TryMoveToFirstFreeRegularCell(player, inventory, item))
        {
            inventory.m_inventory.Add(item);
            return true;
        }

        Log.LogWarning($"Unable to restore backup item {item.m_shared?.m_name ?? "<unknown>"}; no valid inventory cell was available.");
        return false;
    }

    private static bool HasAnySlotTailItem(Inventory inventory)
    {
        return inventory.m_inventory.Any(item => item != null && item.m_gridPos.y >= GetFixedRegularRows());
    }
}
