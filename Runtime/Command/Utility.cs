// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using System;
using System.Collections.Generic;
using System.Text;

namespace AggroBird.Reflection
{
    internal static class Utility
    {
        public static T Last<T>(this List<T> list)
        {
            return list[list.Count - 1];
        }
        public static void PopBack<T>(this List<T> list)
        {
            list.RemoveAt(list.Count - 1);
        }


        private const char EscapeCharacter = '\u200B';

        public static void AppendRTF(this StringBuilder stringBuilder, string str)
        {
            AppendRTF(stringBuilder, str, 0, str.Length);
        }
        public static void AppendRTF(this StringBuilder stringBuilder, StringView str)
        {
            AppendRTF(stringBuilder, str.GetString(), str.Offset, str.Length);
        }
        public static void AppendRTF(this StringBuilder stringBuilder, string str, int startIndex, int count)
        {
            for (int i = 0; i < count; i++)
            {
                char c = str[i + startIndex];
                stringBuilder.Append(c);
                if (c == '<') stringBuilder.Append(EscapeCharacter);
            }
        }
        public static void AppendRTF(this StringBuilder stringBuilder, char c)
        {
            stringBuilder.Append(c);
            stringBuilder.Append(EscapeCharacter);
        }

        public static bool IsStatic(this Type type)
        {
            return type.IsAbstract && type.IsSealed;
        }
    }
}
#endif