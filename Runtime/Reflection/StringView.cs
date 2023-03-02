// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Globalization;
using System.Text;

namespace AggroBird.Reflection
{
    internal static class StringViewUtility
    {
        public static StringView SubView(this string str, int off, int len)
        {
            return new StringView(str, off, len);
        }
        public static StringBuilder AppendStringView(this StringBuilder stringBuilder, StringView stringView)
        {
            if (stringView.Length > 0)
            {
                stringBuilder.Append(stringView.GetString(), stringView.Offset, stringView.Length);
            }
            return stringBuilder;
        }
    }

    internal struct StringView : IComparable<StringView>, IEquatable<StringView>
    {
        public static StringView Empty => default;

        private readonly string str;
        private readonly int off;
        private readonly int len;


        public StringView(string str)
        {
            this.str = str ?? string.Empty;
            off = 0;
            len = this.str.Length;
        }
        public StringView(string str, int off)
        {
            if (off < 0 || off >= str.Length)
            {
                throw new IndexOutOfRangeException("Array subview out of range");
            }
            this.str = str;
            this.off = off;
            len = str.Length - off;
        }
        public StringView(string str, int off, int len)
        {
            if (off < 0 || len < 0 || off + len > str.Length)
            {
                throw new IndexOutOfRangeException("Array subview out of range");
            }
            this.str = str;
            this.off = off;
            this.len = len;
        }

        private StringView(string str, int viewOff, int viewLen, int off, int len)
        {
            if (off < 0 || len < 0 || off + len > viewLen)
            {
                throw new IndexOutOfRangeException("Array subview out of range");
            }
            this.str = str;
            this.off = off + viewOff;
            this.len = len;
        }


        public StringView SubView(int off)
        {
            return new StringView(str, this.off, len, off, len - off);
        }
        public StringView SubView(int off, int len)
        {
            return new StringView(str, this.off, this.len, off, len);
        }

        public static implicit operator StringView(string fromString)
        {
            return new StringView(fromString);
        }


        public char this[int idx]
        {
            get
            {
                if (idx < 0 || idx >= len)
                {
                    throw new IndexOutOfRangeException(nameof(idx));
                }
                return str[off + idx];
            }
        }
        public int Length => len;
        public int Offset => off;
        public int End => off + len;


        public int CompareTo(StringView other)
        {
            if (ReferenceEquals(str, other.str) && len == other.len && off == other.off)
            {
                return 0;
            }
            else
            {
                return CultureInfo.CurrentCulture.CompareInfo.Compare(str, off, len, other.str, other.off, other.len, CompareOptions.Ordinal);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is StringView other)
            {
                return CompareTo(other) == 0;
            }
            return false;
        }
        public bool Equals(StringView other)
        {
            return CompareTo(other) == 0;
        }

        public static bool operator ==(StringView lhs, StringView rhs)
        {
            return lhs.CompareTo(rhs) == 0;
        }
        public static bool operator !=(StringView lhs, StringView rhs)
        {
            return lhs.CompareTo(rhs) != 0;
        }


        public override string ToString()
        {
            return len == 0 ? string.Empty : str.Substring(off, len);
        }
        public override int GetHashCode()
        {
            unsafe
            {
                fixed (char* ptr = str)
                {
                    byte* bytes = (byte*)(ptr + off);
                    int byteCount = len * sizeof(char);

                    uint hash = 2166136261u;
                    for (int i = 0; i < byteCount; i++)
                    {
                        hash ^= bytes[i];
                        hash *= 16777619u;
                    }
                    return (int)hash;
                }
            }
        }

        public string GetString()
        {
            return str ?? string.Empty;
        }
    }
}
#endif