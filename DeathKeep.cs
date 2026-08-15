using System;
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
            OriginalEquipped = item.m_equipped;
            OriginalCustomData = item.m_customData == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(item.m_customData);
        }

        public ItemData Item { get; }
        public Vector2i OriginalGridPos { get; }
        public string OriginalSlotId { get; }
        public SlotKind? OriginalSlotKind { get; }
        public bool OriginalEquipped { get; }
        public Dictionary<string, string> OriginalCustomData { get; }
        public bool WasQuickSlot => OriginalSlotKind == SlotKind.Quick;
        public bool WasSpecialSlot => OriginalSlotKind != null;
    }

    internal sealed class TombStonePreparationState
    {
        public TombStonePreparationState(Inventory? sourceInventory, List<KeepOnDeathItemState> keptItems, bool deathDropUnequipPrepared)
        {
            SourceInventory = sourceInventory;
            KeptItems = keptItems;
            DeathDropUnequipPrepared = deathDropUnequipPrepared;
            DeathDropUnequipCompleted = !deathDropUnequipPrepared;
        }

        public Inventory? SourceInventory { get; }
        public List<KeepOnDeathItemState> KeptItems { get; }
        public bool DeathDropUnequipPrepared { get; }
        public bool DeathDropUnequipCompleted { get; set; }
        public bool Completed { get; set; }
    }

    internal static TombStonePreparationState PrepareCreateTombStone(Player player)
    {
        List<KeepOnDeathItemState> keptItems = PrepareKeepOnDeathItems(player, out Inventory? sourceInventory);
        try
        {
            bool deathDropUnequipPrepared = PreparePlayerDeathDropUnequip(player);
            return new TombStonePreparationState(sourceInventory, keptItems, deathDropUnequipPrepared);
        }
        catch (Exception preparationException)
        {
            try
            {
                RollbackPreparedKeepOnDeathItems(player, sourceInventory, keptItems);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Keep-on-death preparation failed and its removed items could not all be rolled back.",
                    preparationException,
                    rollbackException);
            }

            throw;
        }
    }

    internal static void CompleteCreateTombStone(Player player, TombStonePreparationState? state, bool finalAttempt)
    {
        if (state == null || state.Completed)
        {
            return;
        }

        bool temporarySuppression = state.DeathDropUnequipPrepared &&
                                    state.DeathDropUnequipCompleted &&
                                    state.KeptItems.Count > 0;
        if (temporarySuppression)
        {
            BeginSlotAutoEquipSuppression();
        }

        try
        {
            RestoreKeepOnDeathItems(player, state.KeptItems);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Keep-on-death restoration will retry after an unexpected failure: {ex.GetBaseException().Message}");
        }
        finally
        {
            try
            {
                if (finalAttempt && state.KeptItems.Count > 0)
                {
                    try
                    {
                        EmergencyPreserveKeepOnDeathItems(state.SourceInventory, state.KeptItems);
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"Final keep-on-death preservation encountered an unexpected failure: {ex.GetBaseException().Message}");
                    }
                }
            }
            finally
            {
                if (temporarySuppression)
                {
                    CompleteSlotAutoEquipSuppression();
                }
                else if (!state.DeathDropUnequipCompleted)
                {
                    CompletePlayerDeathDropUnequip(state.DeathDropUnequipPrepared);
                    state.DeathDropUnequipCompleted = true;
                }

                state.Completed = state.DeathDropUnequipCompleted && state.KeptItems.Count == 0;
            }
        }
    }

    internal static List<KeepOnDeathItemState> PrepareKeepOnDeathItems(Player player, out Inventory? sourceInventory)
    {
        sourceInventory = null;
        List<KeepOnDeathItemState> keptItems = new();
        if (!DeathKeepRulesEnabled() || player == null || player.m_isLoading || _yamlConfig.KeepOnDeath == null || _yamlConfig.KeepOnDeath.Count == 0)
        {
            return keptItems;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        sourceInventory = inventory;
        if (inventory == null)
        {
            return keptItems;
        }

        try
        {
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

                KeepOnDeathItemState state = new(item, originalGridPos, originalSlotId, originalSlotKind);
                if (inventory.m_inventory.Remove(item))
                {
                    keptItems.Add(state);
                }
            }
        }
        catch (Exception preparationException)
        {
            try
            {
                RollbackPreparedKeepOnDeathItems(player, inventory, keptItems);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Keep-on-death item collection failed and its removed items could not all be rolled back.",
                    preparationException,
                    rollbackException);
            }

            throw;
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
            return;
        }

        bool changed = false;
        EnsureInventoryHeightForLoad(inventory);
        int index = 0;
        while (index < keptItems.Count)
        {
            KeepOnDeathItemState state = keptItems[index];
            ItemData item = state.Item;
            if (item == null || item.m_shared == null)
            {
                Log.LogWarning("Deferred restoration of an invalid keep-on-death item so its reference is not discarded.");
                index++;
                continue;
            }

            bool safelyInInventory = inventory.ContainsItem(item);
            try
            {
                if (!safelyInInventory)
                {
                    _ = RestoreKeepOnDeathItem(player, inventory, state);
                    safelyInInventory = inventory.ContainsItem(item);
                }
            }
            catch (Exception ex)
            {
                safelyInInventory = inventory.ContainsItem(item);
                Log.LogWarning(
                    safelyInInventory
                        ? $"Keep-on-death item {item.m_shared?.m_name ?? "<unknown>"} reached the inventory despite a restore callback failure: {ex.GetBaseException().Message}"
                        : $"Deferred keep-on-death item {item.m_shared?.m_name ?? "<unknown>"} after a restore failure: {ex.GetBaseException().Message}");
            }

            if (!safelyInInventory)
            {
                index++;
                continue;
            }

            changed = true;
            keptItems.RemoveAt(index);
        }

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

    private static void RollbackPreparedKeepOnDeathItems(
        Player player,
        Inventory? inventory,
        List<KeepOnDeathItemState> keptItems)
    {
        if (keptItems.Count == 0)
        {
            return;
        }

        if (inventory == null)
        {
            throw new InvalidOperationException("The player inventory was unavailable while rolling back keep-on-death preparation.");
        }

        List<Exception> failures = new();
        foreach (KeepOnDeathItemState state in keptItems)
        {
            try
            {
                ItemData item = state.Item;
                if (!inventory.ContainsItem(item))
                {
                    inventory.m_inventory.Add(item);
                }

                if (!inventory.ContainsItem(item))
                {
                    throw new InvalidOperationException("The removed keep-on-death item was not restored to the player inventory.");
                }

                item.m_gridPos = state.OriginalGridPos;
                item.m_equipped = state.OriginalEquipped;
                item.m_customData ??= new Dictionary<string, string>();
                item.m_customData.Clear();
                foreach (KeyValuePair<string, string> entry in state.OriginalCustomData)
                {
                    item.m_customData[entry.Key] = entry.Value;
                }

            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        try
        {
            inventory.Changed();
            ((Humanoid)player).SetupEquipment();
            UpdateCustomEquipmentVisuals(player);
            RefreshExternalEquipmentEffects(player);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Keep-on-death items were rolled back, but equipment refresh failed: {ex.GetBaseException().Message}");
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("One or more removed keep-on-death items could not be rolled back.", failures);
        }

        keptItems.Clear();
    }

    private static void EmergencyPreserveKeepOnDeathItems(
        Inventory? inventory,
        List<KeepOnDeathItemState> keptItems)
    {
        if (inventory == null || keptItems.Count == 0)
        {
            return;
        }

        bool changed = false;
        int index = 0;
        while (index < keptItems.Count)
        {
            KeepOnDeathItemState state = keptItems[index];
            ItemData item = state.Item;
            if (item == null)
            {
                index++;
                continue;
            }

            try
            {
                if (!inventory.m_inventory.Contains(item))
                {
                    InventorySlotSafetyCore.GridCell cell = InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
                        inventory.GetWidth(),
                        inventory.GetHeight(),
                        new InventorySlotSafetyCore.GridCell(-1, -1),
                        (x, y) => inventory.m_inventory.Any(other =>
                            other != null &&
                            other != item &&
                            other.m_gridPos.x == x &&
                            other.m_gridPos.y == y));
                    item.m_gridPos = new Vector2i(cell.X, cell.Y);
                    item.m_equipped = false;
                    ClearItemSlot(item);
                    inventory.m_inventory.Add(item);
                }

                if (!inventory.m_inventory.Contains(item))
                {
                    index++;
                    continue;
                }

                changed = true;
                keptItems.RemoveAt(index);
            }
            catch (Exception ex)
            {
                Log.LogError($"Final keep-on-death fallback could not preserve {item.m_shared?.m_name ?? "<unknown>"}: {ex.GetBaseException().Message}");
                index++;
            }
        }

        if (!changed)
        {
            return;
        }

        try
        {
            inventory.Changed();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Final keep-on-death items are safe in the inventory, but its change callback failed: {ex.GetBaseException().Message}");
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

        return PreserveKeepOnDeathItemWithoutOverwriting(inventory, item);
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

    private static bool PreserveKeepOnDeathItemWithoutOverwriting(Inventory inventory, ItemData item)
    {
        EnsureInventoryHeightForLoad(inventory);
        InventorySlotSafetyCore.GridCell cell = InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
            inventory.GetWidth(),
            inventory.GetHeight(),
            new InventorySlotSafetyCore.GridCell(-1, -1),
            (x, y) => inventory.m_inventory.Any(other => other != null && other.m_gridPos.x == x && other.m_gridPos.y == y));
        item.m_gridPos = new Vector2i(cell.X, cell.Y);
        item.m_equipped = false;
        ClearItemSlot(item);

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
               CanUseCircletExtendedCustomSlot(player, item, slot) &&
               CanUseHipLanternCustomSlot(item, slot) &&
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
