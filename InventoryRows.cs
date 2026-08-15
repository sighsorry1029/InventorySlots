using System;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Vector3 GetCustomSlotPanelElementPosition(int index, float elementSpace)
    {
        return new Vector3((index / CustomSlotPanelRows) * elementSpace, -(index % CustomSlotPanelRows) * elementSpace, 0f);
    }

    private static Vector3 GetQuickSlotPanelElementPosition(int index, float elementSpace)
    {
        return new Vector3((index % QuickSlotPanelColumns) * elementSpace, -(index / QuickSlotPanelColumns) * elementSpace, 0f);
    }

    private static int GetCustomPanelColumns(int slotCount)
    {
        return slotCount <= 0 ? 0 : Mathf.CeilToInt(slotCount / (float)CustomSlotPanelRows);
    }

    private static int GetQuickPanelRows(int slotCount)
    {
        return slotCount <= 0 ? 0 : Mathf.CeilToInt(slotCount / (float)QuickSlotPanelColumns);
    }

    private static int GetInventoryViewportRows(int totalRegularRows)
    {
        int unlockedRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        if (!UseExpandableInventoryRows())
        {
            return Mathf.Max(1, unlockedRows);
        }

        int rememberedRows = GetLastExpandableInventoryRows(unlockedRows);
        return Mathf.Clamp(Mathf.Min(unlockedRows, rememberedRows), 1, Mathf.Max(1, unlockedRows));
    }

    private static int UpdatePlayerInventoryScroll(InventoryGrid playerGrid, int viewportRows, int totalRegularRows)
    {
        if (UseExpandableInventoryRows())
        {
            HandlePlayerInventoryExpandableWheel(playerGrid, totalRegularRows);
            viewportRows = GetInventoryViewportRows(totalRegularRows);
        }

        return viewportRows;
    }

    private static bool UseExpandableInventoryRows()
    {
        return _inventoryRowsDisplayMode == null || _inventoryRowsDisplayMode.Value == InventoryRowsDisplayMode.Expandable;
    }

    private static void HandlePlayerInventoryExpandableWheel(InventoryGrid playerGrid, int totalRegularRows)
    {
        if (totalRegularRows <= BaseRows || !InventoryGui.IsVisible())
        {
            return;
        }

        if (ShouldSuppressInventoryContainerRowsWheel())
        {
            return;
        }

        if (!IsMouseOverPlayerInventory(playerGrid) && !IsGamepadUiScrollActive())
        {
            return;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return;
        }

        int direction = wheel < 0f ? 1 : -1;
        SetExpandableInventoryRows(GetLastExpandableInventoryRows(totalRegularRows) + direction, totalRegularRows);
    }

    private static int GetLastExpandableInventoryRows(int totalRegularRows)
    {
        EnsureLastExpandableInventoryRowsLoaded();
        int maxRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        return Mathf.Clamp(InventoryPanels.LastExpandableInventoryRows, BaseRows, maxRows);
    }

    private static void SetExpandableInventoryRows(int rows, int totalRegularRows)
    {
        EnsureLastExpandableInventoryRowsLoaded();
        int maxRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        int clampedRows = Mathf.Clamp(rows, BaseRows, maxRows);
        if (InventoryPanels.LastExpandableInventoryRows == clampedRows)
        {
            return;
        }

        InventoryPanels.LastExpandableInventoryRows = clampedRows;
        SaveLastExpandableInventoryRows();
    }

    private static void EnsureLastExpandableInventoryRowsLoaded()
    {
        if (InventoryPanels.LastExpandableInventoryRowsLoaded)
        {
            return;
        }

        InventoryPanels.LastExpandableInventoryRowsLoaded = true;
        try
        {
            EnsureClientStateLoaded();
            InventoryPanels.LastExpandableInventoryRows = Mathf.Clamp(InventoryClient.ClientState.Inventory.LastExpandableRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        }
        catch (Exception)
        {
        }
    }

    private static void SaveLastExpandableInventoryRows()
    {
        try
        {
            EnsureClientStateLoaded();
            InventoryClient.ClientState.Inventory.LastExpandableRows = InventoryPanels.LastExpandableInventoryRows;
            SaveClientState();
        }
        catch (Exception)
        {
        }
    }

    private static bool IsMouseOverPlayerInventory(InventoryGrid playerGrid)
    {
        if (playerGrid == null)
        {
            return false;
        }

        Vector2 mouse = GetUiMousePosition();
        if (playerGrid.m_gridRoot != null && RectContainsScreenPoint(playerGrid.m_gridRoot, mouse))
        {
            return true;
        }

        return InventoryGui.instance != null && InventoryGui.instance.m_player != null && RectContainsScreenPoint(InventoryGui.instance.m_player, mouse);
    }

}
