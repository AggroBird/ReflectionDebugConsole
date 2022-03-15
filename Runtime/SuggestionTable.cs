// Copyright, 2021, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AggroBird.DebugConsole
{
    internal struct Suggestion
    {
        public Suggestion(MemberInfo member, string text, int len, int offset)
        {
            this.member = member;
            fullText = text;
            this.len = len;
            this.offset = offset;

            hasOffset = offset > 0;
            offsetText = hasOffset ? fullText.Substring(offset) : fullText;
        }

        // Suggestion member
        public MemberInfo member { get; private set; }

        // Full suggestion string
        public string fullText { get; private set; }
        // Length of highlighted substring
        public int len { get; private set; }

        // Offsets are added when using a namespace (used namespace shows as grey in suggestion)
        public int offset { get; private set; }
        public bool hasOffset { get; private set; }
        public string offsetText { get; private set; }
    }

    internal class SuggestionTable
    {
        private const string ColorGreyOpen = "<color=#979797>";
        private const string ColorWhiteOpen = "<color=#FFFFFF>";
        private const string ColorClose = "</color>";

        public string text = string.Empty;
        public List<Suggestion> allSuggestions = new List<Suggestion>();
        public List<Suggestion> visibleSuggestions = new List<Suggestion>();

        // Amount of suggestion lines (not equal to amount of allSuggestions in case of over/under flow)
        public int lineCount = 0;
        // Actual user input
        public string input = string.Empty;
        // User curser position
        public int position = 0;
        // Query (incomplete input behind the .)
        public string query = string.Empty;
        // Full query (all tokens including subquery)
        public string fullQuery = string.Empty;
        // This indicates that we are not suggesting members but parameters
        public bool isOverloadSuggestion = false;
        // Index of the current parameter we are suggesting
        public int parameterIndex = 0;

        private struct SearchQuery
        {
            public string text;
            // Offset for the highlight (in the case of a using namespace)
            public int offset;
        }

        public SuggestionTable()
        {

        }
        public SuggestionTable(string inputStr, int inputPos, Assembly[] assemblies, string[] usingNamespaces, bool safeMode, StringBuilder stringBuilder = null)
        {
            if (stringBuilder == null) stringBuilder = new StringBuilder();

            input = inputStr;
            position = inputPos;

            Command command = new Command();
            command.Parse(input);

            int charIdx = position - 1;
            char current = charIdx >= 0 && charIdx < input.Length ? input[charIdx] : char.MinValue;

            // Find the closest expression to the cursor
            CmdExpression currentExpression = null;
            foreach (var expr in command.GetExpressions())
            {
                int tokenCount = expr.tokens.Count;
                for (int i = 0; i < tokenCount; i++)
                {
                    var token = expr.tokens[i];
                    int end = token.pos + token.len;
                    if (position == end && (token.tokenType == CmdTokenType.Dereference || (token.tokenType == CmdTokenType.Declaration && !char.IsDigit(token.str[0]))))
                    {
                        // Nearest uncompleted token
                        for (int j = i; j < tokenCount; j++)
                        {
                            expr.tokens.RemoveAt(i);
                        }
                        query = token.str;
                        currentExpression = expr;
                        goto EvaluateExpression;
                    }
                    else if (position == (end + 1) && current == '.')
                    {
                        // Nearest dereference
                        for (int j = i + 1; j < tokenCount; j++)
                        {
                            expr.tokens.RemoveAt(i + 1);
                        }
                        currentExpression = expr;
                        goto EvaluateExpression;
                    }
                }
            }

            // Check if we should suggest parameters instead of members
            if (currentExpression == null)
            {
                // Determine if we are in a method (there is probably a better way for this)
                for (int i = position - 1; i >= 0; i--)
                {
                    char c = input[i];
                    if (char.IsWhiteSpace(c)) continue;
                    if (c != '[' && c != '(' && c != ',') goto EvaluateExpression;
                    break;
                }

                // Find the nearest method
                foreach (var expr in command.GetExpressions().Reverse())
                {
                    int tokenCount = expr.tokens.Count;
                    for (int i = 0; i < tokenCount; i++)
                    {
                        var token = expr.tokens[i];
                        // Only pick methods that are either unclosed or envelop the cursor
                        if (token.tokenType == CmdTokenType.Invoke && token.pos < position && (token.len == token.str.Length || token.pos + token.len > position))
                        {
                            // We are currently in a method, change to overload suggestion mode
                            query = token.str;
                            currentExpression = expr;
                            isOverloadSuggestion = true;
                            parameterIndex = token.argCount;
                            goto EvaluateExpression;
                        }
                    }
                }
            }

        EvaluateExpression:
            if (currentExpression != null)
            {
                // Interpret the command, disregarding any errors.
                command.Interpret(assemblies, usingNamespaces, safeMode, false);
                // Explicitly interpret the command we are currently trying to generate suggestions for
                currentExpression.Interpret(command.context);

                var tokens = currentExpression.tokens;

                // Build full query
                stringBuilder.Clear();
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (stringBuilder.Length != 0) stringBuilder.Append('.');
                    stringBuilder.Append(tokens[i].str);
                }
                if (!isOverloadSuggestion)
                {
                    if (stringBuilder.Length != 0) stringBuilder.Append('.');
                    stringBuilder.Append(query);
                }
                fullQuery = stringBuilder.ToString();

                // If we are not in a chain of dereferences (e.g. an invoke), we can skip scanning the assemblies
                foreach (var token in tokens)
                {
                    if (token.tokenType != CmdTokenType.Dereference)
                    {
                        goto SkipAssemblies;
                    }
                }

                // Scan assemblies and add type suggestions
                List<SearchQuery> queries = new List<SearchQuery>();
                queries.Add(new SearchQuery { text = fullQuery });
                foreach (var ns in usingNamespaces)
                {
                    queries.Add(new SearchQuery { text = $"{ns}.{fullQuery}", offset = ns.Length + 1 });
                }
                foreach (Assembly assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsNested && !type.ContainsGenericParameters && ReflectionUtility.CanExecute(type) && (type.IsPublic || !safeMode))
                        {
                            foreach (var query in queries)
                            {
                                if (type.FullName.StartsWith(query.text, true, null))
                                {
                                    AddSuggestion(type.GetTypeInfo(), query.offset);
                                    break;
                                }
                            }
                        }
                    }
                }

            SkipAssemblies:
                if (tokens.Count == 0 || (tokens.Count == 1 && isOverloadSuggestion))
                {
                    // Get methods in Environment
                    foreach (var method in typeof(Environment).GetMethods(ReflectionUtility.StaticFlags))
                    {
                        if (method.IsSpecialName) continue;

                        if (isOverloadSuggestion)
                        {
                            if (method.Name == query)
                            {
                                AddSuggestion(method);
                            }
                        }
                        else if (method.Name.StartsWith(query, true, null))
                        {
                            AddSuggestion(method);
                        }
                    }

                    if (!isOverloadSuggestion)
                    {
                        // Get keyword helpers in Environment
                        foreach (var property in typeof(Environment).GetProperties(ReflectionUtility.StaticFlags))
                        {
                            if (property.Name.StartsWith(query, true, null))
                            {
                                AddSuggestion(property);
                            }
                        }
                    }
                }

                if (tokens.Count > 0)
                {
                    Type type = null;

                    var last = tokens[tokens.Count - 1];
                    if (isOverloadSuggestion)
                    {
                        if (last.obj != null)
                        {
                            // If either one of these cases is true, we should highlight constructor parameters
                            if (last.obj is Type asType)
                                type = asType;
                            else if (last.obj is DefaultConstructorInfo asDefaultConstructorInfo)
                                type = asDefaultConstructorInfo.DeclaringType;
                            else if (last.obj is ConstructorInfo constructorInfo)
                                type = constructorInfo.DeclaringType;

                            if (type != null)
                            {
                                // Value types have no default constructor, but can be constructed as such
                                if (type.IsValueType)
                                {
                                    AddSuggestion(new DefaultConstructorInfo(type));
                                }

                                // Get all callable constructors
                                foreach (ConstructorInfo constructor in type.GetConstructors(ReflectionUtility.GetInstanceFlags(!safeMode)))
                                {
                                    if (last.CanInvoke(constructor, MatchParameterCount.LessThan))
                                    {
                                        AddSuggestion(constructor);
                                    }
                                }
                                goto SkipMembers;
                            }
                        }
                    }

                    bool isStatic = false;
                    if (isOverloadSuggestion)
                    {
                        // In the case of an overload suggestion, we need to take the node before the current one
                        // else we end up evaluating the method's return type
                        if (tokens.Count > 1)
                        {
                            var beforeLast = tokens[tokens.Count - 2];
                            if (beforeLast.isDereferenceable)
                            {
                                type = beforeLast.resultType;
                                isStatic = beforeLast.isStatic;
                            }
                        }
                    }
                    else if (last.isDereferenceable)
                    {
                        type = last.resultType;
                        isStatic = last.isStatic;
                    }

                    if (type != null)
                    {
                        BindingFlags bindingFlags = ReflectionUtility.GetBindingFlags(isStatic, !safeMode);

                    SearchBaseType:
                        // Iterate all members recursively
                        MemberTypes mask = MemberTypes.Field | MemberTypes.Method | MemberTypes.Property | MemberTypes.NestedType;
                        var memberInfo = type.GetMembers(bindingFlags);
                        foreach (var member in memberInfo)
                        {
                            if ((member.MemberType & mask) != 0 && member.DeclaringType == type)
                            {
                                MethodBase asMethodBase = member as MethodBase;
                                if (asMethodBase != null)
                                {
                                    // Skip property methods
                                    if (asMethodBase.IsSpecialName)
                                    {
                                        continue;
                                    }

                                    if (asMethodBase is MethodInfo asMethodInfo)
                                    {
                                        // Skip overrides for virtual methods
                                        if (asMethodInfo.IsVirtual)
                                        {
                                            var baseDefinition = asMethodInfo.GetBaseDefinition();
                                            if (!baseDefinition.Equals(asMethodBase))
                                            {
                                                continue;
                                            }
                                        }

                                        // Skip generic methods
                                        if (asMethodInfo.ContainsGenericParameters)
                                        {
                                            continue;
                                        }

                                        // Skip interface methods
                                        if (asMethodInfo.Name.Contains('.'))
                                        {
                                            continue;
                                        }

                                        // Skip methods with references as parameter
                                        foreach (var parameter in asMethodInfo.GetParameters())
                                        {
                                            if (parameter.ParameterType.IsByRef)
                                            {
                                                continue;
                                            }
                                        }
                                    }
                                }

                                // Skip generated types
                                if (member.Name[0] == '<')
                                {
                                    continue;
                                }

                                if (member is TypeInfo typeInfo)
                                {
                                    // Skip generic types
                                    if (typeInfo.ContainsGenericParameters)
                                    {
                                        continue;
                                    }
                                }

                                if (isOverloadSuggestion)
                                {
                                    if (member is FieldInfo fieldInfo && typeof(Delegate).IsAssignableFrom(fieldInfo.FieldType))
                                    {
                                        // Use delegate invoke method
                                        asMethodBase = fieldInfo.FieldType.GetMethod("Invoke", ReflectionUtility.InstanceFlags);
                                        if (last.CanInvoke(asMethodBase, MatchParameterCount.LessThan))
                                        {
                                            AddSuggestion(asMethodBase, overrideName: fieldInfo.FieldType.Name);
                                        }
                                    }
                                    else
                                    {
                                        if (asMethodBase != null && member.Name == query && last.CanInvoke(asMethodBase, MatchParameterCount.LessThan))
                                        {
                                            AddSuggestion(member);
                                        }
                                    }
                                }
                                else if (query.Length == 0 || member.Name.StartsWith(query, true, null))
                                {
                                    AddSuggestion(member);
                                }
                            }
                        }

                        if (type.BaseType != null)
                        {
                            type = type.BaseType;
                            goto SearchBaseType;
                        }
                    }
                }

            SkipMembers:
                // Sort
                allSuggestions.Sort((lhs, rhs) => lhs.fullText.CompareTo(rhs.fullText));
            }
        }

        private void AddSuggestion(MemberInfo member, int offset = 0, string overrideName = null)
        {
            bool useOverride = overrideName != null;

            if (member is TypeInfo typeInfo && !typeInfo.IsNested)
            {
                int len = useOverride ? overrideName.Length : fullQuery.Length;
                allSuggestions.Add(new Suggestion(member, useOverride ? overrideName : typeInfo.FullName, len, offset));
            }
            else
            {
                int len = useOverride ? overrideName.Length : query.Length;
                if (member is ConstructorInfo constructorInfo)
                {
                    allSuggestions.Add(new Suggestion(constructorInfo, useOverride ? overrideName : query, len, offset));
                }
                else
                {
                    allSuggestions.Add(new Suggestion(member, useOverride ? overrideName : member.Name, len, offset));
                }
            }
        }

        private int currentHighlightOffset = -1;
        private int currentHighlightIndex = -1;

        public bool UpdateSuggestionIndex(ref int highlightOffset, ref int highlightIndex, int direction, int maxCount, StringBuilder stringBuilder = null)
        {
            if (stringBuilder == null) stringBuilder = new StringBuilder();

            visibleSuggestions.Clear();

            // Clamp direction
            direction = direction > 0 ? 1 : direction < 0 ? -1 : direction;

            if (highlightOffset < 0) highlightOffset = 0;

            int overflow = 0;
            int visibleCount = lineCount = allSuggestions.Count;

            if (visibleCount <= maxCount)
            {
                // No overflow
                highlightOffset = 0;
            }
            else
            {
                lineCount = maxCount;
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

            stringBuilder.Clear();

            // Underflow
            if (highlightOffset > 0)
            {
                stringBuilder.Append($"\n< {highlightOffset} more results >");
            }

            stringBuilder.Append(ColorGreyOpen);
            for (int idx = 0; idx < visibleCount; idx++)
            {
                var suggestion = allSuggestions[highlightOffset + idx];
                visibleSuggestions.Add(suggestion);

                var member = suggestion.member;

                if (stringBuilder.Length > 0) stringBuilder.Append('\n');

                MethodBase method = member as MethodBase;
                if (method != null && method is MethodInfo methodInfo)
                {
                    stringBuilder.Append(ReflectionUtility.GetTypeName(methodInfo.ReturnType));
                }
                else if (member is ConstructorInfo constructorInfo)
                    AppendTypeName(stringBuilder, constructorInfo.DeclaringType);
                else if (member is FieldInfo fieldInfo)
                    stringBuilder.Append(ReflectionUtility.GetTypeName(fieldInfo.FieldType));
                else if (member is PropertyInfo propertyInfo)
                {
                    // Environment type helpers
                    if (propertyInfo.DeclaringType.Equals(typeof(Environment)))
                    {
                        if (propertyInfo.PropertyType == typeof(Type))
                            AppendTypeName(stringBuilder, propertyInfo.GetValue(null) as Type);
                        else
                            stringBuilder.Append(ReflectionUtility.GetTypeName(propertyInfo.PropertyType));
                    }
                    else
                        stringBuilder.Append(ReflectionUtility.GetTypeName(propertyInfo.PropertyType));
                }
                else if (member is TypeInfo typeInfo)
                {
                    // Global type
                    AppendTypeName(stringBuilder, typeInfo);
                }
                stringBuilder.Append(' ');

                bool hasOffset = suggestion.offset > 0;
                if (hasOffset)
                {
                    // Apply offset first (using namespace)
                    stringBuilder.Append(suggestion.fullText.Substring(0, suggestion.offset));
                }

                if (idx == highlightIndex)
                {
                    // Highlight current
                    stringBuilder.Append(ColorWhiteOpen);
                    stringBuilder.Append(suggestion.offsetText);
                    stringBuilder.Append(ColorClose);
                }
                else if (suggestion.len > 0)
                {
                    // Highlight only matching
                    stringBuilder.Append(ColorWhiteOpen);
                    stringBuilder.Append(suggestion.offsetText.Substring(0, suggestion.len));
                    stringBuilder.Append(ColorClose);
                    stringBuilder.Append(suggestion.fullText.Substring(suggestion.len + suggestion.offset));
                }
                else
                {
                    // No highlight
                    stringBuilder.Append(suggestion.fullText);
                }

                if (method != null)
                {
                    // Print parameters
                    stringBuilder.Append('(');
                    var parameters = method.GetParameters();
                    int last = parameters.Length - 1;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var parameter = parameters[i];
                        bool highlight = isOverloadSuggestion && (i == parameterIndex || (i == last && parameterIndex >= parameters.Length));

                        if (highlight) stringBuilder.Append(ColorWhiteOpen);

                        if (i != 0) stringBuilder.Append(", ");
                        if (parameter.GetCustomAttribute<ParamArrayAttribute>(true) != null)
                        {
                            stringBuilder.Append("params ");
                        }
                        stringBuilder.Append(ReflectionUtility.GetTypeName(parameter.ParameterType));
                        if (!string.IsNullOrEmpty(parameter.Name))
                        {
                            stringBuilder.Append(' ');
                            stringBuilder.Append(parameter.Name);
                        }

                        if (highlight) stringBuilder.Append(ColorClose);
                    }
                    stringBuilder.Append(')');
                }
            }
            stringBuilder.Append(ColorClose);

            // Overflow
            if (overflow > 0)
            {
                stringBuilder.Append($"\n< {overflow} more results >");
            }

            text = stringBuilder.ToString();

            return true;
        }
        private static void AppendTypeName(StringBuilder stringBuilder, Type type)
        {
            if (type.IsEnum)
                stringBuilder.Append("enum");
            else if (type.IsInterface)
                stringBuilder.Append("interface");
            else if (type.IsClass)
                stringBuilder.Append("class");
            else if (type.IsValueType)
                stringBuilder.Append("struct");
        }
    }
}
#endif
