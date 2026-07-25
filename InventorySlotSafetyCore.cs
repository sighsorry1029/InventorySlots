using System;

namespace InventorySlots;

internal static class InventorySlotSafetyCore
{
    public readonly struct GridCell
    {
        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    public enum KeepOnDeathRestorePlan
    {
        OriginalSlot,
        EmptyQuickSlot,
        EmptySameSpecialKindSlot,
        OriginalCell,
        FirstFreeRegularCell,
        EmptyNonQuickSpecialSlot,
        PreserveWithoutOverwriting
    }

    public readonly struct KeepOnDeathRestoreOptions
    {
        public KeepOnDeathRestoreOptions(
            bool wasSpecialSlot,
            bool wasQuickSlot,
            bool originalSlotAvailable,
            bool emptyQuickSlotAvailable,
            bool emptySameSpecialKindSlotAvailable,
            bool originalCellAvailable,
            bool freeRegularCellAvailable,
            bool emptyNonQuickSpecialSlotAvailable)
        {
            WasSpecialSlot = wasSpecialSlot;
            WasQuickSlot = wasQuickSlot;
            OriginalSlotAvailable = originalSlotAvailable;
            EmptyQuickSlotAvailable = emptyQuickSlotAvailable;
            EmptySameSpecialKindSlotAvailable = emptySameSpecialKindSlotAvailable;
            OriginalCellAvailable = originalCellAvailable;
            FreeRegularCellAvailable = freeRegularCellAvailable;
            EmptyNonQuickSpecialSlotAvailable = emptyNonQuickSpecialSlotAvailable;
        }

        public bool WasSpecialSlot { get; }
        public bool WasQuickSlot { get; }
        public bool OriginalSlotAvailable { get; }
        public bool EmptyQuickSlotAvailable { get; }
        public bool EmptySameSpecialKindSlotAvailable { get; }
        public bool OriginalCellAvailable { get; }
        public bool FreeRegularCellAvailable { get; }
        public bool EmptyNonQuickSpecialSlotAvailable { get; }
    }

    public static bool CanAutoAdoptGridSlot(bool isInventorySlotsCustomEquipped, string? markedSlotId, string candidateSlotId)
    {
        if (!isInventorySlotsCustomEquipped)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(markedSlotId))
        {
            return true;
        }

        string marked = markedSlotId!.Trim();
        string candidate = candidateSlotId == null ? "" : candidateSlotId.Trim();
        return string.Equals(marked, candidate, StringComparison.OrdinalIgnoreCase);
    }

    public static KeepOnDeathRestorePlan SelectKeepOnDeathRestorePlan(KeepOnDeathRestoreOptions options)
    {
        if (options.WasSpecialSlot)
        {
            if (options.OriginalSlotAvailable)
            {
                return KeepOnDeathRestorePlan.OriginalSlot;
            }

            if (options.WasQuickSlot && options.EmptyQuickSlotAvailable)
            {
                return KeepOnDeathRestorePlan.EmptyQuickSlot;
            }

            if (!options.WasQuickSlot && options.EmptySameSpecialKindSlotAvailable)
            {
                return KeepOnDeathRestorePlan.EmptySameSpecialKindSlot;
            }
        }

        if (options.OriginalCellAvailable)
        {
            return KeepOnDeathRestorePlan.OriginalCell;
        }

        if (!options.WasQuickSlot)
        {
            if (options.FreeRegularCellAvailable)
            {
                return KeepOnDeathRestorePlan.FirstFreeRegularCell;
            }

            if (options.EmptyNonQuickSpecialSlotAvailable)
            {
                return KeepOnDeathRestorePlan.EmptyNonQuickSpecialSlot;
            }
        }
        else if (options.FreeRegularCellAvailable)
        {
            return KeepOnDeathRestorePlan.FirstFreeRegularCell;
        }

        return KeepOnDeathRestorePlan.PreserveWithoutOverwriting;
    }

    public static int ResolveQuickSlotProgressionResetRows(
        int configuredRows,
        int naturallyUnlockedRows,
        Func<int, bool> tryClearRow)
    {
        int configured = Math.Max(0, configuredRows);
        int minimum = configured == 0
            ? 0
            : Math.Max(1, Math.Min(configured, naturallyUnlockedRows));
        for (int row = configured; row > minimum; row--)
        {
            if (!tryClearRow(row))
            {
                return row;
            }
        }

        return minimum;
    }

    public static GridCell SelectNonOverlappingPreservationCell(
        int inventoryWidth,
        int inventoryHeight,
        GridCell preferredCell,
        Func<int, int, bool> isOccupied)
    {
        int width = Math.Max(1, inventoryWidth);
        int height = Math.Max(0, inventoryHeight);
        if (preferredCell.X >= 0 &&
            preferredCell.X < width &&
            preferredCell.Y >= 0 &&
            !isOccupied(preferredCell.X, preferredCell.Y))
        {
            return preferredCell;
        }

        int overflowY = Math.Max(0, height);
        for (int y = overflowY; y < overflowY + height + 32; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!isOccupied(x, y))
                {
                    return new GridCell(x, y);
                }
            }
        }

        int fallbackX = preferredCell.X < 0 ? 0 : preferredCell.X >= width ? width - 1 : preferredCell.X;
        return new GridCell(fallbackX, overflowY + height + 32);
    }

    public static bool IsInventorySlotsTailCell(int inventoryWidth, int fixedRegularRows, GridCell cell)
    {
        int width = Math.Max(1, inventoryWidth);
        int regularRows = Math.Max(0, fixedRegularRows);
        return cell.X >= 0 &&
               cell.X < width &&
               cell.Y >= regularRows;
    }

    public static bool TrySelectLoadPreservationTailCell(
        int inventoryWidth,
        int inventoryHeight,
        int fixedRegularRows,
        GridCell requestedCell,
        Func<int, int, bool> isOccupied,
        out GridCell selectedCell)
    {
        selectedCell = new GridCell(-1, -1);
        if (!IsInventorySlotsTailCell(inventoryWidth, fixedRegularRows, requestedCell))
        {
            return false;
        }

        selectedCell = SelectNonOverlappingPreservationCell(
            inventoryWidth,
            inventoryHeight,
            requestedCell,
            isOccupied);
        return true;
    }

    public static bool TrySelectFirstFreeCell(
        int inventoryWidth,
        int rowCount,
        Func<int, int, bool> isAllowed,
        Func<int, int, bool> isOccupied,
        out GridCell cell)
    {
        int width = Math.Max(1, inventoryWidth);
        int rows = Math.Max(0, rowCount);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (isAllowed(x, y) && !isOccupied(x, y))
                {
                    cell = new GridCell(x, y);
                    return true;
                }
            }
        }

        cell = new GridCell(-1, -1);
        return false;
    }
}
