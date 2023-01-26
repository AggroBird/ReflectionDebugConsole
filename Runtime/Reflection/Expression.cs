// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
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

    internal abstract class Expression
    {
        public abstract object Execute(ExecutionContext context);
        public abstract Type ResultType { get; }
        public virtual TypeCode GetTypeCode() => Type.GetTypeCode(ResultType);

        public virtual bool Assignable => false;
        public virtual object SetValue(ExecutionContext context, object val, bool returnInitialValue) => throw new DebugConsoleException("Expression is read only");


        private class BaseCastAttribute : Attribute { }

        static Expression()
        {
            foreach (var member in typeof(Array).GetMember("GetValue", MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance))
            {
                if (member is MethodInfo methodInfo)
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (parameters.Length == 1)
                    {
                        if (parameters[0].ParameterType == typeof(int))
                        {
                            ArrayGetValueSingleKeyMethodInfo = methodInfo;
                        }
                        else if (parameters[0].ParameterType == typeof(int[]))
                        {
                            ArrayGetValueMultipleKeyMethodInfo = methodInfo;
                        }
                    }
                }
            }

            foreach (var member in typeof(Array).GetMember("SetValue", MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance))
            {
                if (member is MethodInfo methodInfo)
                {
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    if (parameters.Length == 2)
                    {
                        if (parameters[1].ParameterType == typeof(int))
                        {
                            ArraySetValueSingleKeyMethodInfo = methodInfo;
                        }
                        else if (parameters[1].ParameterType == typeof(int[]))
                        {
                            ArraySetValueMultipleKeyMethodInfo = methodInfo;
                        }
                    }
                }
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

        public static readonly MethodInfo ArrayGetValueSingleKeyMethodInfo;
        public static readonly MethodInfo ArrayGetValueMultipleKeyMethodInfo;
        public static readonly MethodInfo ArraySetValueSingleKeyMethodInfo;
        public static readonly MethodInfo ArraySetValueMultipleKeyMethodInfo;

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
                        switch (CompareMethodOverloads(optimal[0], compatible[i], args))
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
        public static PropertyInfo[] GetOptimalOverloads(IReadOnlyList<PropertyInfo> overloads, string methodName, params Expression[] args)
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
                        switch (CompareMethodOverloads(optimal[0].GetMethod, compatible[i].GetMethod, args))
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
        private static int CompareMethodOverloads(MethodBase lhs, MethodBase rhs, Expression[] args)
        {
            int score = 0;

            bool lhsVarArg = HasVariableParameterCount(lhs), rhsVarArg = HasVariableParameterCount(rhs);

            if (lhsVarArg == rhsVarArg)
            {
                ParameterInfo[] lhsParameters = lhs.GetParameters(), rhsParameters = rhs.GetParameters();
                int lhsDefaults = CountDefaults(lhsParameters), rhsDefaults = CountDefaults(rhsParameters);
                if (lhsDefaults != rhsDefaults)
                {
                    PickSmallest(ref score, lhsDefaults, rhsDefaults);
                }
                else if (lhsParameters.Length == rhsParameters.Length)
                {
                    // Compare parameters
                    for (int i = 0; i < args.Length; i++)
                    {
                        PickEqual(ref score, lhsParameters[i].ParameterType, rhsParameters[i].ParameterType, args[i].ResultType);
                    }
                }
                else
                {
                    // Pick smallest overload
                    PickSmallest(ref score, lhsParameters.Length, rhsParameters.Length);
                }
            }
            else
            {
                // Pick non-vararg
                PickEqual(ref score, lhsVarArg, rhsVarArg, false);
            }

            return score < 0 ? -1 : score > 0 ? 1 : 0;
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

        public static bool IncludeMember<T>(T member, bool includeSpecial = false) where T : MemberInfo
        {
            if (member is MethodBase methodBase)
            {
                // Skip generic methods
                if (methodBase.ContainsGenericParameters) return false;

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
                    if (parameter.ParameterType.IsByRef) return false;
                    if (parameter.ParameterType.IsPointer) return false;
                }
            }

            // Skip compiler generated members
            if (member.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
            {
                return false;
            }

            return true;
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
            if (IncludeMember(nestedType))
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

        public static bool HasVariableParameterCount(MethodBase method)
        {
            ParameterInfo[] param = method.GetParameters();
            return param.Length > 0 && param[param.Length - 1].GetCustomAttribute<ParamArrayAttribute>(true) != null;
        }


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

        private static bool IsImplicitConvertableBaseType(Expression expr, Type dstType, out MethodInfo castMethod)
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
            Type srcType = expr.ResultType;

            // No conversion required
            if (srcType == dstType || dstType.IsAssignableFrom(srcType))
            {
                castExpr = expr;
                return true;
            }

            // Null
            if (expr is Null && !dstType.IsValueType)
            {
                castExpr = expr;
                return true;
            }

            if (IsImplicitConvertableBaseType(expr, dstType, out MethodInfo castMethod))
            {
                castExpr = new MethodMember(castMethod, new Expression[] { expr });
                return true;
            }

            if (expr is MethodOverload overload && GetCompatibleDelegateOverload(overload, dstType, out Expression delegateCast))
            {
                castExpr = delegateCast;
                return true;
            }

            castExpr = null;
            return false;
        }

        private static bool IsExplicitConvertableBaseType(Expression expr, Type dstType, out MethodInfo castMethod)
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
            Type srcType = expr.ResultType;

            // No conversion
            if (srcType == dstType)
            {
                castExpr = expr;
                return true;
            }

            // Boxing, unboxing or casting
            if (expr.ResultType == typeof(object) || dstType.IsAssignableFrom(srcType) || dstType.IsSubclassOf(srcType))
            {
                castExpr = new Conversion(dstType, expr);
                return true;
            }

            if (IsExplicitConvertableBaseType(expr, dstType, out MethodInfo castMethod))
            {
                castExpr = new MethodMember(castMethod, new Expression[] { expr });
                return true;
            }

            castExpr = null;
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

        public static bool IsCompatibleOverload(MethodBase method, IReadOnlyList<Expression> args, bool matchParameterCount = true)
        {
            ParameterInfo[] param = method.GetParameters();
            int actualParamCount = param.Length;
            int actualArgCount = args.Count;
            if (actualParamCount > 0)
            {
                int lastParamIndex = actualParamCount - 1;
                ParameterInfo lastParam = param[lastParamIndex];
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
                    if (!IsImplicitConvertable(args[i], param[i].ParameterType, out _))
                        return false;
                }
                return !matchParameterCount || actualArgCount == actualParamCount || param[actualArgCount].HasDefaultValue;
            }
            return false;
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
            MemberInfo[] srcCasts = srcType.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < srcCasts.Length; i++)
            {
                if (srcCasts[i] is MethodInfo castMethod)
                {
                    ParameterInfo[] parameters = castMethod.GetParameters();
                    if (parameters.Length == 1 && castMethod.ReturnType == dstType && parameters[0].ParameterType == srcType)
                    {
                        result = castMethod;
                        return true;
                    }
                }
            }

            MemberInfo[] dstCasts = dstType.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < dstCasts.Length; i++)
            {
                if (dstCasts[i] is MethodInfo castMethod)
                {
                    ParameterInfo[] parameters = castMethod.GetParameters();
                    if (parameters.Length == 1 && castMethod.ReturnType == dstType && parameters[0].ParameterType == srcType)
                    {
                        result = castMethod;
                        return true;
                    }
                }
            }

            result = null;
            return false;
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
        public Namespace(NamespaceIdentifier identifier)
        {
            this.identifier = identifier;
        }

        public readonly NamespaceIdentifier identifier;


        public override object Execute(ExecutionContext context)
        {
            return $"namespace {identifier.Name} ({identifier.ChildCount} {(identifier.ChildCount == 1 ? "child" : "children")})";
        }
        public override Type ResultType => typeof(void);
    }

    internal class Typename : Expression
    {
        public Typename(Type type)
        {
            this.type = type;
        }

        public readonly Type type;


        public override object Execute(ExecutionContext context)
        {
            return $"{GetPrefix(type)}{type.FullName}";
        }
        public override Type ResultType => typeof(void);
    }

    internal class BoxedObject : Expression
    {
        public BoxedObject(object obj)
        {
            this.obj = obj;
            type = obj == null ? typeof(object) : obj.GetType();
            typeCode = Type.GetTypeCode(type);
        }

        public readonly object obj;
        public readonly Type type;
        public readonly TypeCode typeCode;

        public override object Execute(ExecutionContext context) => obj;
        public override Type ResultType => type;
        public override TypeCode GetTypeCode() => typeCode;

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

        public override bool Assignable => !TargetField.IsInitOnly;
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
        public MethodOverload(string methodName, Expression lhs, List<MethodInfo> methods)
        {
            this.methodName = methodName;
            this.lhs = lhs;
            this.methods = methods;
        }
        public MethodOverload(string methodName, List<MethodInfo> methods)
        {
            this.methodName = methodName;
            this.methods = methods;
        }

        public readonly string methodName;
        public readonly Expression lhs;
        public readonly List<MethodInfo> methods;


        public override object Execute(ExecutionContext context)
        {
            if (methods.Count == 1) return methods[0];
            return $"<{methods.Count} methods>";
        }
        public override Type ResultType => typeof(void);
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
    internal abstract class Subscript : Expression
    {

    }

    internal class OnedimensionalSubscript : Subscript
    {
        public OnedimensionalSubscript(Expression lhs, Expression arg, Type elementType)
        {
            this.lhs = lhs;
            this.arg = arg;
            this.elementType = elementType;
        }

        public readonly Expression lhs;
        public readonly Expression arg;
        public readonly Type elementType;


        public override object Execute(ExecutionContext context)
        {
            object obj = lhs.SafeExecute(context);
            int index = arg.Forward<int>(context);
            return ArrayGetValueSingleKeyMethodInfo.Invoke(obj, new object[] { index });
        }
        public override Type ResultType => elementType;

        public override bool Assignable => true;
        public override object SetValue(ExecutionContext context, object val, bool returnInitialValue)
        {
            object obj = lhs.SafeExecute(context);
            int index = arg.Forward<int>(context);
            if (returnInitialValue)
            {
                object result = ArrayGetValueSingleKeyMethodInfo.Invoke(obj, new object[] { index });
                ArraySetValueSingleKeyMethodInfo.Invoke(obj, new object[] { val, index });
                return result;
            }
            else
            {
                ArraySetValueSingleKeyMethodInfo.Invoke(obj, new object[] { val, index });
                return ArrayGetValueSingleKeyMethodInfo.Invoke(obj, new object[] { index });
            }
        }
    }

    internal class MultidimensionalSubscript : Subscript
    {
        public MultidimensionalSubscript(Expression lhs, Expression[] args, Type elementType)
        {
            this.lhs = lhs;
            this.args = args;
            this.elementType = elementType;
        }

        public readonly Expression lhs;
        public readonly Expression[] args;
        public readonly Type elementType;


        public override object Execute(ExecutionContext context)
        {
            object obj = lhs.SafeExecute(context);
            int[] indices = ExpressionUtility.Forward<int>(context, args);
            return ArrayGetValueMultipleKeyMethodInfo.Invoke(obj, new object[] { indices });
        }
        public override Type ResultType => elementType;

        public override bool Assignable => true;
        public override object SetValue(ExecutionContext context, object val, bool returnInitialValue)
        {
            object obj = lhs.SafeExecute(context);
            int[] indices = ExpressionUtility.Forward<int>(context, args);
            if (returnInitialValue)
            {
                object result = ArrayGetValueMultipleKeyMethodInfo.Invoke(obj, new object[] { indices });
                ArraySetValueMultipleKeyMethodInfo.Invoke(obj, new object[] { val, indices });
                return result;
            }
            else
            {
                ArraySetValueMultipleKeyMethodInfo.Invoke(obj, new object[] { val, indices });
                return ArrayGetValueMultipleKeyMethodInfo.Invoke(obj, new object[] { indices });
            }
        }
    }

    internal class CustomSubscript : Subscript
    {
        public CustomSubscript(Expression lhs, Expression[] args, PropertyInfo property)
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
        public override Type ResultType => property.GetMethod.ReturnType;

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

    internal class StringSubscript : Subscript
    {
        public StringSubscript(Expression lhs, Expression arg)
        {
            this.lhs = lhs;
            this.arg = arg;
        }

        public readonly Expression lhs;
        public readonly Expression arg;


        public override object Execute(ExecutionContext context)
        {
            string str = (string)lhs.SafeExecute(context);
            int index = arg.Forward<int>(context);
            return str[index];
        }
        public override Type ResultType => typeof(char);
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
            return constructor.Invoke(ExpressionUtility.Forward<object>(context, args));
        }
        public override Type ResultType => type;
    }

    internal class ArrayConstructor : Expression
    {
        public ArrayConstructor(Type arrayType, Expression[] args)
        {
            this.arrayType = arrayType;
            this.args = args;
        }

        public readonly Type arrayType;
        public readonly Expression[] args;


        public override object Execute(ExecutionContext context)
        {
            int[] lengths = ExpressionUtility.Forward<int>(context, args);
            return Array.CreateInstance(arrayType.GetElementType(), lengths);
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

        public static T Convert<T>(object obj)
        {
            return (T)obj;
        }
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