using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal sealed class EquipmentSlotUpgradeHumanoidSnapshot
{
    public EquipmentSlotUpgradeHumanoidSnapshot(Humanoid humanoid, ItemData item)
    {
        Right = ReferenceEquals(humanoid.m_rightItem, item);
        Left = ReferenceEquals(humanoid.m_leftItem, item);
        Chest = ReferenceEquals(humanoid.m_chestItem, item);
        Legs = ReferenceEquals(humanoid.m_legItem, item);
        Ammo = ReferenceEquals(humanoid.m_ammoItem, item);
        Helmet = ReferenceEquals(humanoid.m_helmetItem, item);
        Shoulder = ReferenceEquals(humanoid.m_shoulderItem, item);
        Utility = ReferenceEquals(humanoid.m_utilityItem, item);
        Trinket = ReferenceEquals(humanoid.m_trinketItem, item);
        HiddenLeft = ReferenceEquals(humanoid.m_hiddenLeftItem, item);
        HiddenRight = ReferenceEquals(humanoid.m_hiddenRightItem, item);
        EquippedFlag = item.m_equipped;
    }

    public bool Right { get; }
    public bool Left { get; }
    public bool Chest { get; }
    public bool Legs { get; }
    public bool Ammo { get; }
    public bool Helmet { get; }
    public bool Shoulder { get; }
    public bool Utility { get; }
    public bool Trinket { get; }
    public bool HiddenLeft { get; }
    public bool HiddenRight { get; }
    public bool EquippedFlag { get; }
    public bool Restore(Humanoid humanoid, ItemData item)
    {
        bool changed = false;
        changed |= RestoreReference(ref humanoid.m_rightItem, item, Right);
        changed |= RestoreReference(ref humanoid.m_leftItem, item, Left);
        changed |= RestoreReference(ref humanoid.m_chestItem, item, Chest);
        changed |= RestoreReference(ref humanoid.m_legItem, item, Legs);
        changed |= RestoreReference(ref humanoid.m_ammoItem, item, Ammo);
        changed |= RestoreReference(ref humanoid.m_helmetItem, item, Helmet);
        changed |= RestoreReference(ref humanoid.m_shoulderItem, item, Shoulder);
        changed |= RestoreReference(ref humanoid.m_utilityItem, item, Utility);
        changed |= RestoreReference(ref humanoid.m_trinketItem, item, Trinket);
        changed |= RestoreReference(ref humanoid.m_hiddenLeftItem, item, HiddenLeft);
        changed |= RestoreReference(ref humanoid.m_hiddenRightItem, item, HiddenRight);
        return changed;
    }

    public bool Matches(Humanoid humanoid, ItemData item)
    {
        return MatchesReference(humanoid.m_rightItem, item, Right) &&
               MatchesReference(humanoid.m_leftItem, item, Left) &&
               MatchesReference(humanoid.m_chestItem, item, Chest) &&
               MatchesReference(humanoid.m_legItem, item, Legs) &&
               MatchesReference(humanoid.m_ammoItem, item, Ammo) &&
               MatchesReference(humanoid.m_helmetItem, item, Helmet) &&
               MatchesReference(humanoid.m_shoulderItem, item, Shoulder) &&
               MatchesReference(humanoid.m_utilityItem, item, Utility) &&
               MatchesReference(humanoid.m_trinketItem, item, Trinket) &&
               MatchesReference(humanoid.m_hiddenLeftItem, item, HiddenLeft) &&
               MatchesReference(humanoid.m_hiddenRightItem, item, HiddenRight);
    }

    private static bool RestoreReference(
        ref ItemData? current,
        ItemData item,
        bool wasOriginal)
    {
        if (wasOriginal)
        {
            if (ReferenceEquals(current, item))
            {
                return false;
            }

            current = item;
            return true;
        }

        if (!ReferenceEquals(current, item))
        {
            return false;
        }

        current = null;
        return true;
    }

    private static bool MatchesReference(
        ItemData? current,
        ItemData item,
        bool wasOriginal) =>
        ReferenceEquals(current, item) == wasOriginal;
}

internal sealed class EquipmentSlotUpgradeTransaction
{
    public EquipmentSlotUpgradeTransaction(
        Player player,
        Inventory inventory,
        ItemData originalItem,
        ItemData originalSnapshot,
        ItemData expectedSlotItem,
        SlotDefinition originalSlot,
        string expectedPrefab,
        int expectedQuality,
        int expectedVariant,
        HashSet<ItemData> initialItems,
        EquipmentSlotUpgradeTransaction? previous)
    {
        Player = player;
        Inventory = inventory;
        OriginalItem = originalItem;
        OriginalSnapshot = originalSnapshot;
        ExpectedSlotItem = expectedSlotItem;
        OriginalSlot = originalSlot;
        OriginalPosition = originalItem.m_gridPos;
        ExpectedPrefab = expectedPrefab;
        ExpectedQuality = expectedQuality;
        ExpectedVariant = expectedVariant;
        InitialItems = initialItems;
        HumanoidSnapshot = new EquipmentSlotUpgradeHumanoidSnapshot(player, originalItem);
        Previous = previous;
    }

    public Player Player { get; }
    public Inventory Inventory { get; }
    public ItemData OriginalItem { get; }
    public ItemData OriginalSnapshot { get; }
    public ItemData ExpectedSlotItem { get; }
    public SlotDefinition OriginalSlot { get; }
    public Vector2i OriginalPosition { get; }
    public string ExpectedPrefab { get; }
    public int ExpectedQuality { get; set; }
    public int ExpectedVariant { get; set; }
    public HashSet<ItemData> InitialItems { get; }
    public EquipmentSlotUpgradeHumanoidSnapshot HumanoidSnapshot { get; }
    public EquipmentSlotUpgradeTransaction? Previous { get; }
    public bool ReplacementAddAttempted { get; set; }
    public bool ReplacementAddInProgress { get; set; }
    public bool ReplacementFinalized { get; set; }
    public bool CapacityProbeObserved { get; set; }
    public bool CapacityCellClaimed { get; set; }
    public string CapacityProbeState { get; set; } = "not observed";
    public bool ReplacementInsertObserved { get; set; }
    public bool ReplacementInsertAllowed { get; set; }
    public string ReplacementInsertState { get; set; } = "not observed";
    public bool Committed { get; set; }
    public bool RolledBack { get; set; }
    public bool RollbackInProgress { get; set; }
    public bool Closed { get; set; }
    public ItemData? ReplacementResult { get; set; }
}

internal sealed class EquipmentSlotUpgradeReplacementAddScope
{
    public EquipmentSlotUpgradeReplacementAddScope(EquipmentSlotUpgradeTransaction transaction)
    {
        Transaction = transaction;
    }

    public EquipmentSlotUpgradeTransaction Transaction { get; }
    public bool Released { get; set; }
    public bool Finalized { get; set; }
}

public sealed partial class InventorySlotsPlugin
{
    [ThreadStatic]
    private static EquipmentSlotUpgradeTransaction? _activeEquipmentSlotUpgradeTransaction;

    internal static EquipmentSlotUpgradeTransaction? BeginEquipmentSlotUpgradeTransaction(
        InventoryGui gui,
        Player player,
        out bool abortCrafting)
    {
        abortCrafting = false;
        bool protectionRequired = false;
        try
        {
            if (gui == null ||
                player == null ||
                player != Player.m_localPlayer)
            {
                return null;
            }

            ItemData? original = gui.m_craftUpgradeItem;
            Recipe? recipe = gui.m_craftRecipe;
            ItemData? recipeItem = recipe?.m_item?.m_itemData;
            Inventory? inventory = player.GetInventory();
            if (original == null ||
                inventory == null ||
                !inventory.ContainsItem(original))
            {
                return null;
            }

            // Only cells that ordinary automatic placement can use are safe to
            // leave to vanilla. A locked or externally reserved cell may still be
            // inside the fixed regular-row range, but vanilla cannot reliably put
            // the replacement back there after removing the original.
            protectionRequired = true;
            if (IsUsableRegularCell(inventory, player, original.m_gridPos))
            {
                protectionRequired = false;
                return null;
            }

            // These operations own their replacement semantics. Evaluate them only
            // after arming fail-closed protection so a failing compatibility probe
            // cannot expose an InventorySlots cell to vanilla's remove-first path.
            // Jewelcrafting owns InventoryGui.DoCrafting while its socket tab is
            // selected. Its state probe first rejects either selected vanilla tab,
            // so a stale socket button cannot exempt a normal vanilla upgrade.
            // Recycle_N_Reclaim does not use DoCrafting and must not be excluded:
            // its frame-cached tab state can otherwise leak into a same-frame upgrade.
            if (IsJewelcraftingSocketTabActive(gui))
            {
                return null;
            }

            bool slotResolved =
                TryGetSlotAtGridPos(
                    inventory,
                    original.m_gridPos,
                    out SlotDefinition? slot) &&
                slot != null;

            // m_craftUpgradeItem and exact inventory ownership are the authoritative
            // mutation state. UI tab/adapter state can change while the craft timer is
            // running, so it must not decide whether item-loss protection is active.
            // Once an InventorySlots cell is involved, every failure below must stop
            // DoCrafting before vanilla removes the original item.
            if (!slotResolved ||
                original.m_shared == null ||
                recipeItem?.m_shared == null ||
                original.m_shared.m_maxStackSize > 1 ||
                recipeItem.m_shared.m_maxStackSize > 1 ||
                inventory.GetItemAt(original.m_gridPos.x, original.m_gridPos.y) != original ||
                !IsEquipmentSlotUpgradeItemEquipped(player, inventory, original, slot!))
            {
                abortCrafting = true;
                NotifyUnsafeEquipmentSlotUpgradeCanceled(
                    player,
                    "the selected item or slot state could not be validated");
                return null;
            }

            string originalPrefab = GetItemPrefabName(original);
            // Match the exact prefab identity that vanilla passes to Inventory.AddItem.
            // Recipe template ItemData does not reliably have m_dropPrefab populated,
            // so deriving this from recipeItem can reject every legitimate upgrade.
            string expectedPrefab = recipe?.m_item != null
                ? CleanPrefabName(recipe.m_item.gameObject.name)
                : "";
            int expectedQuality = original.m_quality + 1;
            if (string.IsNullOrWhiteSpace(originalPrefab) ||
                !string.Equals(originalPrefab, expectedPrefab, StringComparison.OrdinalIgnoreCase) ||
                expectedQuality > recipeItem.m_shared.m_maxQuality)
            {
                abortCrafting = true;
                NotifyUnsafeEquipmentSlotUpgradeCanceled(
                    player,
                    "the selected recipe did not describe the expected one-level replacement " +
                    $"(item='{originalPrefab}', output='{expectedPrefab}', " +
                    $"quality={original.m_quality}->{expectedQuality}, max={recipeItem.m_shared.m_maxQuality})");
                return null;
            }

            if (!CanUseSpecialSlot(player, inventory, original, slot!))
            {
                abortCrafting = true;
                NotifyUnsafeEquipmentSlotUpgradeCanceled(
                    player,
                    "the original equipment slot was not eligible before removal");
                return null;
            }

            ItemData originalSnapshot = original.Clone();
            ItemData expectedSlotItem = originalSnapshot.Clone();
            expectedSlotItem.m_quality = expectedQuality;
            expectedSlotItem.m_variant = original.m_variant;
            HashSet<ItemData> initialItems = new(inventory.m_inventory.Where(item => item != null));
            EquipmentSlotUpgradeTransaction transaction = new(
                player,
                inventory,
                original,
                originalSnapshot,
                expectedSlotItem,
                slot!,
                expectedPrefab,
                expectedQuality,
                original.m_variant,
                initialItems,
                _activeEquipmentSlotUpgradeTransaction);
            _activeEquipmentSlotUpgradeTransaction = transaction;
            return transaction;
        }
        catch (Exception ex)
        {
            abortCrafting = protectionRequired;
            if (abortCrafting)
            {
                NotifyUnsafeEquipmentSlotUpgradeCanceled(
                    player,
                    $"the item-safety snapshot could not be created: {ex}");
            }
            else
            {
                Log.LogWarning($"Could not evaluate equipment-slot upgrade protection: {ex.Message}");
            }

            return null;
        }
    }

    private static void NotifyUnsafeEquipmentSlotUpgradeCanceled(
        Player player,
        string reason)
    {
        Log.LogError($"Canceled an unsafe equipment-slot upgrade because {reason}.");
        try
        {
            ((Character)player).Message(
                MessageHud.MessageType.Center,
                LocalizeUi(
                    "$inventoryslots_upgrade_safety_canceled",
                    "Upgrade canceled because the equipped item could not be returned to its slot safely."),
                0,
                null);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Could not show the equipment-upgrade safety message: {ex.Message}");
        }
    }

    internal static void CompleteEquipmentSlotUpgradeTransaction(
        EquipmentSlotUpgradeTransaction? transaction,
        Exception? exception = null)
    {
        if (transaction == null || transaction.Closed)
        {
            return;
        }

        try
        {
            if (!transaction.ReplacementAddAttempted &&
                transaction.Inventory.ContainsItem(transaction.OriginalItem) &&
                !IsEquipmentSlotUpgradeOriginalStateIntact(transaction) &&
                !TryRestoreExistingEquipmentSlotUpgradeOriginalState(transaction))
            {
                TryRollbackEquipmentSlotUpgrade(
                    transaction,
                    "the original item remained but its slot state could not be restored");
            }

            if (!transaction.ReplacementAddAttempted &&
                !transaction.RolledBack &&
                !transaction.Inventory.ContainsItem(transaction.OriginalItem))
            {
                if (TryAdoptUnobservedEquipmentSlotUpgradeResult(
                        transaction,
                        out bool matchingResultExists))
                {
                    // A higher-priority compatibility prefix can replace or skip
                    // vanilla's positional AddItem before our prefix observes it.
                    // This is observed only in DoCrafting's finalizer, after resource
                    // consumption may already have happened. Preserve a unique owned
                    // result instead of applying the pre-cost rollback policy.
                    transaction.ReplacementAddAttempted = true;
                    transaction.ReplacementFinalized = true;
                    TryCommitPostCostUnobservedEquipmentSlotUpgradeResult(
                        transaction,
                        exception);
                }
                else if (matchingResultExists)
                {
                    // More than one indistinguishable new result cannot be tied to
                    // this transaction safely. Do not add the original as another
                    // copy; preserve the existing results and close fail-closed.
                    Log.LogError(
                        "Equipment upgrade produced multiple untracked matching results; " +
                        "the original was not restored to avoid duplication.");
                    return;
                }
            }

            if (transaction.ReplacementAddAttempted &&
                !transaction.ReplacementFinalized &&
                !transaction.Committed &&
                !transaction.RolledBack)
            {
                ItemData? result = FindEquipmentSlotUpgradeResult(transaction);
                FinalizeEquipmentSlotUpgradeResult(transaction, ref result, exception);
            }

            if (transaction.ReplacementAddAttempted &&
                !transaction.Committed &&
                !transaction.RolledBack)
            {
                TryRollbackEquipmentSlotUpgrade(transaction, "completion recovery");
            }

            if (!transaction.Committed &&
                !transaction.RolledBack &&
                !transaction.Inventory.ContainsItem(transaction.OriginalItem))
            {
                TryRollbackEquipmentSlotUpgrade(
                    transaction,
                    "crafting completed without a verifiable replacement");
            }

            if (InventorySlotSafetyCore.ShouldRollbackInterruptedEquipmentUpgrade(
                    transactionActive: !transaction.Closed,
                    craftingThrew: exception != null,
                    replacementAddAttempted: transaction.ReplacementAddAttempted,
                    originalStillPresent: transaction.Inventory.ContainsItem(transaction.OriginalItem)))
            {
                TryRollbackEquipmentSlotUpgrade(transaction, "crafting was interrupted after removing the original item");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Equipment-slot upgrade protection completion failed: {ex.Message}");
            TryRollbackEquipmentSlotUpgrade(transaction, "completion failed");
        }
        finally
        {
            if (!transaction.Committed &&
                !transaction.RolledBack &&
                (transaction.ReplacementAddAttempted || exception != null) &&
                EnsureEquipmentSlotUpgradeOriginalOwnership(transaction))
            {
                TryNotifyEquipmentSlotUpgradeInventoryChanged(transaction);
                RequestInventoryStateEnsure(
                    transaction.Player,
                    InventoryStateEnsureReason.InventoryChanged,
                    InventoryStateAuditLevel.FullIntegrity);
            }

            transaction.ReplacementAddInProgress = false;
            transaction.Closed = true;
            if (ReferenceEquals(_activeEquipmentSlotUpgradeTransaction, transaction))
            {
                _activeEquipmentSlotUpgradeTransaction = transaction.Previous;
            }
        }
    }

    internal static EquipmentSlotUpgradeReplacementAddScope? BeginEquipmentSlotUpgradeReplacementAdd(
        Inventory inventory,
        string name,
        int stack,
        int quality,
        int variant,
        Vector2i position)
    {
        EquipmentSlotUpgradeTransaction? transaction = _activeEquipmentSlotUpgradeTransaction;
        if (transaction == null ||
            transaction.Closed ||
            transaction.ReplacementAddAttempted ||
            transaction.ReplacementAddInProgress ||
            !ReferenceEquals(inventory, transaction.Inventory) ||
            inventory.ContainsItem(transaction.OriginalItem) ||
            stack != 1 ||
            quality <= 0 ||
            variant < 0 ||
            position != transaction.OriginalPosition ||
            !string.Equals(CleanPrefabName(name), transaction.ExpectedPrefab, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        transaction.ExpectedQuality = quality;
        transaction.ExpectedVariant = variant;
        transaction.ExpectedSlotItem.m_quality = quality;
        transaction.ExpectedSlotItem.m_variant = variant;
        transaction.ReplacementAddAttempted = true;
        transaction.ReplacementAddInProgress = true;
        return new EquipmentSlotUpgradeReplacementAddScope(transaction);
    }

    internal static void ReleaseEquipmentSlotUpgradeReplacementAdd(
        EquipmentSlotUpgradeReplacementAddScope? scope)
    {
        if (scope == null || scope.Released)
        {
            return;
        }

        scope.Released = true;
        scope.Transaction.ReplacementAddInProgress = false;
    }

    internal static void FinalizeEquipmentSlotUpgradeReplacementAdd(
        EquipmentSlotUpgradeReplacementAddScope? scope,
        ref ItemData? result,
        Exception? exception)
    {
        if (scope == null || scope.Finalized)
        {
            return;
        }

        scope.Finalized = true;
        ReleaseEquipmentSlotUpgradeReplacementAdd(scope);
        EquipmentSlotUpgradeTransaction transaction = scope.Transaction;
        transaction.ReplacementFinalized = true;
        try
        {
            FinalizeEquipmentSlotUpgradeResult(transaction, ref result, exception);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Equipment-slot upgrade result validation failed: {ex.Message}");
            result = null;
            TryRollbackEquipmentSlotUpgrade(transaction, "result validation failed");
        }
    }

    internal static bool TryUseEquipmentSlotUpgradeReplacementCell(
        Inventory inventory,
        out Vector2i position)
    {
        position = new Vector2i(-1, -1);
        EquipmentSlotUpgradeTransaction? transaction = _activeEquipmentSlotUpgradeTransaction;
        if (transaction == null ||
            transaction.Closed ||
            !transaction.ReplacementAddInProgress ||
            !ReferenceEquals(inventory, transaction.Inventory))
        {
            return false;
        }

        transaction.CapacityProbeObserved = true;
        Vector2i captured = transaction.OriginalPosition;
        // Vanilla performs a generic FindEmptySlot capacity probe before it creates
        // the positional replacement. The transaction already validated this exact
        // equipment slot and output identity before removing the original, so expose
        // only the newly emptied captured cell here. The nested positional AddItem
        // still performs the full live compatibility validation before ownership can
        // change, keeping this capacity-only claim fail-closed.
        bool originalPresent = inventory.ContainsItem(transaction.OriginalItem);
        bool inBounds = captured.x >= 0 &&
                        captured.y >= 0 &&
                        captured.x < inventory.GetWidth() &&
                        captured.y < inventory.GetHeight();
        ItemData? blocker = inBounds
            ? inventory.GetItemAt(captured.x, captured.y)
            : null;
        transaction.CapacityProbeState =
            $"originalPresent={originalPresent}, inBounds={inBounds}, " +
            $"size={inventory.GetWidth()}x{inventory.GetHeight()}, " +
            $"blocker={(blocker == null ? "<none>" : GetItemPrefabName(blocker))}";
        if (originalPresent || !inBounds || blocker != null)
        {
            return false;
        }

        transaction.CapacityCellClaimed = true;
        position = captured;
        return true;
    }

    internal static Vector2i OverrideEquipmentSlotUpgradeCapacityResult(
        Vector2i originalResult,
        Inventory inventory)
    {
        if (originalResult.x != -1)
        {
            return originalResult;
        }

        return TryUseEquipmentSlotUpgradeReplacementCell(inventory, out Vector2i replacementPosition)
            ? replacementPosition
            : originalResult;
    }

    private static bool TryGetEquipmentSlotUpgradeOriginalSlot(
        EquipmentSlotUpgradeTransaction transaction,
        Inventory inventory,
        Vector2i position,
        out SlotDefinition? slot)
    {
        slot = null;
        return position == transaction.OriginalPosition &&
               TryGetSlotAtGridPos(inventory, position, out slot) &&
               slot != null &&
               string.Equals(slot.Id, transaction.OriginalSlot.Id, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryValidateEquipmentSlotUpgradeReplacementInsert(
        Inventory inventory,
        Player player,
        ItemData item,
        Vector2i position,
        out bool allowOriginal)
    {
        allowOriginal = false;
        EquipmentSlotUpgradeTransaction? transaction = _activeEquipmentSlotUpgradeTransaction;
        if (transaction == null ||
            transaction.Closed ||
            !transaction.ReplacementAddInProgress ||
            !ReferenceEquals(inventory, transaction.Inventory))
        {
            return false;
        }

        transaction.ReplacementInsertObserved = true;
        bool replacementSlotEligible = CanUseCapturedEquipmentUpgradeSlot(
            transaction,
            inventory,
            position,
            item,
            out _);
        bool expectedResult = MatchesExpectedEquipmentSlotUpgradeResult(transaction, item);
        bool resultIsNew = !transaction.InitialItems.Contains(item);
        if (expectedResult &&
            replacementSlotEligible &&
            resultIsNew &&
            transaction.ReplacementResult == null)
        {
            transaction.ReplacementResult = item;
        }

        bool originalRemoved = !inventory.ContainsItem(transaction.OriginalItem);
        bool originalCellEmpty = inventory.GetItemAt(position.x, position.y) == null;
        allowOriginal = InventorySlotSafetyCore.CanReuseOriginalEquipmentSlotForUpgrade(
            transactionActive: true,
            matchingReplacementAdd: expectedResult && replacementSlotEligible && resultIsNew,
            sameInventory: ReferenceEquals(player, transaction.Player),
            originalWasEquipmentSlot: replacementSlotEligible,
            originalRemoved,
            originalCellEmpty,
            slotAcceptsResult: replacementSlotEligible);
        transaction.ReplacementInsertAllowed = allowOriginal;
        transaction.ReplacementInsertState =
            $"expected={expectedResult}, slotEligible={replacementSlotEligible}, " +
            $"new={resultIsNew}, originalRemoved={originalRemoved}, cellEmpty={originalCellEmpty}, " +
            $"position={position.x},{position.y}";
        return true;
    }

    private static bool CanUseCapturedEquipmentUpgradeSlot(
        EquipmentSlotUpgradeTransaction transaction,
        Inventory inventory,
        Vector2i position,
        ItemData item,
        out SlotDefinition? slot)
    {
        if (!TryGetEquipmentSlotUpgradeOriginalSlot(
                transaction,
                inventory,
                position,
                out slot) ||
            slot == null ||
            !IsItemCompatibleWithSpecialSlot(transaction.Player, item, slot))
        {
            return false;
        }

        // Equipment-slot progression may be unlocked solely by the item that
        // vanilla has just removed for this upgrade. The transaction already
        // captured a valid, equipped slot before removal, so re-evaluating that
        // occupancy-dependent unlock here would create a false full-inventory
        // failure. Quick-slot progression is independent of item occupancy and
        // remains safe to revalidate live.
        return slot.Kind != SlotKind.Quick || IsQuickSlotUnlocked(transaction.Player, slot);
    }

    private static void FinalizeEquipmentSlotUpgradeResult(
        EquipmentSlotUpgradeTransaction transaction,
        ref ItemData? result,
        Exception? exception)
    {
        if (transaction.Committed || transaction.RolledBack)
        {
            return;
        }

        ItemData? rawResult = result;
        ItemData? candidate = FindEquipmentSlotUpgradeResult(transaction, rawResult);
        if (exception == null && candidate != null)
        {
            TryReturnEquipmentSlotUpgradeResultToOriginalSlot(transaction, candidate);
        }

        InventorySlotSafetyCore.EquipmentUpgradeCompletionPlan plan =
            SelectEquipmentSlotUpgradeCompletionPlan(transaction, candidate);

        if (exception == null && plan == InventorySlotSafetyCore.EquipmentUpgradeCompletionPlan.Commit)
        {
            transaction.ReplacementResult = candidate;
            // This exact AddItem boundary is the last point before vanilla consumes
            // resources. Commit here once the new item is owned, in the original cell,
            // and equipped. A later crafting exception must never resurrect the original
            // and create an item/resource split-brain.
            transaction.Committed = true;
            result = candidate;
            return;
        }

        string rollbackReason = exception == null
            ? "replacement was not committed to its original slot: " +
              DescribeEquipmentSlotUpgradeResultState(transaction, candidate, rawResult)
            : "replacement add threw an exception";
        result = null;
        TryRollbackEquipmentSlotUpgrade(
            transaction,
            rollbackReason);
    }

    private static string DescribeEquipmentSlotUpgradeResultState(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData? candidate,
        ItemData? rawResult)
    {
        try
        {
            bool owned = candidate != null && transaction.Inventory.ContainsItem(candidate);
            bool isNew = owned && !transaction.InitialItems.Contains(candidate!);
            bool matches = owned && MatchesExpectedEquipmentSlotUpgradeResult(transaction, candidate!);
            bool soleOriginalCell = owned && IsEquipmentSlotUpgradeItemSoleCellOccupant(
                transaction.Inventory,
                candidate!,
                transaction.OriginalPosition);
            ItemData? slotItem = FindItemForSlot(
                transaction.Player,
                transaction.Inventory,
                transaction.OriginalSlot);
            bool equipped = owned && IsEquipmentSlotUpgradeResultEquipped(transaction, candidate!);
            bool slotRef = candidate != null && ReferenceEquals(slotItem, candidate);
            string candidatePosition = candidate == null
                ? "<none>"
                : $"{candidate.m_gridPos.x},{candidate.m_gridPos.y}";
            return
                $"raw={(rawResult == null ? "<null>" : GetItemPrefabName(rawResult))}, " +
                $"candidate={(candidate == null ? "<null>" : GetItemPrefabName(candidate))}, " +
                $"owned={owned}, new={isNew}, matches={matches}, " +
                $"position={candidatePosition}, expected={transaction.OriginalPosition.x},{transaction.OriginalPosition.y}, " +
                $"soleCell={soleOriginalCell}, slotRef={slotRef}, " +
                $"candidateEquippedFlag={(candidate == null ? "<none>" : candidate.m_equipped.ToString())}, " +
                $"equipped={equipped}, originalEquippedAtStart={transaction.HumanoidSnapshot.EquippedFlag}, " +
                $"capacityObserved={transaction.CapacityProbeObserved}, capacityClaimed={transaction.CapacityCellClaimed} " +
                $"({transaction.CapacityProbeState}), insertObserved={transaction.ReplacementInsertObserved}, " +
                $"insertAllowed={transaction.ReplacementInsertAllowed} ({transaction.ReplacementInsertState})";
        }
        catch (Exception ex)
        {
            return $"diagnostic unavailable ({ex.Message})";
        }
    }

    private static InventorySlotSafetyCore.EquipmentUpgradeCompletionPlan SelectEquipmentSlotUpgradeCompletionPlan(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData? candidate)
    {
        bool resultExists = candidate != null && transaction.Inventory.ContainsItem(candidate);
        bool resultIsNew = resultExists && !transaction.InitialItems.Contains(candidate!);
        bool resultMatchesExpected = resultExists &&
                                     MatchesExpectedEquipmentSlotUpgradeResult(transaction, candidate!);
        bool resultInOriginalCell = resultExists &&
                                    IsEquipmentSlotUpgradeItemSoleCellOccupant(
                                        transaction.Inventory,
                                        candidate!,
                                        transaction.OriginalPosition);
        bool resultEquipped = resultInOriginalCell &&
                              IsEquipmentSlotUpgradeResultEquipped(transaction, candidate!);
        return InventorySlotSafetyCore.SelectEquipmentUpgradeCompletionPlan(
            transactionActive: !transaction.Closed,
            replacementAddAttempted: transaction.ReplacementAddAttempted,
            originalStillPresent: transaction.Inventory.ContainsItem(transaction.OriginalItem),
            resultExists,
            resultIsNew,
            resultMatchesExpected,
            resultInOriginalCell,
            resultEquippedInOriginalSlot: resultEquipped);
    }

    private static bool IsEquipmentSlotUpgradeItemSoleCellOccupant(
        Inventory inventory,
        ItemData item,
        Vector2i position)
    {
        return item.m_gridPos == position &&
               inventory.GetItemAt(position.x, position.y) == item &&
               CellContainsOnly(inventory, position, item);
    }

    private static ItemData? FindEquipmentSlotUpgradeResult(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData? preferred = null)
    {
        ItemData? tracked = transaction.ReplacementResult;
        bool trackedValid = tracked != null &&
                            transaction.Inventory.ContainsItem(tracked) &&
                            !transaction.InitialItems.Contains(tracked) &&
                            MatchesExpectedEquipmentSlotUpgradeResult(transaction, tracked);
        bool preferredValid = preferred != null &&
                              transaction.Inventory.ContainsItem(preferred) &&
                              !transaction.InitialItems.Contains(preferred) &&
                              MatchesExpectedEquipmentSlotUpgradeResult(transaction, preferred);

        // The inner positional AddItem prefix sees the candidate before the game
        // actually owns it. If a later compatibility prefix substitutes the insert,
        // replace that stale reference with the outer AddItem's causal result.
        if (preferredValid && !trackedValid)
        {
            transaction.ReplacementResult = preferred;
            tracked = preferred;
            trackedValid = true;
        }

        return trackedValid ? tracked : null;
    }

    private static bool TryAdoptUnobservedEquipmentSlotUpgradeResult(
        EquipmentSlotUpgradeTransaction transaction,
        out bool matchingResultExists)
    {
        matchingResultExists = false;
        ItemData? candidate = null;
        foreach (ItemData item in transaction.Inventory.m_inventory)
        {
            if (item == null ||
                transaction.InitialItems.Contains(item) ||
                !MatchesExpectedEquipmentSlotUpgradeResult(transaction, item))
            {
                continue;
            }

            matchingResultExists = true;
            if (candidate != null)
            {
                return false;
            }

            candidate = item;
        }

        if (candidate == null)
        {
            return false;
        }

        transaction.ReplacementResult = candidate;
        return true;
    }

    private static bool TryCommitPostCostUnobservedEquipmentSlotUpgradeResult(
        EquipmentSlotUpgradeTransaction transaction,
        Exception? craftingException)
    {
        ItemData? candidate = FindEquipmentSlotUpgradeResult(transaction);
        if (candidate == null)
        {
            return false;
        }

        // TryAdoptUnobserved... has already established that this is the unique,
        // owned, new, matching result. Commit before invoking any equip/slot callback:
        // this helper runs after the resource boundary, so a callback must never send
        // the transaction back through rollback and resurrect the original item.
        transaction.ReplacementResult = candidate;
        transaction.Committed = true;

        bool restoredToCapturedSlot = false;
        try
        {
            TryReturnEquipmentSlotUpgradeResultToOriginalSlot(transaction, candidate);
            restoredToCapturedSlot =
                SelectEquipmentSlotUpgradeCompletionPlan(transaction, candidate) ==
                InventorySlotSafetyCore.EquipmentUpgradeCompletionPlan.Commit;
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                $"Could not return a compatibility-created upgrade result to its captured slot: {ex.Message}");
        }

        // At this late boundary we cannot prove whether an unknown compatibility
        // patch already consumed resources. Keeping the unique upgraded result is
        // the only policy that avoids deleting a paid-for item or duplicating it by
        // resurrecting the original. A full audit repairs presentation/slot state.
        bool candidateStillOwned = transaction.Inventory.ContainsItem(candidate) &&
                                   !transaction.InitialItems.Contains(candidate) &&
                                   MatchesExpectedEquipmentSlotUpgradeResult(transaction, candidate);
        if (!restoredToCapturedSlot || !candidateStillOwned || craftingException != null)
        {
            if (candidateStillOwned)
            {
                Log.LogWarning(
                    $"Preserved {GetItemPrefabName(candidate)} from a post-cost compatibility upgrade path; " +
                    "its slot state will be audited without restoring the original item.");
            }
            else
            {
                Log.LogError(
                    "A post-cost compatibility callback changed ownership of the upgraded item; " +
                    "the original was not restored to avoid duplication.");
            }

            TryNotifyEquipmentSlotUpgradeInventoryChanged(transaction);
            RequestInventoryStateEnsure(
                transaction.Player,
                InventoryStateEnsureReason.InventoryChanged,
                InventoryStateAuditLevel.FullIntegrity);
        }

        return true;
    }

    private static void TryReturnEquipmentSlotUpgradeResultToOriginalSlot(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData result)
    {
        if (!transaction.Inventory.ContainsItem(result) ||
            !MatchesExpectedEquipmentSlotUpgradeResult(transaction, result))
        {
            return;
        }

        if (result.m_gridPos != transaction.OriginalPosition)
        {
            if (transaction.Inventory.GetItemAt(
                    transaction.OriginalPosition.x,
                    transaction.OriginalPosition.y) != null ||
                !CanUseSpecialSlot(
                    transaction.Player,
                    transaction.Inventory,
                    result,
                    transaction.OriginalSlot) ||
                !TryEquipIntoSlot(
                    transaction.Player,
                    transaction.Inventory,
                    result,
                    transaction.OriginalSlot))
            {
                return;
            }
        }

        if (transaction.OriginalSlot.Kind == SlotKind.Quick)
        {
            RestoreEquipmentSlotUpgradeQuickReplacementState(
                transaction,
                result);
        }
        else if (transaction.OriginalSlot.Kind == SlotKind.BuiltIn)
        {
            if (!IsEquipmentSlotUpgradeResultEquipped(transaction, result))
            {
                RestoreSlotEquipmentState(
                    transaction.Player,
                    transaction.Inventory,
                    result,
                    transaction.OriginalSlot);
            }

            if (!IsEquipmentSlotUpgradeResultEquipped(transaction, result))
            {
                TryRestoreEquipmentSlotUpgradeBuiltInReplacementState(
                    transaction,
                    result);
            }
        }
        else if (!IsEquipmentSlotUpgradeResultEquipped(transaction, result))
        {
            RestoreSlotEquipmentState(
                transaction.Player,
                transaction.Inventory,
                result,
                transaction.OriginalSlot);
        }
    }

    private static bool TryRestoreEquipmentSlotUpgradeBuiltInReplacementState(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData result)
    {
        if (transaction.OriginalSlot.Kind != SlotKind.BuiltIn ||
            !transaction.Inventory.ContainsItem(result) ||
            !MatchesExpectedEquipmentSlotUpgradeResult(transaction, result) ||
            !IsEquipmentSlotUpgradeItemSoleCellOccupant(
                transaction.Inventory,
                result,
                transaction.OriginalPosition))
        {
            return false;
        }

        ItemData? current = GetBuiltInEquipmentSlotItem(
            transaction.Player,
            transaction.OriginalSlot);
        if (current != null &&
            !ReferenceEquals(current, result) &&
            !ReferenceEquals(current, transaction.OriginalItem))
        {
            return false;
        }

        // The item was equipped before crafting, so preserve that exact captured
        // Humanoid reference instead of calling EquipItem during DoCrafting.
        // EquipItem can legitimately reject while the player is attacking, dodging,
        // or swimming; those transient action guards must not turn an upgrade into a
        // full-inventory failure after the replacement already occupies its slot.
        Humanoid humanoid = transaction.Player;
        bool changed = transaction.HumanoidSnapshot.Restore(humanoid, result);
        bool shouldBeEquipped = transaction.HumanoidSnapshot.EquippedFlag;
        if (result.m_equipped != shouldBeEquipped)
        {
            result.m_equipped = shouldBeEquipped;
            changed = true;
        }

        if (result.m_customData != null &&
            (result.m_customData.ContainsKey(SlotIdKey) ||
             result.m_customData.ContainsKey(EquippedByKey)))
        {
            ClearItemSlot(result);
            changed = true;
        }

        if (changed)
        {
            humanoid.SetupEquipment();
        }

        return IsEquipmentSlotUpgradeItemEquipped(
            transaction.Player,
            transaction.Inventory,
            result,
            transaction.OriginalSlot);
    }

    private static bool IsEquipmentSlotUpgradeResultEquipped(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData result)
    {
        if (transaction.OriginalSlot.Kind == SlotKind.Quick)
        {
            return ReferenceEquals(
                       FindItemForSlot(
                           transaction.Player,
                           transaction.Inventory,
                           transaction.OriginalSlot),
                       result) &&
                   transaction.HumanoidSnapshot.Matches(
                       transaction.Player,
                       result) &&
                   result.m_equipped ==
                   transaction.HumanoidSnapshot.EquippedFlag;
        }

        return IsEquipmentSlotUpgradeItemEquipped(
            transaction.Player,
            transaction.Inventory,
            result,
            transaction.OriginalSlot);
    }

    private static bool RestoreEquipmentSlotUpgradeQuickReplacementState(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData result)
    {
        Humanoid humanoid = transaction.Player;
        bool changed = transaction.HumanoidSnapshot.Restore(
            humanoid,
            result);
        bool shouldBeEquipped =
            transaction.HumanoidSnapshot.EquippedFlag;
        if (result.m_equipped != shouldBeEquipped)
        {
            result.m_equipped = shouldBeEquipped;
            changed = true;
        }

        if (changed)
        {
            humanoid.SetupEquipment();
        }

        return changed;
    }

    private static bool IsEquipmentSlotUpgradeItemEquipped(
        Player player,
        Inventory inventory,
        ItemData item,
        SlotDefinition slot)
    {
        if (!ReferenceEquals(FindItemForSlot(player, inventory, slot), item))
        {
            return false;
        }

        if (slot.Kind == SlotKind.Quick)
        {
            // Quick slots are positional shortcuts, not equipment ownership.
            // A weapon can temporarily be wielded from a Quick slot; it still
            // needs the same full-inventory upgrade transaction.
            return true;
        }

        if (slot.Kind == SlotKind.BuiltIn)
        {
            return item.m_equipped && ((Humanoid)player).IsItemEquiped(item);
        }

        return IsInventorySlotsCustomEquipped(item) &&
               item.m_customData.TryGetValue(SlotIdKey, out string slotId) &&
               string.Equals(slotId, slot.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesExpectedEquipmentSlotUpgradeResult(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData item)
    {
        return item?.m_shared != null &&
               item.m_quality == transaction.ExpectedQuality &&
               item.m_variant == transaction.ExpectedVariant &&
               string.Equals(
                   GetItemPrefabName(item),
                   transaction.ExpectedPrefab,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void TryRollbackEquipmentSlotUpgrade(
        EquipmentSlotUpgradeTransaction transaction,
        string reason)
    {
        if (transaction.Committed || transaction.RolledBack || transaction.RollbackInProgress)
        {
            return;
        }

        transaction.RollbackInProgress = true;
        try
        {
            ItemData original = transaction.OriginalItem;
            if (!EnsureEquipmentSlotUpgradeOriginalOwnership(transaction))
            {
                throw new InvalidOperationException("the original item could not be returned to its inventory");
            }

            bool cleanupFaulted = false;
            ItemData? replacement = transaction.ReplacementResult;
            if (replacement != null &&
                !transaction.InitialItems.Contains(replacement) &&
                transaction.Inventory.ContainsItem(replacement))
            {
                try
                {
                    UnequipInventorySlotsItem(transaction.Player, replacement);
                }
                catch (Exception ex)
                {
                    cleanupFaulted = true;
                    Log.LogWarning($"Could not run equipment cleanup for a failed upgrade result: {ex.Message}");
                }

                try
                {
                    ClearSlotActionState(replacement);
                }
                catch (Exception ex)
                {
                    cleanupFaulted = true;
                    Log.LogWarning($"Could not clear slot action state for a failed upgrade result: {ex.Message}");
                }

                while (transaction.Inventory.m_inventory.Remove(replacement))
                {
                }
            }

            RestoreEquipmentSlotUpgradeItemSnapshot(original, transaction.OriginalSnapshot);
            if (!transaction.Inventory.ContainsItem(original) &&
                !EnsureEquipmentSlotUpgradeOriginalOwnership(transaction))
            {
                throw new InvalidOperationException("the original item was removed again during rollback");
            }

            ItemData? blocker = transaction.Inventory.m_inventory.FirstOrDefault(item =>
                item != null &&
                item != original &&
                item.m_gridPos == transaction.OriginalPosition);
            Vector2i restorePosition = transaction.OriginalPosition;
            bool restoredToOriginalSlot = blocker == null;
            if (!restoredToOriginalSlot)
            {
                restorePosition = SelectEquipmentSlotUpgradePreservationPosition(
                    transaction,
                    original);
            }

            original.m_gridPos = restorePosition;
            if (restoredToOriginalSlot)
            {
                RestoreEquipmentSlotUpgradeOriginalSlotState(
                    transaction,
                    original);
            }
            else
            {
                original.m_equipped = false;
                ClearItemSlot(original);
                Log.LogError(
                    $"Preserved {GetItemPrefabName(original)} outside its blocked equipment slot after upgrade rollback.");
                RequestInventoryStateEnsure(
                    transaction.Player,
                    InventoryStateEnsureReason.InventoryChanged,
                    InventoryStateAuditLevel.FullIntegrity);
            }

            bool replacementStillOwned = replacement != null &&
                                         !transaction.InitialItems.Contains(replacement) &&
                                         transaction.Inventory.ContainsItem(replacement);
            bool restoredStateSafe =
                !restoredToOriginalSlot ||
                IsEquipmentSlotUpgradeOriginalStateIntact(transaction);
            transaction.RolledBack =
                transaction.Inventory.ContainsItem(original) &&
                !replacementStillOwned &&
                restoredStateSafe;
            if (!transaction.RolledBack)
            {
                throw new InvalidOperationException("rollback ownership could not be verified");
            }

            TryNotifyEquipmentSlotUpgradeInventoryChanged(transaction);
            try
            {
                ReloadEpicLootRuntimeItemData(transaction.Player);
            }
            catch (Exception ex)
            {
                cleanupFaulted = true;
                Log.LogWarning($"Could not reload EpicLoot item data after upgrade rollback: {ex.Message}");
            }

            try
            {
                RefreshExternalEquipmentEffects(transaction.Player);
            }
            catch (Exception ex)
            {
                cleanupFaulted = true;
                Log.LogWarning($"Could not refresh external equipment effects after upgrade rollback: {ex.Message}");
            }

            if (cleanupFaulted)
            {
                RequestInventoryStateEnsure(
                    transaction.Player,
                    InventoryStateEnsureReason.InventoryChanged,
                    InventoryStateAuditLevel.FullIntegrity);
            }

            Log.LogWarning(
                $"Restored {GetItemPrefabName(original)} after its equipment-slot upgrade failed: {reason}.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to restore equipment after an upgrade error: {ex}");
            if (EnsureEquipmentSlotUpgradeOriginalOwnership(transaction))
            {
                TryNotifyEquipmentSlotUpgradeInventoryChanged(transaction);
                RequestInventoryStateEnsure(
                    transaction.Player,
                    InventoryStateEnsureReason.InventoryChanged,
                    InventoryStateAuditLevel.FullIntegrity);
            }
        }
        finally
        {
            transaction.RollbackInProgress = false;
        }
    }

    private static bool EnsureEquipmentSlotUpgradeOriginalOwnership(
        EquipmentSlotUpgradeTransaction transaction)
    {
        try
        {
            ItemData original = transaction.OriginalItem;
            if (transaction.Inventory.ContainsItem(original))
            {
                return true;
            }

            original.m_gridPos = SelectEquipmentSlotUpgradePreservationPosition(
                transaction,
                original);
            original.m_equipped = false;
            if (original.m_customData == null)
            {
                original.m_customData = new Dictionary<string, string>();
            }

            transaction.Inventory.m_inventory.Add(original);
            try
            {
                ClearItemSlot(original);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not clear the temporary slot marker during upgrade recovery: {ex.Message}");
            }

            return transaction.Inventory.ContainsItem(original);
        }
        catch (Exception ex)
        {
            Log.LogError($"Could not preserve the original equipment item during upgrade recovery: {ex}");
            return false;
        }
    }

    private static Vector2i SelectEquipmentSlotUpgradePreservationPosition(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData original)
    {
        HashSet<Vector2i> occupied = BuildOccupiedCellSet(
            transaction.Inventory,
            original);
        InventorySlotSafetyCore.GridCell preservationCell =
            InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
                transaction.Inventory.GetWidth(),
                transaction.Inventory.GetHeight(),
                new InventorySlotSafetyCore.GridCell(
                    transaction.OriginalPosition.x,
                    transaction.OriginalPosition.y),
                (x, y) => IsCellOccupied(occupied, x, y));
        return new Vector2i(preservationCell.X, preservationCell.Y);
    }

    private static bool TryRestoreExistingEquipmentSlotUpgradeOriginalState(
        EquipmentSlotUpgradeTransaction transaction)
    {
        ItemData original = transaction.OriginalItem;
        if (!transaction.Inventory.ContainsItem(original))
        {
            return false;
        }

        ItemData? blocker = transaction.Inventory.GetItemAt(
            transaction.OriginalPosition.x,
            transaction.OriginalPosition.y);
        if (blocker != null && blocker != original)
        {
            return false;
        }

        bool changed = original.m_gridPos != transaction.OriginalPosition;
        original.m_gridPos = transaction.OriginalPosition;
        changed |= RestoreEquipmentSlotUpgradeOriginalSlotState(
            transaction,
            original);
        if (changed)
        {
            TryNotifyEquipmentSlotUpgradeInventoryChanged(transaction);
        }

        return IsEquipmentSlotUpgradeOriginalStateIntact(transaction);
    }

    private static bool IsEquipmentSlotUpgradeOriginalStateIntact(
        EquipmentSlotUpgradeTransaction transaction)
    {
        ItemData original = transaction.OriginalItem;
        if (!transaction.Inventory.ContainsItem(original) ||
            !IsEquipmentSlotUpgradeItemSoleCellOccupant(
                transaction.Inventory,
                original,
                transaction.OriginalPosition))
        {
            return false;
        }

        if (transaction.OriginalSlot.Kind != SlotKind.Quick)
        {
            return IsEquipmentSlotUpgradeItemEquipped(
                transaction.Player,
                transaction.Inventory,
                original,
                transaction.OriginalSlot);
        }

        Humanoid humanoid = transaction.Player;
        return transaction.HumanoidSnapshot.Matches(humanoid, original) &&
               original.m_equipped ==
               transaction.HumanoidSnapshot.EquippedFlag;
    }

    private static bool RestoreEquipmentSlotUpgradeOriginalSlotState(
        EquipmentSlotUpgradeTransaction transaction,
        ItemData original)
    {
        if (transaction.OriginalSlot.Kind != SlotKind.Quick)
        {
            return RestoreSlotEquipmentState(
                transaction.Player,
                transaction.Inventory,
                original,
                transaction.OriginalSlot);
        }

        return RestoreEquipmentSlotUpgradeQuickReplacementState(
            transaction,
            original);
    }

    private static void TryNotifyEquipmentSlotUpgradeInventoryChanged(
        EquipmentSlotUpgradeTransaction transaction)
    {
        try
        {
            transaction.Inventory.Changed();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Could not notify inventory listeners after equipment upgrade recovery: {ex.Message}");
        }
    }

    private static void RestoreEquipmentSlotUpgradeItemSnapshot(ItemData item, ItemData snapshot)
    {
        item.m_stack = snapshot.m_stack;
        item.m_durability = snapshot.m_durability;
        item.m_quality = snapshot.m_quality;
        item.m_variant = snapshot.m_variant;
        item.m_worldLevel = snapshot.m_worldLevel;
        item.m_pickedUp = snapshot.m_pickedUp;
        item.m_shared = snapshot.m_shared;
        item.m_crafterID = snapshot.m_crafterID;
        item.m_crafterName = snapshot.m_crafterName;
        if (item.m_customData == null)
        {
            item.m_customData = new Dictionary<string, string>();
        }
        else
        {
            item.m_customData.Clear();
        }

        if (snapshot.m_customData != null)
        {
            foreach (KeyValuePair<string, string> entry in snapshot.m_customData)
            {
                item.m_customData[entry.Key] = entry.Value;
            }
        }
        item.m_gridPos = snapshot.m_gridPos;
        item.m_equipped = snapshot.m_equipped;
        item.m_dropPrefab = snapshot.m_dropPrefab;
        item.m_lastAttackTime = snapshot.m_lastAttackTime;
        item.m_lastProjectile = snapshot.m_lastProjectile;
    }
}
