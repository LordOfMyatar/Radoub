using System;
using System.Collections.Generic;

namespace Radoub.Formats.Common;

/// <summary>
/// Thread-safe least-recently-used cache with a fixed capacity.
/// Used by services (e.g. TextureService) that previously held unbounded Dictionaries
/// and could grow indefinitely during long sessions (#2034).
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _map;
    private readonly LinkedList<Entry> _order;
    private readonly object _lock = new();

    public LruCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<Entry>>(capacity);
        _order = new LinkedList<Entry>();
    }

    public int Capacity => _capacity;

    public int Count
    {
        get { lock (_lock) return _map.Count; }
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default;
            return false;
        }
    }

    public void Set(TKey key, TValue? value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                existing.Value = new Entry(key, value);
                _order.AddFirst(existing);
                return;
            }

            if (_map.Count >= _capacity)
            {
                var tail = _order.Last;
                if (tail != null)
                {
                    _order.RemoveLast();
                    _map.Remove(tail.Value.Key);
                }
            }

            var node = new LinkedListNode<Entry>(new Entry(key, value));
            _order.AddFirst(node);
            _map[key] = node;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _map.Clear();
            _order.Clear();
        }
    }

    private readonly struct Entry
    {
        public Entry(TKey key, TValue? value)
        {
            Key = key;
            Value = value;
        }
        public TKey Key { get; }
        public TValue? Value { get; }
    }
}
