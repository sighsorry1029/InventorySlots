using System;

namespace InventorySlots;

internal enum CraftingViewMode { Grid, List }

internal static class CraftingViewCore
{
    internal const int ListRows = 14;

    internal static bool UseList(CraftingViewMode mode, CraftingTabAdapterKind tab) =>
        mode == CraftingViewMode.List && new CraftingTabAdapterState(tab).IsRedesign;

    internal static int PageStart(bool list, int page, int capacity) => list ? page : page * capacity;

    internal static int PageCount(bool list, int count, int capacity) =>
        list ? Math.Max(1, count - capacity + 1) : Math.Max(1, (count + capacity - 1) / capacity);

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
