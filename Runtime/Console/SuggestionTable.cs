// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AggroBird.DebugConsole
{
    internal abstract class Suggestion
    {
        public Suggestion(IReadOnlyList<string> usingNamespaces)
        {
            this.usingNamespaces = usingNamespaces;
        }

        public abstract string Text { get; }
        public abstract void BuildSuggestionString(StringBuilder output, bool isHighlighted);

        protected readonly IReadOnlyList<string> usingNamespaces;


        private static readonly string Null = $"{Colors.Open(Color.Keyword)}null{Colors.Close}";
        private static readonly string True = $"{Colors.Open(Color.Keyword)}true{Colors.Close}";
        private static readonly string False = $"{Colors.Open(Color.Keyword)}false{Colors.Close}";

        protected void Stringify(Type type, object value, StringBuilder output)
        {
            if (value == null)
            {
                if (type.IsValueType)
                {
                    FormatTypeName(type, output);
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
                FormatTypeName(type, output);
                output.Append(value.ToString());
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Char:
                    output.Append($"{Colors.Open(Color.String)}'{value}'{Colors.Close}");
                    return;
                case TypeCode.String:
                    output.Append($"{Colors.Open(Color.String)}\"{value}\"{Colors.Close}");
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
                    output.Append($"{Colors.Open(Color.Number)}{value}{Colors.Close}");
                    return;

                case TypeCode.Boolean:
                    output.Append((bool)value ? True : False);
                    return;
            }

            output.Append(value.ToString());
        }

        protected static Color GetTypeColor(Type type)
        {
            if (type.IsEnum || type.IsInterface || type.IsGenericParameter)
                return Color.Enum;
            else if (type.IsClass)
                return Color.Class;
            else if (type.IsValueType)
                return Color.Struct;
            else
                return Color.White;
        }
        protected static string Highlight(string str, int len, Color color = Color.White)
        {
            if (len >= str.Length)
            {
                return $"{Colors.Open(color)}<b>{str}</b>{Colors.Close}";
            }
            else if (len > 0)
            {
                return $"{Colors.Open(color)}<b>{str.Substring(0, len)}</b>{str.Substring(len)}{Colors.Close}";
            }
            else
            {
                return $"{Colors.Open(color)}{str}{Colors.Close}";
            }
        }

        protected static string GetPrefix(Type type)
        {
            string result = Expression.GetPrefix(type);
            if (string.IsNullOrEmpty(result)) return result;
            return $"{Colors.Open(Color.Keyword)}{result}{Colors.Close}";
        }


        protected void FormatTypeName(Type type, StringBuilder output, int highlight = 0)
        {
            if (type.IsGenericParameter)
            {
                output.Append(Highlight(type.Name, highlight, GetTypeColor(type)));
                return;
            }

            if (TokenUtility.TryGetBaseTypeName(type, out string baseTypeName))
            {
                output.Append(Highlight(baseTypeName, highlight, Color.Keyword));
                return;
            }

            if (type.IsArray)
            {
                FormatTypeName(type.GetElementType(), output, highlight);
                output.Append('[');
                int rank = type.GetArrayRank();
                for (int i = 0; i < rank - 1; i++)
                {
                    output.Append(',');
                }
                output.Append(']');
                return;
            }

            string fullName = type.FullName;
            if (type.IsGenericType)
            {
                fullName = fullName.Substring(0, fullName.IndexOf('`'));
            }

            // Strip longest using namespace
            if (usingNamespaces != null && usingNamespaces.Count > 0)
            {
                int longestNamespace = 0;
                for (int i = 0; i < usingNamespaces.Count; i++)
                {
                    if (fullName.StartsWith(usingNamespaces[i]))
                    {
                        longestNamespace = Math.Max(longestNamespace, usingNamespaces[i].Length);
                    }
                }
                if (longestNamespace > 0)
                {
                    fullName = fullName.Substring(longestNamespace + 1);
                }
            }

            // Split highlight color for namespace
            int last = fullName.LastIndexOf('.');
            if (last >= 0)
            {
                string typeNamespace = fullName.Substring(0, last + 1);
                string typeName = fullName.Substring(last + 1);
                output.Append(typeNamespace);
                output.Append(Highlight(typeName, highlight, GetTypeColor(type)));
            }
            else
            {
                output.Append(Highlight(fullName, highlight, GetTypeColor(type)));
            }

            if (type.IsGenericType)
            {
                Type[] genericArgs = type.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    output.Append('<');
                    for (int i = 0; i < genericArgs.Length; i++)
                    {
                        if (i != 0) output.Append(", ");
                        FormatTypeName(genericArgs[i], output);
                    }
                    output.Append('>');
                }
            }
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

        private static readonly string GetStr = $" {{ {Colors.Open(Color.Keyword)}get{Colors.Close}; }}";
        private static readonly string SetStr = $" {{ {Colors.Open(Color.Keyword)}set{Colors.Close}; }}";
        private static readonly string GetSetStr = $" {{ {Colors.Open(Color.Keyword)}get{Colors.Close}; {Colors.Open(Color.Keyword)}set{Colors.Close}; }}";


        public override string Text => memberInfo.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            if (memberInfo is FieldInfo field)
            {
                FormatTypeName(field.FieldType, output);
                output.Append($" {Highlight(field.Name, len)}");
            }
            else if (memberInfo is PropertyInfo property)
            {
                FormatTypeName(property.PropertyType, output);
                output.Append($" {Highlight(property.Name, len)}");
                if (property.CanWrite && property.CanRead) output.Append(GetSetStr);
                else if (property.CanWrite) output.Append(SetStr);
                else if (property.CanRead) output.Append(GetStr);
            }
            else if (memberInfo is MethodInfo method)
            {
                FormatTypeName(method.ReturnType, output);
                output.Append($" {Highlight(method.Name, len, Color.Method)}(");
                ParameterInfo[] parameters = method.GetParameters();
                int varArgParam = Expression.HasVariableParameterCount(method) ? parameters.Length - 1 : -1;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0) output.Append(", ");
                    if (i == varArgParam) output.Append($"{Colors.Open(Color.Keyword)}params{Colors.Close} ");
                    FormatTypeName(parameters[i].ParameterType, output);
                    output.Append($" {Colors.Open(Color.Variable)}{parameters[i].Name}{Colors.Close}");
                    if (parameters[i].HasDefaultValue)
                    {
                        output.Append(" = ");
                        Stringify(parameters[i].ParameterType, parameters[i].DefaultValue, output);
                    }
                }
                output.Append(')');
            }
            else if (memberInfo is Type type)
            {
                output.Append(GetPrefix(type));
                FormatTypeName(type, output, len);
            }
            else
            {
                output.Append(Highlight(memberInfo.Name, len));
            }
        }
    }

    internal class OverloadSuggestion : Suggestion
    {
        public OverloadSuggestion(MethodBase overload, int currentParameterIndex, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.overload = overload;
            this.currentParameterIndex = currentParameterIndex;
        }

        private readonly MethodBase overload;
        private readonly int currentParameterIndex;


        public override string Text => overload.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            if (overload is ConstructorInfo constructor)
            {
                output.Append($"{Colors.Open(GetTypeColor(constructor.DeclaringType))}{constructor.DeclaringType.Name}{Colors.Close}(");
            }
            else if (overload is MethodInfo method)
            {
                FormatTypeName(method.ReturnType, output);
                output.Append($" {Colors.Open(Color.Method)}{method.Name}{Colors.Close}(");
            }

            ParameterInfo[] parameters = overload.GetParameters();
            int currentParamIdx = currentParameterIndex;
            if (currentParamIdx >= parameters.Length) currentParamIdx = parameters.Length - 1;
            int varArgParam = Expression.HasVariableParameterCount(overload) ? parameters.Length - 1 : -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) output.Append(", ");
                if (i == currentParamIdx) output.Append("<b>");
                if (i == varArgParam) output.Append($"{Colors.Open(Color.Keyword)}params{Colors.Close} ");
                FormatTypeName(parameters[i].ParameterType, output);
                output.Append($" {Colors.Open(Color.Variable)}{parameters[i].Name}{Colors.Close}");
                if (parameters[i].HasDefaultValue)
                {
                    output.Append($" = ");
                    Stringify(parameters[i].ParameterType, parameters[i].DefaultValue, output);
                }
                if (i == currentParamIdx) output.Append("</b>");
            }
            output.Append(')');
        }
    }

    internal class NamespaceSuggestion : Suggestion
    {
        public NamespaceSuggestion(NamespaceIdentifier identifier, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.identifier = identifier;
            this.highlightLength = highlightLength;
        }

        private readonly NamespaceIdentifier identifier;
        private readonly int highlightLength;

        public override string Text => identifier.Name;
        public override void BuildSuggestionString(StringBuilder output, bool isHighlighted)
        {
            int len = isHighlighted ? int.MaxValue : highlightLength;
            output.Append($"{Colors.Open(Color.Keyword)}namespace {Colors.Close}{Highlight(identifier.Name, len)}");
        }
    }

    internal class TypeSuggestion : Suggestion
    {
        public TypeSuggestion(Type type, int highlightLength, IReadOnlyList<string> usingNamespaces) : base(usingNamespaces)
        {
            this.type = type;
            this.highlightLength = highlightLength;
            if (type.ContainsGenericParameters)
            {
                name = type.Name.Substring(0, type.Name.IndexOf('`'));
            }
            else
            {
                name = type.Name;
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
            FormatTypeName(type, output, len);
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
            FormatTypeName(type, output);
            output.Append($" {Highlight(name, len, Color.Variable)}");
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
            output.Append($"{Highlight(name, len, Color.Keyword)}");
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

                MemberInfo[] members = type.GetMembers(isStatic ? CommandParser.MakeStaticBindingFlags(safeMode) : CommandParser.MakeInstanceBindingFlags(safeMode));
                for (int i = 0; i < members.Length; i++)
                {
                    MemberInfo member = members[i];

                    // Skip any members that dont start with the query
                    if (queryString.Length > 0 && !member.Name.StartsWith(queryString, true, null)) continue;

                    // Skip types (only available in static context)
                    if (member is Type && !isStatic) continue;

                    if (!Expression.IncludeMember(member)) continue;

                    result.Add(new MemberSuggestion(member, queryLength, usingNamespaces));
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
                if (identifier.Children != null)
                {
                    foreach (var pair in identifier.Children)
                    {
                        Identifier child = pair.Value;

                        if (!hasQuery || child.Name.StartsWith(queryString, true, null))
                        {
                            if (child is NamespaceIdentifier namespaceIdentifier)
                                suggestions.Add(child.Name, new NamespaceSuggestion(namespaceIdentifier, queryLength, usingNamespaces));
                            else if (child is TypeIdentifier typeIdentifier)
                                suggestions.Add(child.Name, new TypeSuggestion(typeIdentifier.type, queryLength, usingNamespaces));
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
                    List<Suggestion> result = new List<Suggestion>();
                    foreach (var suggestion in suggestions)
                    {
                        result.Add(suggestion.Value);
                    }
                    result.Sort((lhs, rhs) => lhs.Text.CompareTo(rhs.Text));
                    return result.ToArray();
                }
            }
            return Array.Empty<Suggestion>();
        }
    }

    internal sealed class OverloadList : SuggestionInfo
    {
        public OverloadList(IReadOnlyList<MethodBase> overloads, IReadOnlyList<Expression> args) : base(StringView.Empty, 0, 0)
        {
            this.overloads = overloads;
            this.args = args;
        }

        private readonly IReadOnlyList<MethodBase> overloads;
        private readonly IReadOnlyList<Expression> args;
        public int CurrentArgumentIndex => args.Count;


        public override Suggestion[] GetSuggestions(int cursorPosition, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            List<Suggestion> result = new List<Suggestion>();

            int currentArgumentIndex = CurrentArgumentIndex;
            for (int i = 0; i < overloads.Count; i++)
            {
                if (Expression.IsCompatibleOverload(overloads[i], args, false))
                {
                    if (Expression.HasVariableParameterCount(overloads[i]) || currentArgumentIndex < overloads[i].GetParameters().Length)
                    {
                        result.Add(new OverloadSuggestion(overloads[i], currentArgumentIndex, usingNamespaces));
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

    internal struct SuggestionTable
    {
        public static readonly SuggestionTable Empty = new SuggestionTable();


        public SuggestionTable(string input, int cursorPosition, Identifier identifierTable, IReadOnlyList<string> usingNamespaces, bool safeMode)
        {
            suggestions = Array.Empty<Suggestion>();
            isOverloadList = false;

            this.input = input;
            insertOffset = 0;
            insertLength = 0;

            visible = new List<Suggestion>();
            text = string.Empty;

            currentHighlightOffset = -1;
            currentHighlightIndex = -1;

            if (cursorPosition < input.Length)
            {
                // Strip input behind the cursor, it cannot affect the suggestions
                input = input.Substring(0, cursorPosition);
            }

            CommandParser commandParser = null;
            try
            {
                ArrayView<Token> tokens = new Lexer(input).ToArray();
                commandParser = new CommandParser(tokens, identifierTable, safeMode, 0, true);
            }
            catch { }

            if (commandParser != null && commandParser.tokens.Length >= 2)
            {
                try { commandParser.Parse(); } catch { }

                SuggestionInfo suggestionInfo = commandParser.SuggestionInfo;
                if (suggestionInfo != null)
                {
                    suggestions = suggestionInfo.GetSuggestions(cursorPosition, usingNamespaces, safeMode);
                    isOverloadList = suggestionInfo is OverloadList;
                    insertOffset = suggestionInfo.insertOffset;
                    insertLength = suggestionInfo.insertLength;
                }
            }
        }


        public readonly Suggestion[] suggestions;
        public readonly bool isOverloadList;

        // The string used to build the suggestion table
        public readonly string input;
        // Offset and length of the string in the input that needs to be replaced when inserting a suggestion
        public readonly int insertOffset;
        public readonly int insertLength;

        // List of currently visible suggestions
        public readonly List<Suggestion> visible;
        public string text;

        private int currentHighlightOffset;
        private int currentHighlightIndex;

        public bool Update(ref int highlightOffset, ref int highlightIndex, int direction, int maxCount, StringBuilder output)
        {
            if (suggestions == null || suggestions.Length == 0) return false;

            visible.Clear();

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

            output.Clear();

            // Underflow
            if (highlightOffset > 0)
            {
                output.Append($"\n< {highlightOffset} more results >");
            }

            output.Append(Colors.Open(Color.White));
            for (int i = 0; i < visibleCount; i++)
            {
                var suggestion = suggestions[highlightOffset + i];

                visible.Add(suggestion);

                if (output.Length > 0) output.Append('\n');
                suggestion.BuildSuggestionString(output, i == highlightIndex);
            }
            output.Append(Colors.Close);

            // Overflow
            if (overflow > 0)
            {
                output.Append($"\n< {overflow} more results >");
            }

            text = output.ToString();
            return true;
        }

        public static implicit operator bool(SuggestionTable table)
        {
            return table.suggestions != null && table.suggestions.Length > 0;
        }
    }
}

#endif