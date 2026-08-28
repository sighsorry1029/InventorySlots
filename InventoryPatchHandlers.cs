using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal sealed class HumanoidDropInventorySlotsState
{
    public HumanoidDropInventorySlotsState(
        Humanoid humanoid,
        Inventory inventory,
        ItemData item,
        int amount,
        SlotDefinition slot,
        Vector2i originalPosition,
        HumanoidDropInventorySlotsState? previous)
    {
        Humanoid = humanoid;
        Inventory = inventory;
        Item = item;
        Amount = amount;
        Slot = slot;
        OriginalPosition = originalPosition;
        OriginalSnapshot = item.Clone();
        HumanoidSnapshot = new EquipmentSlotUpgradeHumanoidSnapshot(humanoid, item);
        Previous = previous;
    }

    public Humanoid Humanoid { get; }
    public Inventory Inventory { get; }
    public ItemData Item { get; }
    public int Amount { get; }
    public SlotDefinition Slot { get; }
    public Vector2i OriginalPosition { get; }
    public ItemData OriginalSnapshot { get; }
    public EquipmentSlotUpgradeHumanoidSnapshot HumanoidSnapshot { get; }
    public HumanoidDropInventorySlotsState? Previous { get; }
    public ItemDrop? WorldDrop { get; set; }
    public bool WorldDropCompleted { get; set; }
    public bool Prepared { get; set; }
    public bool Completed { get; set; }
}

internal sealed class InventorySlotsItemDropCreationScope
{
    public InventorySlotsItemDropCreationScope(
        HumanoidDropInventorySlotsState dropState,
        InventorySlotsItemDropCreationScope? previous)
    {
        DropState = dropState;
        Previous = previous;
    }

    public HumanoidDropInventorySlotsState DropState { get; }
    public InventorySlotsItemDropCreationScope? Previous { get; }
    public bool Completed { get; set; }
}

public sealed partial class InventorySlotsPlugin
{
    [ThreadStatic]
    private static HumanoidDropInventorySlotsState? _activeHumanoidDropInventorySlotsState;

    [ThreadStatic]
    private static InventorySlotsItemDropCreationScope? _activeInventorySlotsItemDropCreationScope;

    internal static bool BeginPlayerInventoryLoad(Player player)
    {
        if (IsUnityNull(player))
        {
            return false;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        EnsureInventoryHeightForLoad(inventory);
        return BeginInventoryLoadPreservation(inventory);
    }

    internal static bool BeginInventoryLoad(Inventory? inventory)
    {
        bool preserve = ShouldPreserveInventoryLoad(inventory);
        PrepareInventoryForLoad(inventory);
        return preserve && BeginInventoryLoadPreservation(inventory);
    }

    internal static void EndPlayerInventoryLoad(Player player, bool active)
    {
        if (!active || IsUnityNull(player))
        {
            return;
        }

        EndInventoryLoad(((Humanoid)player).GetInventory(), active);
    }

    internal static void EndInventoryLoad(Inventory? inventory, bool active)
    {
        if (!active || inventory == null)
        {
            return;
        }

        EndInventoryLoadPreservation(inventory);
    }

    internal static void PrepareInventoryForLoad(Inventory? inventory)
    {
        if (inventory != null && ShouldPrepareDetachedPlayerInventoryForLoad(inventory))
        {
            EnsureInventoryHeightForLoad(inventory);
            return;
        }

        Player? player = Player.m_localPlayer;
        if (inventory == null || player == null || inventory != ((Humanoid)player).GetInventory())
        {
            return;
        }

        EnsureInventoryHeightForLoad(inventory);
    }

    private static bool ShouldPreserveInventoryLoad(Inventory? inventory)
    {
        if (inventory == null)
        {
            return false;
        }

        if (TryGetLocalPlayerInventory(inventory, out _))
        {
            return true;
        }

        return ShouldPrepareDetachedPlayerInventoryForLoad(inventory);
    }

    private static bool ShouldPreserveProgressiveRowsDuringLoad(Inventory inventory, Player? player)
    {
        return inventory != null &&
               (IsInventoryLoadPreserving(inventory) || player != null && player.m_isLoading);
    }

    private static void EnsureInventoryHeightForLoad(Inventory? inventory)
    {
        if (inventory == null)
        {
            return;
        }

        int fullHeight = GetInventoryFullHeight(inventory.GetWidth());
        if (inventory.m_height < fullHeight)
        {
            inventory.m_height = fullHeight;
        }
    }

    private static bool ShouldPrepareDetachedPlayerInventoryForLoad(Inventory inventory)
    {
        return HasServerCharactersActive &&
               inventory.GetWidth() == InventoryWidth &&
               inventory.m_height <= BaseRows &&
               string.Equals(inventory.m_name, "Inventory", StringComparison.Ordinal);
    }

    internal static void OnPlayerAwake(Player player)
    {
        InvalidateInventoryPlacementCaches();
        InvalidateCustomEquipmentProjectionCache();
        EnsureInventoryState(player, InventoryStateEnsureReason.PlayerAwake);
    }

    internal static void OnPlayerSpawned(Player player)
    {
        if (player == Player.m_localPlayer)
        {
            InvalidateInventoryPlacementCaches();
            InvalidateCustomEquipmentProjectionCache();
            EnsureInventoryState(player, InventoryStateEnsureReason.PlayerSpawned);
            ApplyAutoFavoriteHotbarSwitchRowForPlayer(player);
            ScheduleEpicLootRespawnRuntimeReload(player);
        }
    }

    internal static void OnPlayerLoaded(Player player)
    {
        InvalidateInventoryPlacementCaches();
        InvalidateCustomEquipmentProjectionCache();
        ClearPendingSlotActions();
        if (player == Player.m_localPlayer)
        {
            PreserveOccupiedQuickSlotRowsDuringLoad(player, ((Humanoid)player).GetInventory());
        }

        EnsureInventoryState(player, InventoryStateEnsureReason.PlayerLoad);
        TryRestoreSlotBackup(player);
        if (player == Player.m_localPlayer)
        {
            PreserveOccupiedQuickSlotRowsDuringLoad(player, ((Humanoid)player).GetInventory());
        }

        EnsureInventoryState(player, InventoryStateEnsureReason.BackupRestore);
        ApplyAutoFavoriteHotbarSwitchRowForPlayer(player);
    }

    internal static void OnPlayerSaving(Player player)
    {
        if (player == Player.m_localPlayer && !player.m_isLoading)
        {
            PrunePendingSlotActions(player);
            EnsureInventoryState(player, InventoryStateEnsureReason.PlayerSave);
        }

        SaveSlotBackup(player);
    }

    internal static void OnGameSpawnPlayer()
    {
        Player? player = Player.m_localPlayer;
        if (player != null && !player.m_isLoading)
        {
            EnsureInventoryState(player, InventoryStateEnsureReason.PlayerSpawned);
            ApplyAutoFavoriteHotbarSwitchRowForPlayer(player);
            ScheduleEpicLootRespawnRuntimeReload(player);
        }
    }

    internal static void OnPlayerInventoryChanged(Player player)
    {
        if (player == Player.m_localPlayer && !player.m_isLoading)
        {
            InvalidateInventoryPlacementCaches();
            InvalidateCustomEquipmentProjectionCache();
            ClearCraftingRequirementAvailabilityCache();
            bool progressionResetPending = HasPendingQuickSlotProgressionReset(player);
            RequestInventoryStateEnsure(
                player,
                progressionResetPending ? InventoryStateEnsureReason.ProgressionReset : InventoryStateEnsureReason.InventoryChanged,
                progressionResetPending ? InventoryStateAuditLevel.FullIntegrity : InventoryStateAuditLevel.SlotLight);
        }
    }

    internal static void OnHumanoidSetupEquipment(Humanoid humanoid)
    {
        Player? player = Player.m_localPlayer;
        if (humanoid == (Humanoid)player && player != null && !player.m_isLoading && !IsCompletingSlotUnequip)
        {
            InvalidateCustomEquipmentProjectionCache();
            RequestInventoryStateEnsure(player, InventoryStateEnsureReason.EquipmentChanged, InventoryStateAuditLevel.SlotLight);
        }
    }

    internal static void OnHumanoidSetupVisEquipment(Humanoid humanoid, bool isRagdoll)
    {
        Player? player = Player.m_localPlayer;
        if (!isRagdoll && humanoid == (Humanoid)player && player != null && !player.m_isLoading)
        {
            UpdateCustomEquipmentVisuals(player);
        }
    }

    internal static void OnInventoryLoaded(Inventory inventory)
    {
        if (TryGetLocalPlayerInventory(inventory, out Player? player))
        {
            PreserveOccupiedQuickSlotRowsDuringLoad(player!, inventory);
            EnsureInventoryState(player!, InventoryStateEnsureReason.InventoryLoad);
        }
    }

    internal static void OnInventoryMoveAll(Inventory targetInventory, Inventory fromInventory)
    {
        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (targetInventory == playerInventory || fromInventory == playerInventory)
        {
            RequestInventoryStateEnsure(player, InventoryStateEnsureReason.InventoryMove, InventoryStateAuditLevel.SlotLight);
        }
    }

    internal static void OnInventoryMoveInventoryToGrave(Inventory graveInventory, Inventory original)
    {
        if (!TryGetLocalPlayerInventory(original, out Player? player))
        {
            return;
        }

        EnsureInventoryState(player!, InventoryStateEnsureReason.Tombstone);
        int originalHeight = GetInventoryPreservationHeight(original, GetInventoryFullHeight(original.GetWidth()));
        original.m_height = originalHeight;
        graveInventory.m_height = Math.Max(GetInventoryFullHeight(graveInventory.GetWidth()), originalHeight);
    }

    internal static bool ShouldAllowInventoryGridDropItem(
        InventoryGrid grid,
        Inventory fromInventory,
        ItemData item,
        int amount,
        Vector2i pos,
        ref bool result)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || item == null)
        {
            return true;
        }

        Inventory targetInventory = grid.m_inventory;
        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (targetInventory == playerInventory && !CanUseCell(player, targetInventory, item, pos))
        {
            return false;
        }

        if (TryHandleProtectedInventoryGridSwap(
                player,
                playerInventory,
                targetInventory,
                fromInventory,
                item,
                amount,
                pos,
                out bool swapResult))
        {
            result = swapResult;
            return false;
        }

        if (targetInventory == playerInventory &&
            !TryGetSlotAtGridPos(targetInventory, pos, out _) &&
            IsInventorySlotsCustomEquipped(item))
        {
            UnequipInventorySlotsItem(player, item);
        }

        return true;
    }

    internal static HumanoidDropInventorySlotsState? PrepareHumanoidDropInventorySlotsItem(
        Humanoid humanoid,
        Inventory inventory,
        ItemData item,
        int amount,
        out bool abortDrop)
    {
        abortDrop = false;
        Player? player = Player.m_localPlayer;
        if (IsHandlingSlotDropOutside ||
            player == null ||
            humanoid != (Humanoid)player ||
            inventory == null ||
            item == null ||
            amount < item.m_stack ||
            inventory != ((Humanoid)player).GetInventory())
        {
            return null;
        }

        Vector2i originalPosition = item.m_gridPos;
        if (!TryResolveSlotItemForExternalRemoval(
                player,
                inventory,
                item,
                out SlotDefinition? slot) ||
            slot == null)
        {
            return null;
        }

        HumanoidDropInventorySlotsState state = new(
            humanoid,
            inventory,
            item,
            amount,
            slot,
            originalPosition,
            _activeHumanoidDropInventorySlotsState);
        _activeHumanoidDropInventorySlotsState = state;
        try
        {
            if (!TryPrepareSlotItemForExternalRemoval(
                    player,
                    inventory,
                    item,
                    out SlotDefinition? preparedSlot) ||
                preparedSlot == null ||
                !string.Equals(
                    preparedSlot.Id,
                    slot.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                abortDrop = true;
                CompleteHumanoidDropInventorySlotsItem(
                    humanoid,
                    inventory,
                    item,
                    result: false,
                    state: state);
            }
            else
            {
                state.Prepared = true;
            }
        }
        catch (Exception exception)
        {
            abortDrop = true;
            CompleteHumanoidDropInventorySlotsItem(
                humanoid,
                inventory,
                item,
                result: false,
                state: state,
                exception: exception);
        }

        return state;
    }

    internal static void CompleteHumanoidDropInventorySlotsItem(
        Humanoid humanoid,
        Inventory inventory,
        ItemData item,
        bool result,
        HumanoidDropInventorySlotsState? state,
        Exception? exception = null)
    {
        if (state == null || state.Completed)
        {
            return;
        }

        state.Completed = true;
        try
        {
            Player? player = Player.m_localPlayer;
            if (player == null ||
                humanoid != state.Humanoid ||
                inventory != state.Inventory ||
                item != state.Item ||
                humanoid != (Humanoid)player)
            {
                return;
            }

            bool itemOwned = ContainsExactItemReference(inventory, item);
            bool liveWorldDrop =
                state.WorldDrop != null && !IsUnityNull(state.WorldDrop);
            bool incompleteWorldDropDiscarded = false;
            if (state.WorldDropCompleted && liveWorldDrop)
            {
                if (itemOwned)
                {
                    inventory.m_inventory.RemoveAll(candidate =>
                        ReferenceEquals(candidate, item));
                    inventory.Changed();
                }

                if (exception != null)
                {
                    Log.LogWarning(
                        $"Equipment drop callback failed after the world item was saved; kept the world copy to avoid duplication: {exception.GetBaseException().Message}");
                }

                return;
            }

            if (liveWorldDrop && !TryDiscardIncompleteInventorySlotsWorldDrop(state.WorldDrop!))
            {
                Log.LogError(
                    "An incomplete equipment world drop could not be discarded; " +
                    "the inventory copy was not restored to avoid duplication.");
                return;
            }

            if (liveWorldDrop)
            {
                incompleteWorldDropDiscarded = true;
                liveWorldDrop = false;
                state.WorldDrop = null;
            }

            if (result && !itemOwned && !incompleteWorldDropDiscarded)
            {
                // A true result without our exact ItemDrop completion is not
                // enough proof to manufacture another inventory copy.
                return;
            }

            if (!itemOwned && !liveWorldDrop)
            {
                inventory.m_inventory.Add(item);
                itemOwned = true;
            }

            if (itemOwned)
            {
                RestoreEquipmentSlotUpgradeItemSnapshot(
                    item,
                    state.OriginalSnapshot);
                ItemData? blocker = inventory.m_inventory.FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate != item &&
                    candidate.m_gridPos == state.OriginalPosition);
                bool restoreSlotState = blocker == null;
                if (restoreSlotState)
                {
                    item.m_gridPos = state.OriginalPosition;
                    state.HumanoidSnapshot.Restore(humanoid, item);
                    RestoreSlotEquipmentState(
                        player,
                        inventory,
                        item,
                        state.Slot);
                }
                else
                {
                    HashSet<Vector2i> occupied = new(
                        inventory.m_inventory
                            .Where(candidate => candidate != null && candidate != item)
                            .Select(candidate => candidate.m_gridPos));
                    InventorySlotSafetyCore.GridCell preservationCell =
                        InventorySlotSafetyCore.SelectNonOverlappingPreservationCell(
                            inventory.GetWidth(),
                            inventory.GetHeight(),
                            new InventorySlotSafetyCore.GridCell(
                                state.OriginalPosition.x,
                                state.OriginalPosition.y),
                            (x, y) => IsCellOccupied(occupied, x, y));
                    item.m_gridPos = new Vector2i(
                        preservationCell.X,
                        preservationCell.Y);
                    item.m_equipped = false;
                    ClearItemSlot(item);
                    ClearVanillaEquipmentReferences(humanoid, item);
                    humanoid.SetupEquipment();
                    RequestInventoryStateEnsure(
                        player,
                        InventoryStateEnsureReason.InventoryChanged,
                        InventoryStateAuditLevel.FullIntegrity);
                }

                inventory.Changed();
            }

            if (exception != null)
            {
                Log.LogWarning(
                    $"Restored equipment after a drop exception before world creation: {exception.GetBaseException().Message}");
            }
        }
        catch (Exception recoveryException)
        {
            Log.LogError(
                $"Equipment drop recovery failed: {recoveryException}");
        }
        finally
        {
            if (ReferenceEquals(
                    _activeHumanoidDropInventorySlotsState,
                    state))
            {
                _activeHumanoidDropInventorySlotsState = state.Previous;
            }
        }
    }

    internal static void OnItemDropAwakeForInventorySlotsDrop(ItemDrop itemDrop)
    {
        OnItemDropAwakeForMultiUserContainerWorldDelivery(itemDrop);

        InventorySlotsItemDropCreationScope? scope =
            _activeInventorySlotsItemDropCreationScope;
        HumanoidDropInventorySlotsState? state = scope?.DropState;
        if (scope == null ||
            state == null ||
            state.Completed ||
            state.WorldDrop != null ||
            itemDrop == null ||
            IsUnityNull(itemDrop) ||
            state.Item?.m_dropPrefab == null)
        {
            return;
        }

        string expectedName = CleanPrefabName(state.Item.m_dropPrefab.name);
        string actualName = CleanPrefabName(itemDrop.gameObject.name);
        if (string.Equals(
                expectedName,
                actualName,
                StringComparison.OrdinalIgnoreCase))
        {
            state.WorldDrop = itemDrop;
        }
    }

    internal static InventorySlotsItemDropCreationScope? BeginInventorySlotsItemDropCreation(
        ItemData item,
        int amount)
    {
        HumanoidDropInventorySlotsState? state =
            _activeHumanoidDropInventorySlotsState;
        if (state == null ||
            state.Completed ||
            !state.Prepared ||
            !ReferenceEquals(state.Item, item) ||
            amount != Math.Min(state.Amount, state.OriginalSnapshot.m_stack) ||
            ReferenceEquals(
                _activeInventorySlotsItemDropCreationScope?.DropState,
                state))
        {
            return null;
        }

        InventorySlotsItemDropCreationScope scope = new(
            state,
            _activeInventorySlotsItemDropCreationScope);
        _activeInventorySlotsItemDropCreationScope = scope;
        return scope;
    }

    internal static void CompleteInventorySlotsItemDropCreation(
        InventorySlotsItemDropCreationScope? scope,
        ItemDrop? result,
        Exception? exception)
    {
        if (scope == null || scope.Completed)
        {
            return;
        }

        scope.Completed = true;
        try
        {
            if (exception == null && result != null && !IsUnityNull(result))
            {
                scope.DropState.WorldDrop = result;
                scope.DropState.WorldDropCompleted = true;
            }
        }
        finally
        {
            if (ReferenceEquals(
                    _activeInventorySlotsItemDropCreationScope,
                    scope))
            {
                _activeInventorySlotsItemDropCreationScope = scope.Previous;
            }
        }
    }

    private static bool TryDiscardIncompleteInventorySlotsWorldDrop(
        ItemDrop itemDrop)
    {
        try
        {
            if (itemDrop == null || IsUnityNull(itemDrop))
            {
                return true;
            }

            ZNetView? nview = itemDrop.m_nview;
            if (nview != null && !IsUnityNull(nview) && nview.IsValid())
            {
                if (!nview.IsOwner())
                {
                    return false;
                }

                nview.Destroy();
                return true;
            }

            UnityEngine.Object.Destroy(itemDrop.gameObject);
            return true;
        }
        catch (Exception exception)
        {
            Log.LogWarning(
                $"Could not discard an incomplete equipment world drop: {exception.Message}");
            return false;
        }
    }

    internal static void OnInventoryGuiUpdateInventory(InventoryGrid playerGrid, Player player)
    {
        if (player != null && playerGrid != null)
        {
            UpdateInventoryGridUi(playerGrid, player);
        }
    }

    internal static void OnInventoryGuiUpdateContainer(InventoryGui gui, Player player)
    {
        if (gui == null || player == null || gui.m_playerGrid == null || !InventoryGui.IsVisible())
        {
            return;
        }

        int viewportRows = GetInventoryViewportRows(GetUsableRegularRows(player));
        UpdateContainerPanelPosition(viewportRows, gui.m_playerGrid.m_elementSpace);
        UpdateContainerWeightPanelPosition();
    }

    internal static bool ShouldAllowTooltipHoverStart(UITooltip tooltip)
    {
        if (ShouldUseInventorySlotsOwnedHoverTooltip(tooltip))
        {
            EnsureTooltipPrefab(tooltip);
            if (tooltip == null || IsUnityNull(tooltip) || tooltip.m_tooltipPrefab == null)
            {
                return false;
            }

            if (!ShouldSuppressVanillaHoverStart(tooltip))
            {
                return true;
            }

            BeginInventorySlotsOwnedHoverTooltip(tooltip);
            HideVanillaTooltipVisual(UITooltip.m_tooltip);
            UpdateInventorySlotsOwnedHoverTooltip(tooltip, resetScroll: true, handleWheel: false);
            return false;
        }

        BeginInventoryContainerHoverTooltipOwnership(tooltip);
        if (tooltip == null || tooltip.m_tooltipPrefab != null)
        {
            return true;
        }

        tooltip.m_topic = "";
        tooltip.m_text = "";
        return false;
    }

    internal static void OnContainerTakeAllResponse(Container container, bool granted)
    {
        Player? player = Player.m_localPlayer;
        if (granted && player != null && !player.m_isLoading && container != null && container.GetComponent<TombStone>() != null)
        {
            EnsureInventoryState(player, InventoryStateEnsureReason.Tombstone);
        }
    }

    internal static void OnInventoryGuiShow()
    {
        OnRealInventoryGuiShown();
        StartQuickSlotPanelIntroAnimation();
        Player? player = Player.m_localPlayer;
        if (player != null && !player.m_isLoading)
        {
            RequestInventoryStateEnsure(player, InventoryStateEnsureReason.GuiShow, InventoryStateAuditLevel.SlotLight);
        }
    }

    internal static void OnInventoryGuiHide()
    {
        CancelMultiUserContainerBatch(includeAreaBatch: false);
        OnInventoryGuiHidden();
        StartQuickSlotPanelOutroAnimation();
        PrunePendingSlotActions();
        RestoreContainerUiState();
        RestorePlayerStatPanels();
        HideCraftingPanelRedesign();
        HideInventoryPinnedTooltips();
        HideInventoryOwnedHoverTooltips();
        ClearInventoryHoverTooltipSources();
    }

    internal static void OnInventoryGuiCloseContainer()
    {
        CancelMultiUserContainerBatch(includeAreaBatch: false);
        RestoreContainerUiState();
    }

    private static void RestoreContainerUiState()
    {
        ClearPendingContainerSortRequest();
        RestoreContainerPanelPosition();
        RestoreContainerWeightPanelPosition();
        HideInventoryActionPanels();
    }

    internal static bool TryOverrideTombStoneEasyFit(TombStone tombStone, Player player, ref bool result)
    {
        EnsureTombstoneContainerHeight(tombStone.m_container, reloadInventory: false, persistHeight: true);
        result = player == null || TombstoneCanFitInventory(tombStone, player);
        return false;
    }

    internal static void OnTombStoneInteract(TombStone tombStone, bool hold)
    {
        if (!hold)
        {
            EnsureTombstoneContainerHeight(tombStone.m_container, reloadInventory: true, persistHeight: true);
        }
    }

    internal static void OnTombStoneInventoryStateChanged(Player player)
    {
        if (player != null && !player.m_isLoading)
        {
            EnsureInventoryState(player, InventoryStateEnsureReason.Tombstone);
        }
    }

    internal static void OnTombStoneTakeAllSuccess(TombStone tombStone)
    {
        CleanupTombstoneFloatingBodyForAutoPickup(tombStone);

        Player? player = Player.m_localPlayer;
        if (player != null && !player.m_isLoading)
        {
            EnsureInventoryState(player, InventoryStateEnsureReason.Tombstone);
        }
    }

    private static bool TryGetLocalPlayerInventory(Inventory? inventory, out Player? player)
    {
        player = Player.m_localPlayer;
        return player != null && inventory != null && inventory == ((Humanoid)player).GetInventory();
    }
}
