using System;
using System.Collections.Generic;

namespace InventorySlots;

internal sealed class TooltipSourceCacheCore<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;
    private readonly List<TKey> _order = new();
    private readonly IEqualityComparer<TKey> _comparer;
    private readonly int _maxEntries;

    public TooltipSourceCacheCore(int maxEntries, IEqualityComparer<TKey>? comparer = null)
    {
        _maxEntries = Math.Max(1, maxEntries);
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
        _items = new Dictionary<TKey, TValue>(_comparer);
    }

    public int Count => _items.Count;

    public void Set(TKey key, TValue value)
    {
        if (!_items.ContainsKey(key))
        {
            _order.Add(key);
        }

        _items[key] = value;
        TrimOverflow();
    }

    public bool TryGet(TKey key, out TValue value) => _items.TryGetValue(key, out value!);

    public bool Remove(TKey key)
    {
        if (!_items.Remove(key))
        {
            return false;
        }

        RemoveOrderedKey(key);
        return true;
    }

    public int RemoveWhere(Func<TKey, TValue, bool> predicate)
    {
        int removed = 0;
        for (int i = _order.Count - 1; i >= 0; i--)
        {
            TKey key = _order[i];
            if (!_items.TryGetValue(key, out TValue? value))
            {
                _order.RemoveAt(i);
                continue;
            }

            if (!predicate(key, value!))
            {
                continue;
            }

            _items.Remove(key);
            _order.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    public void Clear()
    {
        _items.Clear();
        _order.Clear();
    }

    private void TrimOverflow()
    {
        while (_items.Count > _maxEntries && _order.Count > 0)
        {
            TKey key = _order[0];
            _order.RemoveAt(0);
            _items.Remove(key);
        }
    }

    private void RemoveOrderedKey(TKey key)
    {
        for (int i = 0; i < _order.Count; i++)
        {
            if (!_comparer.Equals(_order[i], key))
            {
                continue;
            }

            _order.RemoveAt(i);
            return;
        }
    }
}
