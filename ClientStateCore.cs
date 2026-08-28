using System;
using System.Collections.Generic;

namespace InventorySlots;

internal static class ClientStateCore
{
    public const int DefaultLastExpandableRows = 4;
    public const float DefaultEquipmentSlotsPanelX = -80f;
    public const float DefaultEquipmentSlotsPanelY = 0f;
    public const float DefaultQuickSlotsPanelX = -80f;
    public const float DefaultQuickSlotsPanelY = -552f;
    public const float DefaultQuickSlotsHudX = 64f;
    public const float DefaultQuickSlotsHudY = -520f;
    public const float DefaultQuickSlotsHudElementSpace = 70f;

    public static InventorySlotsClientState Normalize(InventorySlotsClientState? state)
    {
        state ??= new InventorySlotsClientState();
        state.Inventory ??= new InventorySlotsClientInventoryState();
        state.Inventory.EquipmentSlotsPanelPosition = NormalizePanelPosition(
            state.Inventory.EquipmentSlotsPanelPosition,
            DefaultEquipmentSlotsPanelX,
            DefaultEquipmentSlotsPanelY);
        state.Inventory.QuickSlotsPanelPosition = NormalizePanelPosition(
            state.Inventory.QuickSlotsPanelPosition,
            DefaultQuickSlotsPanelX,
            DefaultQuickSlotsPanelY);
        state.Inventory.QuickSlotsHudPosition = NormalizePanelPosition(
            state.Inventory.QuickSlotsHudPosition,
            DefaultQuickSlotsHudX,
            DefaultQuickSlotsHudY);
        if (state.Inventory.QuickSlotsHudElementSpace < 1f ||
            !IsFinite(state.Inventory.QuickSlotsHudElementSpace))
        {
            state.Inventory.QuickSlotsHudElementSpace = DefaultQuickSlotsHudElementSpace;
        }

        Dictionary<string, InventorySlotsClientPlayerState> players = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, InventorySlotsClientPlayerState> entry in state.Players ?? new Dictionary<string, InventorySlotsClientPlayerState>())
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            InventorySlotsClientPlayerState playerState = entry.Value ?? new InventorySlotsClientPlayerState();
            playerState.FavoriteSlots ??= new List<InventorySlotsFavoriteSlot>();
            playerState.CraftingFavorites ??= new List<string>();
            playerState.UpgradeFavorites ??= new List<string>();
            players[entry.Key.Trim()] = playerState;
        }

        state.Players = players;
        return state;
    }

    private static InventorySlotsClientPanelPosition NormalizePanelPosition(
        InventorySlotsClientPanelPosition? position,
        float defaultX,
        float defaultY)
    {
        position ??= new InventorySlotsClientPanelPosition(defaultX, defaultY);
        if (!IsFinite(position.X))
        {
            position.X = defaultX;
        }

        if (!IsFinite(position.Y))
        {
            position.Y = defaultY;
        }

        return position;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
