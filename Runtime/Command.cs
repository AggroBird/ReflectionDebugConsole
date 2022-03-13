// Copyright, 2021, AggrobirdGK

#if !NO_DEBUG_CONSOLE
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace AggroBird.DebugConsole
{
    internal static class Errors
    {
        public const string VoidRef = "Attempted to dereference void type";
        public const string NullRef = "Object reference not set to an instance of an object";
    }

    internal sealed class ConsoleException : Exception
    {
        public ConsoleException(string message) : base(message)
        {

        }
    }

    internal enum CmdTokenType
    {
        Dereference = 0,
        Invoke,
        Subscript,
        Declaration,
        Assignment,
        Variable,
    }

    internal enum StringMode
    {
        None = 0,
        String,
        Char,
    }

    internal enum MatchParameterCount
    {
        Equal,
        LEqual,
        LessThan,
    }

    // Structs have no default constructor member info so this serves as a substitute
    internal sealed class DefaultConstructorInfo : ConstructorInfo
    {
        public DefaultConstructorInfo(Type declaringType)
        {
            this.declaringType = declaringType;
        }

        private Type declaringType;
        private static readonly ParameterInfo[] parameterInfo = new ParameterInfo[0];

        public override Type DeclaringType => declaringType;
        public override ParameterInfo[] GetParameters() => parameterInfo;

        public override MethodAttributes Attributes => throw new NotImplementedException();
        public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();
        public override string Name => throw new NotImplementedException();
        public override Type ReflectedType => throw new NotImplementedException();
        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }
        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }
        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            throw new NotImplementedException();
        }
        public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }
    }

    // Method casts the return value to provided type
    internal sealed class GenericTypeMethodInfo
    {
        public GenericTypeMethodInfo(MethodInfo methodInfo, Type returnType)
        {
            MethodInfo = methodInfo;
            ReturnType = returnType;
        }

        public MethodInfo MethodInfo { get; private set; }
        public Type ReturnType { get; private set; }
    }

    // Invoke a delegate
    internal sealed class DelegateInfo
    {
        public DelegateInfo(FieldInfo fieldInfo, MethodInfo methodInfo)
        {
            FieldInfo = fieldInfo;
            MethodInfo = methodInfo;
            ReturnType = methodInfo.ReturnType;
        }

        public FieldInfo FieldInfo { get; private set; }
        public MethodInfo MethodInfo { get; private set; }
        public Type ReturnType { get; private set; }
    }

    // Assignment
    internal sealed class AssignmentInfo
    {
        public AssignmentInfo(string fieldName, FieldInfo fieldInfo = null, MethodInfo methodInfo = null, bool shiftArgs = false)
        {
            FieldName = fieldName;
            FieldInfo = fieldInfo;
            MethodInfo = methodInfo;
            ShiftArgs = shiftArgs;
        }

        public string FieldName { get; private set; }
        public FieldInfo FieldInfo { get; private set; }
        public MethodInfo MethodInfo { get; private set; }
        public bool ShiftArgs { get; private set; }
    }

    // Console variables
    internal sealed class VariableInfo
    {
        public VariableInfo(string variableName, Type variableType)
        {
            VariableName = variableName;
            this.variableType = variableType;
        }

        public string VariableName { get; private set; }
        public Type variableType = default;
        public object variableValue = default;
    }

    internal sealed class Context
    {
        public void Clear()
        {
            variables.Clear();
        }

        public string[] usingNamespaces = null;
        public Dictionary<string, VariableInfo> variables = new Dictionary<string, VariableInfo>();
        public StringBuilder stringBuilder = new StringBuilder();
        public Assembly[] assemblies = null;
        public bool includePrivate = false;
    }

    internal class StrToken
    {
        public StrToken(string str, int pos)
        {
            this.str = str;
            this.pos = pos;
            len = str.Length;
        }
        public StrToken(StrToken other)
        {
            if (other != null)
            {
                str = other.str;
                pos = other.pos;
                len = other.len;
            }
        }

        public string str = string.Empty;
        public int pos;
        public int len;
    }

    internal sealed class CmdToken : StrToken
    {
        public CmdToken(StrToken token, CmdTokenType tokenType) : base(token)
        {
            this.tokenType = tokenType;
        }

        public CmdTokenType tokenType;
        public List<CmdExpression> args;

        public void InterpretArguments(Context context)
        {
            if (args != null)
            {
                foreach (var arg in args)
                {
                    if (!arg.Interpret(context))
                    {
                        throw arg.exception;
                    }
                }
            }
        }

        public void AddArg(CmdExpression arg)
        {
            if (args == null) args = new List<CmdExpression>();
            args.Add(arg);
        }
        public int argCount => args == null ? 0 : args.Count;

        public object[] EvaluateArgs(Context context)
        {
            object[] result = null;
            int count = argCount;
            if (count > 0)
            {
                result = new object[count];
                for (int i = 0; i < count; i++)
                {
                    var expr = args[i];
                    if (!expr.Execute(context, out result[i]))
                    {
                        throw expr.exception;
                    }
                }
            }
            return result;
        }
        public object[] EvaluateArgs(Context context, MethodBase method)
        {
            object[] result = null;
            int count = argCount;
            if (count > 0)
            {
                ParameterInfo[] parameters = method.GetParameters();
                int actualParamCount = parameters.Length;
                int lastParamIndex = actualParamCount - 1;
                ParameterInfo param = parameters[lastParamIndex];
                if (param.GetCustomAttribute<ParamArrayAttribute>(true) != null)
                {
                    Type paramType = param.ParameterType.GetElementType();
                    int optionalParamCount = count - lastParamIndex;
                    Array optional = Activator.CreateInstance(param.ParameterType, optionalParamCount) as Array;
                    for (int i = 0; i < optionalParamCount; i++)
                    {
                        var expr = args[lastParamIndex + i];
                        if (expr.Execute(context, out object argResult))
                        {
                            optional.SetValue(ReflectionUtility.Convert(paramType, argResult), i);
                        }
                        else
                        {
                            throw expr.exception;
                        }
                    }

                    result = new object[actualParamCount];
                    result[lastParamIndex] = optional;
                    actualParamCount--;
                }
                else
                {
                    result = new object[actualParamCount];
                }

                for (int i = 0; i < actualParamCount; i++)
                {
                    var expr = args[i];
                    if (expr.Execute(context, out object argResult))
                    {
                        result[i] = ReflectionUtility.Convert(parameters[i].ParameterType, argResult);
                    }
                    else
                    {
                        throw expr.exception;
                    }
                }
            }
            return result;
        }

        public object obj = null;

        public bool isStatic => obj is Type;
        public bool isDereferenceable
        {
            get
            {
                if (obj == null)
                {
                    return false;
                }

                if (obj is MethodInfo methodInfo)
                {
                    return methodInfo.ReturnType != typeof(void);
                }
                else if (obj is GenericTypeMethodInfo genericMethodInfo)
                {
                    return genericMethodInfo.ReturnType != typeof(void);
                }
                else if (obj is DelegateInfo delegateInfo)
                {
                    return delegateInfo.ReturnType != typeof(void);
                }

                return true;
            }
        }
        public Type resultType
        {
            get
            {
                if (obj == null)
                {
                    throw new ConsoleException(Errors.NullRef);
                }

                if (obj is Type type)
                {
                    return type;
                }
                else if (obj is FieldInfo fieldInfo)
                {
                    return fieldInfo.FieldType;
                }
                else if (obj is MethodInfo methodInfo)
                {
                    if (methodInfo.ReturnType == typeof(void))
                    {
                        throw new ConsoleException(Errors.VoidRef);
                    }
                    return methodInfo.ReturnType;
                }
                else if (obj is ConstructorInfo constructorInfo)
                {
                    return constructorInfo.DeclaringType;
                }
                else if (obj is GenericTypeMethodInfo genericMethodInfo)
                {
                    if (genericMethodInfo.ReturnType == typeof(void))
                    {
                        throw new ConsoleException(Errors.VoidRef);
                    }
                    return genericMethodInfo.ReturnType;
                }
                else if (obj is DelegateInfo delegateInfo)
                {
                    if (delegateInfo.ReturnType == typeof(void))
                    {
                        throw new ConsoleException(Errors.VoidRef);
                    }
                    return delegateInfo.ReturnType;
                }
                else if (obj is AssignmentInfo)
                {
                    return typeof(void);
                }
                else if (obj is VariableInfo variableInfo)
                {
                    return variableInfo.variableType;
                }
                else
                {
                    return obj.GetType();
                }
            }
        }

        public bool CanConvert(Type requiredType)
        {
            if (obj == null)
            {
                return !requiredType.IsValueType;
            }
            else
            {
                if (isStatic && requiredType is Type)
                {
                    return true;
                }

                if (ReflectionUtility.CanConvert(requiredType, resultType))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNativeLiteral()
        {
            if (tokenType == CmdTokenType.Declaration && ReflectionUtility.IsNativeLiteral(str, out object nativeLiteral))
            {
                obj = nativeLiteral;
                return true;
            }

            return false;
        }

        public bool TryGetField(CmdToken prev, bool includePrivate)
        {
            Type type = prev.resultType;

            BindingFlags bindingFlags = ReflectionUtility.GetBindingFlags(prev.isStatic, includePrivate);

            SearchBaseType:

            foreach (FieldInfo field in type.GetFields(bindingFlags))
            {
                if (field.DeclaringType == type && field.Name == str)
                {
                    obj = field;
                    return true;
                }
            }
            foreach (PropertyInfo property in type.GetProperties(bindingFlags))
            {
                if (property.DeclaringType == type && property.Name == str && property.GetMethod != null && property.GetMethod.GetParameters().Length == 0)
                {
                    obj = property.GetMethod;
                    return true;
                }
            }

            if (type.BaseType != null)
            {
                type = type.BaseType;
                goto SearchBaseType;
            }

            return false;
        }
        public bool TrySetField(bool includePrivate)
        {
            if (argCount != 2) return false;

            var target = args[0];
            var assign = args[1];

            Type type = target.result.resultType;
            Type assignType = assign.result.obj == null ? null : assign.result.resultType;

            BindingFlags bindingFlags = ReflectionUtility.GetBindingFlags(target.result.isStatic, includePrivate);

            bool fieldWasFound = false;

            SearchBaseType:

            foreach (FieldInfo field in type.GetFields(bindingFlags))
            {
                if (field.DeclaringType == type && field.Name == str && !field.IsInitOnly)
                {
                    fieldWasFound = true;

                    if (ReflectionUtility.CanConvert(field.FieldType, assignType))
                    {
                        obj = new AssignmentInfo(str, fieldInfo: field);
                        return true;
                    }
                }
            }
            foreach (PropertyInfo property in type.GetProperties(bindingFlags))
            {
                if (property.DeclaringType == type && property.Name == str && property.SetMethod != null && property.SetMethod.GetParameters().Length == 1 && (property.SetMethod.IsPublic || includePrivate))
                {
                    fieldWasFound = true;

                    ParameterInfo parameter = property.SetMethod.GetParameters()[0];
                    if (ReflectionUtility.CanConvert(parameter.ParameterType, assignType))
                    {
                        obj = new AssignmentInfo(str, methodInfo: property.SetMethod);
                        return true;
                    }
                }
            }

            if (type.BaseType != null)
            {
                type = type.BaseType;
                goto SearchBaseType;
            }

            if (fieldWasFound)
            {
                throw new ConsoleException($"Cannot assign type '{assignType}' to field '{str}' in object '{type}'");
            }

            return false;
        }
        public bool TryGetMethod(Type type, bool isStatic, bool includePrivate)
        {
            MethodInfo result = null;

            BindingFlags bindingFlags = ReflectionUtility.GetBindingFlags(isStatic, includePrivate);

            SearchBaseType:

            foreach (MethodInfo method in type.GetMethods(bindingFlags))
            {
                if (method.DeclaringType == type && !method.IsSpecialName && !method.ContainsGenericParameters && method.Name == str && CanInvoke(method))
                {
                    result = method;
                    goto ResultFound;
                }
            }

            if (type.BaseType != null)
            {
                type = type.BaseType;
                goto SearchBaseType;
            }

            ResultFound:

            if (result != null)
            {
                GenericTypeMethodAttribute genericMethod = result.GetCustomAttribute<GenericTypeMethodAttribute>();
                if (genericMethod != null && genericMethod.ArgIndex >= 0 && genericMethod.ArgIndex < argCount)
                {
                    Type genericType = args[genericMethod.ArgIndex].result.obj as Type;
                    obj = new GenericTypeMethodInfo(result, genericType);
                }
                else
                {
                    obj = result;
                }

                return true;
            }
            else
            {
                return false;
            }
        }
        public bool TryGetMethod(CmdToken prev, bool includePrivate)
        {
            return TryGetMethod(prev.resultType, prev.isStatic, includePrivate);
        }
        public bool TryGetDelegate(CmdToken prev, bool includePrivate)
        {
            if (TryGetField(prev, includePrivate))
            {
                FieldInfo fieldInfo = obj as FieldInfo;
                var fieldType = fieldInfo.FieldType;
                if (typeof(Delegate).IsAssignableFrom(fieldType))
                {
                    MethodInfo invoke = fieldType.GetMethod("Invoke", ReflectionUtility.InstanceFlags);
                    if (CanInvoke(invoke))
                    {
                        obj = new DelegateInfo(fieldInfo, invoke);
                        return true;
                    }
                }
            }

            return false;
        }
        public bool TryGetSubscript(CmdToken prev, bool includePrivate)
        {
            if (!prev.isStatic && argCount > 0)
            {
                Type type = prev.resultType;

                if (type == typeof(Array) || type.IsSubclassOf(typeof(Array)))
                {
                    foreach (MethodInfo method in type.GetMethods(ReflectionUtility.InstanceFlags))
                    {
                        if (method.Name == "GetValue" && CanInvoke(method))
                        {
                            obj = method;
                            return true;
                        }
                    }
                }

                BindingFlags bindingFlags = ReflectionUtility.GetInstanceFlags(includePrivate);

                SearchBaseType:

                foreach (PropertyInfo property in type.GetProperties(bindingFlags))
                {
                    if (property.DeclaringType == type && property.GetMethod != null && CanInvoke(property.GetMethod))
                    {
                        obj = property.GetMethod;
                        return true;
                    }
                }

                if (type.BaseType != null)
                {
                    type = type.BaseType;
                    goto SearchBaseType;
                }
            }
            return false;
        }
        public bool TrySetSubscript(bool includePrivate)
        {
            if (argCount < 3) return false;

            var target = args[0];

            CmdExpression[] subArgs = new CmdExpression[argCount - 1];
            for (int i = 0; i < subArgs.Length; i++)
            {
                subArgs[i] = args[i + 1];
            }

            Type type = target.result.resultType;

            BindingFlags bindingFlags = ReflectionUtility.GetInstanceFlags(includePrivate);

            if (type == typeof(Array) || type.IsSubclassOf(typeof(Array)))
            {
                foreach (MethodInfo method in type.GetMethods(ReflectionUtility.InstanceFlags))
                {
                    if (method.Name == "SetValue" && CanInvoke(method, subArgs, MatchParameterCount.Equal))
                    {
                        obj = new AssignmentInfo(str, methodInfo: method);
                        return true;
                    }
                }
            }

            // Set properties have the indices come first
            for (int i = 0; i < subArgs.Length; i++)
            {
                int j = (i + 1) % subArgs.Length;
                subArgs[i] = args[j + 1];
            }

            SearchBaseType:

            foreach (PropertyInfo property in type.GetProperties(bindingFlags))
            {
                if (property.DeclaringType == type && property.SetMethod != null && CanInvoke(property.SetMethod, subArgs, MatchParameterCount.Equal))
                {
                    obj = new AssignmentInfo(str, methodInfo: property.SetMethod, shiftArgs: true);
                    return true;
                }
            }

            if (type.BaseType != null)
            {
                type = type.BaseType;
                goto SearchBaseType;
            }

            return false;
        }
        public bool TryGetConstructor(bool includePrivate)
        {
            if (obj is Type type)
            {
                if (type.IsValueType && argCount == 0)
                {
                    obj = new DefaultConstructorInfo(type);
                    return true;
                }

                foreach (ConstructorInfo constructor in type.GetConstructors(ReflectionUtility.GetInstanceFlags(includePrivate)))
                {
                    if (CanInvoke(constructor))
                    {
                        obj = constructor;
                        return true;
                    }
                }
            }

            return false;
        }
        private static bool CanInvoke(MethodBase method, IList<CmdExpression> args, MatchParameterCount matchParameterCount = MatchParameterCount.Equal)
        {
            ParameterInfo[] parameters = method.GetParameters();

            int count = args == null ? 0 : args.Count;

            // Methods that require no parameters are always invokable when none are provided
            if (count == 0 && parameters.Length == 0) return true;

            if (parameters.Length > 0)
            {
                int actualParamCount = parameters.Length - 1;
                ParameterInfo parameter = parameters[actualParamCount];
                if (parameter.GetCustomAttribute<ParamArrayAttribute>(true) != null)
                {
                    if (count >= actualParamCount)
                    {
                        for (int i = 0; i < actualParamCount; i++)
                        {
                            if (!args[i].result.CanConvert(parameters[i].ParameterType))
                            {
                                return false;
                            }
                        }

                        Type paramType = parameter.ParameterType.GetElementType();
                        int optionalParamCount = count - actualParamCount;
                        for (int i = 0; i < optionalParamCount; i++)
                        {
                            if (!args[actualParamCount + i].result.CanConvert(paramType))
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }
            }

            switch (matchParameterCount)
            {
                case MatchParameterCount.Equal:
                    if (count == parameters.Length) goto CheckParameters;
                    goto default;

                case MatchParameterCount.LEqual:
                    if (count <= parameters.Length) goto CheckParameters;
                    goto default;

                case MatchParameterCount.LessThan:
                    if (count < parameters.Length) goto CheckParameters;
                    goto default;

                default:
                    return false;
            }

            CheckParameters:
            for (int i = 0; i < count; i++)
            {
                if (!args[i].result.CanConvert(parameters[i].ParameterType))
                {
                    return false;
                }
            }

            return true;
        }
        public bool CanInvoke(MethodBase method, MatchParameterCount matchParameterCount = MatchParameterCount.Equal)
        {
            return CanInvoke(method, args, matchParameterCount);
        }
        public bool TryGetNestedType(CmdToken prev, bool includePrivate)
        {
            if (prev.isStatic)
            {
                BindingFlags bindingFlags = ReflectionUtility.GetStaticFlags(includePrivate);

                Type type = prev.resultType;

                SearchBaseType:

                string nestedName = $"{type.FullName}+{str}";
                foreach (var nestedType in type.GetNestedTypes(bindingFlags))
                {
                    if (nestedType.FullName == nestedName && !nestedType.ContainsGenericParameters)
                    {
                        obj = nestedType;
                        return true;
                    }
                }

                if (type.BaseType != null)
                {
                    type = type.BaseType;
                    goto SearchBaseType;
                }
            }

            return false;
        }
        public bool TryCreateArrayType(CmdToken prev)
        {
            if (argCount == 0)
            {
                prev.obj = (prev.obj as Type).MakeArrayType();
                return true;
            }
            else
            {
                var makeArray = typeof(CmdToken).GetMethod("MakeArray", ReflectionUtility.StaticFlags);
                CmdToken copy = new CmdToken(prev, prev.tokenType);
                copy.obj = prev.obj;
                CmdExpression arg = new CmdExpression();
                arg.tokens.Add(copy);
                args.Insert(0, arg);
                if (!CanInvoke(makeArray))
                {
                    throw new ConsoleException($"Failed to initialize array of type '{prev.resultType}'");
                }
                prev.args = args;
                prev.obj = makeArray;
                return true;
            }
        }

        public static Array MakeArray(Type type, params int[] lengths)
        {
            return Array.CreateInstance(type, lengths);
        }
    }

    internal sealed class CmdExpression
    {
        public CmdExpression()
        {

        }

        public readonly List<CmdToken> tokens = new List<CmdToken>();
        public CmdToken result => tokens.Last();
        private bool isInterpreted = false;
        public Exception exception { get; private set; }

        public bool Interpret(Context context)
        {
            if (isInterpreted)
            {
                // Only interpret once
                return exception == null;
            }
            isInterpreted = true;

            try
            {
                int idx = 0;
                var first = tokens[0];
                if (first.TryGetNativeLiteral())
                {
                    idx++;
                }
                else if (first.tokenType == CmdTokenType.Assignment)
                {
                    // Build assignment expressions
                    first.InterpretArguments(context);
                    int argCount = first.argCount;
                    if (argCount == 1)
                    {
                        // Variable assignment
                        var result = first.args.Last().result;
                        var resultType = result.obj == null ? typeof(object) : result.obj is Type ? typeof(Type) : result.resultType;
                        if (!context.variables.TryGetValue(first.str, out VariableInfo variableInfo))
                        {
                            variableInfo = new VariableInfo(first.str, resultType);
                            context.variables.Add(first.str, variableInfo);
                        }
                        else
                        {
                            variableInfo.variableType = resultType;
                        }
                        first.obj = new AssignmentInfo(first.str);
                    }
                    else if (argCount == 2)
                    {
                        // Property assignment
                        if (!first.TrySetField(context.includePrivate))
                        {
                            throw new ConsoleException($"Failed to find matching field '{first.str}' in type '{first.args[0].result.resultType}'");
                        }
                    }
                    else if (argCount > 2)
                    {
                        // Subscript assignment
                        if (!first.TrySetSubscript(context.includePrivate))
                        {
                            throw new ConsoleException($"Failed to find subscript operator for field '{first.args[0].result.str}'");
                        }
                    }
                    else
                    {
                        throw new ConsoleException("Invalid assignment");
                    }
                    idx++;
                }
                else if (first.tokenType == CmdTokenType.Variable)
                {
                    // Try load variable
                    if (!context.variables.TryGetValue(first.str, out VariableInfo variableInfo))
                    {
                        throw new ConsoleException($"Unknown variable '{first.str}'");
                    }
                    first.obj = variableInfo;
                    idx++;
                }
                else
                {
                    int count = 0;
                    for (; count < tokens.Count; count++)
                    {
                        // find first not dereference
                        var token = tokens[count];
                        if (token.tokenType == CmdTokenType.Invoke)
                        {
                            count++;
                            break;
                        }
                        if (token.tokenType != CmdTokenType.Dereference)
                            break;
                    }
                    if (count > 0)
                    {
                        if (ReflectionUtility.IsNativeType(first.str, out Type type))
                        {
                            goto TryConstruct;
                        }
                        else
                        {
                            // Call Environment globals
                            if (first.tokenType == CmdTokenType.Invoke)
                            {
                                first.InterpretArguments(context);
                                if (first.TryGetMethod(typeof(Environment), true, false))
                                {
                                    idx = 1;
                                    goto Continue;
                                }
                            }

                            // Attempt find type, working upwards through all the namespaces
                            string rootName = string.Empty;
                            List<string> queries = new List<string>();
                            context.stringBuilder.Clear();
                            for (int i = 0; i < count; i++)
                            {
                                var token = tokens[idx];
                                if (context.stringBuilder.Length != 0) context.stringBuilder.Append(".");
                                context.stringBuilder.Append(token.str);
                                rootName = context.stringBuilder.ToString();

                                queries.Clear();
                                queries.Add(rootName);
                                foreach (var ns in context.usingNamespaces)
                                {
                                    queries.Add($"{ns}.{rootName}");
                                }

                                foreach (Assembly assembly in context.assemblies)
                                {
                                    type = assembly.TryGetType(queries);
                                    if (type != null && !type.ContainsGenericParameters && !type.IsNested && ReflectionUtility.CanExecute(type) && (type.IsPublic || context.includePrivate))
                                    {
                                        goto TryConstruct;
                                    }
                                }
                                idx++;
                            }

                            throw new ConsoleException($"Failed to resolve root type '{rootName}'");
                        }

                        TryConstruct:
                        {
                            // Attempt construct object
                            var token = tokens[idx++];
                            token.obj = type;

                            if (token.tokenType == CmdTokenType.Invoke)
                            {
                                token.InterpretArguments(context);
                                if (token.TryGetConstructor(context.includePrivate))
                                {
                                    goto Continue;
                                }
                                throw new ConsoleException($"Failed to find matching constructor for type '{type}'");
                            }

                            // Attempt construct array
                            CmdToken prev = token;
                            if (prev.isStatic && idx < tokens.Count)
                            {
                                token = tokens[idx];
                                if (prev.isStatic && token.tokenType == CmdTokenType.Subscript)
                                {
                                    token.InterpretArguments(context);
                                    if (token.TryCreateArrayType(prev))
                                    {
                                        tokens.RemoveAt(idx);
                                    }
                                }
                            }
                        }
                    }
                }

                if (idx == 0)
                {
                    throw new ConsoleException($"Invalid expression: '{tokens[idx].str}'");
                }

                Continue:
                {
                    CmdToken prev = tokens[idx - 1];
                    for (; idx < tokens.Count; idx++)
                    {
                        var current = tokens[idx];
                        current.InterpretArguments(context);

                        switch (current.tokenType)
                        {
                            case CmdTokenType.Dereference:
                            {
                                // Get field
                                if (current.TryGetField(prev, context.includePrivate))
                                {
                                    break;
                                }

                                // Get nested type
                                if (current.TryGetNestedType(prev, context.includePrivate))
                                {
                                    break;
                                }

                                throw new ConsoleException($"Failed to find matching field '{current.str}' in type '{prev.resultType}'");
                            }

                            case CmdTokenType.Invoke:
                            {
                                // Get method
                                if (current.TryGetMethod(prev, context.includePrivate))
                                {
                                    break;
                                }

                                // Construct nested type
                                if (current.TryGetNestedType(prev, context.includePrivate))
                                {
                                    if (current.TryGetConstructor(context.includePrivate))
                                    {
                                        break;
                                    }
                                }

                                if (current.TryGetDelegate(prev, context.includePrivate))
                                {
                                    break;
                                }

                                throw new ConsoleException($"Failed to find matching method '{current.str}' in type '{prev.resultType}'");
                            }

                            case CmdTokenType.Subscript:
                            {
                                if (prev.isStatic)
                                {
                                    // Array of nested type
                                    if (current.TryCreateArrayType(prev))
                                    {
                                        tokens.RemoveAt(idx);
                                        idx--;
                                        goto SkipPrev;
                                    }
                                }
                                else
                                {
                                    // Subscript
                                    if (current.TryGetSubscript(prev, context.includePrivate))
                                    {
                                        break;
                                    }
                                }

                                throw new ConsoleException($"Failed to find matching subscript operator for type '{prev.resultType}'");
                            }

                            default:
                            {
                                throw new ConsoleException($"Unexpected expression: '{current.str}'");
                            }
                        }

                        prev = current;

                        SkipPrev: continue;
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }

            exception = null;
            return true;
        }
        public bool Execute(Context context, out object result)
        {
            result = typeof(void);

            try
            {
                // Execute each node and store the result
                foreach (var token in tokens)
                {
                    // Dont allow execution on console related objects
                    if (DebugConsole.instance)
                    {
                        if (result != null && (ReferenceEquals(result, DebugConsole.instance) || ReferenceEquals(result, DebugConsole.instance.gameObject)))
                        {
                            result = null;
                        }
                    }

                    if (token.isStatic)
                    {
                        result = token.obj;
                    }
                    else if (token.obj is FieldInfo fieldInfo)
                    {
                        if (!fieldInfo.IsStatic && result == null)
                        {
                            // Null not valid here
                            throw new ConsoleException($"{Errors.NullRef} of type '{fieldInfo.DeclaringType}'");
                        }
                        result = fieldInfo.GetValue(result);
                    }
                    else if (token.obj is MethodInfo methodInfo)
                    {
                        if (!methodInfo.IsStatic && result == null)
                        {
                            // Null not valid here
                            throw new ConsoleException($"{Errors.NullRef} of type '{methodInfo.DeclaringType}'");
                        }
                        result = methodInfo.Invoke(result, token.EvaluateArgs(context, methodInfo));
                        if (methodInfo.ReturnType == typeof(void)) result = typeof(void);
                    }
                    else if (token.obj is ConstructorInfo constructorInfo)
                    {
                        result = Activator.CreateInstance(constructorInfo.DeclaringType, token.EvaluateArgs(context, constructorInfo));
                    }
                    else if (token.obj is GenericTypeMethodInfo genericMethodInfo)
                    {
                        if (!genericMethodInfo.MethodInfo.IsStatic && result == null)
                        {
                            // Null not valid here
                            throw new ConsoleException($"{Errors.NullRef} of type '{genericMethodInfo.MethodInfo.DeclaringType}'");
                        }
                        result = genericMethodInfo.MethodInfo.Invoke(result, token.EvaluateArgs(context, genericMethodInfo.MethodInfo));
                        if (genericMethodInfo.ReturnType == typeof(void)) result = typeof(void);
                    }
                    else if (token.obj is DelegateInfo delegateInfo)
                    {
                        if (!delegateInfo.FieldInfo.IsStatic && result == null)
                        {
                            // Null not valid here
                            throw new ConsoleException($"{Errors.NullRef} of type '{delegateInfo.FieldInfo.DeclaringType}'");
                        }
                        result = delegateInfo.FieldInfo.GetValue(result);
                        if (result == null)
                        {
                            // Null delegate
                            throw new ConsoleException($"{Errors.NullRef} of type '{delegateInfo.FieldInfo.FieldType}'");
                        }
                        result = delegateInfo.MethodInfo.Invoke(result, token.EvaluateArgs(context, delegateInfo.MethodInfo));
                        if (delegateInfo.ReturnType == typeof(void)) result = typeof(void);
                    }
                    else if (token.obj is AssignmentInfo assignmentInfo)
                    {
                        object[] args = token.EvaluateArgs(context);
                        int argCount = token.argCount;
                        if (argCount == 1)
                        {
                            // Variable assignment
                            context.variables[assignmentInfo.FieldName].variableValue = args[0];
                        }
                        else if (argCount == 2)
                        {
                            if (assignmentInfo.FieldInfo != null)
                            {
                                assignmentInfo.FieldInfo.SetValue(args[0], args[1]);
                            }
                            else
                            {
                                assignmentInfo.MethodInfo.Invoke(args[0], new object[] { args[1] });
                            }
                        }
                        else if (argCount > 2)
                        {
                            object[] subArgs = new object[argCount - 1];
                            if (assignmentInfo.ShiftArgs)
                            {
                                // Set properties have the indices come first
                                for (int i = 0; i < subArgs.Length; i++)
                                {
                                    int j = (i + 1) % subArgs.Length;
                                    subArgs[i] = args[j + 1];
                                }
                            }
                            else
                            {
                                for (int i = 0; i < subArgs.Length; i++)
                                {
                                    subArgs[i] = args[i + 1];
                                }
                            }

                            assignmentInfo.MethodInfo.Invoke(args[0], subArgs);
                        }
                        else
                        {
                            throw new ConsoleException("Invalid assignment");
                        }

                        result = typeof(void);
                    }
                    else if (token.obj is VariableInfo variableInfo)
                    {
                        result = variableInfo.variableValue;
                    }
                    else
                    {
                        result = token.obj;
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }

            exception = null;
            return true;
        }
    }

    internal sealed class Command
    {
        public bool Parse(string cmd)
        {
            try
            {
                exception = null;

                tokenStrings.Clear();
                tokenBuilder.Clear();
                expressions.Clear();
                stack.Clear();
                exec.Clear();
                context.Clear();

                if (string.IsNullOrEmpty(cmd))
                {
                    throw new ConsoleException("Invalid command string");
                }

                char p = char.MinValue;
                int lastIdx = cmd.Length;
                StringMode stringMode = StringMode.None;
                StrToken strToken = null;
                char unexpectedChar = p;
                for (int i = 0; i < cmd.Length + 1; i++)
                {
                    char c = i == lastIdx ? ';' : cmd[i];
                    char n = (i < cmd.Length - 1) ? cmd[i + 1] : char.MinValue;
                    if (stringMode != StringMode.None)
                    {
                        if (c == '\\')
                        {
                            // Character escapes
                            switch (n)
                            {
                                case '\\': tokenBuilder.Append('\\'); break;
                                case '"': tokenBuilder.Append('\"'); break;
                                case 'b': tokenBuilder.Append('\b'); break;
                                case 'f': tokenBuilder.Append('\f'); break;
                                case 'n': tokenBuilder.Append('\n'); break;
                                case 'r': tokenBuilder.Append('\r'); break;
                                case 't': tokenBuilder.Append('\t'); break;
                                default: goto NotEscaped;
                            }
                            i++;

                            NotEscaped:
                            escapeCount++;
                        }
                        else
                        {
                            // End of string
                            PushCharacter(ref strToken, c, i);
                            if (c == (stringMode == StringMode.String ? '"' : '\''))
                            {
                                stringMode = StringMode.None;
                                PushStrToken(ref strToken);
                            }
                        }
                    }
                    else if (c == '=')
                    {
                        PushStrToken(ref strToken);

                        if (tokenStrings.Count > 0 && tokenStrings.Last().str != ".")
                        {
                            PushCharacter(ref strToken, c, i);
                            PushStrToken(ref strToken);
                        }
                        else
                        {
                            throw new ConsoleException($"Unexpected '{c}' character");
                        }
                    }
                    else if (char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '-')
                    {
                        // Letters
                        if (tokenBuilder.Length == 0)
                        {
                            if (tokenStrings.Count > 0)
                            {
                                string last = tokenStrings.Last().str;
                                if (last == "]" || last == ")")
                                {
                                    throw new ConsoleException($"Unexpected end of expression");
                                }
                            }

                            // Unsupported character
                            if (!char.IsLetter(c) && !char.IsNumber(c) && c != '_' && c != '$' && (c != '-' || !char.IsNumber(n)))
                            {
                                throw new ConsoleException($"Unexpected '{c}' character");
                            }
                        }
                        else
                        {
                            // Whitespace between identifiers
                            if (char.IsWhiteSpace(p))
                            {
                                throw new ConsoleException($"Unexpected whitespace character");
                            }

                            // Reserved characters not at start of identifiers
                            if (c == '$' || c == '-')
                            {
                                throw new ConsoleException($"Unexpected '{c}' character");
                            }
                        }

                        // Push
                        PushCharacter(ref strToken, c, i);
                    }
                    else
                    {
                        if (c == '"' || c == '\'')
                        {
                            // Begin of string
                            PushStrToken(ref strToken);
                            PushCharacter(ref strToken, c, i);
                            stringMode = c == '"' ? StringMode.String : StringMode.Char;
                        }
                        else if ((c == '.' && !char.IsNumber(n)) || c == ',' || c == ';' || c == '(' || c == '[' || c == ')' || c == ']')
                        {
                            // Symbols
                            PushStrToken(ref strToken);

                            if (!(tokenStrings.Count > 0 && tokenStrings.Last().str == ";" && i == lastIdx))
                            {
                                if (tokenStrings.Count > 0)
                                {
                                    string last = tokenStrings.Last().str;
                                    if (!(IsDeclaration(last) || IsIdentifier(last) || last == ")" || last == "]" || (c == ')' && last == "(") || (c == ']' && last == "[")))
                                    {
                                        if (unexpectedChar == char.MinValue)
                                            unexpectedChar = c;
                                        if (c == ',')
                                        {
                                            throw new ConsoleException($"Unexpected '{c}' character");
                                        }
                                    }
                                }
                                else
                                {
                                    throw new ConsoleException($"Unexpected '{c}' character");
                                }

                                PushCharacter(ref strToken, c, i);
                                PushStrToken(ref strToken);
                            }
                        }
                        else if (c == '.' && char.IsNumber(n))
                        {
                            // Floating point
                            PushCharacter(ref strToken, c, i);
                        }
                        else if (!char.IsWhiteSpace(c))
                        {
                            throw new ConsoleException($"Unexpected '{c}' character");
                        }
                    }
                    p = c;
                }
                if (stringMode != StringMode.None)
                {
                    throw new ConsoleException($"Unterminated string");
                }

                expressions.Add(new CmdExpression());
                stack.Add(0);
                exec.Add(0);

                CmdToken cmdToken = null;
                strToken = null;
                lastIdx = tokenStrings.Count - 2;
                for (int i = 0; i < tokenStrings.Count; i++)
                {
                    strToken = tokenStrings[i];
                    CmdExpression expr = expressions[stack.Last()];
                    bool isLast = i >= lastIdx;

                    if (strToken.str.Length == 1)
                    {
                        char c = strToken.str[0];
                        bool isToken = false;
                        if (c == ';')
                        {
                            PushCmdToken(ref cmdToken);

                            if (!isLast)
                            {
                                exec.Add(expressions.Count);
                                stack[stack.Count - 1] = expressions.Count;
                                expressions.Add(new CmdExpression());
                            }

                            continue;
                        }
                        else if (c == '.')
                        {
                            PushCmdToken(ref cmdToken);

                            continue;
                        }
                        else if (c == ',')
                        {
                            CmdToken parent = GetParentCmdToken();
                            if (parent != null && (cmdToken != null || expr.tokens.Count > 0))
                            {
                                PushCmdToken(ref cmdToken);

                                parent.AddArg(expressions[stack.Last()]);

                                if (!isLast)
                                {
                                    stack[stack.Count - 1] = expressions.Count;
                                    expressions.Add(new CmdExpression());
                                }

                                continue;
                            }
                        }
                        else if (c == '(' || c == '[')
                        {
                            CmdTokenType type = c == '(' ? CmdTokenType.Invoke : CmdTokenType.Subscript;

                            if (type != CmdTokenType.Subscript && cmdToken != null && cmdToken.tokenType == CmdTokenType.Dereference)
                            {
                                cmdToken.tokenType = type;
                            }
                            else
                            {
                                PushCmdToken(ref cmdToken);

                                cmdToken = new CmdToken(null, type);
                            }

                            expr.tokens.Add(cmdToken);
                            cmdToken = null;

                            if (!isLast)
                            {
                                stack.Add(expressions.Count);
                                expressions.Add(new CmdExpression());
                            }

                            continue;
                        }
                        else if (c == ')' || c == ']')
                        {
                            if (stack.Count >= 1)
                            {
                                CmdTokenType type = c == ')' ? CmdTokenType.Invoke : CmdTokenType.Subscript;

                                CmdToken parent = GetParentCmdToken();
                                if (parent != null && parent.tokenType == type)
                                {
                                    parent.len = (strToken.pos - parent.pos) + 1;
                                    PushCmdToken(ref cmdToken);

                                    if (expr.tokens.Count > 0)
                                    {
                                        parent.AddArg(expressions[stack.Last()]);
                                    }

                                    stack.RemoveAt(stack.Count - 1);

                                    continue;
                                }
                            }
                        }
                        else if (c == '=')
                        {
                            if (stack.Count <= 1)
                            {
                                PushCmdToken(ref cmdToken);
                                cmdToken = new CmdToken(strToken, CmdTokenType.Assignment);
                                PushCmdToken(ref cmdToken);
                                continue;
                            }
                            else
                            {
                                throw new ConsoleException($"Assignment can only occur in root expression");
                            }
                        }
                        else
                        {
                            isToken = true;
                        }

                        if (!isToken)
                        {
                            throw new ConsoleException($"Unexpected '{c}' character");
                        }
                    }

                    if (cmdToken != null)
                    {
                        throw new ConsoleException($"Unexpected token '{strToken.str}'");
                    }
                    cmdToken = new CmdToken(strToken, IdentifyTokenType(strToken.str));
                }

                // Scan for assignment tokens
                for (int idx = 0; idx < expressions.Count; idx++)
                {
                    var expr = expressions[idx];
                    int count = 0;
                    for (int i = 0; i < expr.tokens.Count; i++)
                    {
                        if (expr.tokens[i].tokenType == CmdTokenType.Assignment)
                        {
                            if (count > 0)
                            {
                                throw new ConsoleException($"Assignment can only occur once in an expression");
                            }
                            count++;
                        }
                    }

                    if (count == 0) continue;

                    for (int i = 0; i < expr.tokens.Count;)
                    {
                        if (expr.tokens[i].tokenType == CmdTokenType.Assignment)
                        {
                            if (i >= 1 && i < expr.tokens.Count - 1)
                            {
                                var prev = expr.tokens[i - 1];
                                var next = expr.tokens[i + 1];
                                bool isDeref = prev.tokenType == CmdTokenType.Dereference;
                                bool isVar = prev.tokenType == CmdTokenType.Variable;
                                bool isSubscript = prev.tokenType == CmdTokenType.Subscript;
                                if ((isDeref || isVar || isSubscript) && next.tokenType != CmdTokenType.Subscript)
                                {
                                    CmdToken set = new CmdToken(new StrToken(prev.str, prev.pos), CmdTokenType.Assignment);
                                    set.args = new List<CmdExpression>();

                                    if (!isVar)
                                    {
                                        CmdExpression lhs = new CmdExpression();
                                        for (int j = 0; j < i - 1; j++)
                                            lhs.tokens.Add(expr.tokens[j]);
                                        set.args.Add(lhs);
                                        expressions.Add(lhs);
                                    }

                                    CmdExpression rhs = new CmdExpression();
                                    for (int j = i + 1; j < expr.tokens.Count; j++)
                                        rhs.tokens.Add(expr.tokens[j]);
                                    set.args.Add(rhs);
                                    expressions.Add(rhs);

                                    if (isSubscript)
                                    {
                                        // Copy subscript parameters
                                        if (prev.argCount == 0) break;
                                        set.args.AddRange(prev.args);
                                    }

                                    expr.tokens.Clear();
                                    expr.tokens.Add(set);

                                    continue;
                                }
                            }

                            throw new ConsoleException($"Unexpected token '='");
                        }
                        i++;
                    }
                }

                // We throw this down here to allow as many tokens as possible to be identified (for suggestion building later on)
                if (cmdToken != null || stack.Count != 1 || unexpectedChar == ';')
                {
                    throw new ConsoleException($"Unexpected end of expression");
                }
                if (unexpectedChar != char.MinValue)
                {
                    throw new ConsoleException($"Unexpected '{unexpectedChar}' character");
                }

                // Prune empty expressions
                for (int i = 0; i < exec.Count;)
                {
                    if (expressions[exec[i]].tokens.Count == 0)
                    {
                        exec.RemoveAt(i);
                        continue;
                    }
                    i++;
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }

            exception = null;
            return true;
        }
        public bool Interpret(Assembly[] assemblies, string[] usingNamespaces, bool safeMode, bool abortOnException = true)
        {
            if (exception == null || !abortOnException)
            {
                context.usingNamespaces = usingNamespaces;
                context.assemblies = assemblies;
                context.includePrivate = !safeMode;

                for (int pc = 0; pc < exec.Count; pc++)
                {
                    var expr = expressions[exec[pc]];
                    if (!expr.Interpret(context))
                    {
                        if (exception == null)
                        {
                            exception = expr.exception;
                        }

                        if (abortOnException)
                        {
                            return false;
                        }
                    }
                }
            }

            return exception == null;
        }
        public bool Execute(out object result)
        {
            result = typeof(void);

            if (exception == null)
            {
                for (int pc = 0; pc < exec.Count; pc++)
                {
                    var expr = expressions[exec[pc]];
                    if (!expr.Execute(context, out result))
                    {
                        exception = expr.exception;
                        return false;
                    }
                }
            }

            return exception == null;
        }

        public Exception exception { get; private set; }

        private void PushCharacter(ref StrToken token, char c, int pos)
        {
            if (token == null)
            {
                token = new StrToken(string.Empty, pos);
            }
            tokenBuilder.Append(c);
        }
        private void PushStrToken(ref StrToken token)
        {
            if (tokenBuilder.Length > 0)
            {
                token.str = tokenBuilder.ToString();
                token.len = token.str.Length + escapeCount;
                tokenStrings.Add(token);
                tokenBuilder.Clear();
                token = null;
                escapeCount = 0;
            }
        }
        private static CmdTokenType IdentifyTokenType(string token)
        {
            if (ReflectionUtility.IsNativeLiteral(token))
            {
                return CmdTokenType.Declaration;
            }
            else if (token[0] == '$')
            {
                return CmdTokenType.Variable;
            }
            else if (char.IsNumber(token[0]) || token[0] == '-' || token[0] == '"' || token[0] == '\'')
            {
                return CmdTokenType.Declaration;
            }
            return CmdTokenType.Dereference;
        }
        private void PushCmdToken(ref CmdToken token)
        {
            if (token != null)
            {
                expressions[stack.Last()].tokens.Add(token);
                token = null;
            }
        }
        private CmdToken GetParentCmdToken()
        {
            if (stack.Count >= 2)
            {
                return expressions[stack[stack.Count - 2]].tokens.Last();
            }
            return null;
        }
        private static bool IsIdentifier(string str)
        {
            if (str != null && str.Length > 0)
            {
                char c = str[0];
                return char.IsLetterOrDigit(c) || c == '_' || c == '$';
            }
            return false;
        }
        private static bool IsDeclaration(string str)
        {
            if (str != null && str.Length > 0)
            {
                char c = str[0];
                if (c == '-')
                    return str.Length > 1 && char.IsDigit(str[1]);
                else if (c == '"' || c == '\'')
                    return true;
                else
                    return char.IsDigit(c);
            }
            return false;
        }

        private readonly List<StrToken> tokenStrings = new List<StrToken>();
        private readonly StringBuilder tokenBuilder = new StringBuilder(256);
        private int escapeCount = 0;
        private readonly List<CmdExpression> expressions = new List<CmdExpression>();
        private readonly List<int> stack = new List<int>();
        private readonly List<int> exec = new List<int>();
        public readonly Context context = new Context();

        public IReadOnlyList<CmdExpression> GetExpressions() => expressions;
    }
}
#endif