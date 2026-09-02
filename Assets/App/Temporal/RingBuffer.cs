using System;

namespace MemeAR.Temporal
{
    /// <summary>
    /// Fixed-capacity circular buffer. Guarantees bounded memory (no unbounded growth):
    /// once full, the oldest element is overwritten. Enumerated oldest-to-newest via index.
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _start;
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity < 1)
            {
                capacity = 1;
            }

            _items = new T[capacity];
        }

        public int Capacity => _items.Length;
        public int Count => _count;
        public bool IsFull => _count == _items.Length;

        /// <summary>Adds an item; returns the evicted item (default if none was evicted).</summary>
        public T Add(T item)
        {
            T evicted = default;

            if (_count < _items.Length)
            {
                int index = (_start + _count) % _items.Length;
                _items[index] = item;
                _count++;
            }
            else
            {
                evicted = _items[_start];
                _items[_start] = item;
                _start = (_start + 1) % _items.Length;
            }

            return evicted;
        }

        /// <summary>Indexer: 0 = oldest, Count-1 = newest.</summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                {
                    throw new IndexOutOfRangeException();
                }

                return _items[(_start + index) % _items.Length];
            }
        }

        public T Newest => _count == 0 ? default : this[_count - 1];
        public T Oldest => _count == 0 ? default : this[0];

        public void Clear()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                _items[i] = default;
            }

            _start = 0;
            _count = 0;
        }
    }
}
