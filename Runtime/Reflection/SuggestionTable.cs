// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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

    internal abstract class SuggestionObject : IComparable<SuggestionObject>
    {
        public SuggestionObject(IReadOnlyList<string> usingNamespaces)
        {
            this.usingNamespaces = usingNamespaces;
        }

        public abstract string Text { get; }
        public virtual int GenericArgumentCount => 0;
        public virtual int ParameterCount => 0;
        public int CompareTo(SuggestionObject suggestion)
        {
            int textCompare = Text.CompareTo(suggestion.Text);
            if (textCompare == 0)
            {
                int genericArgumentCompare = GenericArgumentCount.CompareTo(suggestion.GenericArgumentCount);
                if (genericArgumentCompare == 0)
                {
                    return ParameterCount.CompareTo(suggestion.ParameterCount);
                }
                return genericArgumentCompare;
            }
            return textCompare;
        }

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

        protected enum TypeNameFlags
        {
            None = 0,
            Namespace = 1,
            DeclaringType = 2,
            GenericArguments = 4,
            AllowKeywords = 8,
            All = 255,
        }
        protected void FormatTypeName(StringBuilder output, Type type, TypeNameFlags flags = TypeNameFlags.All, int highlight = 0)
        {
            // Remove reference
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type.IsGenericParameter)
            {
                output.Append(Highlight(type.Name, highlight, Styles.GetTypeColor(type)));
                return;
            }

            if ((flags & TypeNameFlags.AllowKeywords) != TypeNameFlags.None)
            {
                if (TokenUtility.TryGetBaseTypeName(type, out string baseTypeName))
                {
                    output.Append(Highlight(baseTypeName, highlight, Style.Keyword));
                    return;
                }
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

            // Namespace
            if ((flags & TypeNameFlags.Namespace) != TypeNameFlags.None)
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

            // Declaring type
            if ((flags & TypeNameFlags.DeclaringType) != TypeNameFlags.None)
            {
                Type declaringType = type.DeclaringType;
                if (declaringType != null)
                {
                    FormatTypeName(output, declaringType, ~TypeNameFlags.Namespace);
                    output.Append('.');
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

            // Actual type name
            output.Append(Highlight(typeName, highlight, Styles.GetTypeColor(type)));

            if ((flags & TypeNameFlags.GenericArguments) != TypeNameFlags.None && type.IsGenericType)
            {
                Type[] genericArgs = type.GetGenericArguments();
                int parentArgumentCount = type.DeclaringType == null ? 0 : type.DeclaringType.GetGenericArguments().Length;
                if (genericArgs.Length > parentArgumentCount)
                {
                    output.EscapeRTF('<');
                    for (int i = parentArgumentCount; i < genericArgs.Length; i++)
                    {
                        if (i != 0) output.Append(", ");
                        FormatTypeName(output, genericArgs[i]);
                    }
                    output.Append('>');
                }
            }
        }

        protected void FormatParameter(StringBuilder output, ParameterInfo parameter)
        {
            if (parameter.HasCustomAttribute<ParamArrayAttribute>(true)) output.Append($"{Styles.Open(Style.Keyword)}params{Styles.Close} ");
            if (parameter.IsIn) output.Append($"{Styles.Open(Style.Keyword)}in{Styles.Close} ");
            else if (parameter.IsOut) output.Append($"{Styles.Open(Style.Keyword)}out{Styles.Close} ");
            else if (parameter.ParameterType.IsByRef) output.Append($"{Styles.Open(Style.Keyword)}ref{Styles.Close} ");
            FormatTypeName(output, parameter.ParameterType);
            output.Append($" {Styles.Open(Style.Variable)}{parameter.Name}{Styles.Close}");
        }
        protected void FormatParameters(StringBuilder output, ParameterInfo[] parameters, int currentParameterIndex = -1)
        {
            if (currentParameterIndex >= parameters.Length) currentParameterIndex = parameters.Length - 1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) output.Append(", ");
                if (i == currentParameterIndex) output.Append("<b>");
                FormatParameter(output, parameters[i]);
                if (parameters[i].HasDefaultValue)
                {
                    output.Append($" = ");
                    Stringify(parameters[i].ParameterType, parameters[i].DefaultValue, output);
                }
                if (i == currentParameterIndex) output.Append("</b>");
            }
        }
        protected void FormatGenericArguments(StringBuilder output, Type[] genericArguments, int currentParameterIndex = -1)
        {
            if (genericArguments.Length > 0)
            {
                output.EscapeRTF('<');
                if (currentParameterIndex >= genericArguments.Length) currentParameterIndex = genericArguments.Length - 1;
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    if (i > 0) output.Append(", ");
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

    internal class MemberSuggestion : SuggestionObject
    {
        public MemberSuggestion(MemberInfo memberInfo, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.memberInfo = memberInfo;
            this.highlightLength = highlightLength;

            if (memberInfo is MethodInfo methodInfo)
            {
                genericArgumentCount = methodInfo.GetGenericArguments().Length;
                parameterCount = methodInfo.GetParameters().Length;
            }
        }

        private readonly MemberInfo memberInfo;
        private readonly int highlightLength;

        private readonly int genericArgumentCount;
        private readonly int parameterCount;


        public override string Text => memberInfo.Name;
        public override int GenericArgumentCount => genericArgumentCount;
        public override int ParameterCount => parameterCount;

        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                {
                    if (fieldInfo.IsLiteral)
                    {
                        output.Append($"{Styles.Open(Style.Keyword)}const{Styles.Close} ");
                    }
                    else
                    {
                        if (fieldInfo.IsStatic) output.Append($"{Styles.Open(Style.Keyword)}static{Styles.Close} ");
                        if (fieldInfo.IsInitOnly) output.Append($"{Styles.Open(Style.Keyword)}readonly{Styles.Close} ");
                    }
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
                    if (methodInfo.IsStatic) output.Append($"{Styles.Open(Style.Keyword)}static{Styles.Close} ");
                    FormatTypeName(output, methodInfo.ReturnType);
                    output.Append($" {Highlight(methodInfo.Name, len, Style.Method)}");
                    FormatGenericArguments(output, methodInfo.GetGenericArguments());
                    output.Append('(');
                    ParameterInfo[] parameters = methodInfo.GetParameters();
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0) output.Append(", ");
                        FormatParameter(output, parameters[i]);
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
                    throw new NotImplementedException();
                    //output.Append(GetPrefix(typeInfo));
                    //FormatTypeName(output, typeInfo, highlight: len);
                }
                default:
                {
                    output.Append(Highlight(memberInfo.Name, len));
                }
                break;
            }
        }
    }

    internal class MethodOverloadSuggestion : SuggestionObject
    {
        public MethodOverloadSuggestion(MethodBase overload, int currentParameterIndex, IReadOnlyList<string> usingNamespaces, Type delegateType = null) : base(usingNamespaces)
        {
            this.overload = overload;
            this.currentParameterIndex = currentParameterIndex;
            this.delegateType = delegateType;

            genericArgumentCount = overload is MethodInfo methodInfo ? methodInfo.GetGenericArguments().Length : 0;
            parameterCount = overload.GetParameters().Length;
        }

        private readonly MethodBase overload;
        private readonly int currentParameterIndex;
        private readonly Type delegateType;

        private readonly int genericArgumentCount;
        private readonly int parameterCount;


        public override string Text => overload.Name;
        public override int GenericArgumentCount => genericArgumentCount;
        public override int ParameterCount => parameterCount;

        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            if (overload is ConstructorInfo constructor)
            {
                FormatTypeName(output, constructor.DeclaringType, TypeNameFlags.GenericArguments);
            }
            else if (overload is MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic) output.Append($"{Styles.Open(Style.Keyword)}static{Styles.Close} ");
                FormatTypeName(output, methodInfo.ReturnType);
                if (delegateType == null)
                {
                    output.Append($" {Styles.Open(Style.Method)}{methodInfo.Name}{Styles.Close}");
                }
                else
                {
                    output.Append(' ');
                    FormatTypeName(output, delegateType);
                }
                FormatGenericArguments(output, methodInfo.GetGenericArguments());
            }
            output.Append('(');
            FormatParameters(output, overload.GetParameters(), currentParameterIndex);
            output.Append(')');
        }
    }

    internal class GenericOverloadSuggestion : SuggestionObject
    {
        public GenericOverloadSuggestion(Generic generic, int currentParameterIndex, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.generic = generic;
            this.currentParameterIndex = currentParameterIndex;
            name = Expression.FormatGenericName(generic.Name);

            genericArgumentCount = generic.GetGenericArguments().Length;
            parameterCount = generic.ParameterCount;
        }

        private readonly Generic generic;
        private readonly int currentParameterIndex;
        private string name;

        private readonly int genericArgumentCount;
        private readonly int parameterCount;


        public override string Text => name;
        public override int GenericArgumentCount => genericArgumentCount;
        public override int ParameterCount => parameterCount;

        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            if (generic is GenericType genericType)
            {
                output.Append(Styles.Open(Styles.GetTypeColor(genericType.type)));
                output.Append(name);
                output.Append(Styles.Close);
                FormatGenericArguments(output, generic.GetGenericArguments(), currentParameterIndex);
            }
            else if (generic is GenericMethod genericMethod)
            {
                FormatTypeName(output, genericMethod.methodInfo.ReturnType);
                output.Append(' ');
                output.Append(Styles.Open(Style.Method));
                output.Append(name);
                output.Append(Styles.Close);
                FormatGenericArguments(output, genericMethod.GetGenericArguments(), currentParameterIndex);
                output.Append('(');
                FormatParameters(output, genericMethod.methodInfo.GetParameters());
                output.Append(')');
            }
        }
    }

    internal class SubscriptPropertySuggestion : SuggestionObject
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

    internal class NamespaceSuggestion : SuggestionObject
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

    internal class TypeSuggestion : SuggestionObject
    {
        public TypeSuggestion(Type type, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.type = type;
            this.highlightLength = highlightLength;
            name = Expression.FormatGenericName(type);

            genericArgumentCount = type.GetGenericArguments().Length;
        }

        private readonly Type type;
        private readonly int highlightLength;
        private readonly string name;

        private readonly int genericArgumentCount;


        public override string Text => name;
        public override int GenericArgumentCount => genericArgumentCount;

        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            output.Append(GetPrefix(type));
            FormatTypeName(output, type, TypeNameFlags.GenericArguments, highlight: len);
        }
    }

    internal class VariableSuggestion : SuggestionObject
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

    internal class KeywordSuggestion : SuggestionObject
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

        public abstract SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode);
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


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            if (query.Length == 0 || query.End == cursorPosition)
            {
                string queryString = query.ToString();
                int queryLength = queryString.Length;
                List<SuggestionObject> result = new List<SuggestionObject>();

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
                    result.Sort();
                    return result.ToArray();
                }
            }

            return Array.Empty<SuggestionObject>();
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


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            if (query.Length == 0 || query.End == cursorPosition)
            {
                Dictionary<string, SuggestionObject> suggestions = new Dictionary<string, SuggestionObject>();
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
                    List<SuggestionObject> result = new List<SuggestionObject>(suggestions.Values);
                    result.Sort();
                    return result.ToArray();
                }
            }
            return Array.Empty<SuggestionObject>();
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


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<SuggestionObject> result = new List<SuggestionObject>();

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
                result.Sort();
                return result.ToArray();
            }

            return result.ToArray();
        }
    }

    internal sealed class GenericsOverloadList : SuggestionInfo
    {
        public GenericsOverloadList(IReadOnlyList<Generic> generics, IReadOnlyList<Type> args) : base(StringView.Empty, 0, 0)
        {
            this.generics = generics;
            this.args = args;
        }

        private readonly IReadOnlyList<Generic> generics;
        private readonly IReadOnlyList<Type> args;


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<SuggestionObject> result = new List<SuggestionObject>();

            int currentArgumentIndex = args.Count;
            for (int i = 0; i < generics.Count; i++)
            {
                Generic generic = generics[i];
                int genericArgumentCount = generic.GetGenericArguments().Length;
                if (currentArgumentIndex < genericArgumentCount || (currentArgumentIndex == 0 && genericArgumentCount == 0))
                {
                    result.Add(new GenericOverloadSuggestion(generic, currentArgumentIndex, usingNamespaces));
                }
            }

            if (result.Count > 0)
            {
                result.Sort();
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


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<SuggestionObject> result = new List<SuggestionObject>();

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
                result.Sort();
                return result.ToArray();
            }

            return result.ToArray();
        }
    }

    [Serializable]
    internal struct StyledToken
    {
        public StyledToken(StringView str, Style style)
        {
            offset = str.Offset;
            length = str.Length;
            this.style = style;
        }
        public StyledToken(int offset, int length, Style style)
        {
            this.offset = offset;
            this.length = length;
            this.style = style;
        }

        public int offset;
        public int length;
        public Style style;
    }

    internal struct SuggestionTable
    {
        public SuggestionTable(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            commandText = input;
            visible = new List<SuggestionObject>();
            text = string.Empty;

            currentHighlightOffset = -1;
            currentHighlightIndex = -1;

            suggestions = Array.Empty<SuggestionObject>();
            isOverloadList = false;
            insertOffset = 0;
            insertLength = 0;
            commandStyle = default;
            visibleLineCount = 0;

            containsErrors = false;

            CommandParser commandParser = null;
            ArrayView<Token> tokens;
            try
            {
                tokens = new Lexer(input).ToArray();
                commandParser = new CommandParser(tokens, identifierTable, safeMode, 0, cursorPosition);
            }
            catch
            {
                tokens = ArrayView<Token>.Empty;
            }

            if (commandParser != null)
            {
                try
                {
                    commandParser.Parse();
                }
                catch
                {
                    containsErrors = true;
                }

                SuggestionInfo suggestionInfo = commandParser.SuggestionInfo;
                if (suggestionInfo != null)
                {
                    suggestions = suggestionInfo.GetSuggestions(cursorPosition, usingNamespaces, safeMode);
                    isOverloadList = suggestionInfo is MethodOverloadList || suggestionInfo is GenericsOverloadList;
                    insertOffset = suggestionInfo.insertOffset;
                    insertLength = suggestionInfo.insertLength;
                }

                StyledToken[] commandTokens = commandParser.GetStyledTokens();
                List<StyledToken> styledTokens = new List<StyledToken>();
                for (int i = 0, j = 0; i < tokens.Length; i++)
                {
                    if (j < commandTokens.Length && commandTokens[j].offset == tokens[i].str.Offset)
                    {
                        styledTokens.Add(commandTokens[j++]);
                    }
                    else
                    {
                        Style style;
                        if (tokens[i].Family == TokenFamily.Keyword)
                        {
                            style = Style.Keyword;
                        }
                        else
                        {
                            style = tokens[i].type switch
                            {
                                TokenType.StringLiteral => Style.String,
                                TokenType.CharLiteral => Style.String,
                                TokenType.NumberLiteral => Style.Number,
                                _ => Style.Default,
                            };
                        }
                        if (style != Style.Default)
                        {
                            styledTokens.Add(new StyledToken(tokens[i].str, style));
                        }
                    }
                }
                commandStyle = styledTokens.ToArray();
            }
        }


        public readonly SuggestionObject[] suggestions;
        public readonly bool isOverloadList;
        public int SuggestionCount => suggestions == null ? 0 : suggestions.Length;

        // The string used to build the suggestion table
        public readonly string commandText;
        public readonly StyledToken[] commandStyle;
        // Offset and length of the string in the input that needs to be replaced when inserting a suggestion
        public readonly int insertOffset;
        public readonly int insertLength;

        // List of currently visible suggestions
        public readonly List<SuggestionObject> visible;
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

    [Serializable]
    internal struct SuggestionResult
    {
        public static readonly SuggestionResult Empty = new SuggestionResult(string.Empty, Array.Empty<StyledToken>(), string.Empty, Array.Empty<string>(), 0, 0, 0, false);

        public SuggestionResult(string commandText, StyledToken[] commandStyle, string suggestionText, string[] suggestions, int insertOffset, int insertLength, int visibleLineCount, bool isOverloadList)
        {
            this.commandText = commandText;
            this.commandStyle = commandStyle;
            this.suggestionText = suggestionText;
            this.suggestions = suggestions;
            this.insertOffset = insertOffset;
            this.insertLength = insertLength;
            this.visibleLineCount = visibleLineCount;
            this.isOverloadList = isOverloadList;
        }

        public string commandText;
        public StyledToken[] commandStyle;
        public string suggestionText;
        public string[] suggestions;
        public int insertOffset;
        public int insertLength;
        public int visibleLineCount;
        public bool isOverloadList;
    }

    internal class SuggestionProvider
    {
        private class SuggestionTableBuilder
        {
            public SuggestionTableBuilder(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
            {
                this.input = input;
                this.cursorPosition = cursorPosition;
                this.identifierTable = identifierTable;
                this.usingNamespaces = usingNamespaces;
                this.safeMode = safeMode;
            }

            private readonly string input;
            private readonly int cursorPosition;
            private readonly Identifier identifierTable;
            private readonly IReadOnlyList<string> usingNamespaces;
            private readonly bool safeMode;

            public SuggestionTable Build() => new SuggestionTable(input, cursorPosition, identifierTable, usingNamespaces, safeMode);
        }

        private SuggestionTable suggestionTable = default;
        private Task<SuggestionTable> updateSuggestionsTask = null;
        public bool IsBuildingSuggestions => updateSuggestionsTask != null;
        private Action onComplete;
        private SuggestionResult cachedResult = default;

        public void BuildSuggestions(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            if (IsBuildingSuggestions) throw new DebugConsoleException("Suggestion building operation already in progress");

            SuggestionTableBuilder builder = new SuggestionTableBuilder(input, cursorPosition, identifierTable, usingNamespaces, safeMode);
            suggestionTable = builder.Build();
        }
        public void BuildSuggestionsAsync(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode, Action onComplete)
        {
            if (IsBuildingSuggestions) throw new DebugConsoleException("Suggestion building operation already in progress");

            SuggestionTableBuilder builder = new SuggestionTableBuilder(input, cursorPosition, identifierTable, usingNamespaces, safeMode);
            this.onComplete = onComplete;
            updateSuggestionsTask = Task.Run(() => builder.Build());
        }

        public void Update()
        {
            if (IsBuildingSuggestions)
            {
                // Update the task status and retrieve the result upon completion
                TaskStatus taskStatus = updateSuggestionsTask.Status;
                if (taskStatus >= TaskStatus.RanToCompletion)
                {
                    Exception exception = updateSuggestionsTask.Exception;

                    if (exception != null)
                    {
                        updateSuggestionsTask = null;

                        throw exception;
                    }
                    else
                    {
                        if (taskStatus == TaskStatus.RanToCompletion)
                        {
                            suggestionTable = updateSuggestionsTask.Result;
                            cachedResult = SuggestionResult.Empty;
                            updateSuggestionsTask = null;
                            onComplete?.Invoke();
                        }
                        else
                        {
                            updateSuggestionsTask = null;
                        }
                    }
                }
            }
        }

        public SuggestionResult GetResult(ref int highlightOffset, ref int highlightIndex, int direction, int maxCount, StringBuilder output)
        {
            if (IsBuildingSuggestions) throw new DebugConsoleException("Suggestion building operation still in progress");

            cachedResult.commandText = suggestionTable.commandText;
            cachedResult.commandStyle = suggestionTable.commandStyle;
            cachedResult.insertOffset = suggestionTable.insertOffset;
            cachedResult.insertLength = suggestionTable.insertLength;
            cachedResult.visibleLineCount = suggestionTable.visibleLineCount;
            cachedResult.isOverloadList = suggestionTable.isOverloadList;

            if (suggestionTable.Update(ref highlightOffset, ref highlightIndex, direction, maxCount, output))
            {
                cachedResult.suggestionText = output.ToString();
                cachedResult.suggestions = new string[suggestionTable.visible.Count];
                int idx = 0;
                foreach (var visible in suggestionTable.visible)
                {
                    cachedResult.suggestions[idx++] = visible.Text;
                }
            }

            return cachedResult;
        }
    }
}

#endif