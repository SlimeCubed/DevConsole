using System;

namespace DevConsole
{
    /// <summary>
    /// A fixed size queue that automatically discards old entries when out of space.
    /// </summary>
    internal class LineQueue<T>
    {
        private T[] data;
        private int position;

        public int Count { get; private set; }
        public int Capacity => data.Length;

        public LineQueue(int capacity)
        {
            data = new T[capacity];
            position = capacity - 1;
        }

        public T this[int i]
        {
            get => data[(i + position) % data.Length];
            set => data[(i + position) % data.Length] = value;
        }

        public void Enqueue(T value)
        {
            if (position == 0)
                position = Capacity - 1;
            else
                position--;

            data[position] = value;

            if (Count < Capacity)
                Count++;
        }

        public void Clear()
        {
            Array.Clear(data, 0, data.Length);
            Count = 0;
            position = Capacity - 1;
        }
    }
}
