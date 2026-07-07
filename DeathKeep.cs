using System.Collections.Generic;
using System.Linq;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal sealed class KeepOnDeathItemState
    {
        public KeepOnDeathItemState(ItemData item, Vector2i originalGridPos, string originalSlotId, SlotKind? originalSlotKind)
        {
            Item = item;
            OriginalGridPos = originalGridPos;
            OriginalSlotId = originalSlotId;
            OriginalSlotKind = originalSlotKind;
        }

        public ItemData Item { get; }
        public Vector2i OriginalGridPos { get; }
        public string OriginalSlotId { get; }
        public SlotKind? OriginalSlotKind { get; }
        public bool WasQuickSlot => OriginalSlotKind == SlotKind.Quick;
        public bool WasSpecialSlot => OriginalSlotKind != null;
    }

    internal sealed class TombStonePreparationState
    {
        public TombStonePreparationState(List<KeepOnDeathItemState> keptItems, bool deathDropUnequipPrepared)
        {
            KeptItems = keptItems;
            DeathDropUnequipPrepared = deathDropUnequipPrepared;
        }

        public List<KeepOnDeathItemState> KeptItems { get; }
        public bool DeathDropUnequipPrepared { get; }
        public bool Completed { get; set; }
    }

    internal static TombStonePreparationState PrepareCreateTombStone(Player player)
    {
        List<KeepOnDeathItemState> keptItems = PrepareKeepOnDeathItems(player);
        bool deathDropUnequipPrepared = PreparePlayerDeathDropUnequip(player);
        return new TombStonePreparationState(keptItems, deathDropUnequipPrepared);
    }

    internal static void CompleteCreateTombStone(Player player, TombStonePreparationState? state)
    {
        if (state == null || state.Completed)
        {
            return;
        }

        try
        {
            RestoreKeepOnDeathItems(player, state.KeptItems);
        }
        finally
        {
            CompletePlayerDeathDropUnequip(state.DeathDropUnequipPrepared);
            state.Completed = true;
        }
    }

    internal static List<KeepOnDeathItemState> PrepareKeepOnDeathItems(Player player)
    {
        List<KeepOnDeathItemState> keptItems = new();
        if (!DeathKeepRulesEnabled() || player == null || player.m_isLoading || _yamlConfig.KeepOnDeath == null || _yamlConfig.KeepOnDeath.Count == 0)
        {
            return keptItems;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return keptItems;
        }

        foreach (ItemData item in inventory.m_inventory.ToArray())
        {
            if (item?.m_shared == null || !ShouldKeepOnDeath(item))
            {
                continue;
            }

            Vector2i originalGridPos = item.m_gridPos;
            string originalSlotId = "";
            SlotKind? originalSlotKind = null;
            if (TryGetSlotAtGridPos(inventory, originalGridPos, out SlotDefinition? slot) && slot != null)
            {
                originalSlotId = slot.Id;
                originalSlotKind = slot.Kind;
            }

            if (inventory.m_inventory.Remove(item))
            {
                KeepOnDeathItemState state = new(item, originalGridPos, originalSlotId, originalSlotKind);
                keptItems.Add(state);
            }
        }

        return keptItems;
    }

    internal static void RestoreKeepOnDeathItems(Player player, List<KeepOnDeathItemState>? keptItems)
    {
        if (player == null || keptItems == null || keptItems.Count == 0)
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            keptItems.Clear();
            return;
        }

        bool changed = false;
        EnsureInventoryHeightForLoad(inventory);
        foreach (KeepOnDeathItemState state in keptItems)
        {
            ItemData item = state.Item;
            bool invalidItem = item?.m_shared == null;
            bool alreadyInInventory = !invalidItem && inventory.ContainsItem(item);
            if (invalidItem || alreadyInInventory)
            {
                continue;
            }

            bool restored = RestoreKeepOnDeathItem(player, inventory, state);
            changed |= restored;
        }

        keptItems.Clear();
        if (changed)
        {
            inventory.Changed();
            if (!player.m_isLoading)
            {
                EnsureInventoryState(player, InventoryStateEnsureReason.Tombstone);
            }

            ReloadEpicLootRuntimeItemData(player);
            RefreshExternalEquipmentEffects(player);
        }
    }

    private static bool RestoreKeepOnDeathItem(Player player, Inventory inventory, KeepOnDeathItemState state)
    {
        ItemData item = state.Item;
        Vector2i originalGridPos = state.OriginalGridPos;
        item.m_gridPos = originalGridPos;

        bool originalSlotAvailable = TryGetKeepOnDeathOriginalSlot(state, item, out SlotDefinition? originalSlot) &&
                                     CanRestoreKeepOnDeathItemToSlot(player, inventory, item, originalSlot!);
        bool emptyQuickSlotAvailable = TryFindKeepOnDeathEmptySpecialSlot(player, inventory, item, slot => slot.Kind == SlotKind.Quick, out SlotDefinition? emptyQuickSlot);
        SlotDefinition? emptySameSpecialKindSlot = null;
        bool emptySameSpecialKindSlotAvailable = !state.WasQuickSlot &&
                                                 TryFindKeepOnDeathEmptySpecialSlot(player, inventory, item, slot => slot.Kind == state.OriginalSlotKind, out emptySameSpecialKindSlot);
        bool originalCellAvailable = CanRestoreKeepOnDeathItemAtCell(player, inventory, item, originalGridPos, state);
        bool freeRegularCellAvailable = TryFindFreeRegularCell(player, inventory, out _);
        bool emptyNonQuickSpecialSlotAvailable = TryFindKeepOnDeathEmptySpecialSlot(player, inventory, item, slot => slot.Kind != SlotKind.Quick, out SlotDefinition? emptyNonQuickSpecialSlot);

        InventorySlotSafetyCore.KeepOnDeathRestorePlan plan = InventorySlotSafetyCore.SelectKeepOnDeathRestorePlan(
            new InventorySlotSafetyCore.KeepOnDeathRestoreOptions(
                state.WasSpecialSlot,
                state.WasQuickSlot,
                originalSlotAvailable,
                emptyQuickSlotAvailable,
                emptySameSpecialKindSlotAvailable,
                originalCellAvailable,
                freeRegularCellAvailable,
                emptyNonQuickSpecialSlotAvailable));

        switch (plan)
        {
            case InventorySlotSafetyCore.KeepOnDeathRestorePlan.OriginalSlot:
                return originalSlot != null && TryRestoreKeepOnDeathItemToSlot(player, inventory, item, originalSlot);
            case InventorySlotSafetyCore.KeepOnDeathRestorePlan.EmptyQuickSlot:
                return emptyQuickSlot != null && TryRestoreKeepOnDeathItemToSlot(player, inventory, item, emptyQuickSlot);
            case InventorySlotSafetyCore.KeepOnDeathRestorePlan.EmptySameSpecialKindSlot:
                return emptySameSpecialKindSlot != null && TryRestoreKeepOnDeathItemToSlot(player, inventory, item, emptySameSpecialKindSlot);
            case InventorySlotSafetyCore.KeepOnDeathRestorePlan.OriginalCell:
                return TryRestoreKeepOnDeathItemAtCell(player, inventory, item, originalGridPos, state);
            case InventorySlotSafetyCore.KeepOnDeathRestorePlan.FirstFreeRegularCell:
                item.m_equipped = false;
                ClearItemSlot(item);
                if (TryMoveToFirstFreeRegularCell(player, inventory, item))
                {
                    inventory.m_inventory.Add(item);
                    return true;
                }

                break;
            case InventorySlotSafetyCore.KeepOnDeathRestorePlan.EmptyNonQuickSpecialSlot:
                return emptyNonQuickSpecialSlot != null && TryRestoreKeepOnDeathItemToSlot(player, inventory, item, emptyNonQuickSpecialSlot);
        }

        return PreserveKeepOnDeathItemWithoutOverwriting(inventory, item, originalGridPos);
    }

    private static bool TryRestoreKeepOnDeathItemAtCell(Player player, Inventory inventory, ItemData item, Vector2i target, KeepOnDeathItemState state)
    {
        if (!CanRestoreKeepOnDeathItemAtCell(player, inventory, item, target, state))
        {
            return false;
        }

        if (inventory.GetItemAt(target.x, target.y) != null)
        {
            return false;
        }

        item.m_gridPos = target;
        inventory.m_inventory.Add(item);
        return true;
    }

    private static bool TryGetKeepOnDeathOriginalSlot(KeepOnDeathItemState state, ItemData item, out SlotDefinition? slot)
    {
        slot = null;
        if (string.IsNullOrWhiteSpace(state.OriginalSlotId) ||
            !TryGetSlotById(state.OriginalSlotId, out SlotDefinition? resolved) ||
            resolved == null ||
            !resolved.Accepts(item))
        {
            return false;
        }

        slot = resolved;
        return true;
    }

    private static bool CanRestoreKeepOnDeathItemToSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (slot == null || !CanUseKeepOnDeathSpecialSlot(player, inventory, item, slot))
        {
            return false;
        }

        Vector2i target = GetSlotGridPos(inventory, slot);
        return !IsOutOfBounds(inventory, target) &&
               inventory.GetItemAt(target.x, target.y) == null &&
               CanRestoreKeepOnDeathItemAtCell(player, inventory, item, target, expectedSlotId: slot.Id);
    }

    private static bool TryRestoreKeepOnDeathItemToSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (!CanRestoreKeepOnDeathItemToSlot(player, inventory, item, slot))
        {
            return false;
        }

        Vector2i target = GetSlotGridPos(inventory, slot);
        item.m_gridPos = target;
        inventory.m_inventory.Add(item);
        RestoreKeepOnDeathSlotState(player, inventory, item, slot);
        return true;
    }

    private static void RestoreKeepOnDeathSlotState(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (item == null || slot == null || slot.Kind == SlotKind.Quick)
        {
            return;
        }

        RestoreSlotEquipmentState(player, inventory, item, slot);
    }

    private static bool TryFindKeepOnDeathEmptySpecialSlot(Player player, Inventory inventory, ItemData item, System.Func<SlotDefinition, bool>? slotFilter, out SlotDefinition? foundSlot)
    {
        foundSlot = null;
        foreach (SlotDefinition slot in SlotDefinitions)
        {
            if (slotFilter != null && !slotFilter(slot))
            {
                continue;
            }

            if (!CanUseKeepOnDeathSpecialSlot(player, inventory, item, slot))
            {
                continue;
            }

            Vector2i target = GetSlotGridPos(inventory, slot);
            if (IsOutOfBounds(inventory, target) || inventory.GetItemAt(target.x, target.y) != null)
            {
                continue;
            }

            if (!CanRestoreKeepOnDeathItemAtCell(player, inventory, item, target, expectedSlotId: slot.Id))
            {
                continue;
            }

            foundSlot = slot;
            return true;
        }

        return false;
    }

    private static bool PreserveKeepOnDeathItemWithoutOverwriting(Inventory inventory, ItemData item, Vector2i originalGridPos)
    {
        EnsureInventoryHeightForLoad(inventory);
        InventorySlotSafetyCore.GridCell cell = InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
            inventory.GetWidth(),
            inventory.GetHeight(),
            new InventorySlotSafetyCore.GridCell(originalGridPos.x, originalGridPos.y),
            (x, y) => inventory.m_inventory.Any(other => other != null && other.m_gridPos.x == x && other.m_gridPos.y == y));
        item.m_gridPos = new Vector2i(cell.X, cell.Y);

        inventory.m_inventory.Add(item);
        Log.LogWarning($"Preserved keep-on-death item {item.m_shared?.m_name ?? "<unknown>"} at {FormatGridPos(item.m_gridPos)} because no regular inventory cell was free. No item was overwritten.");
        return true;
    }

    private static bool CanRestoreKeepOnDeathItemAtCell(Player player, Inventory inventory, ItemData item, Vector2i target, KeepOnDeathItemState state)
    {
        string expectedSlotId = state.WasSpecialSlot ? state.OriginalSlotId : "";
        return CanRestoreKeepOnDeathItemAtCell(player, inventory, item, target, expectedSlotId);
    }

    private static bool CanRestoreKeepOnDeathItemAtCell(Player player, Inventory inventory, ItemData item, Vector2i target, string expectedSlotId)
    {
        if (IsOutOfBounds(inventory, target))
        {
            return false;
        }

        if (TryGetSlotAtGridPos(inventory, target, out SlotDefinition? slot))
        {
            if (!string.IsNullOrWhiteSpace(expectedSlotId) &&
                !string.Equals(slot!.Id, expectedSlotId, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return CanUseKeepOnDeathSpecialSlot(player, inventory, item, slot!);
        }

        return IsUsableRegularCell(inventory, player, target);
    }

    private static bool CanUseKeepOnDeathSpecialSlot(Player player, Inventory inventory, ItemData item, SlotDefinition slot)
    {
        if (CanUseSpecialSlot(player, inventory, item, slot))
        {
            return true;
        }

        return item != null &&
               slot != null &&
               slot.Kind != SlotKind.Quick &&
               slot.Accepts(item) &&
               !IsJewelcraftingUtilityGemBlockedForSlot(item, slot) &&
               IsEquipmentSlotProgressionEnabled();
    }

    private static bool ShouldKeepOnDeath(ItemData item)
    {
        foreach (string token in _yamlConfig.KeepOnDeath ?? new List<string>())
        {
            if (KeepOnDeathTokenMatches(item, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeathKeepRulesEnabled() =>
        _deathKeepRulesEnabled == null || _deathKeepRulesEnabled.Value.IsOn();

    private static bool KeepOnDeathTokenMatches(ItemData item, string token)
    {
        return ItemMatchesYamlReferenceToken(item, token);
    }
}
