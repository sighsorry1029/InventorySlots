using System;
using System.Collections.Generic;

namespace InventorySlots;

internal static class ClientStateCore
{
    public const int DefaultLastExpandableRows = 4;
    // Keep the wood-panel edges close after matching the native 14-unit inset.
    public const float DefaultEquipmentSlotsPanelX = -82f;
    public const float DefaultEquipmentSlotsPanelY = 0f;
    public const float DefaultQuickSlotsPanelX = -82f;
    // Three 70-unit equipment rows and both 14-unit background margins, minus
    // a 3-unit overlap for the wood sprite's transparent top/bottom edges.
    public const float DefaultQuickSlotsPanelY = -235f;
    // Fixed viewport offsets from the native 70-unit grid, below chest weight
    // with nine visible player rows. No panel tracking.
    public const float DefaultQuickSlotsHudLeft = 628.5f;
    public const float DefaultQuickSlotsHudTop = 821f;
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
        // A missing HUD position uses the actual viewport's top-left reference.
        // Saved positions remain in HUD-root local coordinates, including old files.
        if (state.Inventory.QuickSlotsHudPosition is { } hudPosition &&
            (!IsFinite(hudPosition.X) || !IsFinite(hudPosition.Y)))
        {
            state.Inventory.QuickSlotsHudPosition = null;
        }
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
