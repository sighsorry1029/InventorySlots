using System;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
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

    internal static bool ShouldAllowInventoryGridDropItem(InventoryGrid grid, ItemData item, Vector2i pos)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || item == null || grid.m_inventory != ((Humanoid)player).GetInventory())
        {
            return true;
        }

        if (!CanUseCell(player, grid.m_inventory, item, pos))
        {
            return false;
        }

        if (!TryGetSlotAtGridPos(grid.m_inventory, pos, out _) && IsInventorySlotsCustomEquipped(item))
        {
            UnequipInventorySlotsItem(player, item);
        }

        return true;
    }

    internal static SlotDefinition? PrepareHumanoidDropInventorySlotsItem(Humanoid humanoid, Inventory inventory, ItemData item, int amount)
    {
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

        return TryPrepareSlotItemForExternalRemoval(player, inventory, item, out SlotDefinition? slot)
            ? slot
            : null;
    }

    internal static void RestoreHumanoidDropInventorySlotsItem(Humanoid humanoid, Inventory inventory, ItemData item, bool result, SlotDefinition? slot)
    {
        if (result || slot == null)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player != null && humanoid == (Humanoid)player)
        {
            RestoreSlotItemAfterFailedExternalRemoval(player, inventory, item, slot);
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
