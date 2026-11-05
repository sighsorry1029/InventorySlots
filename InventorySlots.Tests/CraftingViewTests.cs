using InventorySlots;

internal static class CraftingViewTests
{
    public static void RefinementPreviewsSuccessfulQuality()
    {
        Assert.Equal(5, CraftingViewCore.UpgradePreviewQuality(4, 3, true));
        Assert.Equal(4, CraftingViewCore.UpgradePreviewQuality(3, 3, true));
        Assert.Equal(2, CraftingViewCore.UpgradePreviewQuality(1, 3, true));
        Assert.Equal(1, CraftingViewCore.UpgradePreviewQuality(0, 3, true));
        Assert.Equal(2, CraftingViewCore.UpgradePreviewQuality(1, 3, false));
        Assert.Equal(3, CraftingViewCore.UpgradePreviewQuality(3, 3, false));
        Assert.Equal(4, CraftingViewCore.UpgradePreviewQuality(4, 3, false));
    }

    public static void ListSupportsKnownCraftingTabs()
    {
        CraftingTabAdapterKind[] supported =
        {
            CraftingTabAdapterKind.Vanilla,
            CraftingTabAdapterKind.JewelcraftingSocket,
            CraftingTabAdapterKind.RecycleNReclaim,
            CraftingTabAdapterKind.PathOfValheimanSalvage
        };
        foreach (CraftingTabAdapterKind tab in Enum.GetValues<CraftingTabAdapterKind>())
        {
            Assert.False(CraftingViewCore.UseList(CraftingViewMode.Grid, tab), "An explicit Grid selection must remain Grid for every adapter");
            Assert.Equal(supported.Contains(tab), CraftingViewCore.UseList(CraftingViewMode.List, tab));
        }
    }

    public static void ListScrollWindows()
    {
        int rows = CraftingViewCore.ListRows;
        foreach (int count in new[] { 0, 1, rows - 1, rows, rows + 1, 29, 300 })
        {
            int pages = CraftingViewCore.PageCount(count, rows, 1);
            Assert.True(pages >= 1, "An empty or short list still needs a valid scroll position");
            HashSet<int> seen = new();
            int previousStart = -1;
            for (int page = 0; page < pages; page++)
            {
                int start = CraftingViewCore.PageStart(page, 1);
                Assert.Equal(previousStart + 1, start);
                if (count > rows) Assert.True(start + rows <= count, "No blank tail should be introduced by scrolling");
                for (int i = start; i < Math.Min(count, start + rows); i++) seen.Add(i);
                previousStart = start;
            }
            Assert.Equal(count, seen.Count);
            if (count > rows) Assert.True(seen.Contains(count - 1), "The last recipe must be reachable");
        }
    }

    public static void ListSelectionReveal()
    {
        int rows = CraftingViewCore.ListRows;
        const int start = 20;
        for (int selected = start; selected < start + rows; selected++)
            Assert.Equal(start, CraftingViewCore.RevealSelection(start, selected, 100, rows, 1));
        Assert.Equal(19, CraftingViewCore.RevealSelection(start, 19, 100, rows, 1));
        Assert.Equal(21, CraftingViewCore.RevealSelection(start, start + rows, 100, rows, 1));
        Assert.Equal(0, CraftingViewCore.RevealSelection(start, -1, 0, rows, 1));
        Assert.Equal(0, CraftingViewCore.RevealSelection(start, 3, 5, rows, 1));
    }

    public static void SwitchKeepsSelectedRecipe()
    {
        // Filtered/sorted positions differ from the original recipe indices.
        int[] view = Enumerable.Range(0, 111).Select(i => 1000 - i * 3).ToArray();
        foreach (int selected in new[] { 0, 13, 14, 35, 36, 110 })
        {
            foreach (int gridDimension in new[] { 4, 6, 8 })
            {
                foreach (bool list in new[] { false, true, false })
                {
                    int capacity = list ? CraftingViewCore.ListRows : gridDimension * gridDimension;
                    int columns = list ? 1 : gridDimension;
                    int page = CraftingViewCore.RevealSelection(0, selected, view.Length, capacity, columns);
                    int start = CraftingViewCore.PageStart(page, columns);
                    int[] visible = view.Skip(start).Take(capacity).ToArray();
                    Assert.True(visible.Contains(view[selected]), "Changing presentation must still expose the same recipe identity");
                    Assert.Equal(0, start % columns);
                }
            }
        }
    }

    public static void ScrollModesCoverEveryRecipe()
    {
        foreach (int columns in new[] { 1, 4, 5, 6, 7, 8 })
        {
            int rows = columns == 1 ? CraftingViewCore.ListRows : columns;
            int capacity = rows * columns;
            foreach (int count in new[] { 0, 1, capacity - 1, capacity, capacity + 1, capacity + columns, 301 })
            foreach (CraftingScrollMode mode in Enum.GetValues<CraftingScrollMode>())
            {
                int row = 0;
                int step = mode == CraftingScrollMode.Row ? 1 : rows;
                HashSet<int> seen = new();
                while (true)
                {
                    int start = CraftingViewCore.PageStart(row, columns);
                    Assert.Equal(0, start % columns);
                    for (int i = start; i < Math.Min(count, start + capacity); i++) seen.Add(i);
                    if (count > capacity)
                        Assert.True(start + capacity - columns < count, "Scrolling must not expose a wholly blank trailing row");
                    int next = CraftingViewCore.Scroll(row, 1, mode, count, capacity, columns);
                    Assert.True(next >= row && next - row <= step, "A scroll moves at most the selected row/page distance");
                    if (next == row) break;
                    if (start + capacity + step * columns <= count) Assert.Equal(row + step, next);
                    row = next;
                }
                Assert.Equal(count, seen.Count);
                Assert.Equal(CraftingViewCore.PageCount(count, capacity, columns) - 1, row);
                while (row > 0)
                {
                    int next = CraftingViewCore.Scroll(row, -1, mode, count, capacity, columns);
                    Assert.True(next < row && row - next <= step, "Reverse scrolling must reach the first row");
                    row = next;
                }
                Assert.Equal(0, CraftingViewCore.Scroll(row, -1, mode, count, capacity, columns));
            }
        }
        // Page scrolling moves a viewport from the current row, including at the tail.
        int page = CraftingViewCore.Scroll(0, 1, CraftingScrollMode.Page, 100, 36, 6);
        Assert.Equal(6, page);
        page = CraftingViewCore.Scroll(page, 1, CraftingScrollMode.Page, 100, 36, 6);
        Assert.Equal(11, page);
        Assert.Equal(5, CraftingViewCore.Scroll(page, -1, CraftingScrollMode.Page, 100, 36, 6));
        // Live mode changes continue at the same row; they do not reinterpret it as a page number.
        Assert.Equal(10, CraftingViewCore.Scroll(page, -1, CraftingScrollMode.Row, 100, 36, 6));
        Assert.Equal(14, CraftingViewCore.Scroll(0, 1, CraftingScrollMode.Page, 100, 14, 1));
    }

    public static void GridRevealAndZoomKeepSelectionVisible()
    {
        const int count = 301;
        foreach (int columns in new[] { 4, 5, 6, 7, 8 })
        {
            int capacity = columns * columns;
            const int row = 3;
            int start = row * columns;
            for (int selected = start; selected < start + capacity; selected++)
                Assert.Equal(row, CraftingViewCore.RevealSelection(row, selected, count, capacity, columns));
            Assert.Equal(row - 1, CraftingViewCore.RevealSelection(row, start - 1, count, capacity, columns));
            Assert.Equal(row + 1, CraftingViewCore.RevealSelection(row, start + capacity, count, capacity, columns));

            foreach (int selected in new[] { 0, 15, 35, 36, 100, count - 1 })
            foreach (int nextColumns in new[] { 4, 5, 6, 7, 8 })
            {
                int before = CraftingViewCore.RevealSelection(row, selected, count, capacity, columns);
                int after = CraftingViewCore.RevealSelection(before, selected, count, nextColumns * nextColumns, nextColumns);
                int nextStart = CraftingViewCore.PageStart(after, nextColumns);
                Assert.True(selected >= nextStart && selected < nextStart + nextColumns * nextColumns,
                    "Zoom must retain the selected recipe, including the last partial row");
            }
        }
    }
}
