// Copyright, 2022, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;

namespace AggroBird.DebugConsole
{
    internal static class ReflectionUtility
    {
        public const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.Instance;
        public const BindingFlags PrivateInstanceFlags = InstanceFlags | BindingFlags.NonPublic;
        public const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.Static;
        public const BindingFlags PrivateStaticFlags = StaticFlags | BindingFlags.NonPublic;

        public static BindingFlags GetInstanceFlags(bool includePrivate)
        {
            return includePrivate ? PrivateInstanceFlags : InstanceFlags;
        }
        public static BindingFlags GetStaticFlags(bool includePrivate)
        {
            return includePrivate ? PrivateStaticFlags : StaticFlags;
        }
        public static BindingFlags GetBindingFlags(bool isStatic, bool includePrivate)
        {
            return isStatic ? GetStaticFlags(includePrivate) : GetInstanceFlags(includePrivate);
        }

        internal static Type TryGetType(this Assembly assembly, IReadOnlyList<string> queries)
        {
            foreach (string query in queries)
            {
                var type = assembly.GetType(query);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private class TypeLookup
        {
            public TypeLookup()
            {
                foreach (var property in typeof(Environment).GetProperties(StaticFlags))
                {
                    string name = property.Name;
                    if (property.PropertyType == typeof(Type))
                    {
                        Type type = property.GetMethod.Invoke(null, null) as Type;

                        nameToType.Add(name, type);
                        typeToName.Add(type, name);
                    }
                    else
                    {
                        keywords.Add(name, property);
                    }
                }
            }

            public Dictionary<string, Type> nameToType = new Dictionary<string, Type>();
            public Dictionary<Type, string> typeToName = new Dictionary<Type, string>();
            public Dictionary<string, PropertyInfo> keywords = new Dictionary<string, PropertyInfo>();
        }

        private static TypeLookup typeNameLookup = new TypeLookup();

        public static bool IsNativeLiteral(string str)
        {
            if (str[0] == '"')
            {
                return true;
            }
            else if (str[0] == '\'' && str.Length == 3)
            {
                return true;
            }
            else if (char.IsNumber(str[0]) || str[0] == '-')
            {
                return true;
            }
            else if (typeNameLookup.keywords.ContainsKey(str))
            {
                return true;
            }

            return false;
        }
        public static bool IsNativeLiteral(string str, out object nativeLiteral)
        {
            if (str[0] == '"')
            {
                nativeLiteral = str.Substring(1, str.Length - 2);
            }
            else if (str[0] == '\'' && str.Length == 3)
            {
                nativeLiteral = str[1];
            }
            else if (char.IsNumber(str[0]) || str[0] == '-')
            {
                if (int.TryParse(str, out int asInt))
                {
                    nativeLiteral = asInt;
                }
                else if (decimal.TryParse(str, out decimal asDecimal))
                {
                    nativeLiteral = asDecimal;
                }
                else
                {
                    throw new ConsoleException($"Invalid number literal: '{str}'");
                }
            }
            else
            {
                if (!typeNameLookup.keywords.TryGetValue(str, out PropertyInfo info))
                {
                    throw new ConsoleException($"Invalid expression: '{str}'");
                }

                nativeLiteral = info.GetValue(null, null);
            }

            return true;
        }
        public static bool IsNativeType(string str, out Type nativeType)
        {
            if (typeNameLookup.nameToType.TryGetValue(str, out nativeType))
            {
                return true;
            }

            nativeType = null;
            return false;
        }
        public static string GetTypeName(Type type)
        {
            if (type == null) return "<null>";

            if (typeNameLookup.typeToName.TryGetValue(type, out string name))
            {
                return name;
            }

            return type.ToString();
        }

        public static bool TryFindImplicitOperator(Type dstType, Type srcType, out MethodBase opMethod)
        {
            foreach (var method in dstType.GetMethods(StaticFlags))
            {
                if (method.Name == "op_Implicit" && method.ReturnType == dstType)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == srcType)
                    {
                        opMethod = method;
                        return true;
                    }
                }
            }

            foreach (var method in srcType.GetMethods(StaticFlags))
            {
                if (method.Name == "op_Implicit" && method.ReturnType == dstType)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == srcType)
                    {
                        opMethod = method;
                        return true;
                    }
                }
            }

            opMethod = null;
            return false;
        }

        public static bool IsConvertibleNumber(Type type)
        {
            return (type.IsPrimitive || type == typeof(decimal) || type.IsEnum) && typeof(IConvertible).IsAssignableFrom(type);
        }
        public static bool CanConvert(Type requiredType, Type providedType)
        {
            if (requiredType == null)
            {
                return false;
            }

            if (!requiredType.IsValueType && providedType == null)
            {
                return true;
            }

            if (requiredType == providedType)
            {
                return true;
            }

            if (requiredType.IsAssignableFrom(providedType))
            {
                return true;
            }

            if (IsConvertibleNumber(requiredType) && IsConvertibleNumber(providedType))
            {
                return true;
            }

            if (TryFindImplicitOperator(requiredType, providedType, out _))
            {
                return true;
            }

            return false;
        }

        public static object Convert(Type requiredType, object arg)
        {
            if (arg != null)
            {
                Type type = arg.GetType();

                if (requiredType != type && !requiredType.IsAssignableFrom(type))
                {
                    if (IsConvertibleNumber(requiredType) && IsConvertibleNumber(type))
                    {
                        return (arg as IConvertible).ToType(requiredType, CultureInfo.InvariantCulture);
                    }

                    if (TryFindImplicitOperator(requiredType, type, out MethodBase opMethod))
                    {
                        return opMethod.Invoke(null, new object[] { arg });
                    }
                }
            }
            else if (requiredType.IsValueType)
            {
                throw new NullReferenceException("Attempted to cast a null reference to a value type");
            }

            return arg;
        }
        public static bool TryConvert(Type type, object obj, out object converted)
        {
            if (CanConvert(type, obj == null ? null : obj.GetType()))
            {
                converted = Convert(type, obj);
                return true;
            }

            converted = null;
            return false;
        }

        public static bool CanExecute(Type type)
        {
            // Dont allow execution on console related types
            if (type != null)
            {
                if (type.Assembly == typeof(DebugConsole).Assembly)
                {
                    return type.IsEnum && type.IsPublic;
                }
            }
            return true;
        }

        public static void SetField(object obj, string name, object value, bool includePrivate)
        {
            if (obj == null)
            {
                throw new ConsoleException(Errors.NullRef);
            }

            bool fieldWasFound = false;
            bool fieldWasReadonly = false;
            bool propertyWasFound = false;

            Type type;
            if (obj is Type)
            {
                BindingFlags bindingFlags = GetStaticFlags(includePrivate);

                type = obj as Type;
                foreach (FieldInfo field in type.GetFields(bindingFlags))
                {
                    if (field.Name == name)
                    {
                        fieldWasFound = true;

                        if (field.IsInitOnly)
                        {
                            fieldWasReadonly = true;
                            continue;
                        }

                        if (TryConvert(field.FieldType, value, out object converted))
                        {
                            field.SetValue(null, converted);
                            return;
                        }
                    }
                }
                foreach (PropertyInfo property in type.GetProperties(bindingFlags))
                {
                    if (property.Name == name)
                    {
                        propertyWasFound = true;

                        if (property.SetMethod != null && property.SetMethod.GetParameters().Length == 1 && (property.SetMethod.IsPublic || includePrivate))
                        {
                            fieldWasFound = true;

                            ParameterInfo parameter = property.SetMethod.GetParameters()[0];
                            if (TryConvert(parameter.ParameterType, value, out object converted))
                            {
                                property.SetMethod.Invoke(null, new[] { converted });
                                return;
                            }
                        }
                    }
                }
            }
            else
            {
                BindingFlags bindingFlags = GetInstanceFlags(includePrivate);

                type = obj.GetType();

                SearchBaseType:

                foreach (FieldInfo field in type.GetFields(bindingFlags))
                {
                    if (field.DeclaringType == type && field.Name == name)
                    {
                        fieldWasFound = true;

                        if (field.IsInitOnly)
                        {
                            fieldWasReadonly = true;
                            continue;
                        }

                        if (TryConvert(field.FieldType, value, out object converted))
                        {
                            field.SetValue(obj, converted);
                            return;
                        }
                    }
                }
                foreach (PropertyInfo property in type.GetProperties(bindingFlags))
                {
                    if (property.DeclaringType == type && property.Name == name)
                    {
                        propertyWasFound = true;

                        if (property.SetMethod != null && property.SetMethod.GetParameters().Length == 1 && (property.SetMethod.IsPublic || includePrivate))
                        {
                            fieldWasFound = true;

                            ParameterInfo parameter = property.SetMethod.GetParameters()[0];
                            if (TryConvert(parameter.ParameterType, value, out object converted))
                            {
                                property.SetMethod.Invoke(obj, new[] { converted });
                                return;
                            }
                        }
                    }
                }

                if (type.BaseType != null)
                {
                    type = type.BaseType;
                    goto SearchBaseType;
                }
            }

            if (fieldWasFound)
            {
                if (fieldWasReadonly)
                {
                    throw new ConsoleException($"Cannot assign type readonly field '{name}' in object '{obj}'");
                }
                else
                {
                    string typeName = value == null ? "null" : value.GetType().Name;
                    throw new ConsoleException($"Cannot assign type '{typeName}' to field '{name}' in object '{obj}'");
                }
            }
            else if (propertyWasFound)
            {
                throw new ConsoleException($"Property '{name}' in object '{obj}' is not assignable");
            }
            else
            {
                throw new ConsoleException($"Failed to find member '{name}' in object '{obj}'");
            }
        }

        public static IEnumerable<FieldInfo> GetFieldsRecursive(Type type, BindingFlags flags)
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            SearchBaseType:

            foreach (var field in type.GetFields(flags))
            {
                if (field.DeclaringType == type)
                {
                    fields.Add(field);
                }
            }

            if (type.BaseType != null)
            {
                type = type.BaseType;
                goto SearchBaseType;
            }

            return fields;
        }
    }
}
#endif