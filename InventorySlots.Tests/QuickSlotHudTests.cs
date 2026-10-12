using InventorySlots;

internal static class QuickSlotHudTests
{
    public static void PersistentModes()
    {
        var hud = new QuickSlotHudVisibility();
        Assert.Equal(false, hud.IsVisible(0));
        foreach (QuickSlotHudMode mode in Enum.GetValues<QuickSlotHudMode>())
        {
            hud.SyncContext(mode, 1, 1, 9);
            Assert.Equal(mode == QuickSlotHudMode.On, hud.IsVisible(0));
            for (int i = 0; i < 9; i++)
                Assert.Equal(mode == QuickSlotHudMode.On ? 1f : 0f, hud.GetSlotAlpha(i, 0));
        }

        hud.SyncContext(QuickSlotHudMode.Off, 1, 1, 9);
        hud.NotifyUse(0, 10);
        Assert.Equal(false, hud.IsVisible(10));
        hud.SyncContext(QuickSlotHudMode.On, 1, 1, 9);
        Assert.Equal(1f, hud.GetSlotAlpha(8, 1000));
        hud.SyncContext(QuickSlotHudMode.On, 1, 1, 0);
        Assert.Equal(false, hud.IsVisible(1000));
        Assert.Equal(0f, hud.GetSlotAlpha(0, 1000));
    }

    public static void WholePanelTiming()
    {
        var hud = new QuickSlotHudVisibility();
        hud.SyncContext(QuickSlotHudMode.AllOnUse, 1, 1, 9);
        hud.NotifyUse(5, 10);
        foreach (int i in Enumerable.Range(0, 9))
        {
            Assert.Equal(1f, hud.GetSlotAlpha(i, 10));
            Assert.Equal(1f, hud.GetSlotAlpha(i, 13));
            Assert.Equal(0.5f, hud.GetSlotAlpha(i, 13.125f));
            Assert.Equal(0f, hud.GetSlotAlpha(i, 13.25f));
        }
        Assert.Equal(false, hud.IsVisible(13.25f));

        hud.NotifyUse(1, 13.125f); // Another slot restarts the entire fading panel.
        Assert.Equal(1f, hud.GetSlotAlpha(8, 13.125f));
        Assert.Equal(1f, hud.GetSlotAlpha(5, 16.125f));
        Assert.Equal(false, hud.IsVisible(16.375f));
    }

    public static void IndependentSlots()
    {
        var hud = new QuickSlotHudVisibility();
        hud.SyncContext(QuickSlotHudMode.UsedSlotOnUse, 1, 1, 9);
        hud.NotifyUse(7, 10);
        hud.NotifyUse(1, 11);
        for (int i = 0; i < 9; i++)
            Assert.Equal(i == 1 || i == 7 ? 1f : 0f, hud.GetSlotAlpha(i, 11));
        Assert.Equal(0.5f, hud.GetSlotAlpha(7, 13.125f));
        Assert.Equal(1f, hud.GetSlotAlpha(1, 13.125f));
        hud.NotifyUse(7, 13.125f);
        Assert.Equal(1f, hud.GetSlotAlpha(7, 13.125f));
        Assert.Equal(0f, hud.GetSlotAlpha(1, 14.25f));
        Assert.Equal(true, hud.IsVisible(14.25f));
        Assert.Equal(false, hud.IsVisible(16.375f));
    }

    public static void ContextChanges()
    {
        var hud = new QuickSlotHudVisibility();
        var baseline = (QuickSlotHudMode.UsedSlotOnUse, 1, 1, 9);
        foreach (var changed in new[]
        {
            (QuickSlotHudMode.AllOnUse, 1, 1, 9),
            (QuickSlotHudMode.UsedSlotOnUse, 2, 1, 9),
            (QuickSlotHudMode.UsedSlotOnUse, 1, 2, 9),
            (QuickSlotHudMode.UsedSlotOnUse, 1, 1, 6)
        })
        {
            hud.SyncContext(baseline.Item1, baseline.Item2, baseline.Item3, baseline.Item4);
            hud.NotifyUse(2, 10);
            hud.SyncContext(baseline.Item1, baseline.Item2, baseline.Item3, baseline.Item4);
            Assert.Equal(1f, hud.GetSlotAlpha(2, 11)); // Ordinary updates retain the reveal.
            hud.SyncContext(changed.Item1, changed.Item2, changed.Item3, changed.Item4);
            Assert.Equal(false, hud.IsVisible(11));
            hud.NotifyUse(0, 11);
            hud.SyncContext(changed.Item1, changed.Item2, changed.Item3, changed.Item4);
            Assert.Equal(1f, hud.GetSlotAlpha(0, 11)); // First input before the HUD update survives.
        }

        hud.Clear();
        Assert.Equal(false, hud.IsVisible(11));
        hud.SyncContext(QuickSlotHudMode.UsedSlotOnUse, 1, 1, 9);
        Assert.Equal(0f, hud.GetSlotAlpha(0, 11)); // No stale slot returns after a reload/growth.
    }

    public static void HiddenTimeAndInvalidSlots()
    {
        foreach (var mode in new[] { QuickSlotHudMode.AllOnUse, QuickSlotHudMode.UsedSlotOnUse })
        {
            var hud = new QuickSlotHudVisibility();
            hud.SyncContext(mode, 1, 1, 3);
            hud.NotifyUse(2, 10);
            // No rendering or timer ticking while the native HUD/panel is hidden.
            hud.NotifyUse(-1, 15);
            hud.NotifyUse(3, 15);
            Assert.Equal(false, hud.IsVisible(15));
            Assert.Equal(0f, hud.GetSlotAlpha(2, 15));
            Assert.Equal(0f, hud.GetSlotAlpha(3, 15));
            hud.NotifyUse(2, 15);
            Assert.Equal(1f, hud.GetSlotAlpha(2, 15));
        }
    }
}
