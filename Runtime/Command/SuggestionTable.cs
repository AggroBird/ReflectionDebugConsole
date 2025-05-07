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
        None,
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
                return Style.None;
        }
    }

    internal sealed class StyledStringBuilder
    {
        public StyledStringBuilder(StringBuilder stringBuilder)
        {
            this.stringBuilder = stringBuilder;
        }

        private void BeginStyle(Style style)
        {
            if (currentStyle != style)
            {
                EndStyle();

                currentStyle = style;
            }

            if (!hasText && currentStyle != Style.None)
            {
                if (currentStyle != Style.None)
                {
                    stringBuilder.Append(Styles.Open(currentStyle));
                }
            }

            hasText = true;
        }
        private void EndStyle()
        {
            if (hasText && currentStyle != Style.None)
            {
                stringBuilder.Append(Styles.Close);
            }

            hasText = false;
        }


        public void Append(object obj, Style style = Style.None)
        {
            BeginStyle(style);

            stringBuilder.Append(obj);
        }

        public void Append(string str, Style style = Style.None)
        {
            BeginStyle(style);

            stringBuilder.Append(str);
        }
        public void Append(string str, int startIndex, int count, Style style = Style.None)
        {
            if (count > 0)
            {
                BeginStyle(style);

                stringBuilder.Append(str, startIndex, count);
            }
        }
        public void Append(char c, Style style = Style.None)
        {
            if (!char.IsWhiteSpace(c)) BeginStyle(style);

            stringBuilder.Append(c);
        }

        public void AppendRTF(string str, Style style = Style.None)
        {
            BeginStyle(style);

            stringBuilder.AppendRTF(str);
        }
        public void AppendRTF(StringView str, Style style = Style.None)
        {
            BeginStyle(style);

            stringBuilder.AppendRTF(str);
        }
        public void AppendRTF(string str, int startIndex, int count, Style style = Style.None)
        {
            if (count > 0)
            {
                BeginStyle(style);

                stringBuilder.AppendRTF(str, startIndex, count);
            }
        }
        public void AppendRTF(char c, Style style = Style.None)
        {
            if (!char.IsWhiteSpace(c)) BeginStyle(style);

            stringBuilder.AppendRTF(c);
        }

        public void Clear()
        {
            stringBuilder.Clear();
            currentStyle = Style.None;
            hasText = false;
        }

        public override string ToString()
        {
            EndStyle();

            return stringBuilder.ToString();
        }

        private readonly StringBuilder stringBuilder;
        private Style currentStyle = Style.None;
        private bool hasText = false;
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

        public abstract void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted);

        protected readonly IReadOnlyList<string> usingNamespaces;


        protected void Stringify(Type type, object value, StyledStringBuilder output)
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
                    output.Append("null", Style.Keyword);
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
                    output.Append($"\"{value}\"", Style.String);
                    return;
                case TypeCode.String:
                    output.Append($"'{value}'", Style.String);
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
                    output.Append(value, Style.Number);
                    return;

                case TypeCode.Boolean:
                    output.Append((bool)value ? "true" : "false", Style.Keyword);
                    return;
            }

            output.Append(value.ToString());
        }

        protected static void Highlight(StyledStringBuilder output, string str, int len, Style color = Style.None)
        {
            if (len >= str.Length)
            {
                output.Append($"<b>{str}</b>", color);
            }
            else if (len > 0)
            {
                output.Append($"<b>{str.Substring(0, len)}</b>{str.Substring(len)}", color);
            }
            else
            {
                output.Append(str, color);
            }
        }

        protected static void WritePrefix(StyledStringBuilder output, Type type)
        {
            string prefix = Expression.GetPrefix(type);
            if (!string.IsNullOrEmpty(prefix))
            {
                output.Append(prefix, Style.Keyword);
            }
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
        protected void FormatTypeName(StyledStringBuilder output, Type type, TypeNameFlags flags = TypeNameFlags.All, int highlight = 0)
        {
            // Remove reference
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type.IsGenericParameter)
            {
                Highlight(output, type.Name, highlight, Styles.GetTypeColor(type));
                return;
            }

            if ((flags & TypeNameFlags.AllowKeywords) != TypeNameFlags.None)
            {
                if (TokenUtility.TryGetBaseTypeName(type, out string baseTypeName))
                {
                    Highlight(output, baseTypeName, highlight, Style.Keyword);
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
                            if (typeNamespace.StartsWith(ns, StringComparison.Ordinal) && (typeNamespace.Length == ns.Length || typeNamespace[ns.Length] == '.'))
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
            Highlight(output, typeName, highlight, Styles.GetTypeColor(type));

            if ((flags & TypeNameFlags.GenericArguments) != TypeNameFlags.None && type.IsGenericType)
            {
                Type[] genericArgs = type.GetGenericArguments();
                int parentArgumentCount = type.DeclaringType == null ? 0 : type.DeclaringType.GetGenericArguments().Length;
                if (genericArgs.Length > parentArgumentCount)
                {
                    output.AppendRTF('<');
                    for (int i = parentArgumentCount; i < genericArgs.Length; i++)
                    {
                        if (i != 0) output.Append(", ");
                        FormatTypeName(output, genericArgs[i]);
                    }
                    output.Append('>');
                }
            }
        }

        protected void FormatParameter(StyledStringBuilder output, ParameterInfo parameter)
        {
            if (parameter.HasCustomAttribute<ParamArrayAttribute>(true)) output.Append("params ", Style.Keyword);
            if (parameter.IsIn) output.Append("in ", Style.Keyword);
            else if (parameter.IsOut) output.Append("out ", Style.Keyword);
            else if (parameter.ParameterType.IsByRef) output.Append("ref ", Style.Keyword);
            FormatTypeName(output, parameter.ParameterType);
            output.Append(' ');
            output.Append(parameter.Name, Style.Variable);
        }
        protected void FormatParameters(StyledStringBuilder output, ParameterInfo[] parameters, int currentParameterIndex = -1)
        {
            if (currentParameterIndex >= parameters.Length) currentParameterIndex = parameters.Length - 1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) output.Append(", ");
                if (i == currentParameterIndex) output.Append("<b>");
                FormatParameter(output, parameters[i]);
                if (parameters[i].HasDefaultValue)
                {
                    output.Append(" = ");
                    Stringify(parameters[i].ParameterType, parameters[i].DefaultValue, output);
                }
                if (i == currentParameterIndex) output.Append("</b>");
            }
        }
        protected void FormatGenericArguments(StyledStringBuilder output, Type[] genericArguments, int currentParameterIndex = -1)
        {
            if (genericArguments.Length > 0)
            {
                output.AppendRTF('<');
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

        protected void FormatPropertyGetSet(StyledStringBuilder output, PropertyInfo property)
        {
            output.Append(" { ");
            if (property.CanRead)
            {
                output.Append("get", Style.Keyword);
                output.Append("; ");
            }
            if (property.CanWrite)
            {
                output.Append("set", Style.Keyword);
                output.Append("; ");
            }
            output.Append('}');
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

        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            switch (memberInfo)
            {
                case FieldInfo fieldInfo:
                {
                    if (fieldInfo.IsLiteral)
                    {
                        output.Append("const ", Style.Keyword);
                    }
                    else
                    {
                        if (fieldInfo.IsStatic) output.Append("static ", Style.Keyword);
                        if (fieldInfo.IsInitOnly) output.Append("readonly ", Style.Keyword);
                    }
                    FormatTypeName(output, fieldInfo.FieldType);
                    output.Append(' ');
                    Highlight(output, fieldInfo.Name, len);
                }
                break;
                case PropertyInfo propertyInfo:
                {
                    FormatTypeName(output, propertyInfo.PropertyType);
                    output.Append(' ');
                    Highlight(output, propertyInfo.Name, len);
                    FormatPropertyGetSet(output, propertyInfo);
                }
                break;
                case EventInfo eventInfo:
                {
                    FormatTypeName(output, eventInfo.EventHandlerType);
                    output.Append(' ');
                    Highlight(output, eventInfo.Name, len);
                }
                break;
                case MethodInfo methodInfo:
                {
                    if (methodInfo.IsStatic) output.Append("static ", Style.Keyword);
                    FormatTypeName(output, methodInfo.ReturnType);
                    output.Append(' ');
                    Highlight(output, methodInfo.Name, len, Style.Method);
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
                    Highlight(output, memberInfo.Name, len);
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

        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            if (overload is ConstructorInfo constructor)
            {
                FormatTypeName(output, constructor.DeclaringType, TypeNameFlags.GenericArguments);
            }
            else if (overload is MethodInfo methodInfo)
            {
                if (methodInfo.IsStatic) output.Append("static ", Style.Keyword);
                FormatTypeName(output, methodInfo.ReturnType);
                if (delegateType == null)
                {
                    output.Append(' ');
                    output.Append(methodInfo.Name, Style.Method);
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

        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            if (generic is GenericType genericType)
            {
                output.Append(name, Styles.GetTypeColor(genericType.type));
                FormatGenericArguments(output, generic.GetGenericArguments(), currentParameterIndex);
            }
            else if (generic is GenericMethod genericMethod)
            {
                FormatTypeName(output, genericMethod.methodInfo.ReturnType);
                output.Append(' ');
                output.Append(name, Style.Method);
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
        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
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
        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            output.Append("namespace ", Style.Keyword);
            Highlight(output, identifier.Name, len);
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

        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            WritePrefix(output, type);
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
        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            FormatTypeName(output, type);
            output.Append(' ');
            Highlight(output, name, len, Style.Variable);
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
        public override void BuildSuggestionString(StyledStringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            Highlight(output, name, len, Style.Keyword);
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
                List<SuggestionObject> result = new();

                BindingFlags bindingFlags = Expression.MakeBindingFlags(isStatic, safeMode);
                MemberInfo[] members = Expression.FilterMembers(type.GetMembers(bindingFlags));
                for (int i = 0; i < members.Length; i++)
                {
                    MemberInfo member = members[i];

                    // Skip any members that dont start with the query
                    if (queryString.Length > 0 && !member.Name.StartsWith(queryString, StringComparison.OrdinalIgnoreCase)) continue;

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
                        if (queryString.Length > 0 && !nestedType.Name.StartsWith(queryString, StringComparison.OrdinalIgnoreCase)) continue;

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
                Dictionary<string, SuggestionObject> suggestions = new();
                string queryString = query.ToString();
                int queryLength = queryString.Length;
                bool hasQuery = queryLength > 0;
                foreach (var child in identifier.Children)
                {
                    if (!hasQuery || child.Name.StartsWith(queryString, StringComparison.OrdinalIgnoreCase))
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
                    if (!hasQuery || variables[i].name.StartsWith(queryString, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions[variables[i].name] = new VariableSuggestion(variables[i].type, variables[i].name, queryLength, usingNamespaces);
                    }
                }
                if (includeKeywords)
                {
                    foreach (string keyword in TokenUtility.Keywords)
                    {
                        if (!hasQuery || keyword.StartsWith(queryString, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions[keyword] = new KeywordSuggestion(keyword, queryLength, usingNamespaces);
                        }
                    }
                }
                if (suggestions.Count > 0)
                {
                    List<SuggestionObject> result = new(suggestions.Values);
                    result.Sort();
                    return result.ToArray();
                }
            }
            return Array.Empty<SuggestionObject>();
        }
    }

    internal sealed class MethodOverloadList : SuggestionInfo
    {
        public MethodOverloadList(IReadOnlyList<MethodBase> overloads, IReadOnlyList<Argument> args, Type delegateType = null) : base(StringView.Empty, 0, 0)
        {
            this.overloads = overloads;
            this.args = args;
            this.delegateType = delegateType;
        }

        private readonly IReadOnlyList<MethodBase> overloads;
        private readonly IReadOnlyList<Argument> args;
        private readonly Type delegateType;


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<SuggestionObject> result = new();

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
            List<SuggestionObject> result = new();

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
        public PropertyOverloadList(IReadOnlyList<PropertyInfo> properties, IReadOnlyList<Argument> args, Type declaringType) : base(StringView.Empty, 0, 0)
        {
            this.properties = properties;
            this.args = args;
            this.declaringType = declaringType;
        }

        private readonly IReadOnlyList<PropertyInfo> properties;
        private readonly IReadOnlyList<Argument> args;
        private readonly Type declaringType;
        public int CurrentArgumentIndex => args.Count;


        public override SuggestionObject[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<SuggestionObject> result = new();

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
                List<StyledToken> styledTokens = new();
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
                                _ => Style.None,
                            };
                        }
                        if (style != Style.None)
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
        public readonly int SuggestionCount => suggestions == null ? 0 : suggestions.Length;

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

            StyledStringBuilder styledOutput = new(output);

            // Underflow
            if (highlightOffset > 0)
            {
                styledOutput.Append($"\n< {highlightOffset} more results >");
                visibleLineCount++;
            }

            for (int i = 0; i < visibleCount; i++)
            {
                var suggestion = suggestions[highlightOffset + i];

                visible.Add(suggestion);

                styledOutput.Append('\n');
                suggestion.BuildSuggestionString(styledOutput, i == highlightIndex);
            }
            visibleLineCount += visibleCount;

            // Overflow
            if (overflow > 0)
            {
                styledOutput.Append($"\n< {overflow} more results >");
                visibleLineCount++;
            }

            text = styledOutput.ToString();
            return true;
        }
    }

    [Serializable]
    internal struct SuggestionResult
    {
        public static readonly SuggestionResult Empty = new()
        {
            commandText = string.Empty,
            commandStyle = Array.Empty<StyledToken>(),
            suggestionText = string.Empty,
            suggestions = Array.Empty<string>(),
            highlightOffset = -1,
            highlightIndex = -1,
        };

        public int id;
        public string commandText;
        public StyledToken[] commandStyle;
        public string suggestionText;
        public string[] suggestions;
        public int insertOffset;
        public int insertLength;
        public int visibleLineCount;
        public bool isOverloadList;
        public int highlightOffset;
        public int highlightIndex;
    }
}

#endif