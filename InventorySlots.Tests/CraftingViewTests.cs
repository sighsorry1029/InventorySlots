using InventorySlots;

internal static class CraftingViewTests
{
    public static void ListSupportsKnownCraftingTabs()
    {
        CraftingTabAdapterKind[] supported =
        {
            CraftingTabAdapterKind.Vanilla,
            CraftingTabAdapterKind.JewelcraftingSocket,
            CraftingTabAdapterKind.RecycleNReclaim
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
            int pages = CraftingViewCore.PageCount(true, count, rows);
            Assert.True(pages >= 1, "An empty or short list still needs a valid scroll position");
            HashSet<int> seen = new();
            int previousStart = -1;
            for (int page = 0; page < pages; page++)
            {
                int start = CraftingViewCore.PageStart(true, page, rows);
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
            Assert.Equal(start, CraftingViewCore.RevealSelection(true, start, selected, 100, rows));
        Assert.Equal(19, CraftingViewCore.RevealSelection(true, start, 19, 100, rows));
        Assert.Equal(21, CraftingViewCore.RevealSelection(true, start, start + rows, 100, rows));
        Assert.Equal(0, CraftingViewCore.RevealSelection(true, start, -1, 0, rows));
        Assert.Equal(0, CraftingViewCore.RevealSelection(true, start, 3, 5, rows));
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
                    int page = CraftingViewCore.RevealSelection(list, 0, selected, view.Length, capacity);
                    int start = CraftingViewCore.PageStart(list, page, capacity);
                    int[] visible = view.Skip(start).Take(capacity).ToArray();
                    Assert.True(visible.Contains(view[selected]), "Changing presentation must still expose the same recipe identity");
                    if (!list) Assert.Equal(0, start % capacity);
                }
            }
        }
    }
}
