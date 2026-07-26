using System;

namespace InventorySlots;

internal enum InventoryPlacementScope
{
    General,
    Container,
    LocalPlayer,
    LoadPreservation
}

internal static class InventoryPlacementPolicyCore
{
    public static bool TrySelectAutomaticPlacementCell(
        int inventoryWidth,
        int rowCount,
        bool preferHotbar,
        Func<int, int, bool> isAllowed,
        Func<int, int, bool> isOccupied,
        out InventorySlotSafetyCore.GridCell cell)
    {
        int width = Math.Max(1, inventoryWidth);
        int rows = Math.Max(0, rowCount);
        if (preferHotbar)
        {
            if (TrySelectFirstFreeCellInRows(
                    width,
                    0,
                    Math.Min(1, rows),
                    isAllowed,
                    isOccupied,
                    out cell))
            {
                return true;
            }

            return TrySelectFirstFreeCellInRows(
                width,
                1,
                rows,
                isAllowed,
                isOccupied,
                out cell);
        }

        if (TrySelectFirstFreeCellInRows(width, 1, rows, isAllowed, isOccupied, out cell))
        {
            return true;
        }

        return TrySelectFirstFreeCellInRows(width, 0, Math.Min(1, rows), isAllowed, isOccupied, out cell);
    }

    public static int CountTopFirstPolicyEmptyCells(
        int inventoryWidth,
        int rowCount,
        Func<int, int, bool> isAllowed,
        Func<int, int, bool> isOccupied)
    {
        int width = Math.Max(1, inventoryWidth);
        int rows = Math.Max(0, rowCount);
        int count = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (isAllowed(x, y) && !isOccupied(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    public static bool CanAcceptInventoryLimit(long currentAmount, int incomingAmount, int maxAmount)
    {
        if (incomingAmount <= 0 || maxAmount < 0)
        {
            return true;
        }

        return Math.Max(0L, currentAmount) + incomingAmount <= maxAmount;
    }

    private static bool TrySelectFirstFreeCellInRows(
        int width,
        int startRow,
        int endRow,
        Func<int, int, bool> isAllowed,
        Func<int, int, bool> isOccupied,
        out InventorySlotSafetyCore.GridCell cell)
    {
        for (int y = startRow; y < endRow; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (isAllowed(x, y) && !isOccupied(x, y))
                {
                    cell = new InventorySlotSafetyCore.GridCell(x, y);
                    return true;
                }
            }
        }

        cell = new InventorySlotSafetyCore.GridCell(-1, -1);
        return false;
    }
}
