using System;
using System.Runtime.CompilerServices;
using UnityEngine;
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
        PerformInventoryStateAudit(player, currentReason, currentAuditLevel);
        if (!ConsumePendingInventoryEnsure(out currentReason, out currentAuditLevel) || IsUnityNull(player))
        {
            return;
        }

        RequestInventoryStateEnsure(player, currentReason, currentAuditLevel);
    }

    internal static void RequestInventoryStateEnsure(Player? player, InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        if (IsUnityNull(player) || player!.m_isLoading)
        {
            return;
        }

        InventorySafety.DeferredEnsureReason = MergeInventoryEnsureReasons(InventorySafety.DeferredEnsureReason, reason);
        InventorySafety.DeferredAuditLevel = MaxAuditLevel(InventorySafety.DeferredAuditLevel, NormalizeAuditLevel(auditLevel, reason));
        InventorySafety.DeferredEnsureFrame = Math.Max(InventorySafety.DeferredEnsureFrame, Time.frameCount + 1);
        MarkRecentInventoryActivityForHeavyAudit(reason);
    }

    private static void MarkRecentInventoryActivityForHeavyAudit(InventoryStateEnsureReason reason)
    {
        if (reason is InventoryStateEnsureReason.InventoryChanged
            or InventoryStateEnsureReason.EquipmentChanged
            or InventoryStateEnsureReason.InventoryMove
            or InventoryStateEnsureReason.SlotAction)
        {
            InventorySafety.HeavyAuditDelayUntil = Math.Max(InventorySafety.HeavyAuditDelayUntil, Time.time + HeavySafetyAuditActivityDelay);
        }
    }

    private static void ProcessDeferredInventoryStateEnsure(Player player)
    {
        if (InventorySafety.DeferredAuditLevel == InventoryStateAuditLevel.None || InventorySafety.DeferredEnsureReason == InventoryStateEnsureReason.Unknown)
        {
            return;
        }

        if (InventorySafety.DeferredEnsureFrame > Time.frameCount)
        {
            return;
        }

        InventoryStateEnsureReason reason = InventorySafety.DeferredEnsureReason;
        InventoryStateAuditLevel auditLevel = InventorySafety.DeferredAuditLevel;
        InventorySafety.DeferredEnsureReason = InventoryStateEnsureReason.Unknown;
        InventorySafety.DeferredAuditLevel = InventoryStateAuditLevel.None;
        InventorySafety.DeferredEnsureFrame = -1;
        EnsureInventoryState(player, reason, auditLevel);
    }

    private static void PerformInventoryStateAudit(Player player, InventoryStateEnsureReason reason, InventoryStateAuditLevel auditLevel)
    {
        InventoryStateAuditLevel normalizedAuditLevel = NormalizeAuditLevel(auditLevel, reason);
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
                    RefreshSlotLightProjectionSignature(player, inventory);
                }
                else
                {
                    ProjectSlotDisplayState(player, inventory);
                    RefreshSlotLightProjectionSignature(player, inventory);
                }
            }
            else if (normalizedAuditLevel >= InventoryStateAuditLevel.SlotLight && syncedStateReady)
            {
                int signature = ComputeSlotLightProjectionSignature(player, inventory);
                bool skipped = !inventoryChanged && CanSkipSlotLightProjection(signature);
                if (!skipped)
                {
                    bool changed = ProjectSlotDisplayState(player, inventory);
                    if (changed)
                    {
                        signature = ComputeSlotLightProjectionSignature(player, inventory);
                    }
                    RememberSlotLightProjectionSignature(signature);
                }
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
    }

    private static bool ProjectSlotDisplayState(Player player, Inventory inventory)
    {
        bool changed = false;

        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (slot.Kind == SlotKind.Quick)
            {
                continue;
            }

            ItemData? item = FindItemForSlotIncludingGridCandidate(player, inventory, slot);
            if (item == null)
            {
                continue;
            }

            changed |= RestoreSlotEquipmentState(player, inventory, item, slot);
        }

        if (changed)
        {
            inventory.Changed();
            RefreshExternalEquipmentEffects(player);
        }

        return changed;
    }

    private static bool HasInventoryStateSignatureChanged(Player player, Inventory inventory)
    {
        return ComputeInventoryStateSignature(player, inventory) != InventorySafety.LastFullIntegrityAuditSignature;
    }

    private static void RefreshInventoryStateSignature(Player player, Inventory inventory)
    {
        InventorySafety.LastFullIntegrityAuditSignature = ComputeInventoryStateSignature(player, inventory);
    }

    private static bool CanSkipSlotLightProjection(int signature)
    {
        return InventorySafety.LastSlotLightProjectionSignature != int.MinValue &&
               InventorySafety.LastSlotLightProjectionSignature == signature;
    }

    private static void RefreshSlotLightProjectionSignature(Player player, Inventory inventory)
    {
        InventorySafety.LastSlotLightProjectionSignature = ComputeSlotLightProjectionSignature(player, inventory);
    }

    private static void RememberSlotLightProjectionSignature(int signature)
    {
        InventorySafety.LastSlotLightProjectionSignature = signature;
    }

    private static int ComputeSlotLightProjectionSignature(Player player, Inventory inventory)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(GetPlayerId(player));
            hash = hash * 31 + inventory.m_width;
            hash = hash * 31 + inventory.m_height;
            hash = hash * 31 + GetUsableRegularRows(player);
            hash = hash * 31 + _slotDefinitionVersion;
            hash = hash * 31 + GetKnownMaterialHash(player);
            foreach (SlotDefinition slot in SlotDefinitions)
            {
                if (slot.Kind == SlotKind.Quick)
                {
                    continue;
                }

                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(slot.Id);
                hash = hash * 31 + (int)slot.Kind;
            }

            Humanoid humanoid = player;
            AddEquippedItemSignature(humanoid.m_helmetItem, ref hash);
            AddEquippedItemSignature(humanoid.m_chestItem, ref hash);
            AddEquippedItemSignature(humanoid.m_legItem, ref hash);
            AddEquippedItemSignature(humanoid.m_shoulderItem, ref hash);
            AddEquippedItemSignature(humanoid.m_utilityItem, ref hash);
            AddEquippedItemSignature(humanoid.m_trinketItem, ref hash);

            foreach (ItemData item in inventory.m_inventory)
            {
                if (item == null || !HasSlotLightProjectionState(item))
                {
                    continue;
                }

                AddItemProjectionSignature(item, ref hash);
            }

            return hash;
        }
    }

    private static bool HasSlotLightProjectionState(ItemData item)
    {
        return item.m_customData.ContainsKey(SlotIdKey) ||
               item.m_customData.ContainsKey(EquippedByKey);
    }

    private static void AddEquippedItemSignature(ItemData? item, ref int hash)
    {
        if (item == null)
        {
            hash = hash * 31;
            return;
        }

        AddItemProjectionSignature(item, ref hash);
    }

    private static void AddItemProjectionSignature(ItemData item, ref int hash)
    {
        hash = hash * 31 + RuntimeHelpers.GetHashCode(item);
        hash = hash * 31 + item.m_gridPos.x;
        hash = hash * 31 + item.m_gridPos.y;
        hash = hash * 31 + item.m_quality;
        hash = hash * 31 + item.m_variant;
        hash = hash * 31 + (item.m_equipped ? 1 : 0);

        string prefab = GetItemPrefabName(item);
        if (!string.IsNullOrEmpty(prefab))
        {
            hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(prefab);
        }

        if (item.m_customData.TryGetValue(SlotIdKey, out string slotId))
        {
            hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(slotId);
        }

        if (item.m_customData.TryGetValue(EquippedByKey, out string equippedBy))
        {
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(equippedBy);
        }
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
