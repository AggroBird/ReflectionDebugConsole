// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AggroBird.Reflection
{
    internal class ExecutionContext
    {
        private readonly List<Block> stack = new List<Block>();
        public readonly List<VariableReference> variables = new List<VariableReference>();

        public Block CurrentScope => stack.Last();

        public void Push(Block scope)
        {
            stack.Add(scope);
        }
        public void Pop()
        {
            stack.PopBack();
        }
    }

    internal static class ExpressionUtility
    {
        public static T[] Forward<T>(ExecutionContext context, Expression[] args)
        {
            if (args.Length == 0) return Array.Empty<T>();
            T[] result = new T[args.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = args[i].Forward<T>(context);
            }
            return result;
        }
        public static T Forward<T>(this Expression expr, ExecutionContext context)
        {
            return (T)expr.Forward(context, typeof(T));
        }
        public static object Forward(this Expression expr, ExecutionContext context, Type dstType)
        {
            if (dstType == typeof(Expression))
            {
                return expr;
            }
            if (Expression.IsImplicitConvertable(expr, dstType, out Expression castExpr))
            {
                return castExpr.Execute(context);
            }
            throw new DebugConsoleException($"Expression {expr} to destination type '{dstType}'");
        }

        public static object SafeExecute(this Expression expr, ExecutionContext context)
        {
            if (expr == null) return null;
            object obj = expr.Execute(context);
            if (obj == null) throw new NullResultException();
            return obj;
        }
    }

    internal sealed class ArraySubscriptPropertyInfo : PropertyInfo
    {
        public ArraySubscriptPropertyInfo(Type arrayType, MethodInfo getMethod, MethodInfo setMethod)
        {
            this.arrayType = arrayType;
            this.getMethod = getMethod;
            this.setMethod = setMethod;
        }

        private readonly Type arrayType;
        private readonly MethodInfo getMethod;
        private readonly MethodInfo setMethod;

        public override string Name => "Item";
        public override PropertyAttributes Attributes => PropertyAttributes.None;

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override Type PropertyType => arrayType.GetElementType();
        public override Type DeclaringType => arrayType;
        public override Type ReflectedType => arrayType;

        public override MethodInfo GetGetMethod(bool nonPublic) => getMethod;
        public override MethodInfo GetSetMethod(bool nonPublic) => setMethod;

        public override ParameterInfo[] GetIndexParameters() => getMethod.GetParameters();

        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override bool IsDefined(Type attributeType, bool inherit) => false;

        public override MethodInfo[] GetAccessors(bool nonPublic) => throw new NotImplementedException();
        public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) => getMethod.Invoke(obj, index);
        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture) => setMethod.Invoke(obj, CombineParameters(value, index));
        private static object[] CombineParameters(object value, object[] index)
        {
            object[] result = new object[index.Length + 1];
            result[0] = value;
            for (int i = 0; i < index.Length; i++)
            {
                result[i + 1] = index[i];
            }
            return result;
        }
    }

    internal class EnumeratorInfo
    {
        public EnumeratorInfo(Type elementType, MethodInfo getEnumerator, MethodInfo moveNext, MethodInfo getCurrent)
        {
            this.elementType = elementType;
            this.getEnumerator = getEnumerator;
            this.moveNext = moveNext;
            this.getCurrent = getCurrent;
        }

        private readonly Type elementType;
        private readonly MethodInfo getEnumerator;
        private readonly MethodInfo moveNext;
        private readonly MethodInfo getCurrent;

        public object GetEnumerator(object collection)
        {
            return getEnumerator.Invoke(collection, null);
        }
        public bool MoveNext(object enumerator)
        {
            return (bool)moveNext.Invoke(enumerator, null);
        }
        public object Current(object enumerator)
        {
            return getCurrent.Invoke(enumerator, null);
        }

        public Type ElementType => elementType;
    }

    internal abstract class Generic
    {
        public abstract string Name { get; }
        public abstract Type[] GetGenericArguments();
    }

    internal class GenericMethod : Generic
    {
        public GenericMethod(MethodInfo methodInfo)
        {
            this.methodInfo = methodInfo;
        }

        public readonly MethodInfo methodInfo;

        public override string Name => methodInfo.Name;
        public override Type[] GetGenericArguments() => methodInfo.GetGenericArguments();
    }

    internal class GenericType : Generic
    {
        public GenericType(Type type)
        {
            this.type = type;

            int parentGenericArgumentCount = type.DeclaringType == null ? 0 : type.DeclaringType.GetGenericArguments().Length;
            Type[] selfGenericArguments = type.GetGenericArguments();
            int genericArgumentCount = selfGenericArguments.Length - parentGenericArgumentCount;
            genericArguments = new Type[genericArgumentCount];
            for (int i = parentGenericArgumentCount; i < selfGenericArguments.Length; i++)
            {
                genericArguments[i] = selfGenericArguments[i - parentGenericArgumentCount];
            }
        }

        public readonly Type type;
        public readonly Type[] genericArguments;

        public override string Name => type.Name;
        public override Type[] GetGenericArguments() => genericArguments;
    }

    internal abstract class Expression
    {
        public abstract object Execute(ExecutionContext context);
        public abstract Type ResultType { get; }
        public virtual TypeCode GetTypeCode() => Type.GetTypeCode(ResultType);

        public virtual bool IsConstant => false;

        public virtual bool Assignable => false;
        public virtual object SetValue(ExecutionContext context, object val, bool returnInitialValue) => throw new DebugConsoleException("Expression is read only");


        private class BaseCastAttribute : Attribute { }


        static Expression()
        {
            Type arrayType = typeof(Array);
            MethodInfo[] getMethods = arrayType.GetMember("GetValue", MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance) as MethodInfo[];
            MethodInfo[] setMethods = arrayType.GetMember("SetValue", MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance) as MethodInfo[];
            Array.Sort(getMethods, new ArraySubscriptPropertySorter());
            Array.Sort(setMethods, new ArraySubscriptPropertySorter(true));
            ArrayGetSetMethods = new GetSetMethods[getMethods.Length];
            for (int i = 0; i < getMethods.Length; i++)
            {
                ArrayGetSetMethods[i] = new GetSetMethods(getMethods[i], setMethods[i], GetArraySubscriptPropertyIndexCount(getMethods[i], false));
            }

            foreach (var member in typeof(Expression).FindMembers(MemberTypes.Method, BindingFlags.Public | BindingFlags.Static, CastMemberFilter, null))
            {
                if (member is MethodInfo castMethod)
                {
                    switch (Type.GetTypeCode(castMethod.ReturnType))
                    {
                        case TypeCode.Char: CastToChar = castMethod; break;
                        case TypeCode.SByte: CastToSByte = castMethod; break;
                        case TypeCode.Byte: CastToByte = castMethod; break;
                        case TypeCode.Int16: CastToInt16 = castMethod; break;
                        case TypeCode.UInt16: CastToUInt16 = castMethod; break;
                        case TypeCode.Int32: CastToInt32 = castMethod; break;
                        case TypeCode.UInt32: CastToUInt32 = castMethod; break;
                        case TypeCode.Int64: CastToInt64 = castMethod; break;
                        case TypeCode.UInt64: CastToUInt64 = castMethod; break;
                        case TypeCode.Single: CastToSingle = castMethod; break;
                        case TypeCode.Double: CastToDouble = castMethod; break;
                    }
                }
            }
        }

        private static bool CastMemberFilter(MemberInfo m, object filterCriteria) => m.GetCustomAttribute<BaseCastAttribute>() != null;

        private static int GetArraySubscriptPropertyIndexCount(MethodInfo method, bool setMethod)
        {
            ParameterInfo[] parameters = method.GetParameters();
            ParameterInfo firstParam = parameters[setMethod ? 1 : 0];
            return firstParam.ParameterType.IsArray ? int.MaxValue : parameters.Length;
        }

        private readonly struct ArraySubscriptPropertySorter : IComparer<MethodInfo>
        {
            public ArraySubscriptPropertySorter(bool setMethod)
            {
                paramIndex = setMethod ? 1 : 0;
            }

            private readonly int paramIndex;

            public int Compare(MethodInfo lhs, MethodInfo rhs)
            {
                ParameterInfo lhsParam = lhs.GetParameters()[paramIndex];
                ParameterInfo rhsParam = rhs.GetParameters()[paramIndex];
                bool lhsArray = lhsParam.ParameterType.IsArray;
                bool rhsArray = rhsParam.ParameterType.IsArray;
                int lhsParamCount = lhsArray ? int.MaxValue : lhs.GetParameters().Length;
                int rhsParamCount = rhsArray ? int.MaxValue : rhs.GetParameters().Length;
                int paramCount = lhsParamCount.CompareTo(rhsParamCount);
                if (paramCount != 0) return paramCount;
                Type lhsType = lhsArray ? lhsParam.ParameterType.GetElementType() : lhsParam.ParameterType;
                Type rhsType = rhsArray ? rhsParam.ParameterType.GetElementType() : rhsParam.ParameterType;
                return ((int)Type.GetTypeCode(lhsType)).CompareTo((int)Type.GetTypeCode(rhsType));
            }
        }

        private readonly struct GetSetMethods
        {
            public GetSetMethods(MethodInfo getMethod, MethodInfo setMethod, int indexCount)
            {
                this.getMethod = getMethod;
                this.setMethod = setMethod;
                this.indexCount = indexCount;
            }

            public readonly MethodInfo getMethod;
            public readonly MethodInfo setMethod;
            public readonly int indexCount;
        }
        private static readonly GetSetMethods[] ArrayGetSetMethods;

        private const string OpImplicit = "op_Implicit";
        private const string OpExplicit = "op_Explicit";

        private static readonly MethodInfo CastToChar;
        private static readonly MethodInfo CastToSByte;
        private static readonly MethodInfo CastToByte;
        private static readonly MethodInfo CastToInt16;
        private static readonly MethodInfo CastToUInt16;
        private static readonly MethodInfo CastToInt32;
        private static readonly MethodInfo CastToUInt32;
        private static readonly MethodInfo CastToInt64;
        private static readonly MethodInfo CastToUInt64;
        private static readonly MethodInfo CastToSingle;
        private static readonly MethodInfo CastToDouble;

        private static int MaxDelegateParameterCount = 16;

        private static readonly Type[] ActionTypes =
        {
            typeof(Action),
            typeof(Action<int>).GetGenericTypeDefinition(),
            typeof(Action<int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Action<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
        };
        private static readonly Type[] FuncTypes =
        {
            typeof(Func<int>).GetGenericTypeDefinition(),
            typeof(Func<int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
            typeof(Func<int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int, int>).GetGenericTypeDefinition(),
        };


        public static BindingFlags MakeBindingFlags(bool isStatic, bool safeMode)
        {
            BindingFlags result = BindingFlags.Public | BindingFlags.FlattenHierarchy | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            if (!safeMode) result |= BindingFlags.NonPublic;
            return result;
        }

        public static PropertyInfo[] GetSubscriptProperties(Type type, bool safeMode)
        {
            return type.GetMember("Item", MemberTypes.Property, MakeBindingFlags(false, safeMode)) as PropertyInfo[];
        }
        public static PropertyInfo[] GetArraySubscriptProperties(Type arrayType)
        {
            int rank = arrayType.GetArrayRank();
            for (int i = 0; i < ArrayGetSetMethods.Length - 1; i++)
            {
                GetSetMethods methods = ArrayGetSetMethods[i];
                if (methods.indexCount == rank)
                {
                    return new PropertyInfo[] { new ArraySubscriptPropertyInfo(arrayType, methods.getMethod, methods.setMethod) };
                }
            }
            GetSetMethods last = ArrayGetSetMethods[ArrayGetSetMethods.Length - 1];
            return new PropertyInfo[] { new ArraySubscriptPropertyInfo(arrayType, last.getMethod, last.setMethod) };
        }

        private static readonly PropertyInfo[] StringSubscriptProperties = new PropertyInfo[] { typeof(string).GetProperty("Chars") };
        public static PropertyInfo[] GetStringSubscriptProperties() => StringSubscriptProperties;

        public static T[] GetOptimalOverloads<T>(IReadOnlyList<T> overloads, params Expression[] args) where T : MethodBase
        {
            if (overloads != null && overloads.Count > 0)
            {
                List<T> optimal = new List<T>();
                List<T> compatible = new List<T>();
                for (int i = 0; i < overloads.Count; i++)
                {
                    if (IsCompatibleOverload(overloads[i], args))
                    {
                        compatible.Add(overloads[i]);
                    }
                }

                if (compatible.Count > 0)
                {
                    optimal.Add(compatible[0]);
                    for (int i = 1; i < compatible.Count; i++)
                    {
                        switch (CompareMethodOverloads(optimal[0].GetParameters(), compatible[i].GetParameters(), args))
                        {
                            case 0:
                                optimal.Add(compatible[i]);
                                break;
                            case 1:
                                optimal.Clear();
                                optimal.Add(compatible[i]);
                                break;
                        }
                    }
                }

                if (optimal.Count != 0) return optimal.ToArray();
            }

            return Array.Empty<T>();
        }
        public static PropertyInfo[] GetOptimalOverloads(IReadOnlyList<PropertyInfo> overloads, params Expression[] args)
        {
            if (overloads != null && overloads.Count > 0)
            {
                List<PropertyInfo> optimal = new List<PropertyInfo>();
                List<PropertyInfo> compatible = new List<PropertyInfo>();
                for (int i = 0; i < overloads.Count; i++)
                {
                    if (IsCompatibleOverload(overloads[i].GetMethod, args))
                    {
                        compatible.Add(overloads[i]);
                    }
                }

                if (compatible.Count > 0)
                {
                    optimal.Add(compatible[0]);
                    for (int i = 1; i < compatible.Count; i++)
                    {
                        switch (CompareMethodOverloads(optimal[0].GetIndexParameters(), compatible[i].GetIndexParameters(), args))
                        {
                            case 0:
                                optimal.Add(compatible[i]);
                                break;
                            case 1:
                                optimal.Clear();
                                optimal.Add(compatible[i]);
                                break;
                        }
                    }
                }

                if (optimal.Count != 0) return optimal.ToArray();
            }

            return Array.Empty<PropertyInfo>();
        }

        private static int CompareMethodOverloads(ParameterInfo[] lhs, ParameterInfo[] rhs, Expression[] args)
        {
            int score = 0;

            bool lhsVarArg = HasVariableParameterCount(lhs), rhsVarArg = HasVariableParameterCount(rhs);

            if (lhsVarArg == rhsVarArg)
            {
                int lhsDefaults = CountDefaults(lhs), rhsDefaults = CountDefaults(rhs);
                if (lhsDefaults != rhsDefaults)
                {
                    PickSmallest(ref score, lhsDefaults, rhsDefaults);
                }
                else if (lhs.Length == rhs.Length)
                {
                    // Compare parameters
                    for (int i = 0; i < args.Length; i++)
                    {
                        PickSmallest(ref score, GetInheritanceWeight(lhs[i].ParameterType, args[i].ResultType), GetInheritanceWeight(rhs[i].ParameterType, args[i].ResultType));
                    }
                }
                else
                {
                    // Pick smallest overload
                    PickSmallest(ref score, lhs.Length, rhs.Length);
                }
            }
            else
            {
                // Pick non-vararg
                PickEqual(ref score, lhsVarArg, rhsVarArg, false);
            }

            return score < 0 ? -1 : score > 0 ? 1 : 0;
        }
        private static int CountDefaults(ParameterInfo[] param)
        {
            int result = 0;
            for (int i = 0; i < param.Length; i++)
            {
                if (param[i].HasDefaultValue)
                {
                    result++;
                }
            }
            return result;
        }

        private static int GetInheritanceWeight(Type baseType, Type type)
        {
            if (type.Equals(baseType))
            {
                return 0;
            }
            else if (type.IsSubclassOf(baseType))
            {
                int depth = 0;
                while (true)
                {
                    if (type.Equals(baseType))
                    {
                        return depth;
                    }
                    else
                    {
                        type = type.BaseType;
                        depth++;
                    }
                }
            }
            return int.MaxValue;
        }

        private static void PickEqual(ref int score, bool lhs, bool rhs, bool val)
        {
            if (lhs != rhs)
            {
                if (lhs == val)
                    score--;
                else if (rhs == val)
                    score++;
            }
        }
        private static void PickEqual(ref int score, Type lhs, Type rhs, Type val)
        {
            if (lhs != rhs)
            {
                if (lhs == val)
                    score--;
                else if (rhs == val)
                    score++;
            }
        }
        private static void PickSmallest(ref int score, int lhs, int rhs)
        {
            if (lhs < rhs)
                score--;
            else if (lhs > rhs)
                score++;
        }

        private static bool IsSupportedReflectionType(Type type)
        {
            if (type.IsByRef) return false;
            if (type.IsPointer) return false;
            if (type.IsSubclassOf(typeof(Delegate)))
            {
                // Check delegate type
                MethodInfo invokeMethod = type.GetMethod("Invoke");
                if (invokeMethod == null) return false;
                if (!IncludeMember(invokeMethod, true)) return false;
            }
            return true;
        }
        public static bool IncludeMember<T>(T member, bool includeSpecial = false) where T : MemberInfo
        {
            if (member is MethodBase methodBase)
            {
                if (!includeSpecial)
                {
                    // Skip property methods
                    if (methodBase.IsSpecialName) return false;
                    // Skip explicit interface methods
                    if (methodBase.Name.IndexOf('.') != -1) return false;
                }

                // Skip methods with references or pointers as parameter
                foreach (var parameter in methodBase.GetParameters())
                {
                    if (!IsSupportedReflectionType(parameter.ParameterType)) return false;
                    // Skip methods with empty parameter names
                    if (string.IsNullOrEmpty(parameter.Name)) return false;
                }
                if (methodBase is MethodInfo methodInfo)
                {
                    if (!IsSupportedReflectionType(methodInfo.ReturnType)) return false;
                }
            }
            else if (member is PropertyInfo propertyInfo)
            {
                if (!IsSupportedReflectionType(propertyInfo.PropertyType)) return false;

                MethodInfo getMethod = propertyInfo.GetMethod;
                if (getMethod != null)
                {
                    // Skip subscript properties
                    if (getMethod.GetParameters().Length > 0) return false;
                    if (!IncludeMember(getMethod, true)) return false;
                }
                MethodInfo setMethod = propertyInfo.SetMethod;
                if (setMethod != null)
                {
                    // Skip subscript properties
                    if (setMethod.GetParameters().Length > 1) return false;
                    if (!IncludeMember(setMethod, true)) return false;
                }
            }
            else if (member is FieldInfo fieldInfo)
            {
                if (!IsSupportedReflectionType(fieldInfo.FieldType)) return false;
            }
            else if (member is EventInfo eventInfo)
            {
                if (!IsSupportedReflectionType(eventInfo.EventHandlerType)) return false;
            }
            else if (member is Type type)
            {
                if (!IsSupportedReflectionType(type)) return false;
            }

            // Skip compiler generated members
            if (!includeSpecial && member.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
            {
                return false;
            }

            return true;
        }

        public static EnumeratorInfo GetEnumerator(Type type)
        {
            foreach (MethodInfo getEnumerator in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (getEnumerator.GetParameters().Length == 0 && !getEnumerator.ReturnType.Equals(typeof(void)) && getEnumerator.Name == "GetEnumerator")
                {
                    Type enumeratorType = getEnumerator.ReturnType;
                    MethodInfo moveNext = null;
                    foreach (MethodInfo method in enumeratorType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (method.GetParameters().Length == 0 && method.ReturnType.Equals(typeof(bool)) && method.Name == "MoveNext")
                        {
                            moveNext = method;
                            break;
                        }
                    }
                    MethodInfo getCurrent = null;
                    foreach (PropertyInfo property in enumeratorType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (property.CanRead && property.GetIndexParameters().Length == 0 && !property.PropertyType.Equals(typeof(void)) && property.Name == "Current")
                        {
                            getCurrent = property.GetMethod;
                            break;
                        }
                    }
                    if (moveNext == null || getCurrent == null)
                    {
                        throw new DebugConsoleException($"foreach requires that the return type '{enumeratorType}' of '{type}.GetEnumerator()' must have a suitable public 'MoveNext' method and public 'Current' property");
                    }
                    return new EnumeratorInfo(type.IsArray ? type.GetElementType() : getCurrent.ReturnType, getEnumerator, moveNext, getCurrent);
                }
            }
            throw new DebugConsoleException($"foreach statement cannot operate on variables of type '{type}' because it does not contain a public instance or extension definition for 'GetEnumerator'");
        }


        private abstract class MemberKey
        {

        }

        private sealed class FieldKey : MemberKey
        {
            public FieldKey(MemberInfo memberInfo)
            {
                fieldName = memberInfo.Name;
            }

            private readonly string fieldName;

            public override bool Equals(object obj)
            {
                return obj is FieldKey other && fieldName.Equals(other.fieldName);
            }
            public override int GetHashCode()
            {
                return fieldName.GetHashCode();
            }
        }

        private sealed class MethodKey : MemberKey
        {
            public MethodKey(MethodInfo methodInfo)
            {
                methodName = methodInfo.Name;
                parameters = methodInfo.GetParameters();
            }
            public MethodKey(ConstructorInfo methodInfo)
            {
                methodName = methodInfo.Name;
                parameters = methodInfo.GetParameters();
            }

            private readonly string methodName;
            private readonly ParameterInfo[] parameters;

            public override bool Equals(object obj)
            {
                if (obj is MethodKey other && methodName.Equals(other.methodName))
                {
                    if (parameters.Length == other.parameters.Length)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (!parameters[i].ParameterType.Equals(other.parameters[i].ParameterType))
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                }
                return false;
            }
            public override int GetHashCode()
            {
                int result = methodName.GetHashCode();
                for (int i = 0; i < parameters.Length; i++)
                {
                    result ^= parameters[i].ParameterType.GetHashCode();
                }
                return result;
            }
        }

        public static Type[] FilterMembers(Type[] types)
        {
            List<Type> result = new List<Type>();
            for (int i = 0; i < types.Length; i++)
            {
                if (IncludeMember(types[i]))
                {
                    result.Add(types[i]);
                }
            }
            return result.ToArray();
        }
        public static Type FilterMembers(Type nestedType)
        {
            if (nestedType != null && IncludeMember(nestedType))
            {
                return nestedType;
            }
            return null;
        }
        public static T[] FilterMembers<T>(T[] members, bool includeSpecial = false) where T : MemberInfo
        {
            // Filter hidden members
            Dictionary<MemberKey, T> result = new Dictionary<MemberKey, T>();
            for (int i = 0; i < members.Length; i++)
            {
                T member = members[i];
                if (IncludeMember(member, includeSpecial))
                {
                    MemberKey key;
                    switch (member)
                    {
                        case FieldInfo fieldInfo:
                            key = new FieldKey(fieldInfo);
                            break;
                        case PropertyInfo propertyInfo:
                            key = new FieldKey(propertyInfo);
                            break;
                        case EventInfo eventInfo:
                            key = new FieldKey(eventInfo);
                            break;
                        case MethodInfo methodInfo:
                            key = new MethodKey(methodInfo);
                            break;
                        case ConstructorInfo constructorInfo:
                            key = new MethodKey(constructorInfo);
                            break;
                        default:
                            continue;
                    }
                    // Hide if type derives from member declaring type
                    if (result.TryGetValue(key, out T value))
                    {
                        if (member.DeclaringType.IsSubclassOf(value.DeclaringType))
                        {
                            result[key] = member;
                        }
                    }
                    else
                    {
                        result.Add(key, member);
                    }
                }
            }
            return result.Values.ToArray();
        }


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

        public static Delegate CreateDelegate(MethodInfo method)
        {
            return method.CreateDelegate(CreateGenericDelegateType(method));
        }
        public static Delegate CreateDelegate(MethodInfo method, object target)
        {
            return method.CreateDelegate(CreateGenericDelegateType(method), target);
        }

        public static bool HasVariableParameterCount(ParameterInfo[] param)
        {
            return param.Length > 0 && param[param.Length - 1].GetCustomAttribute<ParamArrayAttribute>(true) != null;
        }
        public static bool HasVariableParameterCount(MethodBase method) => HasVariableParameterCount(method.GetParameters());


        public static bool GetCompatibleDelegateOverload(MethodOverload overload, Type delegateType, out Expression castExpr)
        {
            for (int i = 0; i < overload.methods.Count; i++)
            {
                Type type = CreateGenericDelegateType(overload.methods[i]);

                if (delegateType.IsAssignableFrom(type))
                {
                    castExpr = new CreateDelegate(overload.lhs, overload.methods[i], type);
                    return true;
                }
            }
            castExpr = null;
            return false;
        }
        private static bool CanBindMethodOverloadToDelegate(Expression expr, Type dstType, out Expression castExpr)
        {
            if (expr is MethodOverload overload)
            {
                if (GetCompatibleDelegateOverload(overload, dstType, out castExpr))
                {
                    return true;
                }
                else
                {
                    throw new DebugConsoleException($"Failed to bind compatible overload of method '{overload.methodName}' to type '{dstType}'");
                }
            }

            castExpr = expr;
            return false;
        }

        private static bool IsImplicitConvertableByOperator(Expression expr, Type dstType, out MethodInfo castMethod)
        {
            Type srcType = expr.ResultType;

            TypeCode srcTypeCode = Type.GetTypeCode(srcType);
            TypeCode dstTypeCode = Type.GetTypeCode(dstType);

            // Only language native implicit casts
            switch (srcTypeCode)
            {
                case TypeCode.Char:
                    switch (dstTypeCode)
                    {
                        case TypeCode.UInt16: castMethod = CastToUInt16; return true;
                        case TypeCode.Int32: castMethod = CastToInt32; return true;
                        case TypeCode.UInt32: castMethod = CastToUInt32; return true;
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.UInt64: castMethod = CastToUInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.SByte:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int16: castMethod = CastToInt16; return true;
                        case TypeCode.Int32: castMethod = CastToInt32; return true;
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.Byte:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int16: castMethod = CastToInt16; return true;
                        case TypeCode.UInt16: castMethod = CastToUInt16; return true;
                        case TypeCode.Int32: castMethod = CastToInt32; return true;
                        case TypeCode.UInt32: castMethod = CastToUInt32; return true;
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.UInt64: castMethod = CastToUInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.Int16:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int32: castMethod = CastToInt32; return true;
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.UInt16:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int32: castMethod = CastToInt32; return true;
                        case TypeCode.UInt32: castMethod = CastToUInt32; return true;
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.UInt64: castMethod = CastToUInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.Int32:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.UInt32:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.UInt64: castMethod = CastToUInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.Int64:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.UInt64:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
                case TypeCode.Single:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
            }

            if (FindValidCastOperator(OpImplicit, srcType, dstType, out castMethod)) return true;

            castMethod = null;
            return false;
        }
        public static bool IsImplicitConvertable(Expression expr, Type dstType, out Expression castExpr)
        {
            // Delegate conversion
            if (CanBindMethodOverloadToDelegate(expr, dstType, out castExpr))
            {
                return true;
            }

            Type srcType = expr.ResultType;
            castExpr = expr;

            // No conversion required
            if (dstType.IsAssignableFrom(srcType))
            {
                return true;
            }

            // Null
            if (expr is Null && !dstType.IsValueType)
            {
                return true;
            }

            // Find cast operator
            if (IsImplicitConvertableByOperator(expr, dstType, out MethodInfo castMethod))
            {
                castExpr = new MethodMember(castMethod, new Expression[] { expr });
                return true;
            }

            return false;
        }

        private static bool IsExplicitConvertableByOperator(Expression expr, Type dstType, out MethodInfo castMethod)
        {
            Type srcType = expr.ResultType;

            TypeCode srcTypeCode = Type.GetTypeCode(srcType);
            TypeCode dstTypeCode = Type.GetTypeCode(dstType);

            // Regular typecast
            switch (srcTypeCode)
            {
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Char: castMethod = CastToChar; return true;
                        case TypeCode.SByte: castMethod = CastToSByte; return true;
                        case TypeCode.Byte: castMethod = CastToByte; return true;
                        case TypeCode.Int16: castMethod = CastToInt16; return true;
                        case TypeCode.UInt16: castMethod = CastToUInt16; return true;
                        case TypeCode.Int32: castMethod = CastToInt32; return true;
                        case TypeCode.UInt32: castMethod = CastToUInt32; return true;
                        case TypeCode.Int64: castMethod = CastToInt64; return true;
                        case TypeCode.UInt64: castMethod = CastToUInt64; return true;
                        case TypeCode.Single: castMethod = CastToSingle; return true;
                        case TypeCode.Double: castMethod = CastToDouble; return true;
                    }
                    break;
            }

            if (FindValidCastOperator(OpImplicit, srcType, dstType, out castMethod)) return true;
            if (FindValidCastOperator(OpExplicit, srcType, dstType, out castMethod)) return true;

            castMethod = null;
            return false;
        }
        public static bool IsExplicitConvertable(Expression expr, Type dstType, out Expression castExpr)
        {
            // Delegate conversion
            if (CanBindMethodOverloadToDelegate(expr, dstType, out castExpr))
            {
                return true;
            }

            Type srcType = expr.ResultType;
            castExpr = expr;

            // No conversion
            if (srcType.Equals(dstType))
            {
                return true;
            }

            // Boxing / unboxing
            if (dstType.Equals(typeof(object)) || srcType.Equals(typeof(object)))
            {
                castExpr = new Conversion(dstType, expr);
                return true;
            }

            // Downcast / upcast
            if (dstType.IsAssignableFrom(srcType) || srcType.IsAssignableFrom(dstType))
            {
                castExpr = new Conversion(dstType, expr);
                return true;
            }

            // Find cast operator
            if (IsExplicitConvertableByOperator(expr, dstType, out MethodInfo castMethod))
            {
                castExpr = new MethodMember(castMethod, new Expression[] { expr });
                return true;
            }

            return false;
        }


        public static void CheckImplicitConvertible(Expression expr, Type dstType, out Expression result)
        {
            if (!IsImplicitConvertable(expr, dstType, out result))
            {
                throw new InvalidCastException(expr.ResultType, dstType);
            }
        }
        public static void CheckConvertibleBool(Expression expr, out Expression result)
        {
            CheckImplicitConvertible(expr, typeof(bool), out result);
        }
        public static void CheckImplicitConvertible(Expression[] args, Type dstType)
        {
            for (int i = 0; i < args.Length; i++)
            {
                CheckImplicitConvertible(args[i], dstType, out args[i]);
            }
        }

        public static bool IsConstantArguments(Expression[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].IsConstant)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsCompatibleOverload(ParameterInfo[] parameters, IReadOnlyList<Expression> args, bool matchParameterCount = true)
        {
            int actualParamCount = parameters.Length;
            int actualArgCount = args.Count;
            if (actualParamCount > 0)
            {
                int lastParamIndex = actualParamCount - 1;
                ParameterInfo lastParam = parameters[lastParamIndex];
                if (lastParam.GetCustomAttribute<ParamArrayAttribute>(true) != null)
                {
                    if (actualArgCount >= actualParamCount)
                    {
                        Type paramType = lastParam.ParameterType.GetElementType();
                        int optionalArgCount = args.Count - lastParamIndex;
                        for (int i = 0; i < optionalArgCount; i++)
                        {
                            var expr = args[lastParamIndex + i];
                            if (!IsImplicitConvertable(expr, paramType, out _))
                            {
                                return false;
                            }
                        }
                        actualArgCount -= optionalArgCount;
                    }
                    actualParamCount--;
                }
            }
            if (actualArgCount <= actualParamCount)
            {
                for (int i = 0; i < actualArgCount; i++)
                {
                    if (!IsImplicitConvertable(args[i], parameters[i].ParameterType, out _))
                        return false;
                }
                return !matchParameterCount || actualArgCount == actualParamCount || parameters[actualArgCount].HasDefaultValue;
            }
            return false;
        }
        public static bool IsCompatibleOverload(MethodBase method, IReadOnlyList<Expression> args, bool matchParameterCount = true)
        {
            return IsCompatibleOverload(method.GetParameters(), args, matchParameterCount);
        }

        public static object InvokeMethod(ExecutionContext context, MethodInfo method, object target, Expression[] args)
        {
            ParameterInfo[] param = method.GetParameters();
            if (param.Length == 0)
            {
                return method.Invoke(target, Array.Empty<object>());
            }
            else
            {
                int actualParamCount = param.Length;
                int actualArgCount = args.Length;

                object[] converted = new object[actualParamCount];

                int lastParamIndex = actualParamCount - 1;
                ParameterInfo lastParam = param[lastParamIndex];
                if (lastParam.GetCustomAttribute<ParamArrayAttribute>(true) != null)
                {
                    Type paramType = lastParam.ParameterType.GetElementType();
                    int optionalArgCount = args.Length - lastParamIndex;
                    if (optionalArgCount < 0) optionalArgCount = 0;
                    Array optionalArray = Activator.CreateInstance(lastParam.ParameterType, optionalArgCount) as Array;
                    for (int i = 0; i < optionalArgCount; i++)
                    {
                        optionalArray.SetValue(args[lastParamIndex + i].Forward(context, paramType), i);
                    }
                    actualArgCount -= optionalArgCount;
                    converted[lastParamIndex] = optionalArray;
                    actualParamCount--;
                }

                for (int i = 0; i < actualArgCount; i++)
                {
                    converted[i] = args[i].Forward(context, param[i].ParameterType);
                }
                for (int i = actualArgCount; i < actualParamCount; i++)
                {
                    converted[i] = param[i].DefaultValue;
                }
                return method.Invoke(target, converted);
            }
        }

        private static bool FindValidCastOperator(string name, Type srcType, Type dstType, out MethodInfo result)
        {
            MethodInfo[] srcCasts = srcType.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy) as MethodInfo[];
            for (int i = 0; i < srcCasts.Length; i++)
            {
                MethodInfo castMethod = srcCasts[i];
                ParameterInfo[] parameters = castMethod.GetParameters();
                if (parameters.Length == 1 && castMethod.ReturnType.Equals(dstType) && parameters[0].ParameterType.IsAssignableFrom(srcType))
                {
                    result = castMethod;
                    return true;
                }
            }

            MethodInfo[] dstCasts = dstType.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy) as MethodInfo[];
            for (int i = 0; i < dstCasts.Length; i++)
            {
                MethodInfo castMethod = dstCasts[i];
                ParameterInfo[] parameters = castMethod.GetParameters();
                if (parameters.Length == 1 && castMethod.ReturnType.Equals(dstType) && parameters[0].ParameterType.IsAssignableFrom(srcType))
                {
                    result = castMethod;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public static T[] ExtractConstantValues<T>(Expression[] args)
        {
            T[] result = new T[args.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (T)args[i].Execute(null);
            }
            return result;
        }

        public static Type CreateArrayType(Type elementType, int rank = 1)
        {
            return rank <= 1 ? elementType.MakeArrayType() : elementType.MakeArrayType(rank);
        }

        public static string FormatGenericName(Type type)
        {
            return FormatGenericName(type.Name);
        }
        public static string FormatGenericName(string name)
        {
            int idx = name.IndexOf('`');
            if (idx != -1)
            {
                return name.Substring(0, idx);
            }
            return name;
        }


        public static string GetPrefix(Type type)
        {
            if (type.IsEnum)
                return "enum ";
            else if (type.IsInterface)
                return "interface ";
            else if (type.IsClass)
            {
                if (type.IsSubclassOf(typeof(Delegate)))
                    return "delegate ";
                else
                    return "class ";
            }
            else if (type.IsValueType)
                return "struct ";
            else
                return string.Empty;
        }

        [BaseCast]
        public static bool ToBool(object val)
        {
            Type srcType = val.GetType();
            if (srcType == typeof(bool)) return (bool)val;
            throw new InvalidCastException(srcType, typeof(bool));
        }
        [BaseCast]
        public static char ToChar(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (char)(sbyte)val;
                    case TypeCode.Byte: return (char)(byte)val;
                    case TypeCode.Int16: return (char)(short)val;
                    case TypeCode.UInt16: return (char)(ushort)val;
                    case TypeCode.Int32: return (char)(int)val;
                    case TypeCode.UInt32: return (char)(uint)val;
                    case TypeCode.Int64: return (char)(long)val;
                    case TypeCode.UInt64: return (char)(ulong)val;
                    case TypeCode.Single: return (char)(float)val;
                    case TypeCode.Double: return (char)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(double));
        }
        [BaseCast]
        public static sbyte ToSByte(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (sbyte)(char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (sbyte)(byte)val;
                    case TypeCode.Int16: return (sbyte)(short)val;
                    case TypeCode.UInt16: return (sbyte)(ushort)val;
                    case TypeCode.Int32: return (sbyte)(int)val;
                    case TypeCode.UInt32: return (sbyte)(uint)val;
                    case TypeCode.Int64: return (sbyte)(long)val;
                    case TypeCode.UInt64: return (sbyte)(ulong)val;
                    case TypeCode.Single: return (sbyte)(float)val;
                    case TypeCode.Double: return (sbyte)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(sbyte));
        }
        [BaseCast]
        public static byte ToByte(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (byte)(char)val;
                    case TypeCode.SByte: return (byte)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (byte)(short)val;
                    case TypeCode.UInt16: return (byte)(ushort)val;
                    case TypeCode.Int32: return (byte)(int)val;
                    case TypeCode.UInt32: return (byte)(uint)val;
                    case TypeCode.Int64: return (byte)(long)val;
                    case TypeCode.UInt64: return (byte)(ulong)val;
                    case TypeCode.Single: return (byte)(float)val;
                    case TypeCode.Double: return (byte)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(byte));
        }
        [BaseCast]
        public static short ToInt16(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (short)(char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (short)(ushort)val;
                    case TypeCode.Int32: return (short)(int)val;
                    case TypeCode.UInt32: return (short)(uint)val;
                    case TypeCode.Int64: return (short)(long)val;
                    case TypeCode.UInt64: return (short)(ulong)val;
                    case TypeCode.Single: return (short)(float)val;
                    case TypeCode.Double: return (short)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(short));
        }
        [BaseCast]
        public static ushort ToUInt16(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (ushort)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (ushort)(short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (ushort)(int)val;
                    case TypeCode.UInt32: return (ushort)(uint)val;
                    case TypeCode.Int64: return (ushort)(long)val;
                    case TypeCode.UInt64: return (ushort)(ulong)val;
                    case TypeCode.Single: return (ushort)(float)val;
                    case TypeCode.Double: return (ushort)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(ushort));
        }
        [BaseCast]
        public static int ToInt32(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (int)(uint)val;
                    case TypeCode.Int64: return (int)(long)val;
                    case TypeCode.UInt64: return (int)(ulong)val;
                    case TypeCode.Single: return (int)(float)val;
                    case TypeCode.Double: return (int)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(int));
        }
        [BaseCast]
        public static uint ToUInt32(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (uint)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (uint)(short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (uint)(int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (uint)(long)val;
                    case TypeCode.UInt64: return (uint)(ulong)val;
                    case TypeCode.Single: return (uint)(float)val;
                    case TypeCode.Double: return (uint)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(uint));
        }
        [BaseCast]
        public static long ToInt64(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (long)val;
                    case TypeCode.UInt64: return (long)(ulong)val;
                    case TypeCode.Single: return (long)(float)val;
                    case TypeCode.Double: return (long)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(long));
        }
        [BaseCast]
        public static ulong ToUInt64(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (ulong)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (ulong)(short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (ulong)(int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (ulong)(long)val;
                    case TypeCode.UInt64: return (ulong)val;
                    case TypeCode.Single: return (ulong)(float)val;
                    case TypeCode.Double: return (ulong)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(ulong));
        }
        [BaseCast]
        public static float ToSingle(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (long)val;
                    case TypeCode.UInt64: return (ulong)val;
                    case TypeCode.Single: return (float)val;
                    case TypeCode.Double: return (float)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(float));
        }
        [BaseCast]
        public static double ToDouble(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (long)val;
                    case TypeCode.UInt64: return (ulong)val;
                    case TypeCode.Single: return (float)val;
                    case TypeCode.Double: return (double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(double));
        }
    }

    internal class Namespace : Expression
    {
        public Namespace(Identifier identifier)
        {
            this.identifier = identifier;
        }

        public readonly Identifier identifier;


        public override object Execute(ExecutionContext context) => throw new DebugConsoleException("Namespace cannot be used as expression");
        public override Type ResultType => throw new DebugConsoleException("Namespace cannot be used as expression");
    }

    internal class Typename : Expression
    {
        public Typename(Type type)
        {
            this.type = type;
        }

        public readonly Type type;


        public override object Execute(ExecutionContext context) => throw new DebugConsoleException("Typename cannot be used as expression");
        public override Type ResultType => throw new DebugConsoleException("Typename cannot be used as expression");
    }

    internal class GenericTypename : Expression
    {
        public GenericTypename(Type type)
        {
            this.type = type;
        }

        public readonly Type type;


        public override object Execute(ExecutionContext context) => throw new DebugConsoleException($"Unexpected use of an unbound generic type '{type}'");
        public override Type ResultType => throw new DebugConsoleException($"Unexpected use of an unbound generic type '{type}'");
    }

    internal class BoxedObject : Expression
    {
        public BoxedObject(object obj)
        {
            this.obj = obj;
            type = obj == null ? typeof(object) : obj.GetType();
            typeCode = Type.GetTypeCode(type);

            switch (typeCode)
            {
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.String:
                    isConstant = true;
                    break;
                default:
                    isConstant = false;
                    break;
            }
        }

        public readonly object obj;
        public readonly Type type;
        public readonly TypeCode typeCode;
        public readonly bool isConstant;

        public override object Execute(ExecutionContext context) => obj;
        public override Type ResultType => type;
        public override TypeCode GetTypeCode() => typeCode;

        public override bool IsConstant => isConstant;

        public override string ToString()
        {
            return obj == null ? "null" : obj.ToString();
        }
    }

    internal class Null : Expression
    {
        public override object Execute(ExecutionContext context) => null;
        public override Type ResultType => typeof(object);
    }

    // Fields
    internal class FieldMember : Expression
    {
        public FieldMember(Expression lhs, FieldInfo fieldInfo)
        {
            this.lhs = lhs;
            fields.Add(fieldInfo);
        }
        public FieldMember(FieldInfo fieldInfo)
        {
            fields.Add(fieldInfo);
        }

        public readonly Expression lhs;
        public readonly List<FieldInfo> fields = new List<FieldInfo>();
        private FieldInfo TargetField => fields.Last();

        public override bool IsConstant => TargetField.IsLiteral;


        public override object Execute(ExecutionContext context)
        {
            object current = lhs.SafeExecute(context);
            for (int i = 0; i < fields.Count; i++)
            {
                if (!fields[i].IsStatic && current == null) throw new NullResultException();
                current = fields[i].GetValue(current);
            }
            return current;
        }
        public override Type ResultType => TargetField.FieldType;

        public override bool Assignable => !TargetField.IsInitOnly && !TargetField.IsLiteral;
        public override object SetValue(ExecutionContext context, object val, bool returnInitialValue)
        {
            object root = lhs.SafeExecute(context);

            // Build a list of all objects
            object current = root;
            List<object> objects = new List<object>();
            for (int i = 0; i < fields.Count - 1; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsStatic && current == null) throw new NullResultException();
                objects.Add(current);
                current = field.GetValue(current);
            }

            // Set final field
            FieldInfo target = fields.Last();
            if (!target.IsStatic && current == null) throw new NullResultException();
            object result = returnInitialValue ? target.GetValue(current) : null;
            target.SetValue(current, val);

            // Write values back (in case any of them were valuetypes)
            for (int i = fields.Count - 2; i >= 0; i--)
            {
                fields[i].SetValue(objects[i], current);
                current = objects[i];
            }

            if (returnInitialValue)
            {
                return result;
            }
            else
            {
                // Return the current value
                current = root;
                for (int i = 0; i < fields.Count; i++)
                {
                    current = fields[i].GetValue(current);
                }
                return current;
            }
        }
    }

    // Properties
    internal class PropertyMember : Expression
    {
        public PropertyMember(Expression lhs, PropertyInfo propertyInfo)
        {
            this.lhs = lhs;
            this.propertyInfo = propertyInfo;
        }
        public PropertyMember(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
        }

        public readonly Expression lhs;
        public readonly PropertyInfo propertyInfo;


        public override object Execute(ExecutionContext context)
        {
            if (!propertyInfo.CanRead) throw new DebugConsoleException($"Property '{propertyInfo}' is not readable");
            return propertyInfo.GetValue(lhs.SafeExecute(context));
        }
        public override Type ResultType => propertyInfo.PropertyType;

        public override bool Assignable => propertyInfo.CanWrite;
        public override object SetValue(ExecutionContext context, object val, bool returnInitialValue)
        {
            object obj = lhs.SafeExecute(context);
            if (returnInitialValue)
            {
                object result = propertyInfo.GetValue(obj);
                propertyInfo.SetValue(obj, val);
                return result;
            }
            else
            {
                propertyInfo.SetValue(obj, val);
                return propertyInfo.GetValue(obj);
            }
        }
    }

    // Events
    internal class EventMember : Expression
    {
        private class EventReferenceException : DebugConsoleException
        {
            public EventReferenceException() : base("Event can only appear on the left hand side of += or -=")
            {

            }
        }

        public EventMember(Expression lhs, EventInfo eventInfo)
        {
            this.lhs = lhs;
            this.eventInfo = eventInfo;
        }
        public EventMember(EventInfo eventInfo)
        {
            this.eventInfo = eventInfo;
        }


        public readonly Expression lhs;
        public readonly EventInfo eventInfo;

        public void AddEventHandler(ExecutionContext context, Delegate handler)
        {
            eventInfo.AddEventHandler(lhs.SafeExecute(context), handler);
        }
        public void RemoveEventHandler(ExecutionContext context, Delegate handler)
        {
            eventInfo.RemoveEventHandler(lhs.SafeExecute(context), handler);
        }

        public override object Execute(ExecutionContext context)
        {
            throw new EventReferenceException();
        }
        public override Type ResultType => throw new EventReferenceException();
    }

    // Methods
    internal class MethodOverload : Expression
    {
        public MethodOverload(string methodName, Expression lhs, IReadOnlyList<MethodInfo> methods)
        {
            this.methodName = methodName;
            this.lhs = lhs;
            this.methods = methods;
        }
        public MethodOverload(string methodName, IReadOnlyList<MethodInfo> methods)
        {
            this.methodName = methodName;
            this.methods = methods;
        }

        public readonly string methodName;
        public readonly Expression lhs;
        public readonly IReadOnlyList<MethodInfo> methods;


        public override object Execute(ExecutionContext context) => throw new DebugConsoleException("Method cannot be used as expression");
        public override Type ResultType => throw new DebugConsoleException("Method cannot be used as expression");
    }

    internal class MethodMember : Expression
    {
        public MethodMember(Expression lhs, MethodInfo method, Expression[] args)
        {
            this.lhs = lhs;
            this.method = method;
            this.args = args;
        }
        public MethodMember(MethodInfo method, Expression[] args)
        {
            this.method = method;
            this.args = args;
        }

        public readonly Expression lhs;
        public readonly MethodInfo method;
        public readonly Expression[] args;


        public override object Execute(ExecutionContext context)
        {
            object result = InvokeMethod(context, method, lhs.SafeExecute(context), args);
            if (method.ReturnType == typeof(void)) return VoidResult.Empty;
            return result;
        }
        public override Type ResultType => method.ReturnType;
    }

    internal class CreateDelegate : Expression
    {
        public CreateDelegate(Expression lhs, MethodInfo method, Type delegateType)
        {
            this.lhs = lhs;
            this.method = method;
            this.delegateType = delegateType;
        }

        public readonly Expression lhs;
        public readonly MethodInfo method;
        public readonly Type delegateType;


        public override object Execute(ExecutionContext context)
        {
            return method.CreateDelegate(delegateType, lhs.SafeExecute(context));
        }
        public override Type ResultType => delegateType;
    }

    // Subscript
    internal class Subscript : Expression
    {
        public Subscript(Expression lhs, Expression[] args, PropertyInfo property)
        {
            this.lhs = lhs;
            this.args = args;
            this.property = property;
        }

        public readonly Expression lhs;
        public readonly Expression[] args;
        public readonly PropertyInfo property;


        public override object Execute(ExecutionContext context)
        {
            return InvokeMethod(context, property.GetMethod, lhs.SafeExecute(context), args);
        }
        public override Type ResultType => property.PropertyType;

        public override bool Assignable => property.CanWrite;
        public override object SetValue(ExecutionContext context, object val, bool returnInitialValue)
        {
            object obj = lhs.SafeExecute(context);
            if (returnInitialValue)
            {
                object result = InvokeMethod(context, property.GetMethod, obj, args);
                property.SetValue(obj, val, ExpressionUtility.Forward<object>(context, args));
                return result;
            }
            else
            {
                property.SetValue(obj, val, ExpressionUtility.Forward<object>(context, args));
                return InvokeMethod(context, property.GetMethod, obj, args);
            }
        }
    }

    // Constructors
    internal class DefaultConstructor : Expression
    {
        public DefaultConstructor(Type type)
        {
            this.type = type;
        }

        public readonly Type type;


        public override object Execute(ExecutionContext context)
        {
            if (type.IsByRefLike)
            {
                throw new DebugConsoleException("Unable to create byref-like structures using reflection");
            }

            return Activator.CreateInstance(type);
        }
        public override Type ResultType => type;
    }

    internal class Constructor : Expression
    {
        public Constructor(Type type, ConstructorInfo constructor, Expression[] args)
        {
            this.type = type;
            this.constructor = constructor;
            this.args = args;
        }

        public readonly Type type;
        public readonly ConstructorInfo constructor;
        public readonly Expression[] args;


        public override object Execute(ExecutionContext context)
        {
            if (type.IsByRefLike)
            {
                throw new DebugConsoleException("Unable to create byref-like structures using reflection");
            }

            return constructor.Invoke(ExpressionUtility.Forward<object>(context, args));
        }
        public override Type ResultType => type;
    }

    internal class ArrayInitializer : Expression
    {
        public readonly List<Expression> values = new List<Expression>();

        public void ValidateInitializerLength(int[] lengths)
        {
            ValidateInitializerLength(lengths, 0);
        }
        private void ValidateInitializerLength(int[] lengths, int depth)
        {
            if (depth == lengths.Length)
            {
                throw new DebugConsoleException($"Unexpected array initializer");
            }

            if (lengths[depth] == -1)
            {
                lengths[depth] = values.Count;
            }
            else
            {
                if (lengths[depth] != values.Count)
                {
                    throw new DebugConsoleException($"An array initializer of length '{lengths[depth]}' is expected");
                }
            }

            int nextDepth = depth + 1;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is ArrayInitializer nested)
                {
                    nested.ValidateInitializerLength(lengths, nextDepth);
                }
                else if (nextDepth != lengths.Length)
                {
                    throw new DebugConsoleException("A nested array initializer is expected");
                }
            }
        }

        private void InitializeArray(ExecutionContext context, Array array, int[] indices, int depth)
        {
            for (int i = 0; i < values.Count; i++)
            {
                indices[depth] = i;

                if (values[i] is ArrayInitializer nested)
                {
                    nested.InitializeArray(context, array, indices, depth + 1);
                }
                else
                {
                    array.SetValue(values[i].Execute(context), indices);
                }
            }
        }
        public void InitializeArray(ExecutionContext context, Array array)
        {
            int rank = array.GetType().GetArrayRank();
            switch (rank)
            {
                case 1:
                    for (int i = 0; i < values.Count; i++)
                    {
                        array.SetValue(values[i].Execute(context), i);
                    }
                    break;
                case 2:
                    for (int i = 0; i < values.Count; i++)
                    {
                        ArrayInitializer n0 = (ArrayInitializer)values[i];
                        for (int j = 0; j < n0.values.Count; j++)
                        {
                            array.SetValue(n0.values[j].Execute(context), i, j);
                        }
                    }
                    break;
                case 3:
                    for (int i = 0; i < values.Count; i++)
                    {
                        ArrayInitializer n0 = (ArrayInitializer)values[i];
                        for (int j = 0; j < n0.values.Count; j++)
                        {
                            ArrayInitializer n1 = (ArrayInitializer)n0.values[i];
                            for (int k = 0; k < n1.values.Count; k++)
                            {
                                array.SetValue(n1.values[k].Execute(context), i, j, k);
                            }
                        }
                    }
                    break;
                default:
                    InitializeArray(context, array, new int[rank], 0);
                    break;
            }
        }

        public override Type ResultType => throw new NotImplementedException();
        public override object Execute(ExecutionContext context) => throw new NotImplementedException();
    }

    internal class DynamicSizeArrayConstructor : Expression
    {
        public DynamicSizeArrayConstructor(Type elementType, Expression[] lengths, ArrayInitializer initializer = null)
        {
            this.elementType = elementType;
            arrayType = CreateArrayType(elementType, lengths.Length);
            this.lengths = lengths;
            this.initializer = initializer;
        }

        public readonly Type elementType;
        public readonly Type arrayType;
        public readonly Expression[] lengths;
        public readonly ArrayInitializer initializer;


        public override object Execute(ExecutionContext context)
        {
            int[] lengths = ExpressionUtility.Forward<int>(context, this.lengths);
            Array array = Array.CreateInstance(elementType, lengths);
            if (initializer != null)
            {
                initializer.InitializeArray(context, array);
            }
            return array;
        }
        public override Type ResultType => arrayType;
    }

    internal class ConstantSizeArrayConstructor : Expression
    {
        public ConstantSizeArrayConstructor(Type elementType, int[] lengths, ArrayInitializer initializer = null)
        {
            this.elementType = elementType;
            arrayType = CreateArrayType(elementType, lengths.Length);
            this.lengths = lengths;
            this.initializer = initializer;
        }

        public readonly Type elementType;
        public readonly Type arrayType;
        public readonly int[] lengths;
        public readonly ArrayInitializer initializer;


        public override object Execute(ExecutionContext context)
        {
            Array array = Array.CreateInstance(elementType, lengths);
            if (initializer != null)
            {
                initializer.InitializeArray(context, array);
            }
            return array;
        }
        public override Type ResultType => arrayType;
    }

    // Operators
    internal class UnaryOperator : Expression
    {
        public UnaryOperator(Expression arg, UnaryOperatorFunction func)
        {
            this.arg = arg;
            this.func = func;
        }

        public readonly Expression arg;
        public readonly UnaryOperatorFunction func;

        public override bool IsConstant => arg.IsConstant;


        public override object Execute(ExecutionContext context) => func.Invoke(context, arg);
        public override Type ResultType => func.ReturnType;
    }

    internal class InfixOperator : Expression
    {
        public InfixOperator(Expression lhs, Expression rhs, InfixOperatorFunction func)
        {
            this.lhs = lhs;
            this.rhs = rhs;
            this.func = func;
        }

        public readonly Expression lhs;
        public readonly Expression rhs;
        public readonly InfixOperatorFunction func;

        public override bool IsConstant => lhs.IsConstant && rhs.IsConstant;


        public override object Execute(ExecutionContext context) => func.Invoke(context, lhs, rhs);
        public override Type ResultType => func.ReturnType;
    }

    internal class LogicalAnd : Expression
    {
        public LogicalAnd(Expression lhs, Expression rhs)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }

        public readonly Expression lhs;
        public readonly Expression rhs;


        public override object Execute(ExecutionContext context)
        {
            bool lhsResult = lhs.Forward<bool>(context);
            if (!lhsResult) return false;
            return rhs.Forward<bool>(context);
        }
        public override Type ResultType => typeof(bool);
    }

    internal class LogicalOr : Expression
    {
        public LogicalOr(Expression lhs, Expression rhs)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }

        public readonly Expression lhs;
        public readonly Expression rhs;


        public override object Execute(ExecutionContext context)
        {
            bool lhsResult = lhs.Forward<bool>(context);
            if (lhsResult) return true;
            return rhs.Forward<bool>(context);
        }
        public override Type ResultType => typeof(bool);
    }

    internal class Conditional : Expression
    {
        public Conditional(Expression condition, Expression consequent, Expression alternative, Type resultType)
        {
            this.condition = condition;
            this.consequent = consequent;
            this.alternative = alternative;
            this.resultType = resultType;
        }

        public readonly Expression condition;
        public readonly Expression consequent;
        public readonly Expression alternative;
        public readonly Type resultType;


        public override object Execute(ExecutionContext context)
        {
            bool cond = condition.Forward<bool>(context);
            if (cond) return consequent.Forward(context, resultType);
            return alternative.Forward(context, resultType);
        }
        public override Type ResultType => resultType;
    }

    internal class Assignment : Expression
    {
        public Assignment(Expression lhs, Expression rhs, bool returnInitialValue = false)
        {
            this.lhs = lhs;
            this.rhs = rhs;
            this.returnInitialValue = returnInitialValue;
        }

        public readonly Expression lhs;
        public readonly Expression rhs;
        public readonly bool returnInitialValue;

        public override object Execute(ExecutionContext context)
        {
            return lhs.SetValue(context, rhs.Execute(context), returnInitialValue);
        }
        public override Type ResultType => lhs.ResultType;
    }

    internal class DelegateAdd : Expression
    {
        public DelegateAdd(Expression lhs, Expression rhs)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }

        public readonly Expression lhs;
        public readonly Expression rhs;

        public override object Execute(ExecutionContext context)
        {
            return lhs.SetValue(context, Delegate.Combine(lhs.Execute(context) as Delegate, rhs.Execute(context) as Delegate), false);
        }
        public override Type ResultType => lhs.ResultType;
    }

    internal class DelegateRemove : Expression
    {
        public DelegateRemove(Expression lhs, Expression rhs)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }

        public readonly Expression lhs;
        public readonly Expression rhs;

        public override object Execute(ExecutionContext context)
        {
            return lhs.SetValue(context, Delegate.Remove(lhs.Execute(context) as Delegate, rhs.Execute(context) as Delegate), false);
        }
        public override Type ResultType => lhs.ResultType;
    }

    internal class DelegateInvoke : Expression
    {
        public DelegateInvoke(Expression lhs, MethodInfo invoke, Expression[] args)
        {
            this.lhs = lhs;
            this.invoke = invoke;
            this.args = args;
        }

        public readonly Expression lhs;
        public readonly MethodInfo invoke;
        public readonly Expression[] args;

        public override object Execute(ExecutionContext context)
        {
            object result = InvokeMethod(context, invoke, lhs.SafeExecute(context), args);
            if (invoke.ReturnType == typeof(void)) return VoidResult.Empty;
            return result;
        }
        public override Type ResultType => invoke.ReturnType;
    }

    internal class EventAdd : Expression
    {
        public EventAdd(EventMember lhs, Expression rhs)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }

        public readonly EventMember lhs;
        public readonly Expression rhs;

        public override object Execute(ExecutionContext context)
        {
            lhs.AddEventHandler(context, rhs.Execute(context) as Delegate);
            return VoidResult.Empty;
        }
        public override Type ResultType => typeof(void);
    }

    internal class EventRemove : Expression
    {
        public EventRemove(EventMember lhs, Expression rhs)
        {
            this.lhs = lhs;
            this.rhs = rhs;
        }

        public readonly EventMember lhs;
        public readonly Expression rhs;

        public override object Execute(ExecutionContext context)
        {
            lhs.RemoveEventHandler(context, rhs.Execute(context) as Delegate);
            return VoidResult.Empty;
        }
        public override Type ResultType => typeof(void);
    }

    internal class Cast<T> : Expression
    {
        public Cast(Func<Expression, T> func, Expression rhs)
        {
            this.func = func;
            this.rhs = rhs;
        }

        public readonly Func<Expression, T> func;
        public readonly Expression rhs;


        public override object Execute(ExecutionContext context)
        {
            return func(rhs);
        }
        public override Type ResultType => typeof(T);
    }

    internal class Conversion : Expression
    {
        public Conversion(Type type, Expression rhs)
        {
            this.type = type;
            this.rhs = rhs;
        }

        public readonly Type type;
        public readonly Expression rhs;


        public override object Execute(ExecutionContext context)
        {
            return ConvertMethod.MakeGenericMethod(type).Invoke(this, new object[] { rhs.Execute(context) });
        }
        public override Type ResultType => type;


        private static readonly MethodInfo ConvertMethod = typeof(Conversion).GetMethod("Convert");
        public static T Convert<T>(object obj) => (T)obj;
    }

    internal class IsCast : Expression
    {
        public IsCast(Expression lhs, Type type)
        {
            this.lhs = lhs;
            this.type = type;
        }

        public readonly Expression lhs;
        public readonly Type type;

        public override object Execute(ExecutionContext context)
        {
            object val = lhs.Execute(context);
            if (val != null)
            {
                Type lhsType = val.GetType();
                return type.IsAssignableFrom(lhsType);
            }
            return false;
        }
        public override Type ResultType => typeof(bool);
    }

    internal class AsCast : Expression
    {
        public AsCast(Expression lhs, Type type)
        {
            this.lhs = lhs;
            this.type = type;
        }

        public readonly Expression lhs;
        public readonly Type type;

        public override object Execute(ExecutionContext context)
        {
            object val = lhs.Execute(context);
            if (val != null)
            {
                Type lhsType = val.GetType();
                if (type.IsAssignableFrom(lhsType))
                {
                    return val;
                }
            }
            return null;
        }
        public override Type ResultType => type;
    }

    // Blocks
    internal class Block : Expression
    {
        public Block()
        {

        }
        public Block(Expression singleExpression)
        {
            expressions.Add(singleExpression);
        }

        public readonly List<Expression> expressions = new List<Expression>();

        public object ExecuteExpressions(ExecutionContext context)
        {
            object result = VoidResult.Empty;
            for (int i = 0; i < expressions.Count; i++)
            {
                result = expressions[i].Execute(context);
            }
            return result;
        }


        public override object Execute(ExecutionContext context)
        {
            int stackOffset = context.variables.Count;
            context.Push(this);
            object result = ExecuteExpressions(context);
            context.Pop();
            context.variables.RemoveRange(stackOffset, context.variables.Count - stackOffset);
            return result;
        }
        public override Type ResultType => typeof(void);
    }

    internal class IfBlock : Block
    {
        public IfBlock(Expression condition)
        {
            this.condition = condition;
        }

        public readonly Expression condition;

        private Expression next = null;
        private Expression last = null;

        public void AddSubBlock(Expression next)
        {
            if (last != null)
                ((IfBlock)last).next = next;
            else
                this.next = next;

            last = next;
        }


        public override object Execute(ExecutionContext context)
        {
            object result = VoidResult.Empty;
            if (condition.Forward<bool>(context))
            {
                result = base.Execute(context);
            }
            else if (next != null)
            {
                result = next.Execute(context);
            }
            return result;
        }
    }

    internal class ElseIfBlock : IfBlock
    {
        public ElseIfBlock(Expression condition) : base(condition)
        {

        }
    }

    internal class ElseBlock : Block
    {

    }

    internal class ForBlock : Block
    {
        public ForBlock(Expression init, Expression condition, Expression step, int maxIterations)
        {
            this.init = init;
            this.condition = condition;
            this.step = step;
            this.maxIterations = maxIterations;
        }

        public readonly Expression init;
        public readonly Expression condition;
        public readonly Expression step;
        public readonly int maxIterations;


        public override object Execute(ExecutionContext context)
        {
            int outerStackOffset = context.variables.Count;
            context.Push(this);
            object result = VoidResult.Empty;
            int iterCount = 0;
            init?.Execute(context);
            for (; condition == null || condition.Forward<bool>(context); step?.Execute(context))
            {
                int innerStackOffset = context.variables.Count;

                for (int i = 0; i < expressions.Count; i++)
                {
                    result = expressions[i].Execute(context);
                }

                if (++iterCount >= maxIterations) throw new DebugConsoleException($"Maximum loop iteration reached ({maxIterations})");

                context.variables.RemoveRange(innerStackOffset, context.variables.Count - innerStackOffset);
            }
            context.Pop();
            context.variables.RemoveRange(outerStackOffset, context.variables.Count - outerStackOffset);
            return result;
        }
    }

    internal class WhileBlock : Block
    {
        public WhileBlock(Expression condition, int maxIterations)
        {
            this.condition = condition;
            this.maxIterations = maxIterations;
        }

        public readonly Expression condition;
        public readonly int maxIterations;


        public override object Execute(ExecutionContext context)
        {
            int outerStackOffset = context.variables.Count;
            object result = VoidResult.Empty;
            int iterCount = 0;
            while (condition.Forward<bool>(context))
            {
                result = base.Execute(context);

                if (++iterCount >= maxIterations) throw new DebugConsoleException($"Maximum loop iteration reached ({maxIterations})");

                context.variables.RemoveRange(outerStackOffset, context.variables.Count - outerStackOffset);
            }
            return result;
        }
    }

    internal class ForeachBlock : Block
    {
        public ForeachBlock(Expression collection, EnumeratorInfo enumeratorInfo, VariableDeclaration iterVar, int maxIterations)
        {
            this.collection = collection;
            this.enumeratorInfo = enumeratorInfo;
            this.iterVar = iterVar;
            this.maxIterations = maxIterations;
        }

        public readonly Expression collection;
        public readonly EnumeratorInfo enumeratorInfo;
        public readonly VariableDeclaration iterVar;
        public readonly int maxIterations;


        public override object Execute(ExecutionContext context)
        {
            int outerStackOffset = context.variables.Count;
            context.Push(this);
            object result = VoidResult.Empty;
            int iterCount = 0;
            object collectionObj = collection.Execute(context);
            object enumeratorObj = enumeratorInfo.GetEnumerator(collectionObj);
            iterVar.Execute(context);
            for (; enumeratorInfo.MoveNext(enumeratorObj);)
            {
                int innerStackOffset = context.variables.Count;

                iterVar.Value.UpdateValue(enumeratorInfo.Current(enumeratorObj));

                for (int i = 0; i < expressions.Count; i++)
                {
                    result = expressions[i].Execute(context);
                }

                if (++iterCount >= maxIterations) throw new DebugConsoleException($"Maximum loop iteration reached ({maxIterations})");

                context.variables.RemoveRange(innerStackOffset, context.variables.Count - innerStackOffset);
            }
            context.Pop();
            context.variables.RemoveRange(outerStackOffset, context.variables.Count - outerStackOffset);
            return result;
        }
    }

    // Variables
    internal class VariableDeclaration : Expression
    {
        public VariableDeclaration(Type type, string name)
        {
            this.type = type;
            this.name = name;

            Value = new VariableReference(type, name);
        }

        public readonly Type type;
        public readonly string name;


        public VariableReference Value { get; protected set; }

        public override object Execute(ExecutionContext context)
        {
            Value.UpdateValue(type.IsValueType ? Activator.CreateInstance(type) : null);
            context.variables.Add(Value);
            return VoidResult.Empty;
        }
        public override Type ResultType => typeof(void);
    }

    internal class VariableAssignment : VariableDeclaration
    {
        public VariableAssignment(Type type, string name, Expression rhs) : base(type, name)
        {
            this.rhs = rhs;

            Value = new VariableReference(type, name);
        }

        public readonly Expression rhs;


        public override object Execute(ExecutionContext context)
        {
            Value.UpdateValue(rhs.Execute(context));
            context.variables.Add(Value);
            return VoidResult.Empty;
        }
    }

    internal class VariableReference : Expression
    {
        public VariableReference(Type type, string name)
        {
            this.name = name;
            this.type = type;
        }

        public readonly string name;
        private readonly Type type;
        public object Value { get; private set; }

        public void UpdateValue(object value)
        {
            Value = value;
        }


        public override object Execute(ExecutionContext context)
        {
            return Value;
        }
        public override Type ResultType => type;

        public override bool Assignable => true;
        public override object SetValue(ExecutionContext context, object val, bool returnInitialValue)
        {
            if (returnInitialValue)
            {
                object result = Value;
                Value = val;
                return result;
            }
            else
            {
                Value = val;
                return Value;
            }
        }
    }
}
#endif