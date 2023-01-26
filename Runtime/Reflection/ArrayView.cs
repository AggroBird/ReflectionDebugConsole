// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Diagnostics;

namespace AggroBird.Reflection
{
    internal static class ArrayViewUtility
    {
        public static ArrayView<T> SubView<T>(this T[] arr, int off)
        {
            return new ArrayView<T>(arr, off);
        }
        public static ArrayView<T> SubView<T>(this T[] arr, int off, int len)
        {
            return new ArrayView<T>(arr, off, len);
        }
    }

    internal sealed class ArrayViewProxy<T>
    {
        private readonly T[] array;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => array;

        public ArrayViewProxy(ArrayView<T> arrayView)
        {
            array = new T[arrayView.Length];
            arrayView.CopyTo(0, array, 0, arrayView.Length);
        }
    }

    [DebuggerTypeProxy(typeof(ArrayViewProxy<>))]
    internal struct ArrayView<T>
    {
        public static ArrayView<T> Empty => default;

        private readonly T[] arr;
        private readonly int off;
        private readonly int len;


        public ArrayView(T[] arr)
        {
            this.arr = arr ?? Array.Empty<T>();
            off = 0;
            len = this.arr.Length;
        }
        public ArrayView(T[] arr, int off)
        {
            if (off < 0 || off >= arr.Length)
            {
                throw new IndexOutOfRangeException("Array subview out of range");
            }
            this.arr = arr;
            this.off = off;
            len = arr.Length - off;
        }
        public ArrayView(T[] arr, int off, int len)
        {
            if (off < 0 || len < 0 || off + len > arr.Length)
            {
                throw new IndexOutOfRangeException("Array subview out of range");
            }
            this.arr = arr;
            this.off = off;
            this.len = len;
        }

        private ArrayView(T[] arr, int viewOff, int viewLen, int off, int len)
        {
            if (off < 0 || len < 0 || off + len > viewLen)
            {
                throw new IndexOutOfRangeException("Array subview out of range");
            }
            this.arr = arr;
            this.off = off + viewOff;
            this.len = len;
        }


        public ArrayView<T> SubView(int off)
        {
            return new ArrayView<T>(arr, this.off, len, off, len - off);
        }
        public ArrayView<T> SubView(int off, int len)
        {
            return new ArrayView<T>(arr, this.off, this.len, off, len);
        }

        public static implicit operator ArrayView<T>(T[] fromArray)
        {
            return new ArrayView<T>(fromArray);
        }


        public T this[int idx]
        {
            get
            {
                if (idx < 0 || idx >= len)
                {
                    throw new IndexOutOfRangeException(nameof(idx));
                }
                return arr[off + idx];
            }
        }
        public int Length => len;
        public int Offset => off;

        public void CopyTo(int sourceIndex, T[] destinationArray, int destinationIndex, int length)
        {
            if (length != 0)
            {
                if (sourceIndex < 0 || length < 0 || sourceIndex + length > arr.Length)
                {
                    throw new IndexOutOfRangeException("Array subview out of range");
                }
                Array.Copy(arr, sourceIndex + off, destinationArray, destinationIndex, length);
            }
        }
        public T[] ToArray()
        {
            if (len == 0) return Array.Empty<T>();
            T[] result = new T[len];
            Array.Copy(arr, off, result, 0, len);
            return result;
        }
    }
}
#endif