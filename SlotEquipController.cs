using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static ItemData? FindItemForSlot(Player player, Inventory inventory, SlotDefinition slot)
    {
        if (slot.Kind == SlotKind.Quick)
        {
            if (!IsQuickSlotUnlocked(player, slot))
            {
                return null;
            }

            Vector2i slotPos = GetSlotGridPos(inventory, slot);
            ItemData? quickItem = inventory.GetItemAt(slotPos.x, slotPos.y);
            return quickItem != null && slot.Accepts(quickItem) ? quickItem : null;
        }

        ItemData? builtIn = GetBuiltInEquipmentSlotItem(player, slot);
        if (builtIn != null && slot.Accepts(builtIn))
        {
            return builtIn;
        }

        return FindCustomEquippedItemForSlot(inventory, slot);
    }

    private static ItemData? GetBuiltInEquipmentSlotItem(Player player, SlotDefinition slot)
    {
        Humanoid humanoid = player;
        return slot.Id switch
        {
            "helmet" => humanoid.m_helmetItem,
            "chest" => humanoid.m_chestItem,
            "legs" => humanoid.m_legItem,
            "cape" => humanoid.m_shoulderItem,
            "trinket" => humanoid.m_trinketItem,
            "utility" => humanoid.m_utilityItem,
            _ => null
        };
    }

    private static ItemData? FindItemForSlotIncludingGridCandidate(Player player, Inventory inventory, SlotDefinition slot)
    {
        ItemData? item = FindItemForSlot(player, inventory, slot);
        if (item != null ||
            InventorySafety.SuppressSlotAutoEquip ||
            slot.Kind != SlotKind.BuiltIn ||
            inventory == null)
        {
            return item;
        }

        Vector2i slotPos = GetSlotGridPos(inventory, slot);
        ItemData? gridItem = inventory.GetItemAt(slotPos.x, slotPos.y);
        return gridItem != null && slot.Accepts(gridItem) && CanUseSpecialSlot(player, inventory, gridItem, slot)
            ? gridItem
            : null;
    }

    private static bool RestoreBuiltInSlotEquipmentState(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (player == null || inventory == null || item == null || slot == null || slot.Kind != SlotKind.BuiltIn)
        {
            return false;
        }

        bool changed = false;
        if (item.m_customData != null &&
            (item.m_customData.ContainsKey(SlotIdKey) || item.m_customData.ContainsKey(EquippedByKey)))
        {
            ClearItemSlot(item);
            changed = true;
        }

        Humanoid humanoid = player;
        bool equipped = false;
        try
        {
            equipped = humanoid.IsItemEquiped(item);
            if (!equipped)
            {
                equipped = humanoid.EquipItem(item, true);
                changed |= equipped;
            }
        }
        catch
        {
            equipped = item.m_equipped;
        }

        if (equipped && !item.m_equipped)
        {
            item.m_equipped = true;
            changed = true;
        }

        if (changed)
        {
            humanoid.SetupEquipment();
        }

        return changed;
    }

    private static bool RestoreSlotEquipmentState(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (player == null || inventory == null || item == null || slot == null || slot.Kind == SlotKind.Quick)
        {
            return false;
        }

        if (slot.Kind == SlotKind.BuiltIn)
        {
            return RestoreBuiltInSlotEquipmentState(player, inventory, item, slot);
        }

        bool changed = false;
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
        return changed;
    }

    private static ItemData? FindCustomEquippedItemForSlot(Inventory inventory, SlotDefinition slot)
    {
        if (inventory?.m_inventory == null)
        {
            return null;
        }

        foreach (ItemData item in inventory.m_inventory)
        {
            if (item != null &&
                IsInventorySlotsCustomEquipped(item) &&
                item.m_customData.TryGetValue(SlotIdKey, out string id) &&
                string.Equals(id, slot.Id, StringComparison.OrdinalIgnoreCase) &&
                slot.Accepts(item))
            {
                return item;
            }
        }

        return null;
    }

    internal static bool TryEquipIntoSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (!CanUseSpecialSlot(player, inventory, item, slot))
        {
            if (IsJewelcraftingUtilityGemBlockedForSlot(item, slot))
            {
                ShowJewelcraftingCannotEquipGemMessage(player);
            }

            return false;
        }

        if (slot.Kind == SlotKind.Quick)
        {
            return TryPlaceQuickItemIntoSlot(player, inventory, item, slot);
        }

        if (!inventory.ContainsItem(item))
        {
            inventory.m_inventory.Add(item);
        }

        Vector2i target = GetSlotGridPos(inventory, slot);
        Vector2i incomingOriginalPos = item.m_gridPos;
        bool incomingOriginalPosUsed = false;
        List<SlotEquipItemSnapshot> itemSnapshots = CaptureSlotEquipItemSnapshots(player, inventory, item, slot);
        HumanoidEquipmentSnapshot equipmentSnapshot = CaptureHumanoidEquipmentSnapshot(player);
        bool FailSlotEquip()
        {
            RestoreSlotEquipMutationSnapshots(player, inventory, itemSnapshots, equipmentSnapshot);
            return false;
        }

        ItemData? current = FindItemForSlot(player, inventory, slot);
        if (current != null && current != item)
        {
            UnequipInventorySlotsItem(player, current);
            if (!TryRelocateSlotEquipBlockingItem(player, inventory, current, item, incomingOriginalPos, ref incomingOriginalPosUsed))
            {
                return FailSlotEquip();
            }
        }

        ItemData? blockingAtTarget = inventory.GetItemAt(target.x, target.y);
        if (blockingAtTarget != null && blockingAtTarget != item && blockingAtTarget != current)
        {
            UnequipInventorySlotsItem(player, blockingAtTarget);
            if (!TryRelocateSlotEquipBlockingItem(player, inventory, blockingAtTarget, item, incomingOriginalPos, ref incomingOriginalPosUsed))
            {
                return FailSlotEquip();
            }
        }

        UnequipConflictingCustomEquipmentItems(player, inventory, item, slot);

        if (slot.Kind == SlotKind.BuiltIn)
        {
            if (!((Humanoid)player).IsItemEquiped(item) && !((Humanoid)player).EquipItem(item, true))
            {
                return FailSlotEquip();
            }
        }
        else
        {
            item.m_equipped = true;
        }

        if (slot.Kind == SlotKind.CustomEquipment)
        {
            MarkItemSlot(player, item, slot);
            OnCustomEquipmentCompatEquipped(player, item);
        }
        else
        {
            ClearItemSlot(item);
        }

        item.m_gridPos = target;
        ((Humanoid)player).SetupEquipment();
        if (slot.Kind == SlotKind.CustomEquipment)
        {
            UpdateCustomEquipmentVisuals(player);
            RefreshExternalEquipmentEffects(player);
        }

        inventory.Changed();
        return true;
    }

    private sealed class SlotEquipItemSnapshot
    {
        public SlotEquipItemSnapshot(ItemData item, bool wasInInventory)
        {
            Item = item;
            WasInInventory = wasInInventory;
            GridPos = item.m_gridPos;
            Equipped = item.m_equipped;
            CustomData = new Dictionary<string, string>(item.m_customData);
        }

        public ItemData Item { get; }
        public bool WasInInventory { get; }
        public Vector2i GridPos { get; }
        public bool Equipped { get; }
        public Dictionary<string, string> CustomData { get; }
    }

    private sealed class HumanoidEquipmentSnapshot
    {
        public HumanoidEquipmentSnapshot(Humanoid humanoid)
        {
            RightItem = humanoid.m_rightItem;
            LeftItem = humanoid.m_leftItem;
            ChestItem = humanoid.m_chestItem;
            LegItem = humanoid.m_legItem;
            AmmoItem = humanoid.m_ammoItem;
            HelmetItem = humanoid.m_helmetItem;
            ShoulderItem = humanoid.m_shoulderItem;
            UtilityItem = humanoid.m_utilityItem;
            TrinketItem = humanoid.m_trinketItem;
        }

        public ItemData? RightItem { get; }
        public ItemData? LeftItem { get; }
        public ItemData? ChestItem { get; }
        public ItemData? LegItem { get; }
        public ItemData? AmmoItem { get; }
        public ItemData? HelmetItem { get; }
        public ItemData? ShoulderItem { get; }
        public ItemData? UtilityItem { get; }
        public ItemData? TrinketItem { get; }
    }

    private static List<SlotEquipItemSnapshot> CaptureSlotEquipItemSnapshots(Player player, Inventory inventory, ItemData incoming, SlotDefinition slot)
    {
        List<SlotEquipItemSnapshot> snapshots = new();
        HashSet<ItemData> seen = new();

        void Add(ItemData? candidate)
        {
            if (candidate == null || !seen.Add(candidate))
            {
                return;
            }

            snapshots.Add(new SlotEquipItemSnapshot(candidate, inventory.ContainsItem(candidate)));
        }

        Add(incoming);
        Add(FindItemForSlot(player, inventory, slot));
        Vector2i target = GetSlotGridPos(inventory, slot);
        Add(inventory.GetItemAt(target.x, target.y));

        foreach (ItemData candidate in inventory.m_inventory.ToArray())
        {
            if (candidate == null || candidate == incoming || !slot.Accepts(candidate))
            {
                continue;
            }

            if (candidate.m_equipped ||
                ((Humanoid)player).IsItemEquiped(candidate) ||
                candidate.m_customData.ContainsKey(SlotIdKey))
            {
                Add(candidate);
            }
        }

        return snapshots;
    }

    private static HumanoidEquipmentSnapshot CaptureHumanoidEquipmentSnapshot(Player player) =>
        new((Humanoid)player);

    private static void RestoreSlotEquipMutationSnapshots(Player player, Inventory inventory, List<SlotEquipItemSnapshot> itemSnapshots, HumanoidEquipmentSnapshot equipmentSnapshot)
    {
        foreach (SlotEquipItemSnapshot snapshot in itemSnapshots)
        {
            ItemData item = snapshot.Item;
            bool inInventory = inventory.ContainsItem(item);
            if (snapshot.WasInInventory && !inInventory)
            {
                inventory.m_inventory.Add(item);
            }
            else if (!snapshot.WasInInventory && inInventory)
            {
                inventory.m_inventory.Remove(item);
            }

            item.m_gridPos = snapshot.GridPos;
            item.m_equipped = snapshot.Equipped;
            item.m_customData.Clear();
            foreach (KeyValuePair<string, string> entry in snapshot.CustomData)
            {
                item.m_customData[entry.Key] = entry.Value;
            }
        }

        RestoreHumanoidEquipmentSnapshot(player, equipmentSnapshot);
    }

    private static void RestoreHumanoidEquipmentSnapshot(Player player, HumanoidEquipmentSnapshot snapshot)
    {
        Humanoid humanoid = player;
        humanoid.m_rightItem = snapshot.RightItem;
        humanoid.m_leftItem = snapshot.LeftItem;
        humanoid.m_chestItem = snapshot.ChestItem;
        humanoid.m_legItem = snapshot.LegItem;
        humanoid.m_ammoItem = snapshot.AmmoItem;
        humanoid.m_helmetItem = snapshot.HelmetItem;
        humanoid.m_shoulderItem = snapshot.ShoulderItem;
        humanoid.m_utilityItem = snapshot.UtilityItem;
        humanoid.m_trinketItem = snapshot.TrinketItem;
        humanoid.SetupEquipment();
        UpdateCustomEquipmentVisuals(player);
    }

    internal static bool TryRouteInventoryUseToDedicatedSlot(Player player, Inventory inventory, ItemData item, out bool handled)
    {
        handled = false;
        if (!CanRouteEquipToDedicatedSlot(player, inventory, item))
        {
            return false;
        }

        if (!TryFindDedicatedEquipmentSlot(player, inventory, item, out SlotDefinition? slot))
        {
            return false;
        }

        handled = true;
        return TryEquipIntoDedicatedSlot(player, inventory, item, slot!);
    }

    internal static bool TryHandleDedicatedSlotUse(Player player, Inventory inventory, ItemData item, out bool allowVanilla)
    {
        allowVanilla = true;
        if (player == null || inventory == null || item == null || player != Player.m_localPlayer || inventory != ((Humanoid)player).GetInventory())
        {
            return false;
        }

        if (!TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? slot) || slot!.Kind == SlotKind.Quick)
        {
            return false;
        }

        bool equipped = ((Humanoid)player).IsItemEquiped(item) || HasInventorySlotsSlot(item);
        if (!equipped)
        {
            allowVanilla = IsEquipableForInventorySlotsRouting(item);
            if (!allowVanilla)
            {
                TryEquipIntoSlot(player, inventory, item, slot);
            }

            return true;
        }

        if (!TryFindFreeRegularCell(player, inventory, out _))
        {
            allowVanilla = false;
            ((Character)player).Message(MessageHud.MessageType.Center, "$msg_inventoryfull", 0, null);
            return true;
        }

        if (player.IsEquipActionQueued(item))
        {
            InventorySafety.SlotUnequipToInventoryRequests.Remove(item);
        }
        else
        {
            InventorySafety.SlotUnequipToInventoryRequests[item] = Time.time;
        }

        allowVanilla = IsEquipableForInventorySlotsRouting(item);
        if (!allowVanilla)
        {
            CompleteSlotUnequipToInventory(player, item);
        }

        return true;
    }

    internal static bool HasPendingSlotUnequipRequest(ItemData item)
    {
        return item != null && InventorySafety.SlotUnequipToInventoryRequests.ContainsKey(item);
    }

    internal static void SetSlotUnequipInProgress(bool inProgress)
    {
        InventorySafety.SlotUnequipInProgress = inProgress;
    }

    internal static void SetSlotAutoEquipSuppressed(bool suppressed)
    {
        InventorySafety.SuppressSlotAutoEquip = suppressed;
    }

    internal static bool TryRouteHumanoidEquipToDedicatedSlot(Humanoid humanoid, ItemData item)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || humanoid != (Humanoid)player)
        {
            return false;
        }

        Inventory inventory = humanoid.GetInventory();
        if (!CanRouteEquipToDedicatedSlot(player, inventory, item))
        {
            return false;
        }

        if (TryGetCachedDedicatedSlotRouteFailure(player, inventory, item))
        {
            return false;
        }

        if (!TryFindDedicatedEquipmentSlot(player, inventory, item, out SlotDefinition? slot))
        {
            CacheDedicatedSlotRouteFailure(player, inventory, item);
            return false;
        }

        if (TryEquipIntoDedicatedSlot(player, inventory, item, slot!))
        {
            return true;
        }

        return false;
    }

    private static bool CanRouteEquipToDedicatedSlot(Player player, Inventory? inventory, ItemData? item)
    {
        if (InventorySafety.RoutingEquipToDedicatedSlot || player == null || item == null || inventory == null || player.m_isLoading)
        {
            return false;
        }

        if (player != Player.m_localPlayer || inventory != ((Humanoid)player).GetInventory() || !inventory.ContainsItem(item))
        {
            return false;
        }

        if (!IsUsableRegularCell(inventory, player, item.m_gridPos))
        {
            return false;
        }

        return inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y) == item;
    }

    private static bool TryFindDedicatedEquipmentSlot(Player player, Inventory inventory, ItemData item, out SlotDefinition? slot)
    {
        slot = SlotDefinitions.FirstOrDefault(s => s.Kind == SlotKind.CustomEquipment && CanUseSpecialSlot(player, inventory, item, s));
        if (slot != null)
        {
            return true;
        }

        slot = SlotDefinitions.FirstOrDefault(s => s.Kind == SlotKind.BuiltIn && CanUseSpecialSlot(player, inventory, item, s));
        return slot != null;
    }

    private static bool TryGetCachedDedicatedSlotRouteFailure(Player player, Inventory inventory, ItemData item)
    {
        int context = ComputeDedicatedSlotRouteFailureContext(player);
        int itemKey = ComputeDedicatedSlotRouteCacheItemKey(item);
        return ReferenceEquals(InventorySafety.DedicatedSlotRouteFailureCacheInventory, inventory) &&
               InventorySafety.DedicatedSlotRouteFailureCacheVersion == InventoryDefinitions.SlotDefinitionVersion &&
               InventorySafety.DedicatedSlotRouteFailureCacheContext == context &&
               InventorySafety.DedicatedSlotRouteFailureCacheItemKey == itemKey;
    }

    private static void CacheDedicatedSlotRouteFailure(Player player, Inventory inventory, ItemData item)
    {
        InventorySafety.DedicatedSlotRouteFailureCacheInventory = inventory;
        InventorySafety.DedicatedSlotRouteFailureCacheVersion = InventoryDefinitions.SlotDefinitionVersion;
        InventorySafety.DedicatedSlotRouteFailureCacheContext = ComputeDedicatedSlotRouteFailureContext(player);
        InventorySafety.DedicatedSlotRouteFailureCacheItemKey = ComputeDedicatedSlotRouteCacheItemKey(item);
    }

    private static int ComputeDedicatedSlotRouteFailureContext(Player player)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(GetPlayerId(player));
            hash = hash * 31 + InventoryDefinitions.SlotDefinitionVersion;
            hash = hash * 31 + GetKnownMaterialHash(player);
            hash = hash * 31 + (ObjectDB.instance?.m_items?.Count ?? -1);
            return hash;
        }
    }

    private static int ComputeDedicatedSlotRouteCacheItemKey(ItemData item)
    {
        unchecked
        {
            int hash = ComputeCanAddItemCacheItemKey(item);
            hash = hash * 31 + (int)(item.m_shared?.m_itemType ?? 0);
            hash = hash * 31 + (int)(item.m_shared?.m_skillType ?? 0);
            hash = hash * 31 + item.m_variant;
            hash = hash * 31 + GetItemCustomDataOrderIndependentHash(item);
            return hash;
        }
    }

    internal static bool TryCompletePendingSlotEquip(Humanoid humanoid, ItemData item, out bool result)
    {
        result = false;
        Player? player = Player.m_localPlayer;
        if (player == null || humanoid != (Humanoid)player || item == null || !InventorySafety.PendingSlotEquips.TryGetValue(item, out PendingSlotEquip? pending))
        {
            return false;
        }

        ClearSlotActionState(item);
        Inventory inventory = humanoid.GetInventory();
        if (inventory == null || !inventory.ContainsItem(item) || !pending.Slot.Accepts(item))
        {
            return true;
        }

        result = TryEquipIntoDedicatedSlot(player, inventory, item, pending.Slot);
        if (!result)
        {
            ((Character)player).Message(MessageHud.MessageType.Center, "$msg_cantuse", 0, null);
        }

        return true;
    }

    private static bool TryEquipIntoDedicatedSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        InventorySafety.RoutingEquipToDedicatedSlot = true;
        try
        {
            return TryEquipIntoSlot(player, inventory, item, slot);
        }
        finally
        {
            InventorySafety.RoutingEquipToDedicatedSlot = false;
        }
    }

    private static bool TryPlaceQuickItemIntoSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (!IsQuickSlotUnlocked(player, slot))
        {
            return false;
        }

        Vector2i target = GetSlotGridPos(inventory, slot);
        ItemData? current = inventory.GetItemAt(target.x, target.y);
        if (current != null && current != item)
        {
            if (!TryRelocateBlockingItem(player, inventory, current, item))
            {
                return false;
            }
        }

        if (((Humanoid)player).IsItemEquiped(item))
        {
            ((Humanoid)player).UnequipItem(item, true);
        }

        bool customEquipmentChanged = false;
        if (IsInventorySlotsCustomEquipped(item))
        {
            OnCustomEquipmentCompatUnequipping(player, item);
            item.m_equipped = false;
            ClearItemSlot(item);
            ((Humanoid)player).SetupEquipment();
            customEquipmentChanged = true;
        }

        ClearItemSlot(item);
        item.m_gridPos = target;
        if (!inventory.ContainsItem(item))
        {
            inventory.m_inventory.Add(item);
        }

        inventory.Changed();
        if (customEquipmentChanged)
        {
            RefreshExternalEquipmentEffects(player);
        }

        return true;
    }

    internal static void UnequipInventorySlotsItem(Player player, ItemData item)
    {
        if (item == null)
        {
            return;
        }

        if (((Humanoid)player).IsItemEquiped(item) && !IsInventorySlotsCustomEquipped(item))
        {
            ((Humanoid)player).UnequipItem(item, true);
        }

        bool equipmentStateChanged = false;
        if (IsInventorySlotsCustomEquipped(item))
        {
            OnCustomEquipmentCompatUnequipping(player, item);
            item.m_equipped = false;
            ClearItemSlot(item);
            ((Humanoid)player).SetupEquipment();
            UpdateCustomEquipmentVisuals(player);
            equipmentStateChanged = true;
        }

        equipmentStateChanged |= ForceClearEquipmentReference(player, item);
        if (equipmentStateChanged)
        {
            RefreshExternalEquipmentEffects(player);
        }
    }

    private static void UnequipConflictingCustomEquipmentItems(Player player, Inventory inventory, ItemData incoming, SlotDefinition slot)
    {
        if (player == null || inventory == null || incoming == null || slot.Kind != SlotKind.CustomEquipment)
        {
            return;
        }

        foreach (ItemData other in inventory.m_inventory.ToArray())
        {
            if (other == null || other == incoming || !slot.Accepts(other))
            {
                continue;
            }

            bool explicitlyEquippedForSlot =
                other.m_customData.TryGetValue(SlotIdKey, out string id) &&
                string.Equals(id, slot.Id, StringComparison.OrdinalIgnoreCase);
            bool equipped = other.m_equipped || ((Humanoid)player).IsItemEquiped(other) || explicitlyEquippedForSlot;
            if (!equipped)
            {
                continue;
            }

            UnequipInventorySlotsItem(player, other);
        }
    }

    internal static void UnequipCustomEquipmentForDeathDrop(Player player)
    {
        if (IsUnityNull(player) || player!.m_isLoading)
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return;
        }

        bool changed = false;
        foreach (ItemData item in GetCustomEquippedItems(player).ToArray())
        {
            OnCustomEquipmentCompatUnequipping(player, item);
            item.m_equipped = false;
            ClearItemSlot(item);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        UpdateCustomEquipmentVisuals(player);
        RefreshExternalEquipmentEffects(player);
    }

    internal static bool TryHandleRegularItemDragIntoEquipmentSlot(InventoryGui gui, InventoryGrid targetGrid, Vector2i pos, InventoryGrid.Modifier mod)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || gui == null || targetGrid == null || mod != InventoryGrid.Modifier.Select || gui.m_dragGo == null || gui.m_dragItem == null || gui.m_dragInventory == null)
        {
            return false;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (playerInventory == null || gui.m_dragInventory != playerInventory || targetGrid.GetInventory() != playerInventory || !playerInventory.ContainsItem(gui.m_dragItem))
        {
            return false;
        }

        ItemData dragItem = gui.m_dragItem;
        if (!TryGetSlotAtGridPos(playerInventory, pos, out SlotDefinition? targetSlot) || targetSlot!.Kind == SlotKind.Quick)
        {
            return false;
        }

        if (IsJewelcraftingUtilityGemBlockedForSlot(dragItem, targetSlot))
        {
            ShowJewelcraftingCannotEquipGemMessage(player);
            gui.SetupDragItem(null, null, 1);
            gui.UpdateCraftingPanel();
            return true;
        }

        if (!targetSlot.Accepts(dragItem))
        {
            return false;
        }

        if (!IsUsableRegularCell(playerInventory, player, dragItem.m_gridPos) || playerInventory.GetItemAt(dragItem.m_gridPos.x, dragItem.m_gridPos.y) != dragItem)
        {
            return false;
        }

        if (!TryQueueSlotEquip(player, playerInventory, dragItem, targetSlot))
        {
            ((Character)player).Message(MessageHud.MessageType.Center, "$msg_cantuse", 0, null);
        }

        gui.SetupDragItem(null, null, 1);
        gui.UpdateCraftingPanel();
        return true;
    }

    internal static void TryPinInventoryItemTooltipFromSelection(InventoryGui gui, InventoryGrid grid, Vector2i pos, InventoryGrid.Modifier mod)
    {
        // Inventory/container comparison tooltips are pinned from hover + middle-click.
        // Plain item clicks should keep their vanilla drag/use behavior.
    }

    private static bool TryQueueSlotEquip(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (player == null || inventory == null || item == null || slot == null || slot.Kind == SlotKind.Quick || !CanUseSpecialSlot(player, inventory, item, slot))
        {
            return false;
        }

        ClearSlotActionState(item);
        InventorySafety.PendingSlotEquips[item] = new PendingSlotEquip(slot, Time.time);
        player.RemoveEquipAction(item);

        if (IsEquipableForInventorySlotsRouting(item) && item.m_shared.m_equipDuration > 0f)
        {
            player.QueueEquipAction(item);
        }
        else if (!((Humanoid)player).EquipItem(item, true))
        {
            ClearSlotActionState(item);
            return false;
        }

        return true;
    }

    private static bool IsEquipableForInventorySlotsRouting(ItemData? item) =>
        item != null && item.IsEquipable();

    internal static bool TryPrepareSlotItemForExternalRemoval(Player player, Inventory inventory, ItemData item, out SlotDefinition? slot)
    {
        slot = null;
        if (player == null || inventory == null || item == null || !inventory.ContainsItem(item) || item.m_shared?.m_questItem == true)
        {
            return false;
        }

        bool inDedicatedSlot = TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? gridSlot) && gridSlot!.Kind != SlotKind.Quick;
        bool customEquipped = IsInventorySlotsCustomEquipped(item);
        if (!inDedicatedSlot && !customEquipped)
        {
            return false;
        }

        slot = gridSlot ?? GetSlotFromItemMarker(item);
        if (slot == null || slot.Kind == SlotKind.Quick)
        {
            return false;
        }

        InventorySafety.SlotUnequipToInventoryRequests.Remove(item);
        UnequipInventorySlotsItem(player, item);
        return true;
    }

    private static bool TryGetEquipmentSlotForItem(Player player, Inventory inventory, ItemData item, out SlotDefinition? slot)
    {
        slot = null;
        if (player == null || inventory == null || item == null || !inventory.ContainsItem(item))
        {
            return false;
        }

        bool inDedicatedSlot = TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? gridSlot) && gridSlot!.Kind != SlotKind.Quick;
        bool customEquipped = IsInventorySlotsCustomEquipped(item);
        if (!inDedicatedSlot && !customEquipped)
        {
            return false;
        }

        slot = gridSlot ?? GetSlotFromItemMarker(item);
        return slot != null && slot.Kind != SlotKind.Quick && CanUseSpecialSlot(player, inventory, item, slot);
    }

    internal static bool TryHandleSlotItemDragOut(InventoryGui gui, InventoryGrid targetGrid, Vector2i pos, InventoryGrid.Modifier mod)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || gui == null || targetGrid == null || mod != InventoryGrid.Modifier.Select || gui.m_dragGo == null || gui.m_dragItem == null || gui.m_dragInventory == null)
        {
            return false;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (playerInventory == null || gui.m_dragInventory != playerInventory || !playerInventory.ContainsItem(gui.m_dragItem))
        {
            return false;
        }

        ItemData dragItem = gui.m_dragItem;
        if (!TryGetEquipmentSlotForItem(player, playerInventory, dragItem, out SlotDefinition? sourceSlot))
        {
            return false;
        }

        Inventory targetInventory = targetGrid.GetInventory();
        bool targetIsPlayerInventory = targetInventory == playerInventory;
        if (targetIsPlayerInventory)
        {
            if (TryGetSlotAtGridPos(playerInventory, pos, out SlotDefinition? targetSlot) && targetSlot!.Kind != SlotKind.Quick)
            {
                gui.SetupDragItem(null, null, 1);
                gui.UpdateCraftingPanel();
                return true;
            }

            if (!CanUseCell(player, playerInventory, dragItem, pos))
            {
                gui.SetupDragItem(null, null, 1);
                gui.UpdateCraftingPanel();
                return true;
            }
        }

        if (!TryQueueSlotUnequip(player, playerInventory, dragItem, sourceSlot!, targetIsPlayerInventory ? PendingSlotUnequipDestination.PlayerInventory : PendingSlotUnequipDestination.Container, targetInventory, pos, gui.m_dragAmount))
        {
            ((Character)player).Message(MessageHud.MessageType.Center, "$msg_inventoryfull", 0, null);
            gui.SetupDragItem(null, null, 1);
            gui.UpdateCraftingPanel();
            return true;
        }

        gui.SetupDragItem(null, null, 1);
        gui.UpdateCraftingPanel();
        return true;
    }

    internal static bool TryHandleSlotItemDropOutside(InventoryGui gui)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || gui == null || gui.m_dragGo == null || gui.m_dragItem == null || gui.m_dragInventory == null)
        {
            return false;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        ItemData dragItem = gui.m_dragItem;
        if (playerInventory == null || gui.m_dragInventory != playerInventory || !playerInventory.ContainsItem(dragItem))
        {
            return false;
        }

        if (!TryGetEquipmentSlotForItem(player, playerInventory, dragItem, out SlotDefinition? sourceSlot))
        {
            return false;
        }

        if (!TryQueueSlotUnequip(player, playerInventory, dragItem, sourceSlot!, PendingSlotUnequipDestination.DropOutside, null, dragItem.m_gridPos, gui.m_dragAmount))
        {
            gui.SetupDragItem(null, null, 1);
            gui.UpdateCraftingPanel();
            return true;
        }

        gui.m_moveItemEffects.Create(((Component)gui).transform.position, Quaternion.identity);
        gui.SetupDragItem(null, null, 1);
        gui.UpdateCraftingPanel();
        return true;
    }

    private static bool TryQueueSlotUnequip(Player player, Inventory playerInventory, ItemData item, SlotDefinition sourceSlot, PendingSlotUnequipDestination destination, Inventory? targetInventory, Vector2i targetPos, int amount)
    {
        if (player == null || playerInventory == null || item == null || sourceSlot == null || sourceSlot.Kind == SlotKind.Quick || !playerInventory.ContainsItem(item))
        {
            return false;
        }

        amount = Mathf.Clamp(amount, 1, item.m_stack);
        if (amount < item.m_stack)
        {
            return false;
        }

        if (destination == PendingSlotUnequipDestination.PlayerInventory)
        {
            if (!CanUseCell(player, playerInventory, item, targetPos))
            {
                return false;
            }

            ItemData? targetItem = playerInventory.GetItemAt(targetPos.x, targetPos.y);
            if (targetItem != null && targetItem != item)
            {
                ((Character)player).Message(MessageHud.MessageType.Center, "$msg_inventoryfull", 0, null);
                return false;
            }
        }
        else if (destination == PendingSlotUnequipDestination.Container)
        {
            if (targetInventory == null || targetInventory == playerInventory || !targetInventory.CanAddItem(item, amount))
            {
                return false;
            }
        }

        ClearSlotActionState(item);
        InventorySafety.PendingSlotUnequips[item] = new PendingSlotUnequip(sourceSlot, destination, targetInventory, targetPos, amount, Time.time);
        player.RemoveEquipAction(item);

        if (item.m_shared.m_equipDuration > 0f)
        {
            player.QueueUnequipAction(item);
        }
        else
        {
            ((Humanoid)player).UnequipItem(item, true);
        }

        return true;
    }

    internal static bool TryCompletePendingSlotUnequip(Player player, ItemData item)
    {
        if (player == null || item == null || !InventorySafety.PendingSlotUnequips.TryGetValue(item, out PendingSlotUnequip? pending))
        {
            return false;
        }

        ClearSlotActionState(item);
        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (playerInventory == null || !playerInventory.ContainsItem(item))
        {
            return true;
        }

        bool completed;
        bool wasSlotAutoEquipSuppressed = InventorySafety.SuppressSlotAutoEquip;
        InventorySafety.SuppressSlotAutoEquip = true;
        try
        {
            OnCustomEquipmentCompatUnequipping(player, item);
            item.m_equipped = false;
            ClearItemSlot(item);
            ForceClearEquipmentReference(player, item);

            completed = pending.Destination switch
            {
                PendingSlotUnequipDestination.PlayerInventory => CompletePendingSlotUnequipToInventory(player, playerInventory, item, pending.TargetPos),
                PendingSlotUnequipDestination.Container => CompletePendingSlotUnequipToContainer(player, playerInventory, item, pending.TargetInventory, pending.TargetPos, pending.Amount),
                PendingSlotUnequipDestination.DropOutside => CompletePendingSlotUnequipDropOutside(player, playerInventory, item, pending.Amount),
                _ => false
            };
        }
        finally
        {
            InventorySafety.SuppressSlotAutoEquip = wasSlotAutoEquipSuppressed;
        }

        if (!completed)
        {
            RestoreSlotItemAfterFailedExternalRemoval(player, playerInventory, item, pending.SourceSlot);
            ((Character)player).Message(MessageHud.MessageType.Center, "$msg_inventoryfull", 0, null);
        }

        playerInventory.Changed();
        RefreshExternalEquipmentEffects(player);
        RequestInventoryStateEnsure(player, InventoryStateEnsureReason.SlotAction, InventoryStateAuditLevel.SlotLight);
        return true;
    }

    private static bool CompletePendingSlotUnequipToInventory(Player player, Inventory inventory, ItemData item, Vector2i targetPos)
    {
        if (CanUseCell(player, inventory, item, targetPos))
        {
            ItemData? targetItem = inventory.GetItemAt(targetPos.x, targetPos.y);
            if (targetItem == null || targetItem == item)
            {
                item.m_gridPos = targetPos;
                return true;
            }
        }

        return TryMoveToFirstFreeRegularCell(player, inventory, item);
    }

    private static bool CompletePendingSlotUnequipToContainer(Player player, Inventory playerInventory, ItemData item, Inventory? targetInventory, Vector2i targetPos, int amount)
    {
        if (targetInventory == null || targetInventory == playerInventory || !targetInventory.CanAddItem(item, amount))
        {
            return false;
        }

        return targetInventory.MoveItemToThis(playerInventory, item, amount, targetPos.x, targetPos.y);
    }

    private static bool CompletePendingSlotUnequipDropOutside(Player player, Inventory inventory, ItemData item, int amount)
    {
        InventorySafety.HandlingSlotDropOutside = true;
        try
        {
            return player.DropItem(inventory, item, amount);
        }
        finally
        {
            InventorySafety.HandlingSlotDropOutside = false;
        }
    }

    internal static void RestoreSlotItemAfterFailedExternalRemoval(Player player, Inventory inventory, ItemData item, SlotDefinition? slot)
    {
        if (player == null || inventory == null || item == null || slot == null || !inventory.ContainsItem(item) || !CanUseSpecialSlot(player, inventory, item, slot))
        {
            return;
        }

        TryEquipIntoSlot(player, inventory, item, slot);
    }

    private static bool ForceClearEquipmentReference(Player player, ItemData item)
    {
        if (player == null || item == null)
        {
            return false;
        }

        Humanoid humanoid = player;
        bool changed = false;
        if (humanoid.m_rightItem == item)
        {
            humanoid.m_rightItem = null;
            changed = true;
        }

        if (humanoid.m_leftItem == item)
        {
            humanoid.m_leftItem = null;
            changed = true;
        }

        if (humanoid.m_chestItem == item)
        {
            humanoid.m_chestItem = null;
            changed = true;
        }

        if (humanoid.m_legItem == item)
        {
            humanoid.m_legItem = null;
            changed = true;
        }

        if (humanoid.m_ammoItem == item)
        {
            humanoid.m_ammoItem = null;
            changed = true;
        }

        if (humanoid.m_helmetItem == item)
        {
            humanoid.m_helmetItem = null;
            changed = true;
        }

        if (humanoid.m_shoulderItem == item)
        {
            humanoid.m_shoulderItem = null;
            changed = true;
        }

        if (humanoid.m_utilityItem == item)
        {
            humanoid.m_utilityItem = null;
            changed = true;
        }

        if (humanoid.m_trinketItem == item)
        {
            humanoid.m_trinketItem = null;
            changed = true;
        }

        if (item.m_equipped)
        {
            item.m_equipped = false;
            changed = true;
        }

        if (HasInventorySlotsSlot(item))
        {
            OnCustomEquipmentCompatUnequipping(player, item);
            ClearItemSlot(item);
            changed = true;
        }

        if (changed)
        {
            humanoid.SetupEquipment();
        }

        return changed;
    }

    private static SlotDefinition? GetSlotFromItemMarker(ItemData item)
    {
        if (item?.m_customData == null || !item.m_customData.TryGetValue(SlotIdKey, out string id))
        {
            return null;
        }

        return SlotDefinitions.FirstOrDefault(slot => string.Equals(slot.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool CompleteSlotUnequipToInventory(Player player, ItemData item)
    {
        if (player == null || item == null || !InventorySafety.SlotUnequipToInventoryRequests.Remove(item))
        {
            return false;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null || !inventory.ContainsItem(item))
        {
            return false;
        }

        if (!TryGetSlotAtGridPos(inventory, item.m_gridPos, out SlotDefinition? slot) || slot!.Kind == SlotKind.Quick)
        {
            return false;
        }

        item.m_equipped = false;
        OnCustomEquipmentCompatUnequipping(player, item);
        ClearItemSlot(item);
        if (!TryMoveToFirstFreeRegularCell(player, inventory, item))
        {
            TryEquipIntoSlot(player, inventory, item, slot);
            ((Character)player).Message(MessageHud.MessageType.Center, "$msg_inventoryfull", 0, null);
            return true;
        }

        ((Humanoid)player).SetupEquipment();
        inventory.Changed();
        RefreshExternalEquipmentEffects(player);
        return true;
    }

    private static void MarkItemSlot(Player player, ItemData item, SlotDefinition slot)
    {
        item.m_customData[SlotIdKey] = slot.Id;
        item.m_customData[EquippedByKey] = GetPlayerId(player);
        InvalidateCustomEquipmentProjectionCache();
    }

    internal static void ClearItemSlot(ItemData item)
    {
        if (item?.m_customData == null)
        {
            return;
        }

        item.m_customData.Remove(SlotIdKey);
        item.m_customData.Remove(EquippedByKey);
        InvalidateCustomEquipmentProjectionCache();
    }

    internal static void ClearSlotActionState(ItemData item)
    {
        if (item == null)
        {
            return;
        }

        InventorySafety.PendingSlotEquips.Remove(item);
        InventorySafety.PendingSlotUnequips.Remove(item);
        InventorySafety.SlotUnequipToInventoryRequests.Remove(item);
    }

    internal static void ClearCustomEquipmentState(ItemData item)
    {
        if (item == null)
        {
            return;
        }

        OnCustomEquipmentCompatUnequipping(Player.m_localPlayer, item);
        item.m_equipped = false;
        ClearItemSlot(item);
        ClearSlotActionState(item);
        RefreshExternalEquipmentEffects(Player.m_localPlayer);
    }

    internal static bool HasInventorySlotsSlot(ItemData? item)
    {
        return item != null && item.m_customData.ContainsKey(SlotIdKey);
    }

    internal static bool IsInventorySlotsCustomEquipped(ItemData? item)
    {
        return item != null && item.m_equipped && item.m_customData.ContainsKey(SlotIdKey);
    }

    private static bool CanAutoAdoptGridSlot(ItemData item, SlotDefinition slot)
    {
        string? markedSlotId = item.m_customData.TryGetValue(SlotIdKey, out string id) ? id : null;
        return InventorySlotSafetyCore.CanAutoAdoptGridSlot(IsInventorySlotsCustomEquipped(item), markedSlotId, slot.Id);
    }

    internal static bool CanAutoPlaceItemInSpecialSlot(Player player, Inventory inventory, ItemData item)
    {
        return SlotDefinitions.Any(slot => CanAutoPlaceItemInSpecialSlot(player, inventory, item, slot));
    }

    internal static bool TryAutoPlaceItemInSpecialSlot(Player player, Inventory inventory, ItemData item)
    {
        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (CanAutoPlaceItemInSpecialSlot(player, inventory, item, slot) &&
                TryEquipIntoSlot(player, inventory, item, slot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanAutoPlaceItemInSpecialSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        return CanUseEmptySpecialSlot(player, inventory, item, slot) &&
               CanAutoAdoptGridSlot(item, slot);
    }

}
