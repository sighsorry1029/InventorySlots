namespace InventoryActions;

// A timeout/cancel stops the batch, not the external transaction. Keep the gate
// closed until that same response has finished applying; never infer a refund.
internal sealed class MultiUserChestRequestGate
{
    private int? _requestId;
    private float _started;
    internal bool Busy { get; private set; }
    internal bool Applying { get; private set; }
    internal bool Completed { get; private set; }
    internal bool ContinueBatch { get; private set; }
    internal bool Uncertain { get; private set; }
    internal bool Registered => _requestId.HasValue;

    internal bool Begin(float now)
    {
        if (Busy) return false;
        Busy = true;
        ContinueBatch = true;
        _started = now;
        return true;
    }

    internal bool Register(int id)
    {
        if (!Busy || _requestId.HasValue) return false;
        _requestId = id; // MUC uses the full signed Int32 range, including zero.
        return true;
    }

    internal bool BeginResponse(int id)
    {
        if (!Busy || Completed || Applying || _requestId != id) return false;
        Applying = true;
        return true;
    }

    internal void FinishResponse(bool fullyApplied)
    {
        if (!Applying) return;
        Applying = false;
        Completed = true;
        ContinueBatch &= fullyApplied;
    }

    internal void Stop() => ContinueBatch = false;

    internal bool CheckTimeout(float now)
    {
        if (!Busy || Completed || Uncertain || now - _started < 10f) return false;
        Uncertain = true;
        Stop();
        return true;
    }
}
