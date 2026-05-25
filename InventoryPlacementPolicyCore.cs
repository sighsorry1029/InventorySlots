using System;

namespace InventorySlots;

internal enum InventoryPlacementScope
{
    General,
    Container,
    LocalPlayer,
    LoadPreservation
}

internal enum InventoryPlacementQueryPlan
{
    RunOriginal,
    TopFirstAllCells,
    LocalPlayerRegularCells,
    LoadPreservationRegularCells
}

internal static class InventoryPlacementPolicyCore
{
    public static InventoryPlacementQueryPlan SelectQueryPlan(InventoryPlacementScope scope) =>
        scope switch
        {
            InventoryPlacementScope.Container => InventoryPlacementQueryPlan.TopFirstAllCells,
            InventoryPlacementScope.LocalPlayer => InventoryPlacementQueryPlan.LocalPlayerRegularCells,
            InventoryPlacementScope.LoadPreservation => InventoryPlacementQueryPlan.LoadPreservationRegularCells,
            _ => InventoryPlacementQueryPlan.RunOriginal
        };

    public static bool TrySelectTopFirstCell(
        int inventoryWidth,
        int rowCount,
        Func<int, int, bool> isAllowed,
        Func<int, int, bool> isOccupied,
        out InventorySlotSafetyCore.GridCell cell) =>
        InventorySlotSafetyCore.TrySelectFirstFreeCell(
            inventoryWidth,
            rowCount,
            isAllowed,
            isOccupied,
            out cell);

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
}
