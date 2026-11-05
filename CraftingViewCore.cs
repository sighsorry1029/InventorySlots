using System;

namespace InventorySlots;

internal enum CraftingViewMode { Grid, List }
internal enum CraftingScrollMode { Row, Page }

internal static class CraftingViewCore
{
    internal const int ListRows = 14;

    // Refinement previews the successful native outcome, even above the safe cap.
    // At an ordinary station an already-refined item must not preview a downgrade.
    internal static int UpgradePreviewQuality(int current, int safeMaximum, bool refinement) =>
        refinement ? current + 1 : Math.Min(current + 1, Math.Max(current, safeMaximum));

    internal static bool UseList(CraftingViewMode mode, CraftingTabAdapterKind tab) =>
        mode == CraftingViewMode.List && new CraftingTabAdapterState(tab).IsRedesign;

    // Both views store the top visible row. Scroll mode only changes the input
    // distance, so switching Row/Page live never reinterprets the current position.
    internal static int PageStart(int topRow, int columns) => topRow * columns;

    internal static int VisibleRows(int capacity, int columns) => Math.Max(1, capacity / columns);

    internal static int PageCount(int count, int capacity, int columns) =>
        Math.Max(1, (count + columns - 1) / columns - VisibleRows(capacity, columns) + 1);

    internal static int Scroll(int topRow, int direction, CraftingScrollMode mode, int count, int capacity, int columns)
    {
        int step = mode == CraftingScrollMode.Page ? VisibleRows(capacity, columns) : 1;
        return Math.Max(0, Math.Min(PageCount(count, capacity, columns) - 1, topRow + Math.Sign(direction) * step));
    }

    // Navigate displayed positions, never native recipe indices. Grid rows do
    // not wrap horizontally; a short final row uses its nearest existing cell.
    internal static int MoveSelection(bool list, int selected, int count, int columns, int horizontal, int vertical)
    {
        if (count <= 0) return -1;
        if (selected < 0 || selected >= count) return 0;
        if (list) return Math.Max(0, Math.Min(count - 1, selected + Math.Sign(vertical)));

        columns = Math.Max(1, columns);
        int rowStart = selected / columns * columns;
        if (vertical < 0) return rowStart == 0 ? selected : selected - columns;
        if (vertical > 0)
            return rowStart + columns >= count ? selected : Math.Min(count - 1, selected + columns);
        return Math.Max(rowStart, Math.Min(Math.Min(count - 1, rowStart + columns - 1), selected + Math.Sign(horizontal)));
    }

    // Retain a visible selection's window, including a partially scrolled grid.
    // List callers only reveal after a selection, filter or view-mode change.
    internal static int RevealSelection(int topRow, int selected, int count, int capacity, int columns)
    {
        int last = PageCount(count, capacity, columns) - 1;
        if (selected < 0 || selected >= count) return 0;
        int start = Math.Max(0, Math.Min(last, topRow));
        int selectedRow = selected / columns;
        int visibleRows = VisibleRows(capacity, columns);
        if (selectedRow < start) start = selectedRow;
        else if (selectedRow >= start + visibleRows) start = selectedRow - visibleRows + 1;
        return Math.Max(0, Math.Min(last, start));
    }
}
