﻿using System;
using System.Collections.Generic;

namespace SLGame
{
    class PriorityQueue<T> where T : IComparable<T>
    {
        const int HEAP_SIZE = 100;

        T[] heap;

        public int Count { get; private set; }

        public PriorityQueue() : this(HEAP_SIZE) { }

        public PriorityQueue(int capacity)
        {
            heap = new T[capacity];
        }

        public void Push(T v)
        {
            if (Count >= heap.Length) Array.Resize(ref heap, Count * 2);
            heap[Count] = v;
            SiftUp(Count++);
        }

        public T Pop()
        {
            var v = Top();
            heap[0] = heap[--Count];
            if (Count > 0) SiftDown(0);
            return v;
        }

        public T Top()
        {
            if (Count > 0) return heap[0];
            return default;
        }

        void SiftUp(int n)
        {
            var v = heap[n];
            for (var n2 = n / 2; n > 0 && v.CompareTo(heap[n2]) > 0; n = n2, n2 /= 2) heap[n] = heap[n2];
            heap[n] = v;
        }

        void SiftDown(int n)
        {
            var v = heap[n];
            for (var n2 = n * 2; n2 < Count; n = n2, n2 *= 2)
            {
                if (n2 + 1 < Count && heap[n2 + 1].CompareTo(heap[n2]) > 0) n2++;
                if (v.CompareTo(heap[n2]) >= 0) break;
                heap[n] = heap[n2];
            }
            heap[n] = v;
        }
    }
}