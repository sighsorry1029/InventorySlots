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
    public int Version { get; set; } = 1;
    public InventorySlotsClientInventoryState Inventory { get; set; } = new();
    public Dictionary<string, InventorySlotsClientPlayerState> Players { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class InventorySlotsClientInventoryState
{
    public int LastExpandableRows { get; set; } = 4;
    public InventorySlotsClientPanelPosition EquipmentSlotsPanelPosition { get; set; } = new(-80f, 0f);
    public InventorySlotsClientPanelPosition QuickSlotsPanelPosition { get; set; } = new(-80f, -552f);
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
