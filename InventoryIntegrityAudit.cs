using System;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static void RequestLocalInventoryState(InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        Player? player = Player.m_localPlayer;
        if (!IsUnityNull(player) && !player!.m_isLoading)
        {
            RequestInventoryStateEnsure(player, reason, auditLevel);
        }
    }

    internal static void EnsureInventoryState(Player player, InventoryStateEnsureReason reason = InventoryStateEnsureReason.Unknown)
    {
        EnsureInventoryState(player, reason, GetDefaultInventoryStateAuditLevel(reason));
    }

    internal static void EnsureInventoryState(Player player, InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        if (IsUnityNull(player))
        {
            return;
        }

        if (InventorySafety.EnsuringInventoryState)
        {
            QueuePendingInventoryEnsure(reason, auditLevel);
            return;
        }

        InventoryStateEnsureReason currentReason = reason;
        InventoryStateAuditLevel currentAuditLevel = NormalizeAuditLevel(auditLevel, reason);
        for (int pass = 0; pass < MaxInventoryStateAuditPasses; pass++)
        {
            PerformInventoryStateAudit(player, currentReason, currentAuditLevel);
            if (!ConsumePendingInventoryEnsure(out currentReason, out currentAuditLevel) || IsUnityNull(player))
            {
                return;
            }
        }

        Log.LogDebug($"Inventory safety audit reached follow-up pass limit; deferred reason {currentReason} will wait for the next normal inventory event.");
    }

    internal static void RequestInventoryStateEnsure(Player? player, InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        if (IsUnityNull(player) || player!.m_isLoading)
        {
            return;
        }

        InventorySafety.DeferredEnsureReason = MergeInventoryEnsureReasons(InventorySafety.DeferredEnsureReason, reason);
        InventorySafety.DeferredAuditLevel = MaxAuditLevel(InventorySafety.DeferredAuditLevel, NormalizeAuditLevel(auditLevel, reason));
    }

    private static void ProcessDeferredInventoryStateEnsure(Player player)
    {
        if (InventorySafety.DeferredAuditLevel == InventoryStateAuditLevel.None || InventorySafety.DeferredEnsureReason == InventoryStateEnsureReason.Unknown)
        {
            return;
        }

        InventoryStateEnsureReason reason = InventorySafety.DeferredEnsureReason;
        InventoryStateAuditLevel auditLevel = InventorySafety.DeferredAuditLevel;
        InventorySafety.DeferredEnsureReason = InventoryStateEnsureReason.Unknown;
        InventorySafety.DeferredAuditLevel = InventoryStateAuditLevel.None;
        EnsureInventoryState(player, reason, auditLevel);
    }

    private static void PerformInventoryStateAudit(Player player, InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        RecordInventoryEnsureReason(reason);
        InventorySafety.PendingEnsureReason = InventoryStateEnsureReason.Unknown;
        InventorySafety.PendingAuditLevel = InventoryStateAuditLevel.None;
        InventorySafety.EnsuringInventoryState = true;
        try
        {
            Inventory inventory = ((Humanoid)player).GetInventory();
            if (inventory == null)
            {
                return;
            }

            InventoryStateAuditLevel normalizedAuditLevel = NormalizeAuditLevel(auditLevel, reason);
            bool syncedStateReady = IsSyncedStateReady();
            int fullHeight = GetFullHeightForWidth(inventory.m_width);
            bool inventoryChanged = false;

            if (inventory.m_height < fullHeight)
            {
                inventory.m_height = fullHeight;
                inventoryChanged = true;
            }

            int targetHeight = EnsureForeignSlotItemsPreserved(
                player,
                inventory,
                fullHeight,
                recoverToRegularCells: normalizedAuditLevel >= InventoryStateAuditLevel.FullIntegrity && syncedStateReady,
                warnLockedRows: ShouldWarnLockedRowsDuringAudit(reason),
                out bool foreignSlotItemsChanged);
            inventoryChanged |= foreignSlotItemsChanged;

            if (inventory.m_height != targetHeight)
            {
                inventory.m_height = targetHeight;
                inventoryChanged = true;
            }

            if (inventoryChanged)
            {
                inventory.Changed();
            }

            Container? tombstone = player.m_tombstone != null ? player.m_tombstone.GetComponent<Container>() : null;
            if (tombstone != null && tombstone.m_height < targetHeight)
            {
                tombstone.m_height = targetHeight;
            }

            if (normalizedAuditLevel >= InventoryStateAuditLevel.FullIntegrity && syncedStateReady)
            {
                if (ShouldRunFullIntegrityValidation(reason, player, inventory))
                {
                    ValidateAndProjectInventory(player, inventory);
                    RefreshInventoryStateSignature(player, inventory);
                }
                else
                {
                    ProjectSlotDisplayState(player, inventory);
                }
            }
            else if (normalizedAuditLevel >= InventoryStateAuditLevel.SlotLight && syncedStateReady)
            {
                ProjectSlotDisplayState(player, inventory);
            }

            if (normalizedAuditLevel >= InventoryStateAuditLevel.SlotLight)
            {
                UpdateCustomEquipmentVisuals(player);
            }
        }
        finally
        {
            InventorySafety.EnsuringInventoryState = false;
        }
    }

    private static void QueuePendingInventoryEnsure(InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        if (reason == InventoryStateEnsureReason.Unknown)
        {
            return;
        }

        InventorySafety.PendingEnsureReason = MergeInventoryEnsureReasons(InventorySafety.PendingEnsureReason, reason);
        InventorySafety.PendingAuditLevel = MaxAuditLevel(InventorySafety.PendingAuditLevel, NormalizeAuditLevel(auditLevel, reason));
    }

    private static bool ConsumePendingInventoryEnsure(out InventoryStateEnsureReason reason, out InventoryStateAuditLevel auditLevel)
    {
        reason = InventorySafety.PendingEnsureReason;
        auditLevel = InventorySafety.PendingAuditLevel;
        InventorySafety.PendingEnsureReason = InventoryStateEnsureReason.Unknown;
        InventorySafety.PendingAuditLevel = InventoryStateAuditLevel.None;
        if (reason == InventoryStateEnsureReason.Unknown)
        {
            auditLevel = InventoryStateAuditLevel.None;
            return false;
        }

        auditLevel = NormalizeAuditLevel(auditLevel, reason);
        return true;
    }

    private static InventoryStateAuditLevel GetDefaultInventoryStateAuditLevel(InventoryStateEnsureReason reason)
    {
        return reason switch
        {
            InventoryStateEnsureReason.PeriodicAudit => InventoryStateAuditLevel.FullIntegrity,
            InventoryStateEnsureReason.InventoryChanged => InventoryStateAuditLevel.SlotLight,
            InventoryStateEnsureReason.EquipmentChanged => InventoryStateAuditLevel.SlotLight,
            InventoryStateEnsureReason.InventoryMove => InventoryStateAuditLevel.SlotLight,
            InventoryStateEnsureReason.GuiShow => InventoryStateAuditLevel.FullIntegrity,
            InventoryStateEnsureReason.SlotAction => InventoryStateAuditLevel.SlotLight,
            _ => InventoryStateAuditLevel.FullIntegrity
        };
    }

    private static bool ShouldRunFullIntegrityValidation(InventoryStateEnsureReason reason, Player player, Inventory inventory)
    {
        if (MustRunFullIntegrityValidation(reason))
        {
            return true;
        }

        return HasInventoryStateSignatureChanged(player, inventory);
    }

    private static bool MustRunFullIntegrityValidation(InventoryStateEnsureReason reason)
    {
        return reason is InventoryStateEnsureReason.PlayerAwake
            or InventoryStateEnsureReason.PlayerSpawned
            or InventoryStateEnsureReason.PlayerLoad
            or InventoryStateEnsureReason.PlayerSave
            or InventoryStateEnsureReason.InventoryLoad
            or InventoryStateEnsureReason.Tombstone
            or InventoryStateEnsureReason.BackupRestore
            or InventoryStateEnsureReason.YamlReload
            or InventoryStateEnsureReason.JewelcraftingSlotRefresh
            or InventoryStateEnsureReason.ConfigChanged
            or InventoryStateEnsureReason.ReentrantFollowUp;
    }

    private static bool ShouldWarnLockedRowsDuringAudit(InventoryStateEnsureReason reason)
    {
        return reason is not InventoryStateEnsureReason.PlayerAwake
            and not InventoryStateEnsureReason.PlayerSpawned
            and not InventoryStateEnsureReason.PlayerLoad
            and not InventoryStateEnsureReason.InventoryLoad
            and not InventoryStateEnsureReason.BackupRestore;
    }

    private static InventoryStateAuditLevel NormalizeAuditLevel(InventoryStateAuditLevel auditLevel, InventoryStateEnsureReason reason)
    {
        return auditLevel == InventoryStateAuditLevel.None ? GetDefaultInventoryStateAuditLevel(reason) : auditLevel;
    }

    private static InventoryStateAuditLevel MaxAuditLevel(InventoryStateAuditLevel current, InventoryStateAuditLevel next)
    {
        return current >= next ? current : next;
    }

    private static InventoryStateEnsureReason MergeInventoryEnsureReasons(InventoryStateEnsureReason current, InventoryStateEnsureReason next)
    {
        if (current == InventoryStateEnsureReason.Unknown || current == next)
        {
            return next;
        }

        return next == InventoryStateEnsureReason.Unknown ? current : InventoryStateEnsureReason.ReentrantFollowUp;
    }

    private static void RecordInventoryEnsureReason(InventoryStateEnsureReason reason)
    {
        InventorySafety.EnsureCounts.TryGetValue(reason, out int current);
        InventorySafety.EnsureCounts[reason] = current + 1;

        if (current == 0 && reason != InventoryStateEnsureReason.Unknown)
        {
            Log.LogDebug($"Inventory safety audit reason observed: {reason}");
        }
    }

    private static void ProjectSlotDisplayState(Player player, Inventory inventory)
    {
        bool changed = false;

        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (slot.Kind == SlotKind.Quick)
            {
                continue;
            }

            ItemData? item = FindItemForSlot(player, inventory, slot);
            if (item == null)
            {
                continue;
            }

            if (slot.Kind == SlotKind.CustomEquipment)
            {
                if (!item.m_equipped)
                {
                    item.m_equipped = true;
                    changed = true;
                }

                string playerId = GetPlayerId(player);
                bool slotMarkerChanged =
                    item.m_customData == null ||
                    !item.m_customData.TryGetValue(SlotIdKey, out string slotId) ||
                    !string.Equals(slotId, slot.Id, StringComparison.OrdinalIgnoreCase) ||
                    !item.m_customData.TryGetValue(EquippedByKey, out string equippedBy) ||
                    equippedBy != playerId;

                if (slotMarkerChanged)
                {
                    MarkItemSlot(player, item, slot);
                    changed = true;
                }

                OnCustomEquipmentCompatEquipped(player, item);
            }
            else
            {
                changed |= SyncBuiltInSlotEquippedFlag(player, item, slot);
                if (item.m_customData != null &&
                    (item.m_customData.ContainsKey(SlotIdKey) || item.m_customData.ContainsKey(EquippedByKey)))
                {
                    ClearItemSlot(item);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            inventory.Changed();
        }
    }

    private static bool HasInventoryStateSignatureChanged(Player player, Inventory inventory)
    {
        return ComputeInventoryStateSignature(player, inventory) != InventorySafety.LastFullIntegrityAuditSignature;
    }

    private static void RefreshInventoryStateSignature(Player player, Inventory inventory)
    {
        InventorySafety.LastFullIntegrityAuditSignature = ComputeInventoryStateSignature(player, inventory);
    }

    private static int ComputeInventoryStateSignature(Player player, Inventory inventory)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + inventory.m_width;
            hash = hash * 31 + inventory.m_height;
            hash = hash * 31 + SlotDefinitions.Count;
            foreach (SlotDefinition slot in SlotDefinitions)
            {
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(slot.Id);
                hash = hash * 31 + (int)slot.Kind;
            }

            foreach (ItemData item in inventory.m_inventory)
            {
                if (item == null)
                {
                    continue;
                }

                hash = hash * 31 + item.m_gridPos.x;
                hash = hash * 31 + item.m_gridPos.y;
                hash = hash * 31 + item.m_stack;
                hash = hash * 31 + item.m_quality;
                hash = hash * 31 + (item.m_equipped ? 1 : 0);

                string prefab = GetItemPrefabName(item);
                if (!string.IsNullOrEmpty(prefab))
                {
                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(prefab);
                }

                if (item.m_customData != null && item.m_customData.TryGetValue(SlotIdKey, out string slotId))
                {
                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(slotId);
                }
            }

            return hash;
        }
    }
}
