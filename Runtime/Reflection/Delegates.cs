// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Reflection;

namespace AggroBird.Reflection
{
    internal static class Delegates
    {
        private const int MaxDelegateParameterCount = 16;

        private static readonly Type[] ActionTypes =
        {
            typeof(Action),
            typeof(Action<>),
            typeof(Action<,>),
            typeof(Action<,,>),
            typeof(Action<,,,>),
            typeof(Action<,,,,>),
            typeof(Action<,,,,,>),
            typeof(Action<,,,,,,>),
            typeof(Action<,,,,,,,>),
            typeof(Action<,,,,,,,,>),
            typeof(Action<,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,,,>),
        };

        private static readonly Type[] FuncTypes =
        {
            typeof(Func<>),
            typeof(Func<,>),
            typeof(Func<,,>),
            typeof(Func<,,,>),
            typeof(Func<,,,,>),
            typeof(Func<,,,,,>),
            typeof(Func<,,,,,,>),
            typeof(Func<,,,,,,,>),
            typeof(Func<,,,,,,,,>),
            typeof(Func<,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,,,>),
        };

        public static Type CreateGenericDelegateType(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length > MaxDelegateParameterCount)
            {
                throw new DebugConsoleException($"Method {method}: unsupported parameter count ({parameters.Length})");
            }

            if (method.ReturnType == typeof(void))
            {
                if (parameters.Length == 0) return ActionTypes[0];
                Type[] parameterTypes = new Type[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameterTypes[i] = parameters[i].ParameterType;
                }
                return ActionTypes[parameters.Length].MakeGenericType(parameterTypes);
            }
            else
            {
                Type[] parameterTypes = new Type[parameters.Length + 1];
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameterTypes[i] = parameters[i].ParameterType;
                }
                parameterTypes[parameters.Length] = method.ReturnType;
                return FuncTypes[parameters.Length].MakeGenericType(parameterTypes);
            }
        }
    }
}

#endif