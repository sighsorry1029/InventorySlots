using InventorySlots;

internal static class PathOfValheimanTests
{
    public static void RowLimits()
    {
        var rows = new PathOfValheimanRows();
        rows.Load(null);
        Assert.Equal(4, rows.TotalRows(4, 9));
        rows.Update(3, 4, -1);
        Assert.Equal(7, rows.TotalRows(4, 9));
        Assert.Equal(9, rows.TotalRows(InventorySlotSafetyCore.GetCombinedRegularRows(4, 3), 9));
        Assert.Equal(9, rows.TotalRows(InventorySlotSafetyCore.GetCombinedRegularRows(6, 3), 9));
        rows.Update(1, 4, -1);
        Assert.Equal(5, rows.TotalRows(4, 9));
        Assert.Equal(9, rows.TotalRows(9, 9));
    }

    public static void RowRemoval()
    {
        var rows = new PathOfValheimanRows();
        rows.Load("3");
        rows.Update(0, 4, 6); // Last bonus row still contains an item.
        Assert.Equal(7, rows.TotalRows(4, 9));
        rows.Update(0, 4, 4);
        Assert.Equal(5, rows.TotalRows(4, 9));
        rows.Update(0, 4, -1);
        Assert.Equal(4, rows.TotalRows(4, 9));
        rows.Update(2, 4, -1);
        Assert.Equal(6, rows.TotalRows(4, 9));
    }

    public static void SavedRows()
    {
        foreach (var (saved, expected) in new (string?, int)[] { (null, 4), ("", 4), ("bad", 4), ("-1", 4), ("2", 6), ("99", 7) })
        {
            var rows = new PathOfValheimanRows();
            rows.Load(saved);
            Assert.True(rows.Loaded, "saved state is resolved once per player/load");
            Assert.Equal(expected, rows.TotalRows(4, 9));
        }
        var first = new PathOfValheimanRows();
        var second = new PathOfValheimanRows();
        first.Load("3");
        second.Load("0");
        Assert.Equal(7, first.TotalRows(4, 9));
        Assert.Equal(4, second.TotalRows(4, 9));
    }
}
