using System;

namespace InventoryActions;

internal static class ContainerActionCore
{
    internal static int CountMovedAmount(int before, int after, int requestedAmount, bool moveSucceeded, bool useMoveSucceededFallback)
    {
        int moved = Math.Max(0, before - after);
        return moved == 0 && moveSucceeded && useMoveSucceededFallback ? Math.Max(0, requestedAmount) : moved;
    }

    internal static int CompareGridOrder(int leftX, int leftY, int rightX, int rightY)
    {
        int y = leftY.CompareTo(rightY);
        return y != 0 ? y : leftX.CompareTo(rightX);
    }
}
