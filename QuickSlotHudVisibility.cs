using System;
using System.Collections.Generic;

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
    private float[] _slotUseTimes = Array.Empty<float>();
    private string[] _slotIds = Array.Empty<string>();
    private float _lastUseTime = float.NegativeInfinity;

    public void SyncContext(QuickSlotHudMode mode, int playerId, IReadOnlyList<string> slotIds)
    {
        int unlockedCount = slotIds.Count;
        if (_initialized && _mode == mode && _playerId == playerId && SameSlots(slotIds))
        {
            return;
        }

        _mode = mode;
        _playerId = playerId;
        if (_slotUseTimes.Length != unlockedCount)
        {
            _slotUseTimes = new float[unlockedCount];
            _slotIds = new string[unlockedCount];
        }
        for (int i = 0; i < unlockedCount; i++) _slotIds[i] = slotIds[i];

        ResetTimers();
        _initialized = true;
    }

    private bool SameSlots(IReadOnlyList<string> slots)
    {
        if (_slotIds.Length != slots.Count) return false;
        for (int i = 0; i < slots.Count; i++)
            if (!string.Equals(_slotIds[i], slots[i], StringComparison.Ordinal)) return false;
        return true;
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
