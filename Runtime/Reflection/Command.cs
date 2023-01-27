// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AggroBird.Reflection
{
    internal struct VoidResult
    {
        public static readonly VoidResult Empty = new VoidResult();
    }

    internal readonly struct StyledToken
    {
        public StyledToken(StringView str, Style style)
        {
            this.str = str;
            this.style = style;
        }

        public readonly StringView str;
        public readonly Style style;
    }

    internal sealed class CommandParser : ExpressionParser
    {
        private enum LiteralType
        {
            Any,
            Float,
            UInt,
            Long,
            ULong,
        }

        public CommandParser(ArrayView<Token> tokens, Identifier identifierTable, bool safeMode, int maxIterationCount, int cursorPosition = -1) : base(tokens)
        {
            this.identifierTable = identifierTable;
            this.safeMode = safeMode;
            this.maxIterationCount = maxIterationCount;
            this.cursorPosition = cursorPosition;
            generateSuggestionInfo = cursorPosition >= 0;
            styledTokens = generateSuggestionInfo ? new List<StyledToken>() : null;
        }

        private bool expectSemicolon = false;

        private void ParseBlock()
        {
            TokenType next = Peek();
            if (next == TokenType.Semicolon)
            {
                expectSemicolon = false;
                Advance();
                return;
            }

            if (expectSemicolon) throw new DebugConsoleException("Expected ';' at the end of expression");

            switch (next)
            {
                case TokenType.LBrace:
                {
                    Advance();
                    Block block = new Block();
                    stack.Last().expressions.Add(block);
                    if (!Match(TokenType.RBrace))
                        Push(block);
                }
                break;

                case TokenType.RBrace:
                {
                    Advance();
                    Pop();
                }
                break;

                case TokenType.If:
                {
                    AddStyledToken(CurrentToken.str, Style.Keyword);

                    Advance();
                    Consume(TokenType.LParen);
                    Expression condition = ParseNext();
                    Expression.CheckConvertibleBool(condition, out condition);
                    Consume(TokenType.RParen);
                    IfBlock ifBlock = new IfBlock(condition);
                    stack.Last().expressions.Add(ifBlock);
                    ParseOptionalBlock(ifBlock);

                ParseNextIf:
                    if (Match(TokenType.Else, out Token elseToken))
                    {
                        AddStyledToken(elseToken.str, Style.Keyword);

                        if (Match(TokenType.If, out Token ifToken))
                        {
                            AddStyledToken(ifToken.str, Style.Keyword);

                            Consume(TokenType.LParen);
                            condition = ParseNext();
                            Expression.CheckConvertibleBool(condition, out condition);
                            Consume(TokenType.RParen);
                            ElseIfBlock elseIfBlock = new ElseIfBlock(condition);
                            ParseOptionalBlock(elseIfBlock);
                            ifBlock.AddSubBlock(elseIfBlock);

                            goto ParseNextIf;
                        }
                        else
                        {
                            ElseBlock elseBlock = new ElseBlock();
                            ParseOptionalBlock(elseBlock);
                            ifBlock.AddSubBlock(elseBlock);
                        }
                    }
                }
                break;

                case TokenType.For:
                {
                    AddStyledToken(CurrentToken.str, Style.Keyword);

                    Advance();
                    Consume(TokenType.LParen);
                    Expression init = ParseOptionalExpression(true, true);
                    Expression condition = ParseOptionalExpression(false, true);
                    if (condition != null) Expression.CheckConvertibleBool(condition, out condition);
                    Expression step = ParseOptionalExpression(false, false);
                    Consume(TokenType.RParen);
                    ForBlock forBlock = new ForBlock(init, condition, step, maxIterationCount);
                    stack.Last().expressions.Add(forBlock);
                    ParseOptionalBlock(forBlock);
                }
                break;

                case TokenType.While:
                {
                    AddStyledToken(CurrentToken.str, Style.Keyword);

                    Advance();
                    Consume(TokenType.LParen);
                    Expression condition = ParseNext();
                    Expression.CheckConvertibleBool(condition, out condition);
                    Consume(TokenType.RParen);
                    WhileBlock whileBlock = new WhileBlock(condition, maxIterationCount);
                    stack.Last().expressions.Add(whileBlock);
                    ParseOptionalBlock(whileBlock);
                }
                break;

                default:
                {
                    stack.Last().expressions.Add(ParseRootExpression());
                }
                break;
            }
        }
        private void ParseOptionalBlock(Block parent)
        {
            if (Match(TokenType.LBrace))
            {
                Push(parent);
                while (!Match(TokenType.RBrace))
                {
                    ParseBlock();
                }
                if (expectSemicolon) throw new UnexpectedEndOfExpressionException();
                Pop();
            }
            else
            {
                parent.expressions.Add(ParseRootExpression());
            }
        }
        private Expression ParseRootExpression()
        {
            Expression result = ParseNext();
            if (Peek() == TokenType.Identifier && result is Typename typename)
            {
                VariableDeclaration declaration;
                Token name = Consume();
                string varName = name.ToString();
                AddStyledToken(name.str, Style.Variable);
                if (Peek() == TokenType.Assign)
                {
                    Advance();
                    Expression rhs = ParseNext();
                    if (!Expression.IsImplicitConvertable(rhs, typename.type, out rhs))
                    {
                        throw new InvalidCastException(rhs.ResultType, typename.type);
                    }
                    declaration = new VariableAssignment(typename.type, varName, rhs);
                }
                else
                {
                    declaration = new VariableDeclaration(typename.type, varName);
                }
                variables.Last().Add(declaration);
                variableCount++;
                return declaration;
            }
            expectSemicolon = !Match(TokenType.Semicolon);
            return result;
        }
        private Expression ParseOptionalExpression(bool allowDeclaration, bool requireSemicolon)
        {
            TokenType next = Peek();
            Expression result;
            switch (next)
            {
                case TokenType.RParen:
                case TokenType.RBrace:
                case TokenType.RBracket:
                case TokenType.Semicolon:
                case TokenType.Comma:
                    result = null;
                    break;

                default:
                    result = allowDeclaration ? ParseRootExpression() : ParseNext();
                    break;
            }
            if (requireSemicolon) Consume(TokenType.Semicolon);
            return result;
        }

        private void Push(Block block)
        {
            stack.Add(block);
            variables.Add(new List<VariableDeclaration>());
        }
        private void Pop()
        {
            if (stack.Count == 1) throw new UnexpectedTokenException(Peek());
            variableCount -= variables.Last().Count;
            variables.PopBack();
            stack.PopBack();
        }


        private Block root = new Block();
        private List<Block> stack = new List<Block>();
        private List<List<VariableDeclaration>> variables = new List<List<VariableDeclaration>>();
        private int variableCount = 0;
        private VariableDeclaration[] ExportVariables()
        {
            if (variableCount == 0) return Array.Empty<VariableDeclaration>();

            List<VariableDeclaration> result = new List<VariableDeclaration>();
            for (int i = 0; i < variables.Count; i++)
            {
                if (variables[i].Count > 0)
                {
                    result.AddRange(variables[i]);
                }
            }
            return result.ToArray();
        }

        private readonly Identifier identifierTable;
        private readonly bool safeMode;
        private readonly int maxIterationCount;

        private bool isParsed = false;
        private Command result = null;

        private readonly int cursorPosition;
        private readonly bool generateSuggestionInfo;
        private bool GenerateSuggestionInfoAtToken(Token token)
        {
            if (generateSuggestionInfo)
            {
                if (token.str.Offset + token.str.Length == cursorPosition)
                {
                    return true;
                }
                else if (position > 0 && tokens[position - 1] == token)
                {
                    return tokens[position].str.Offset == cursorPosition;
                }
            }
            return false;
        }
        public SuggestionInfo SuggestionInfo { get; private set; } = null;

        private readonly List<StyledToken> styledTokens;
        public StyledToken[] GetStyledTokens()
        {
            if (!generateSuggestionInfo)
            {
                return Array.Empty<StyledToken>();
            }
            else
            {
                return styledTokens.ToArray();
            }
        }
        private void AddStyledToken(StringView str, Style style)
        {
            if (generateSuggestionInfo)
            {
                styledTokens.Add(new StyledToken(str, style));
            }
        }
        private void AddStyledToken(StringView str, Type type)
        {
            if (generateSuggestionInfo)
            {
                styledTokens.Add(new StyledToken(str, Styles.GetTypeColor(type)));
            }
        }


        public Command Parse()
        {
            if (isParsed) return result;
            isParsed = true;

            stack.Add(root);
            variables.Add(new List<VariableDeclaration>());

            while (true)
            {
                TokenType next = Peek();
                if (next == TokenType.Eol) break;
                ParseBlock();
            }

            if (stack.Count != 1) throw new UnexpectedEndOfExpressionException();

            result = new Command(root);
            return result;
        }


        private bool Identify(Token token, Identifier identifier, out Expression result)
        {
            if (identifier is NamespaceIdentifier namespaceIdentifier)
            {
                AddStyledToken(token.str, Style.Default);
                result = new Namespace(namespaceIdentifier);
                return true;
            }
            else if (identifier is TypeIdentifier typeIdentifier)
            {
                AddStyledToken(token.str, typeIdentifier.type);
                result = new Typename(typeIdentifier.type);
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        protected override Expression NameCallback(Token token)
        {
            if (GenerateSuggestionInfoAtToken(token))
            {
                SuggestionInfo = new IdentifierList(token.str, token.str.Offset, token.str.Length, identifierTable, ExportVariables(), true);
            }

            string varName = token.ToString();

            // Check local variables
            for (int i = variables.Count - 1; i >= 0; i--)
            {
                var scopeVars = variables[i];
                for (var j = scopeVars.Count - 1; j >= 0; j--)
                {
                    if (scopeVars[j].name == varName)
                    {
                        AddStyledToken(token.str, Style.Variable);
                        return scopeVars[j].Value;
                    }
                }
            }

            // Check identifier table
            if (identifierTable.TryFindIdentifier(varName, out Identifier identifier) && Identify(token, identifier, out Expression result))
            {
                return result;
            }

            throw new UnknownIdentifierException(token.str);
        }
        protected override Expression PrefixCallback(Token token)
        {
            TokenInfo info = TokenUtility.GetTokenInfo(token.type);

            if (info.IsPrefix)
            {
                Expression rhs = ParseNext(Precedence.Prefix);

                if (info.IsIncrement)
                {
                    if (!rhs.Assignable)
                    {
                        throw new DebugConsoleException("The right-hand side of an increment or decrement must be an assignable variable");
                    }
                }

                Expression result = Operators.MakeUnaryOperator(token.type, rhs);

                if (info.IsIncrement)
                {
                    return new Assignment(rhs, result);
                }

                return result;
            }

            throw new UnexpectedTokenException(token);
        }
        protected override Expression PostfixCallback(Expression lhs, Token token)
        {
            TokenInfo info = TokenUtility.GetTokenInfo(token.type);

            if (info.IsPostfix)
            {
                if (info.IsIncrement)
                {
                    if (!lhs.Assignable)
                    {
                        throw new DebugConsoleException("The left-hand side of an increment or decrement must be an assignable variable");
                    }
                }

                Expression result = Operators.MakeUnaryOperator(token.type, lhs);

                if (info.IsIncrement)
                {
                    return new Assignment(lhs, result, true);
                }

                return result;
            }

            throw new UnexpectedTokenException(token);
        }
        protected override Expression InfixCallback(Expression lhs, Token token)
        {
            if (token.type == TokenType.Period)
            {
                if (GenerateSuggestionInfoAtToken(token))
                {
                    switch (lhs)
                    {
                        case Namespace ns:
                            SuggestionInfo = new IdentifierList(StringView.Empty, token.str.End, 0, ns.identifier, Array.Empty<VariableDeclaration>(), false);
                            break;
                        case Typename typename:
                            SuggestionInfo = new MemberList(StringView.Empty, token.str.End, 0, typename.type, true);
                            break;
                        default:
                            if (lhs.ResultType != typeof(void))
                            {
                                SuggestionInfo = new MemberList(StringView.Empty, token.str.End, 0, lhs.ResultType, false);
                            }
                            break;
                    }
                }

                Token next = Consume(TokenType.Identifier);
                {
                    string query = next.str.ToString();
                    if (lhs is Namespace ns)
                    {
                        if (GenerateSuggestionInfoAtToken(next))
                        {
                            SuggestionInfo = new IdentifierList(next.str, next.str.Offset, next.str.Length, ns.identifier, Array.Empty<VariableDeclaration>(), false);
                        }

                        if (ns.identifier.TryFindIdentifier(query, out Identifier identifier) && Identify(next, identifier, out Expression result))
                        {
                            return result;
                        }
                    }
                    else if (lhs is Typename typename)
                    {
                        if (GenerateSuggestionInfoAtToken(next))
                        {
                            SuggestionInfo = new MemberList(next.str, next.str.Offset, next.str.Length, typename.type, true);
                        }

                        MemberInfo[] members = Expression.FilterMembers(typename.type.GetMember(query, MakeStaticBindingFlags()));
                        if (members == null || members.Length == 0)
                        {
                            Type nestedType = Expression.FilterMembers(typename.type.GetNestedType(query, MakeStaticBindingFlags()));
                            if (nestedType != null)
                            {
                                // Nested type
                                return new Typename(nestedType);
                            }

                            // No member found
                            throw new UnknownIdentifierException(next.str);
                        }
                        else if (members.Length > 1 || members[0] is MethodInfo)
                        {
                            // Multiple methods overload
                            AddStyledToken(next.str, Style.Method);
                            return new MethodOverload(query, MakeMethodOverloadList(members, next.str));
                        }
                        else if (members[0] is FieldInfo fieldInfo)
                        {
                            // Field
                            return new FieldMember(fieldInfo);
                        }
                        else if (members[0] is PropertyInfo propertyInfo)
                        {
                            // Property
                            return new PropertyMember(propertyInfo);
                        }
                        else if (members[0] is EventInfo eventInfo)
                        {
                            // Event
                            return new EventMember(eventInfo);
                        }
                    }
                    else
                    {
                        if (GenerateSuggestionInfoAtToken(next) && lhs.ResultType != typeof(void))
                        {
                            SuggestionInfo = new MemberList(next.str, next.str.Offset, next.str.Length, lhs.ResultType, false);
                        }

                        MemberInfo[] members = Expression.FilterMembers(lhs.ResultType.GetMember(query, MakeInstanceBindingFlags()));
                        if (lhs.ResultType == typeof(void)) throw new VoidTypeException();
                        if (members == null || members.Length == 0)
                        {
                            // No member found
                            throw new UnknownIdentifierException(next.str);
                        }
                        else if (members.Length > 1 || members[0] is MethodInfo)
                        {
                            // Multiple methods overload
                            AddStyledToken(next.str, Style.Method);
                            return new MethodOverload(query, lhs, MakeMethodOverloadList(members, next.str));
                        }
                        else if (members[0] is FieldInfo fieldInfo)
                        {
                            // Field
                            if (lhs is FieldMember append)
                            {
                                append.fields.Add(fieldInfo);
                                return append;
                            }
                            return new FieldMember(lhs, fieldInfo);
                        }
                        else if (members[0] is PropertyInfo propertyInfo)
                        {
                            // Property
                            return new PropertyMember(lhs, propertyInfo);
                        }
                        else if (members[0] is EventInfo eventInfo)
                        {
                            // Event
                            return new EventMember(lhs, eventInfo);
                        }
                    }
                    throw new UnknownIdentifierException(next.str);
                }
            }
            else
            {
                TokenInfo info = TokenUtility.GetTokenInfo(token.type);
                if (info.IsInfix)
                {
                    Expression rhs = ParseNext(GetGrammar(token.type).AssociativePrecedence);
                    switch (token.type)
                    {
                        case TokenType.Assign:
                            if (lhs.Assignable)
                            {
                                Expression.CheckImplicitConvertible(rhs, lhs.ResultType, out rhs);
                                return new Assignment(lhs, rhs);
                            }
                            throw new DebugConsoleException("The left-hand side of an assignment must be an assignable variable");

                        case TokenType.LogicalAnd:
                            Expression.CheckConvertibleBool(lhs, out lhs);
                            Expression.CheckConvertibleBool(rhs, out rhs);
                            return new LogicalAnd(lhs, rhs);

                        case TokenType.LogicalOr:
                            Expression.CheckConvertibleBool(lhs, out lhs);
                            Expression.CheckConvertibleBool(rhs, out rhs);
                            return new LogicalOr(lhs, rhs);

                        default:
                            TokenType op = token.type;
                            if (info.IsCompound)
                            {
                                if (lhs is EventMember eventMember)
                                {
                                    if (Expression.IsImplicitConvertable(rhs, eventMember.eventInfo.EventHandlerType, out rhs))
                                    {
                                        switch (op)
                                        {
                                            case TokenType.CompoundAdd: return new EventAdd(eventMember, rhs);
                                            case TokenType.CompoundSub: return new EventRemove(eventMember, rhs);
                                            default: throw new UnexpectedTokenException(token);
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidCastException(rhs.ResultType, eventMember.eventInfo.EventHandlerType);
                                    }
                                }

                                if (!lhs.Assignable)
                                {
                                    throw new DebugConsoleException("The left-hand side of an assignment must be an assignable variable");
                                }

                                if (lhs.ResultType.IsSubclassOf(typeof(Delegate)))
                                {
                                    if (Expression.IsImplicitConvertable(rhs, lhs.ResultType, out rhs))
                                    {
                                        switch (op)
                                        {
                                            case TokenType.CompoundAdd: return new DelegateAdd(lhs, rhs);
                                            case TokenType.CompoundSub: return new DelegateRemove(lhs, rhs);
                                            default: throw new UnexpectedTokenException(token);
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidCastException(rhs.ResultType, lhs.ResultType);
                                    }
                                }

                                // Replace compound with regular operator
                                switch (op)
                                {
                                    case TokenType.CompoundAdd: op = TokenType.Add; break;
                                    case TokenType.CompoundSub: op = TokenType.Sub; break;
                                    case TokenType.CompoundMul: op = TokenType.Mul; break;
                                    case TokenType.CompoundDiv: op = TokenType.Div; break;
                                    case TokenType.CompoundMod: op = TokenType.Mod; break;
                                    case TokenType.CompoundLsh: op = TokenType.Lsh; break;
                                    case TokenType.CompoundRsh: op = TokenType.Rsh; break;
                                    case TokenType.CompoundAnd: op = TokenType.BitwiseAnd; break;
                                    case TokenType.CompoundXor: op = TokenType.BitwiseXor; break;
                                    case TokenType.CompoundOr: op = TokenType.BitwiseOr; break;
                                    default: throw new UnexpectedTokenException(token);
                                }
                            }

                            Expression result = Operators.MakeInfixOperator(op, lhs, rhs);

                            if (info.IsCompound)
                            {
                                // Write back value
                                if (lhs.ResultType != result.ResultType)
                                {
                                    if (!Expression.IsExplicitConvertable(result, lhs.ResultType, out result))
                                    {
                                        throw new InvalidCastException(result.ResultType, lhs.ResultType);
                                    }
                                }
                                return new Assignment(lhs, result);
                            }
                            return result;
                    }
                }
            }

            throw new UnexpectedTokenException(token);
        }
        protected override Expression CallCallback(Expression lhs, Token token)
        {
            if (lhs is MethodOverload methodOverload)
            {
                Expression[] args = ParseMethodArguments(token, methodOverload.methods);

                MethodInfo[] optimal = Expression.GetOptimalOverloads(methodOverload.methods, args);

                if (optimal.Length == 0) throw new DebugConsoleException($"No compatible overload found for method '{methodOverload.methodName}'");
                if (optimal.Length != 1) throw new DebugConsoleException($"Ambiguous overloads for method '{methodOverload.methodName}'");

                return new MethodMember(methodOverload.lhs, optimal[0], args);
            }
            else if (lhs is Typename typename)
            {
                BindingFlags flags = MakeInstanceBindingFlags();
                ConstructorInfo[] constructors = Expression.FilterMembers(typename.type.GetConstructors(MakeInstanceBindingFlags()), true);

                Expression[] args = ParseMethodArguments(token, constructors);

                if (typename.type.IsValueType && args.Length == 0)
                {
                    return new DefaultConstructor(typename.type);
                }

                ConstructorInfo[] optimal = Expression.GetOptimalOverloads(constructors, args);

                if (optimal.Length == 0) throw new DebugConsoleException($"No compatible constructor found for type '{typename.type}'");
                if (optimal.Length != 1) throw new DebugConsoleException($"Ambiguous constructors for type '{typename.type}'");

                return new Constructor(typename.type, optimal[0], args);
            }
            else if (lhs.ResultType.IsSubclassOf(typeof(Delegate)))
            {
                MethodInfo[] overloads = new MethodInfo[] { lhs.ResultType.GetMethod("Invoke") };

                Expression[] args = ParseMethodArguments(token, overloads, lhs.ResultType);

                MethodInfo[] optimal = Expression.GetOptimalOverloads(overloads, args);

                if (optimal.Length == 0) throw new DebugConsoleException($"Arguments are not compatible with delegate '{lhs.ResultType}'");

                return new DelegateInvoke(lhs, overloads[0], args);
            }
            throw new UnexpectedTokenException(token);
        }
        protected override Expression SubscriptCallback(Expression lhs, Token token)
        {
            if (lhs is Typename typename)
            {
                if (Match(TokenType.RBracket))
                {
                    // One-dimensional array
                    return new Typename(typename.type.MakeArrayType(1));
                }
                else if (Peek() == TokenType.Comma)
                {
                    // Multi-dimensional array
                    int rank = ParseArrayRank();
                    return new Typename(typename.type.MakeArrayType(rank));
                }
                else
                {
                    // Array alloc
                    Expression[] lengths = ParseArguments(TokenType.RBracket);
                    Expression.CheckImplicitConvertible(lengths, typeof(int));
                    return new ArrayConstructor(typename.type.MakeArrayType(lengths.Length), lengths);
                }
            }
            else
            {
                // Try get subscript operator from array
                PropertyInfo[] properties;
                if (lhs.ResultType.BaseType == typeof(Array))
                {
                    properties = Expression.GetArraySubscriptProperties(lhs.ResultType);
                }
                else if (lhs.ResultType == typeof(string))
                {
                    properties = new PropertyInfo[] { lhs.ResultType.GetProperty("Chars") };
                }
                else
                {
                    properties = Expression.GetSubscriptProperties(lhs.ResultType, safeMode);
                }

                Expression[] args = ParseSubscriptArguments(token, properties, lhs.ResultType);

                PropertyInfo[] optimal = Expression.GetOptimalOverloads(properties, args);

                if (optimal.Length == 0) throw new DebugConsoleException($"No compatible subscript operator found for type '{lhs.ResultType}'");
                if (optimal.Length != 1) throw new DebugConsoleException($"Ambiguous subscript operator for type '{lhs.ResultType}'");

                return new Subscript(lhs, args, optimal[0]);
            }
        }
        protected override Expression LiteralCallback(Token token)
        {
            switch (token.type)
            {
                case TokenType.StringLiteral:
                    AddStyledToken(token.str, Style.String);
                    return new BoxedObject(FormatStringLiteral(token.str));

                case TokenType.CharLiteral:
                    AddStyledToken(token.str, Style.String);
                    return new BoxedObject(FormatCharLiteral(token.str));

                case TokenType.NumberLiteral:
                {
                    AddStyledToken(token.str, Style.Number);

                    string value = token.str.ToString();

                    int numBase = 10;

                    int idx = 0;
                    if (value.Length > 2)
                    {
                        if (value[1] == 'x' || value[1] == 'X')
                        {
                            numBase = 16;
                            idx += 2;
                        }
                        else if (value[1] == 'b' || value[1] == 'B')
                        {
                            numBase = 2;
                            idx += 2;
                        }
                    }
                    int prefix = idx;

                    LiteralType literalType = LiteralType.Any;
                    bool isFloat = false;
                    switch (numBase)
                    {
                        case 16:
                        {
                            for (; idx < value.Length; idx++)
                            {
                                char c = value[idx];
                                if (c >= '0' && c <= '9') continue;
                                if (c >= 'a' && c <= 'f') continue;
                                if (c >= 'A' && c <= 'F') continue;
                                goto ParsePostfix;
                            }
                        }
                        break;

                        case 2:
                        {
                            for (; idx < value.Length; idx++)
                            {
                                char c = value[idx];
                                switch (c)
                                {
                                    case '0':
                                    case '1':
                                        continue;

                                    default:
                                        goto ParsePostfix;
                                }
                            }
                        }
                        break;

                        default:
                        {
                            bool isExp = false;
                            char prev = '\0';
                            for (; idx < value.Length; idx++)
                            {
                                char c = value[idx];
                                switch (c)
                                {
                                    case '.':
                                        // Ensure single .
                                        if (isFloat) goto InvalidLiteral;
                                        isFloat = true;
                                        break;

                                    case 'e':
                                    case 'E':
                                        // Ensure exponential
                                        if (isExp) goto InvalidLiteral;
                                        isFloat = isExp = true;
                                        break;

                                    case '-':
                                        // Ensure correct usage of negative exp
                                        if (prev != 'e' && prev != 'E') goto InvalidLiteral;
                                        break;

                                    default:
                                        // Check for valid numbers
                                        if (!(c >= '0' && c <= '9')) goto ParsePostfix;
                                        break;
                                }
                                prev = c;
                            }
                        }
                        break;
                    }

                ParsePostfix:;
                    int postFixLength = value.Length - idx;
                    if (postFixLength > 0)
                    {
                        switch (value[idx])
                        {
                            case 'f':
                            case 'F':
                                // Float
                                if (numBase != 10) goto InvalidLiteral;
                                idx++;
                                literalType = LiteralType.Float;
                                break;

                            case 'l':
                            case 'L':
                                // Long
                                idx++;
                                literalType = LiteralType.Long;
                                break;

                            case 'u':
                            case 'U':
                                idx++;
                                if (idx < value.Length)
                                {
                                    switch (value[idx])
                                    {
                                        case 'l':
                                        case 'L':
                                            // Ulong
                                            idx++;
                                            literalType = LiteralType.ULong;
                                            break;
                                    }
                                }
                                else
                                {
                                    // Uint
                                    literalType = LiteralType.UInt;
                                }
                                break;
                        }

                        // Ensure we are at the end
                        if (idx != value.Length) goto InvalidLiteral;
                    }

                    int subLen = value.Length - postFixLength - prefix;
                    if (subLen <= 0) goto InvalidLiteral;
                    string sub = value.Substring(prefix, subLen);
                    if (isFloat)
                    {
                        if (double.TryParse(sub, out double result))
                        {
                            switch (literalType)
                            {
                                case LiteralType.Float: return new BoxedObject((float)result);
                                case LiteralType.Any: return new BoxedObject(result);
                            }
                        }
                    }
                    else
                    {
                        ulong result = 0;
                        switch (numBase)
                        {
                            case 16:
                            {
                                // Base 16
                                if (!ulong.TryParse(sub, System.Globalization.NumberStyles.HexNumber, null, out result))
                                {
                                    goto InvalidLiteral;
                                }
                            }
                            break;

                            case 2:
                            {
                                // Base 2
                                if (sub.Length > 64) goto InvalidLiteral;
                                for (int i = 0; i < sub.Length; i++)
                                {
                                    if (sub[i] == '1') result |= (ulong)1 << (sub.Length - i - 1);
                                }
                            }
                            break;

                            default:
                            {
                                // Any other format
                                if (!ulong.TryParse(sub, out result))
                                {
                                    goto InvalidLiteral;
                                }
                            }
                            break;
                        }

                        switch (literalType)
                        {
                            // Force to specified format
                            case LiteralType.Float: return new BoxedObject((float)result);
                            case LiteralType.UInt: return new BoxedObject((uint)result);
                            case LiteralType.Long: return new BoxedObject((long)result);
                            case LiteralType.ULong: return new BoxedObject(result);
                            case LiteralType.Any:
                                // Pick smallest fitting literal
                                if (result <= int.MaxValue) return new BoxedObject((int)result);
                                if (result <= uint.MaxValue) return new BoxedObject((uint)result);
                                if (result <= long.MaxValue) return new BoxedObject((long)result);
                                return new BoxedObject(result);
                        }
                    }

                InvalidLiteral:
                    throw new InvalidLiteralException(value);
                }
                default:
                    throw new UnexpectedTokenException(token);
            }
        }
        protected override Expression KeywordCallback(Token token)
        {
            AddStyledToken(token.str, Style.Keyword);

            if (GenerateSuggestionInfoAtToken(token))
            {
                SuggestionInfo = new IdentifierList(token.str, token.str.Offset, token.str.Length, identifierTable, Array.Empty<VariableDeclaration>(), true);
            }

            switch (token.type)
            {
                case TokenType.Null: return new Null();
                case TokenType.True: return new BoxedObject(true);
                case TokenType.False: return new BoxedObject(false);

                case TokenType.Typeof:
                {
                    // Get typeof
                    if (Match(TokenType.LParen))
                    {
                        Expression expr = ParseNext();
                        if (expr is Typename typename && Match(TokenType.RParen))
                            return new BoxedObject(typename.type);
                        else
                            throw new DebugConsoleException("typeof expects a type as argument");
                    }
                    throw new UnexpectedTokenException(token);
                }

                case TokenType.Bool: return new Typename(typeof(bool));
                case TokenType.Char: return new Typename(typeof(char));
                case TokenType.Void: return new Typename(typeof(void));
                case TokenType.Byte: return new Typename(typeof(byte));
                case TokenType.SByte: return new Typename(typeof(sbyte));
                case TokenType.Short: return new Typename(typeof(short));
                case TokenType.UShort: return new Typename(typeof(ushort));
                case TokenType.Int: return new Typename(typeof(int));
                case TokenType.UInt: return new Typename(typeof(uint));
                case TokenType.Long: return new Typename(typeof(long));
                case TokenType.ULong: return new Typename(typeof(ulong));
                case TokenType.Float: return new Typename(typeof(float));
                case TokenType.Double: return new Typename(typeof(double));
                case TokenType.Decimal: return new Typename(typeof(decimal));
                case TokenType.String: return new Typename(typeof(string));
                case TokenType.Object: return new Typename(typeof(object));

                default: throw new UnexpectedTokenException(token);
            }
        }
        protected override Expression GroupCallback(Token token)
        {
            Expression expr = ParseNext();
            Consume(TokenType.RParen);
            if (expr is Typename typename)
            {
                Expression rhs = ParseNext(Precedence.Prefix);
                if (!Expression.IsExplicitConvertable(rhs, typename.type, out Expression cast))
                {
                    throw new DebugConsoleException($"Unable to cast type '{rhs.ResultType}' to '{typename.type}'");
                }
                return cast;
            }
            return expr;
        }
        protected override Expression ConditionalCallback(Expression lhs, Token token)
        {
            Expression.CheckConvertibleBool(lhs, out lhs);

            Expression consequent = ParseNext();
            Consume(TokenType.Colon);
            Expression alternative = ParseNext(GetGrammar(token.type).AssociativePrecedence);

            if (consequent.ResultType == alternative.ResultType)
            {
                return new Conditional(lhs, consequent, alternative, consequent.ResultType);
            }
            else
            {
                if (Expression.IsImplicitConvertable(consequent, alternative.ResultType, out Expression c0))
                {
                    return new Conditional(lhs, c0, alternative, consequent.ResultType);
                }
                else if (Expression.IsImplicitConvertable(alternative, consequent.ResultType, out Expression c1))
                {
                    return new Conditional(lhs, consequent, c1, alternative.ResultType);
                }
                else
                {
                    throw new DebugConsoleException($"Type of conditional expression cannot be determined because there is no implicit conversion between '{consequent.ResultType}' and '{alternative.ResultType}'");
                }
            }
        }

        private readonly StringBuilder stringBuilder = new StringBuilder();
        private string FormatStringLiteral(StringView str)
        {
            if (str[str.Length - 1] != '"') throw new UnexpectedEndOfExpressionException();
            if (str.Length == 2) return string.Empty;
            str = str.SubView(1, str.Length - 2);

            stringBuilder.Clear();

            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (c == '\\')
                {
                    stringBuilder.Append(ParseCharacterEscape(str, ref i));
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }
        private char FormatCharLiteral(StringView str)
        {
            if (str[str.Length - 1] != '\'') throw new UnexpectedEndOfExpressionException();
            if (str.Length == 2) throw new DebugConsoleException("Empty character literal");
            str = str.SubView(1, str.Length - 2);

            if (str[0] == '\\')
            {
                int idx = 0;
                char c = ParseCharacterEscape(str, ref idx);
                if (idx != str.Length - 1) throw new DebugConsoleException("Too many characters in character literal");
                return c;
            }
            else
            {
                if (str.Length > 1) throw new DebugConsoleException("Too many characters in character literal");
                return str[0];
            }
        }
        private char ParseCharacterEscape(StringView str, ref int idx)
        {
            if (idx == str.Length - 1) throw new IndexOutOfRangeException();
            char c = str[++idx];
            switch (c)
            {
                case '0': return '\0';
                case '\'': return '\'';
                case '\"': return '\"';
                case '\\': return '\\';
                case 'a': return '\a';
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'v': return '\v';
                case 'u':
                {
                    int remaining = str.Length - idx;
                    if (remaining >= 5 && uint.TryParse(str.SubView(idx + 1, 4).ToString(), System.Globalization.NumberStyles.AllowHexSpecifier, null, out uint charCode))
                    {
                        idx += 4;
                        return (char)charCode;
                    }
                }
                break;
            }
            throw new DebugConsoleException($"Unexpected character escape code: {c}");
        }


        private static List<MethodInfo> MakeMethodOverloadList(MemberInfo[] members, StringView methodName)
        {
            List<MethodInfo> result = new List<MethodInfo>();
            for (int i = 0; i < members.Length; i++)
            {
                if (!(members[i] is MethodInfo method))
                {
                    throw new DebugConsoleException($"Ambigious member '{methodName}' for type '{members[i].DeclaringType}'");
                }
                if (method.IsGenericMethod) continue;
                result.Add(method);
            }
            return result;
        }

        private Expression[] ParseMethodArguments(Token token, IReadOnlyList<MethodBase> overloads, Type delegateType = null)
        {
            if (GenerateSuggestionInfoAtToken(token))
            {
                SuggestionInfo = new MethodOverloadList(overloads, Array.Empty<Expression>(), delegateType);
            }

            TokenType closingToken = TokenType.RParen;
            if (!Match(closingToken))
            {
                List<Expression> args = new List<Expression>();
                while (true)
                {
                    args.Add(ParseNext());
                    Token next = Consume();
                    if (next.type == closingToken)
                        break;
                    else if (next.type != TokenType.Comma)
                        throw new UnexpectedTokenException(next);

                    if (GenerateSuggestionInfoAtToken(next))
                    {
                        SuggestionInfo = new MethodOverloadList(overloads, args, delegateType);
                    }
                }
                return args.ToArray();
            }
            return Array.Empty<Expression>();
        }
        private Expression[] ParseSubscriptArguments(Token token, IReadOnlyList<PropertyInfo> properties, Type declaringType)
        {
            if (GenerateSuggestionInfoAtToken(token))
            {
                SuggestionInfo = new PropertyOverloadList(properties, Array.Empty<Expression>(), declaringType);
            }

            TokenType closingToken = TokenType.RBracket;
            if (!Match(closingToken))
            {
                List<Expression> args = new List<Expression>();
                while (true)
                {
                    args.Add(ParseNext());
                    Token next = Consume();
                    if (next.type == closingToken)
                        break;
                    else if (next.type != TokenType.Comma)
                        throw new UnexpectedTokenException(next);

                    if (GenerateSuggestionInfoAtToken(next))
                    {
                        SuggestionInfo = new PropertyOverloadList(properties, args, declaringType);
                    }
                }
                return args.ToArray();
            }
            return Array.Empty<Expression>();
        }
        private Expression[] ParseArguments(TokenType closingToken)
        {
            if (!Match(closingToken))
            {
                List<Expression> args = new List<Expression>();
                while (true)
                {
                    args.Add(ParseNext());
                    Token next = Consume();
                    if (next.type == closingToken)
                        break;
                    else if (next.type != TokenType.Comma)
                        throw new UnexpectedTokenException(next);
                }
                return args.ToArray();
            }
            return Array.Empty<Expression>();
        }
        private int ParseArrayRank()
        {
            int rank = 1;
            while (true)
            {
                Token next = Consume();
                if (next.type == TokenType.RBracket)
                    break;
                else if (next.type != TokenType.Comma)
                    throw new UnexpectedTokenException(next);
                rank++;
            }
            return rank;
        }

        private BindingFlags MakeInstanceBindingFlags() => Expression.MakeBindingFlags(false, safeMode);
        private BindingFlags MakeStaticBindingFlags() => Expression.MakeBindingFlags(true, safeMode);

        protected override Precedence PeekNextPrecedence(Expression lhs)
        {
            if (Peek() == TokenType.Comma) return Precedence.Root;
            return base.PeekNextPrecedence(lhs);
        }
    }

    internal sealed class Command
    {
        public Command(Block root)
        {
            this.root = root;
        }

        private readonly Block root;


        public object Execute()
        {
            ExecutionContext context = new ExecutionContext();

            return root.Execute(context);
        }
    }
}
#endif