using System;
using System.Collections.Generic;

namespace InventorySlots;

internal sealed class InventorySlotsFavoriteSlot
{
    public int X { get; set; }
    public int Y { get; set; }
}

internal sealed class InventorySlotsClientState
{
    public InventorySlotsClientInventoryState Inventory { get; set; } = new();
    public Dictionary<string, InventorySlotsClientPlayerState> Players { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class InventorySlotsClientInventoryState
{
    public int LastExpandableRows { get; set; } = ClientStateCore.DefaultLastExpandableRows;
    public InventorySlotsClientPanelPosition EquipmentSlotsPanelPosition { get; set; } = new(
        ClientStateCore.DefaultEquipmentSlotsPanelX,
        ClientStateCore.DefaultEquipmentSlotsPanelY);
    public InventorySlotsClientPanelPosition QuickSlotsPanelPosition { get; set; } = new(
        ClientStateCore.DefaultQuickSlotsPanelX,
        ClientStateCore.DefaultQuickSlotsPanelY);
    public InventorySlotsClientPanelPosition QuickSlotsHudPosition { get; set; } = new(
        ClientStateCore.DefaultQuickSlotsHudX,
        ClientStateCore.DefaultQuickSlotsHudY);
    public float QuickSlotsHudElementSpace { get; set; } = ClientStateCore.DefaultQuickSlotsHudElementSpace;
}

internal sealed class InventorySlotsClientPanelPosition
{
    public InventorySlotsClientPanelPosition()
    {
    }

    public InventorySlotsClientPanelPosition(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; set; }
    public float Y { get; set; }
}

internal sealed class InventorySlotsClientPlayerState
{
    public List<InventorySlotsFavoriteSlot> FavoriteSlots { get; set; } = new();
    public List<string> CraftingFavorites { get; set; } = new();
    public List<string> UpgradeFavorites { get; set; } = new();
}
