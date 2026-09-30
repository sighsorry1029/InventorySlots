using InventoryActions;

internal static class MultiUserChestRestockTests
{
    internal static void ResponseCorrelation()
    {
        foreach (int id in new[] { int.MinValue, -1, 0, int.MaxValue })
        {
            var gate = new MultiUserChestRequestGate();
            Assert.Equal(true, gate.Begin(0));
            Assert.Equal(false, gate.Begin(1));
            Assert.Equal(true, gate.Register(id));
            Assert.Equal(false, gate.Register(42));
            Assert.Equal(false, gate.BeginResponse(42));
            Assert.Equal(false, gate.Completed);
            Assert.Equal(true, gate.BeginResponse(id));
            // MUC has removed its package/slot lock by this point, but the
            // destination has not necessarily changed yet.
            Assert.Equal(false, gate.Completed);
            Assert.Equal(false, gate.BeginResponse(id));
            gate.FinishResponse(true);
            Assert.Equal(true, gate.Completed && gate.ContinueBatch);
            Assert.Equal(false, gate.BeginResponse(id));
        }
    }

    internal static void TimeoutAndLateResponse()
    {
        var gate = new MultiUserChestRequestGate();
        gate.Begin(5);
        gate.Register(7);
        Assert.Equal(false, gate.CheckTimeout(14.9f));
        Assert.Equal(true, gate.CheckTimeout(15));
        Assert.Equal(false, gate.CheckTimeout(16));
        Assert.Equal(true, gate.Busy && gate.Uncertain && !gate.Completed);
        Assert.Equal(false, gate.Begin(30));
        Assert.Equal(true, gate.BeginResponse(7));
        gate.FinishResponse(true);
        Assert.Equal(true, gate.Completed);
        Assert.Equal(false, gate.ContinueBatch);
    }

    internal static void CancelDuringResponse()
    {
        var gate = new MultiUserChestRequestGate();
        gate.Begin(0);
        gate.Register(1);
        gate.BeginResponse(1);
        gate.Stop();
        Assert.Equal(true, gate.Applying && !gate.Completed);
        gate.FinishResponse(true);
        Assert.Equal(true, gate.Completed);
        Assert.Equal(false, gate.ContinueBatch);
    }

    internal static void PartialDelivery()
    {
        var gate = new MultiUserChestRequestGate();
        gate.Begin(0);
        gate.FinishResponse(true); // No matching response, no completion.
        Assert.Equal(false, gate.Completed);
        gate.Register(0);
        gate.BeginResponse(0);
        gate.FinishResponse(false);
        Assert.Equal(true, gate.Completed);
        Assert.Equal(false, gate.ContinueBatch);
    }
}
