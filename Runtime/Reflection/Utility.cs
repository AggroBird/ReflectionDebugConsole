// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System.Collections.Generic;

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
    }
}
#endif