// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AggroBird.Reflection
{
    internal enum Style : uint
    {
        Default,
        Class,
        Struct,
        Enum,
        Keyword,
        Number,
        String,
        Method,
        Variable,
        Error,
    }

    internal static class Styles
    {
        private static readonly uint[] HexCodes =
        {
            0xFFFFFF,
            0x4EC9B0,
            0x86C691,
            0xB8D7A3,
            0x569CD6,
            0xFF64F3,
            0xD69D85,
            0xDCDCAA,
            0x9CDCFE,
            0xFC3E36,
        };

        private static string[] MakeColorCodes()
        {
            string[] result = new string[HexCodes.Length];
            for (int i = 0; i < HexCodes.Length; i++)
            {
                result[i] = $"<color=#{HexCodes[i]:X}>";
            }
            return result;
        }
        private static readonly string[] ColorCodes = MakeColorCodes();

        public static string Open(Style style)
        {
            return ColorCodes[(int)style];
        }
        public const string Close = "</color>";

        public static Style GetTypeColor(Type type)
        {
            if (type.IsEnum || type.IsInterface || type.IsGenericParameter)
                return Style.Enum;
            else if (type.IsClass)
                return Style.Class;
            else if (type.IsValueType)
                return Style.Struct;
            else
                return Style.Default;
        }
    }

    internal abstract class Suggestion
    {
        public Suggestion(IReadOnlyList<string> usingNamespaces)
        {
            this.usingNamespaces = usingNamespaces;
        }

        public abstract string Text { get; }
        public abstract void BuildSuggestionString(StringBuilder output, bool isHighlighted);

        protected readonly IReadOnlyList<string> usingNamespaces;


        private static readonly string Null = $"{Styles.Open(Style.Keyword)}null{Styles.Close}";
        private static readonly string True = $"{Styles.Open(Style.Keyword)}true{Styles.Close}";
        private static readonly string False = $"{Styles.Open(Style.Keyword)}false{Styles.Close}";

        private static readonly string GetStr = $" {{ {Styles.Open(Style.Keyword)}get{Styles.Close}; }}";
        private static readonly string SetStr = $" {{ {Styles.Open(Style.Keyword)}set{Styles.Close}; }}";
        private static readonly string GetSetStr = $" {{ {Styles.Open(Style.Keyword)}get{Styles.Close}; {Styles.Open(Style.Keyword)}set{Styles.Close}; }}";


        protected void Stringify(Type type, object value, StringBuilder output)
        {
            if (value == null)
            {
                if (type.IsValueType)
                {
                    FormatTypeName(output, type);
                    output.Append("()");
                }
                else
                {
                    output.Append(Null);
                }
                return;
            }

            if (type.IsEnum)
            {
                FormatTypeName(output, type);
                output.Append('.');
                output.Append(value.ToString());
                return;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Char:
                    output.Append($"{Styles.Open(Style.String)}'{value}'{Styles.Close}");
                    return;
                case TypeCode.String:
                    output.Append($"{Styles.Open(Style.String)}\"{value}\"{Styles.Close}");
                    return;

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
                    output.Append($"{Styles.Open(Style.Number)}{value}{Styles.Close}");
                    return;

                case TypeCode.Boolean:
                    output.Append((bool)value ? True : False);
                    return;
            }

            output.Append(value.ToString());
        }

        protected static string Highlight(string str, int len, Style color = Style.Default)
        {
            if (len >= str.Length)
            {
                return $"{Styles.Open(color)}<b>{str}</b>{Styles.Close}";
            }
            else if (len > 0)
            {
                return $"{Styles.Open(color)}<b>{str.Substring(0, len)}</b>{str.Substring(len)}{Styles.Close}";
            }
            else
            {
                return $"{Styles.Open(color)}{str}{Styles.Close}";
            }
        }

        protected static string GetPrefix(Type type)
        {
            string result = Expression.GetPrefix(type);
            if (string.IsNullOrEmpty(result)) return result;
            return $"{Styles.Open(Style.Keyword)}{result}{Styles.Close}";
        }

        protected void FormatTypeName(StringBuilder output, Type type, bool includeNamespace = true, bool includeGenericArguments = true, int highlight = 0)
        {
            if (type.IsGenericParameter)
            {
                output.Append(Highlight(type.Name, highlight, Styles.GetTypeColor(type)));
                return;
            }

            if (TokenUtility.TryGetBaseTypeName(type, out string baseTypeName))
            {
                output.Append(Highlight(baseTypeName, highlight, Style.Keyword));
                return;
            }

            // Format array
            if (type.IsArray)
            {
                FormatTypeName(output, type.GetElementType(), highlight: highlight);
                output.Append('[');
                int rank = type.GetArrayRank();
                for (int i = 0; i < rank - 1; i++)
                {
                    output.Append(',');
                }
                output.Append(']');
                return;
            }

            if (includeNamespace)
            {
                string typeNamespace = type.Namespace;
                if (!string.IsNullOrEmpty(typeNamespace))
                {
                    if (usingNamespaces != null && usingNamespaces.Count > 0)
                    {
                        int longestNamespace = 0;
                        for (int i = 0; i < usingNamespaces.Count; i++)
                        {
                            string ns = usingNamespaces[i];
                            if (typeNamespace.StartsWith(ns) && (typeNamespace.Length == ns.Length || typeNamespace[ns.Length] == '.'))
                            {
                                longestNamespace = Math.Max(longestNamespace, ns.Length);
                            }
                        }
                        if (longestNamespace > 0)
                        {
                            if (longestNamespace != typeNamespace.Length)
                            {
                                longestNamespace++;
                                output.Append(typeNamespace, longestNamespace, typeNamespace.Length - longestNamespace);
                                output.Append('.');
                            }
                        }
                        else
                        {
                            output.Append(typeNamespace);
                            output.Append('.');
                        }
                    }
                    else
                    {
                        output.Append(typeNamespace);
                        output.Append('.');
                    }
                }
            }

            string typeName = type.Name;

            // Split generic
            if (type.IsGenericType)
            {
                int split = typeName.IndexOf('`');
                if (split != -1) typeName = typeName.Substring(0, split);
            }

            // Split nested types
            if (type.IsNested)
            {
                int split = typeName.LastIndexOf('+');
                if (split != -1) typeName = typeName.Substring(split + 1);
            }

            output.Append(Highlight(typeName, highlight, Styles.GetTypeColor(type)));

            if (includeGenericArguments && type.IsGenericType)
            {
                Type[] genericArgs = type.GetGenericArguments();
                int parentArgumentCount = type.DeclaringType == null ? 0 : type.DeclaringType.GetGenericArguments().Length;
                if (genericArgs.Length > parentArgumentCount)
                {
                    output.Append('<');
                    for (int i = parentArgumentCount; i < genericArgs.Length; i++)
                    {
                        if (i != 0) output.Append(", ");
                        FormatTypeName(output, genericArgs[i]);
                    }
                    output.Append('>');
                }
            }
        }

        protected void FormatParameters(StringBuilder output, ParameterInfo[] parameters, int currentParameterIndex)
        {
            if (currentParameterIndex >= parameters.Length) currentParameterIndex = parameters.Length - 1;
            int varArgParam = Expression.HasVariableParameterCount(parameters) ? parameters.Length - 1 : -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) output.Append(", ");
                if (i == currentParameterIndex) output.Append("<b>");
                if (i == varArgParam) output.Append($"{Styles.Open(Style.Keyword)}params{Styles.Close} ");
                FormatTypeName(output, parameters[i].ParameterType);
                output.Append($" {Styles.Open(Style.Variable)}{parameters[i].Name}{Styles.Close}");
                if (parameters[i].HasDefaultValue)
                {
                    output.Append($" = ");
                    Stringify(parameters[i].ParameterType, parameters[i].DefaultValue, output);
                }
                if (i == currentParameterIndex) output.Append("</b>");
            }
        }
        protected void FormatGenericArguments(StringBuilder output, Type generic, int currentParameterIndex)
        {
            int parentGenericArgumentCount = generic.DeclaringType == null ? 0 : generic.DeclaringType.GetGenericArguments().Length;
            Type[] genericArguments = generic.GetGenericArguments();
            if (genericArguments.Length > parentGenericArgumentCount)
            {
                output.Append('<');
                currentParameterIndex += parentGenericArgumentCount;
                if (currentParameterIndex >= genericArguments.Length) currentParameterIndex = genericArguments.Length - 1;
                for (int i = parentGenericArgumentCount; i < genericArguments.Length; i++)
                {
                    if (i > parentGenericArgumentCount) output.Append(", ");
                    if (i == currentParameterIndex) output.Append("<b>");
                    FormatTypeName(output, genericArguments[i]);
                    if (i == currentParameterIndex) output.Append("</b>");
                }
                output.Append('>');
            }
        }

        protected void FormatPropertyGetSet(StringBuilder output, PropertyInfo property)
        {
            if (property.CanWrite && property.CanRead) output.Append(GetSetStr);
            else if (property.CanWrite) output.Append(SetStr);
            else if (property.CanRead) output.Append(GetStr);
        }
    }

    internal class MemberSuggestion : Suggestion
    {
        public MemberSuggestion(MemberInfo memberInfo, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.memberInfo = memberInfo;
            this.highlightLength = highlightLength;
        }

        private readonly MemberInfo memberInfo;
        private readonly int highlightLength;


        public override string Text => memberInfo.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                {
                    if (fieldInfo.IsInitOnly) output.Append($"{Styles.Open(Style.Keyword)}readonly{Styles.Close} ");
                    if (fieldInfo.IsLiteral) output.Append($"{Styles.Open(Style.Keyword)}const{Styles.Close} ");
                    FormatTypeName(output, fieldInfo.FieldType);
                    output.Append($" {Highlight(fieldInfo.Name, len)}");
                }
                break;
                case PropertyInfo propertyInfo:
                {
                    FormatTypeName(output, propertyInfo.PropertyType);
                    output.Append($" {Highlight(propertyInfo.Name, len)}");
                    FormatPropertyGetSet(output, propertyInfo);
                }
                break;
                case EventInfo eventInfo:
                {
                    FormatTypeName(output, eventInfo.EventHandlerType);
                    output.Append($" {Highlight(eventInfo.Name, len)}");
                }
                break;
                case MethodInfo methodInfo:
                {
                    FormatTypeName(output, methodInfo.ReturnType);
                    output.Append($" {Highlight(methodInfo.Name, len, Style.Method)}(");
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    int varArgParam = Expression.HasVariableParameterCount(methodInfo) ? parameters.Length - 1 : -1;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0) output.Append(", ");
                        if (i == varArgParam) output.Append($"{Styles.Open(Style.Keyword)}params{Styles.Close} ");
                        FormatTypeName(output, parameters[i].ParameterType);
                        output.Append($" {Styles.Open(Style.Variable)}{parameters[i].Name}{Styles.Close}");
                        if (parameters[i].HasDefaultValue)
                        {
                            output.Append(" = ");
                            Stringify(parameters[i].ParameterType, parameters[i].DefaultValue, output);
                        }
                    }
                    output.Append(')');
                }
                break;
                case Type typeInfo:
                {
                    output.Append(GetPrefix(typeInfo));
                    FormatTypeName(output, typeInfo, highlight: len);
                }
                break;
                default:
                {
                    output.Append(Highlight(memberInfo.Name, len));
                }
                break;
            }
        }
    }

    internal class MethodOverloadSuggestion : Suggestion
    {
        public MethodOverloadSuggestion(MethodBase overload, int currentParameterIndex, IReadOnlyList<string> usingNamespaces, Type delegateType = null) : base(usingNamespaces)
        {
            this.overload = overload;
            this.currentParameterIndex = currentParameterIndex;
            this.delegateType = delegateType;
        }

        private readonly MethodBase overload;
        private readonly int currentParameterIndex;
        private readonly Type delegateType;


        public override string Text => overload.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            if (overload is ConstructorInfo constructor)
            {
                FormatTypeName(output, constructor.DeclaringType, includeNamespace: false);
            }
            else if (overload is MethodInfo method)
            {
                FormatTypeName(output, method.ReturnType);
                if (delegateType == null)
                {
                    output.Append($" {Styles.Open(Style.Method)}{method.Name}{Styles.Close}");
                }
                else
                {
                    output.Append(' ');
                    FormatTypeName(output, delegateType);
                }
            }

            output.Append('(');
            FormatParameters(output, overload.GetParameters(), currentParameterIndex);
            output.Append(')');
        }
    }

    internal class GenericOverloadSuggestion : Suggestion
    {
        public GenericOverloadSuggestion(Type generic, int currentParameterIndex, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.generic = generic;
            this.currentParameterIndex = currentParameterIndex;
        }

        private readonly Type generic;
        private readonly int currentParameterIndex;


        public override string Text => generic.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            output.Append(GetPrefix(generic));
            FormatTypeName(output, generic, includeNamespace: false, includeGenericArguments: false);
            FormatGenericArguments(output, generic, currentParameterIndex);
        }
    }

    internal class SubscriptPropertySuggestion : Suggestion
    {
        public SubscriptPropertySuggestion(PropertyInfo property, int currentParameterIndex, IReadOnlyList<string> usingNamespaces, Type declaringType) : base(usingNamespaces)
        {
            this.property = property;
            this.currentParameterIndex = currentParameterIndex;
            this.declaringType = declaringType;
        }

        private readonly PropertyInfo property;
        private readonly int currentParameterIndex;
        private readonly Type declaringType;


        public override string Text => property.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            FormatTypeName(output, property.PropertyType);
            if (declaringType.BaseType != typeof(Array))
            {
                output.Append(' ');
                FormatTypeName(output, declaringType);
            }

            output.Append('[');
            FormatParameters(output, property.GetIndexParameters(), currentParameterIndex);
            output.Append(']');
            FormatPropertyGetSet(output, property);
        }
    }

    internal class NamespaceSuggestion : Suggestion
    {
        public NamespaceSuggestion(Identifier identifier, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.identifier = identifier;
            this.highlightLength = highlightLength;
        }

        private readonly Identifier identifier;
        private readonly int highlightLength;

        public override string Text => identifier.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            output.Append($"{Styles.Open(Style.Keyword)}namespace {Styles.Close}{Highlight(identifier.Name, len)}");
        }
    }

    internal class TypeSuggestion : Suggestion
    {
        public TypeSuggestion(Type type, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.type = type;
            this.highlightLength = highlightLength;

            name = type.Name;
            int idx = name.IndexOf('`');
            if (idx != -1)
            {
                name = name.Substring(0, idx);
            }
        }

        private readonly Type type;
        private readonly int highlightLength;
        private readonly string name;

        public override string Text => name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            output.Append(GetPrefix(type));
            FormatTypeName(output, type, includeNamespace: false, highlight: len);
        }
    }

    internal class VariableSuggestion : Suggestion
    {
        public VariableSuggestion(Type type, string name, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.type = type;
            this.name = name;
            this.highlightLength = highlightLength;
        }

        private readonly Type type;
        private readonly string name;
        private readonly int highlightLength;


        public override string Text => name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            FormatTypeName(output, type);
            output.Append($" {Highlight(name, len, Style.Variable)}");
        }
    }

    internal class KeywordSuggestion : Suggestion
    {
        public KeywordSuggestion(string name, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.name = name;
            this.highlightLength = highlightLength;
        }

        private readonly string name;
        private readonly int highlightLength;


        public override string Text => name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            output.Append($"{Highlight(name, len, Style.Keyword)}");
        }
    }

    internal abstract class SuggestionInfo
    {
        public SuggestionInfo(StringView query, int insertOffset, int insertLength)
        {
            this.query = query;
            this.insertOffset = insertOffset;
            this.insertLength = insertLength;
        }

        public readonly StringView query;
        public readonly int insertOffset;
        public readonly int insertLength;

        public abstract Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode);
    }

    internal sealed class MemberList : SuggestionInfo
    {
        public MemberList(StringView query, int insertOffset, int insertLength, Type type, bool isStatic) : base(query, insertOffset, insertLength)
        {
            this.type = type;
            this.isStatic = isStatic;
        }

        public readonly Type type;
        public readonly bool isStatic;


        public override Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            if (query.Length == 0 || query.End == cursorPosition)
            {
                string queryString = query.ToString();
                int queryLength = queryString.Length;
                List<Suggestion> result = new List<Suggestion>();

                BindingFlags bindingFlags = Expression.MakeBindingFlags(isStatic, safeMode);
                MemberInfo[] members = Expression.FilterMembers(type.GetMembers(bindingFlags));
                for (int i = 0; i < members.Length; i++)
                {
                    MemberInfo member = members[i];

                    // Skip any members that dont start with the query
                    if (queryString.Length > 0 && !member.Name.StartsWith(queryString, true, null)) continue;

                    // Skip types (only available in static context)
                    if (member is Type && !isStatic) continue;

                    result.Add(new MemberSuggestion(member, queryLength, usingNamespaces));
                }
                if (isStatic)
                {
                    // Get nested types
                    Type[] nestedTypes = Expression.FilterMembers(type.GetNestedTypes(bindingFlags));
                    for (int i = 0; i < nestedTypes.Length; i++)
                    {
                        Type nestedType = nestedTypes[i];

                        // Skip any members that dont start with the query
                        if (queryString.Length > 0 && !nestedType.Name.StartsWith(queryString, true, null)) continue;

                        result.Add(new TypeSuggestion(nestedType, queryLength, usingNamespaces));
                    }
                }
                if (result.Count > 0)
                {
                    // Sort alphabetically (GetMembers result may not be sorted)
                    result.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
                    return result.ToArray();
                }
            }

            return Array.Empty<Suggestion>();
        }
    }

    internal sealed class IdentifierList : SuggestionInfo
    {
        public IdentifierList(StringView query, int insertOffset, int insertLength, Identifier identifier, VariableDeclaration[] variables, bool includeKeywords) : base(query, insertOffset, insertLength)
        {
            this.identifier = identifier;
            this.variables = variables;
            this.includeKeywords = includeKeywords;
        }

        public readonly Identifier identifier;
        private readonly VariableDeclaration[] variables;
        private readonly bool includeKeywords;


        public override Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            if (query.Length == 0 || query.End == cursorPosition)
            {
                Dictionary<string, Suggestion> suggestions = new Dictionary<string, Suggestion>();
                string queryString = query.ToString();
                int queryLength = queryString.Length;
                bool hasQuery = queryLength > 0;
                foreach (var child in identifier.Children)
                {
                    if (!hasQuery || child.Name.StartsWith(queryString, true, null))
                    {
                        if (child.IsNamespace)
                        {
                            suggestions[child.Name] = new NamespaceSuggestion(child, queryLength, usingNamespaces);
                        }
                        else
                        {
                            foreach (Type type in child.Types)
                            {
                                suggestions[type.Name] = new TypeSuggestion(type, queryLength, usingNamespaces);
                            }
                        }
                    }
                }
                for (int i = 0; i < variables.Length; i++)
                {
                    if (!hasQuery || variables[i].name.StartsWith(queryString, true, null))
                    {
                        suggestions[variables[i].name] = new VariableSuggestion(variables[i].type, variables[i].name, queryLength, usingNamespaces);
                    }
                }
                if (includeKeywords)
                {
                    foreach (string keyword in TokenUtility.Keywords)
                    {
                        if (!hasQuery || keyword.StartsWith(queryString, true, null))
                        {
                            suggestions[keyword] = new KeywordSuggestion(keyword, queryLength, usingNamespaces);
                        }
                    }
                }
                if (suggestions.Count > 0)
                {
                    List<Suggestion> result = new List<Suggestion>(suggestions.Values);
                    result.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
                    return result.ToArray();
                }
            }
            return Array.Empty<Suggestion>();
        }
    }

    internal sealed class MethodOverloadList : SuggestionInfo
    {
        public MethodOverloadList(IReadOnlyList<MethodBase> overloads, IReadOnlyList<Expression> args, Type delegateType = null) : base(StringView.Empty, 0, 0)
        {
            this.overloads = overloads;
            this.args = args;
            this.delegateType = delegateType;
        }

        private readonly IReadOnlyList<MethodBase> overloads;
        private readonly IReadOnlyList<Expression> args;
        private readonly Type delegateType;


        public override Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<Suggestion> result = new List<Suggestion>();

            int currentArgumentIndex = args.Count;
            for (int i = 0; i < overloads.Count; i++)
            {
                ParameterInfo[] parameters = overloads[i].GetParameters();
                if (Expression.IsCompatibleOverload(parameters, args, false))
                {
                    if (Expression.HasVariableParameterCount(parameters) || currentArgumentIndex < parameters.Length || (currentArgumentIndex == 0 && parameters.Length == 0))
                    {
                        result.Add(new MethodOverloadSuggestion(overloads[i], currentArgumentIndex, usingNamespaces, delegateType));
                    }
                }
            }

            if (result.Count > 0)
            {
                result.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
                return result.ToArray();
            }

            return result.ToArray();
        }
    }

    internal sealed class GenericsOverloadList : SuggestionInfo
    {
        public GenericsOverloadList(IReadOnlyList<Type> generics, IReadOnlyList<Type> args) : base(StringView.Empty, 0, 0)
        {
            this.generics = generics;
            this.args = args;
        }

        private readonly IReadOnlyList<Type> generics;
        private readonly IReadOnlyList<Type> args;


        public override Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<Suggestion> result = new List<Suggestion>();

            int currentArgumentIndex = args.Count;
            for (int i = 0; i < generics.Count; i++)
            {
                Type generic = generics[i];
                int parentGenericArgumentCount = generic.DeclaringType == null ? 0 : generic.GetGenericArguments().Length;
                int actualGenericArgumentCount = generic.GetGenericArguments().Length - parentGenericArgumentCount;
                if (currentArgumentIndex < actualGenericArgumentCount || (currentArgumentIndex == 0 && actualGenericArgumentCount == 0))
                {
                    result.Add(new GenericOverloadSuggestion(generic, currentArgumentIndex, usingNamespaces));
                }
            }

            if (result.Count > 0)
            {
                result.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
                return result.ToArray();
            }

            return result.ToArray();
        }
    }

    internal sealed class PropertyOverloadList : SuggestionInfo
    {
        public PropertyOverloadList(IReadOnlyList<PropertyInfo> properties, IReadOnlyList<Expression> args, Type declaringType) : base(StringView.Empty, 0, 0)
        {
            this.properties = properties;
            this.args = args;
            this.declaringType = declaringType;
        }

        private readonly IReadOnlyList<PropertyInfo> properties;
        private readonly IReadOnlyList<Expression> args;
        private readonly Type declaringType;
        public int CurrentArgumentIndex => args.Count;


        public override Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<Suggestion> result = new List<Suggestion>();

            int currentArgumentIndex = CurrentArgumentIndex;
            for (int i = 0; i < properties.Count; i++)
            {
                ParameterInfo[] parameters = properties[i].GetIndexParameters();
                if (Expression.IsCompatibleOverload(parameters, args, false))
                {
                    if (Expression.HasVariableParameterCount(parameters) || currentArgumentIndex < parameters.Length)
                    {
                        result.Add(new SubscriptPropertySuggestion(properties[i], currentArgumentIndex, usingNamespaces, declaringType));
                    }
                }
            }

            if (result.Count > 0)
            {
                result.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
                return result.ToArray();
            }

            return result.ToArray();
        }
    }

    internal readonly struct StyledCommand
    {
        public StyledCommand(string command, StyledToken[] styledTokens)
        {
            this.command = command;
            this.styledTokens = styledTokens;
        }

        public readonly string command;
        public readonly StyledToken[] styledTokens;

        public static implicit operator bool(StyledCommand styledCommand)
        {
            return !string.IsNullOrEmpty(styledCommand.command);
        }
    }

    internal struct SuggestionTable
    {
        public static readonly SuggestionTable Empty = new SuggestionTable();

        public SuggestionTable(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            visible = new List<Suggestion>();
            text = string.Empty;

            currentHighlightOffset = -1;
            currentHighlightIndex = -1;

            suggestions = Array.Empty<Suggestion>();
            isOverloadList = false;
            insertOffset = 0;
            insertLength = 0;
            styledOutput = default;
            visibleLineCount = 0;

            containsErrors = false;

            CommandParser commandParser = null;
            try
            {
                ArrayView<Token> tokens = new Lexer(input).ToArray();
                commandParser = new CommandParser(tokens, identifierTable, safeMode, 0, cursorPosition);
            }
            catch { }

            if (commandParser != null)
            {
                try { commandParser.Parse(); } catch { containsErrors = true; }

                SuggestionInfo suggestionInfo = commandParser.SuggestionInfo;
                if (suggestionInfo != null)
                {
                    suggestions = suggestionInfo.GetSuggestions(cursorPosition, usingNamespaces, safeMode);
                    isOverloadList = suggestionInfo is MethodOverloadList || suggestionInfo is GenericsOverloadList;
                    insertOffset = suggestionInfo.insertOffset;
                    insertLength = suggestionInfo.insertLength;
                }
                styledOutput = new StyledCommand(input, commandParser.GetStyledTokens());
            }
        }


        public readonly Suggestion[] suggestions;
        public readonly bool isOverloadList;
        public int SuggestionCount => suggestions == null ? 0 : suggestions.Length;

        // The string used to build the suggestion table
        public readonly StyledCommand styledOutput;
        // Offset and length of the string in the input that needs to be replaced when inserting a suggestion
        public readonly int insertOffset;
        public readonly int insertLength;

        // List of currently visible suggestions
        public readonly List<Suggestion> visible;
        public string text;
        // Amount of suggestion lines including overflow
        public int visibleLineCount;

        public readonly bool containsErrors;

        private int currentHighlightOffset;
        private int currentHighlightIndex;

        public bool Update(ref int highlightOffset, ref int highlightIndex, int direction, int maxCount, StringBuilder output)
        {
            if (suggestions == null || suggestions.Length == 0) return false;

            // Clamp direction
            direction = direction > 0 ? 1 : direction < 0 ? -1 : direction;

            if (highlightOffset < 0) highlightOffset = 0;

            int overflow = 0;
            int visibleCount = suggestions.Length;

            if (visibleCount <= maxCount)
            {
                // No overflow
                highlightOffset = 0;
            }

            visibleCount -= highlightOffset;

            if (highlightOffset > 0) maxCount--;
            if (visibleCount > maxCount)
            {
                maxCount--;
                overflow = visibleCount - maxCount;
                visibleCount = maxCount;
            }

            // This nasty piece of code can probably be rewritten, but it works for the current case
            // It calculates the scrolling for the suggestions when there is overflow
            if (direction != 0)
            {
                if (highlightIndex < 0)
                {
                    if (highlightOffset == 0)
                    {
                        if (direction == 1)
                            highlightIndex = visibleCount - 1;
                        else
                        {
                            if (overflow == 2)
                            {
                                highlightOffset = 2;
                                overflow = 0;
                                highlightIndex = visibleCount - 2;
                            }
                            else if (overflow > 0)
                            {
                                highlightOffset = 2;
                                overflow--;
                                visibleCount--;
                                highlightIndex = visibleCount - 1;
                            }
                        }
                    }
                    else
                        highlightIndex = visibleCount - 1;
                }
                else
                {
                    highlightIndex -= direction;
                }

                if (highlightIndex < 0)
                {
                    if (highlightOffset == 2 && overflow == 0)
                    {
                        highlightOffset = 0;
                        highlightIndex = 1;
                        overflow = 2;
                    }
                    else if (highlightOffset > 0)
                    {
                        if (overflow == 0)
                        {
                            overflow = 2;
                            visibleCount--;
                        }
                        else
                        {
                            overflow++;
                        }
                        highlightOffset--;

                        if (highlightOffset == 1)
                        {
                            highlightOffset = 0;
                            visibleCount++;
                            highlightIndex = 1;
                        }
                        else
                        {
                            highlightIndex = 0;
                        }
                    }
                    else
                    {
                        highlightIndex = 0;
                    }
                }

                if (highlightIndex >= visibleCount)
                {
                    if (highlightOffset == 0 && overflow == 2)
                    {
                        highlightOffset = 2;
                        highlightIndex = visibleCount - 2;
                        overflow = 0;
                    }
                    else if (overflow > 0)
                    {
                        if (highlightOffset == 0)
                        {
                            highlightOffset = 2;
                            visibleCount--;
                        }
                        else
                        {
                            highlightOffset++;
                        }
                        overflow--;

                        if (overflow == 1)
                        {
                            overflow = 0;
                            visibleCount++;
                            highlightIndex = visibleCount - 2;
                        }
                        else
                        {
                            highlightIndex = visibleCount - 1;
                        }
                    }
                    else
                    {
                        highlightIndex = visibleCount - 1;
                    }
                }
            }

            // No need to regenerate if nothing changed
            if (currentHighlightOffset == highlightOffset && currentHighlightIndex == highlightIndex) return false;
            currentHighlightOffset = highlightOffset;
            currentHighlightIndex = highlightIndex;

            visible.Clear();
            output.Clear();
            visibleLineCount = 0;

            // Underflow
            if (highlightOffset > 0)
            {
                output.Append($"\n< {highlightOffset} more results >");
                visibleLineCount++;
            }

            output.Append(Styles.Open(Style.Default));
            for (int i = 0; i < visibleCount; i++)
            {
                var suggestion = suggestions[highlightOffset + i];

                visible.Add(suggestion);

                output.Append('\n');
                suggestion.BuildSuggestionString(output, i == highlightIndex);
            }
            output.Append(Styles.Close);
            visibleLineCount += visibleCount;

            // Overflow
            if (overflow > 0)
            {
                output.Append($"\n< {overflow} more results >");
                visibleLineCount++;
            }

            text = output.ToString();

            return true;
        }
    }
}

#endif