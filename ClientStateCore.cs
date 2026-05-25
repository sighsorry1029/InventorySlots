using System;
using System.Collections.Generic;

namespace InventorySlots;

internal static class ClientStateCore
{
    public static InventorySlotsClientState Normalize(InventorySlotsClientState? state)
    {
        state ??= new InventorySlotsClientState();
        state.Inventory ??= new InventorySlotsClientInventoryState();
        state.Inventory.EquipmentSlotsPanelPosition ??= new InventorySlotsClientPanelPosition(-80f, 0f);
        state.Inventory.QuickSlotsPanelPosition ??= new InventorySlotsClientPanelPosition(-80f, -552f);

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
}
