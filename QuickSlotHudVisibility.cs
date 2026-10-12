using System;

namespace InventorySlots;

internal enum QuickSlotHudMode
{
    Off,
    AllOnUse,
    UsedSlotOnUse,
    On
}

// Transient display state only. Timers follow slot identities, not the items in them.
internal sealed class QuickSlotHudVisibility
{
    private const float HoldSeconds = 3f;
    private const float FadeSeconds = 0.25f;
    private bool _initialized;
    private QuickSlotHudMode _mode;
    private int _playerId;
    private int _definitionVersion;
    private float[] _slotUseTimes = Array.Empty<float>();
    private float _lastUseTime = float.NegativeInfinity;

    public void SyncContext(QuickSlotHudMode mode, int playerId, int definitionVersion, int unlockedCount)
    {
        unlockedCount = Math.Max(0, unlockedCount);
        if (_initialized && _mode == mode && _playerId == playerId &&
            _definitionVersion == definitionVersion && _slotUseTimes.Length == unlockedCount)
        {
            return;
        }

        _mode = mode;
        _playerId = playerId;
        _definitionVersion = definitionVersion;
        if (_slotUseTimes.Length != unlockedCount)
        {
            _slotUseTimes = new float[unlockedCount];
        }

        ResetTimers();
        _initialized = true;
    }

    public void NotifyUse(int slotIndex, float now)
    {
        if (!_initialized || slotIndex < 0 || slotIndex >= _slotUseTimes.Length ||
            (_mode != QuickSlotHudMode.AllOnUse && _mode != QuickSlotHudMode.UsedSlotOnUse))
        {
            return;
        }

        _lastUseTime = now;
        _slotUseTimes[slotIndex] = now;
    }

    public bool IsVisible(float now) => _initialized && _slotUseTimes.Length > 0 &&
        (_mode == QuickSlotHudMode.On ||
         ((_mode == QuickSlotHudMode.AllOnUse || _mode == QuickSlotHudMode.UsedSlotOnUse) &&
          FadeAlpha(_lastUseTime, now) > 0f));

    public float GetSlotAlpha(int slotIndex, float now)
    {
        if (!_initialized || slotIndex < 0 || slotIndex >= _slotUseTimes.Length)
        {
            return 0f;
        }

        return _mode switch
        {
            QuickSlotHudMode.On => 1f,
            QuickSlotHudMode.AllOnUse => FadeAlpha(_lastUseTime, now),
            QuickSlotHudMode.UsedSlotOnUse => FadeAlpha(_slotUseTimes[slotIndex], now),
            _ => 0f
        };
    }

    public void Clear()
    {
        _initialized = false;
        ResetTimers();
    }

    private void ResetTimers()
    {
        _lastUseTime = float.NegativeInfinity;
        for (int i = 0; i < _slotUseTimes.Length; i++)
        {
            _slotUseTimes[i] = float.NegativeInfinity;
        }
    }

    private static float FadeAlpha(float usedAt, float now) =>
        Math.Max(0f, Math.Min(1f, 1f - (now - usedAt - HoldSeconds) / FadeSeconds));
}
