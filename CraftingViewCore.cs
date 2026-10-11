using System;

namespace InventorySlots;

internal enum CraftingViewMode { Grid, List }

internal static class CraftingViewCore
{
    internal const int ListRows = 14;

    // Refinement previews the successful native outcome, even above the safe cap.
    // At an ordinary station an already-refined item must not preview a downgrade.
    internal static int UpgradePreviewQuality(int current, int safeMaximum, bool refinement) =>
        refinement ? current + 1 : Math.Min(current + 1, Math.Max(current, safeMaximum));

    internal static bool UseList(CraftingViewMode mode, CraftingTabAdapterKind tab) =>
        mode == CraftingViewMode.List && new CraftingTabAdapterState(tab).IsRedesign;

    internal static int PageStart(bool list, int page, int capacity) => list ? page : page * capacity;

    internal static int PageCount(bool list, int count, int capacity) =>
        list ? Math.Max(1, count - capacity + 1) : Math.Max(1, (count + capacity - 1) / capacity);

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

    // A list scroll does not change selection. Reveal it only after a selection,
    // filter or view-mode change, retaining the current window when possible.
    internal static int RevealSelection(bool list, int page, int selected, int count, int capacity)
    {
        int last = PageCount(list, count, capacity) - 1;
        if (selected < 0 || selected >= count) return 0;
        if (!list) return Math.Min(last, selected / capacity);
        int start = Math.Max(0, Math.Min(last, page));
        if (selected < start) start = selected;
        else if (selected >= start + capacity) start = selected - capacity + 1;
        return Math.Max(0, Math.Min(last, start));
    }
}
