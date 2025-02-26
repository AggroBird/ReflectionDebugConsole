// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AggroBird.Reflection
{
    internal enum Precedence
    {
        Root = 0,
        Comma, // ,
        Compound, // a?b:c, =, += -= etc.
        LogicalOr, // ||
        LogicalAnd, // &&
        BitwiseOr, // |
        BitwiseXor, // ^
        BitwiseAnd, // &
        Equality, // == !=
        Relational, // < <= > >=
        Threeway, // <=>
        Shift, // << >>
        Sum, // + -
        Product, // * / %
        Prefix, // ++a
        Postfix, // a++
        Primary, // .
        Call, // a() a[]
    }

    internal enum TokenFamily
    {
        Seperator,
        Operator,
        Keyword,
        Identifier,
        Literal,
    }

    internal enum OperatorType
    {
        None = 0,
        Prefix = 1 << 0,
        Postfix = 1 << 1,
        Infix = 1 << 2,
        Compound = 1 << 3,
        Comparison = 1 << 4,
        Bitwise = 1 << 5,
        Shift = 1 << 6,
        Increment = 1 << 7,
    }

    internal enum TokenType
    {
        [TokenInfo(TokenFamily.Seperator)]
        Eol,

        // Seperators
        [TokenInfo("(", TokenFamily.Seperator)]
        LParen,
        [TokenInfo(")", TokenFamily.Seperator)]
        RParen,
        [TokenInfo("{", TokenFamily.Seperator)]
        LBrace,
        [TokenInfo("}", TokenFamily.Seperator)]
        RBrace,
        [TokenInfo("[", TokenFamily.Seperator)]
        LBracket,
        [TokenInfo("]", TokenFamily.Seperator)]
        RBracket,
        [TokenInfo(";", TokenFamily.Seperator)]
        Semicolon,

        [TokenInfo(".", TokenFamily.Operator)]
        Period,

        [TokenInfo("++", TokenFamily.Operator, operatorType: OperatorType.Increment | OperatorType.Prefix | OperatorType.Postfix, unaryOperatorName: "op_Increment")]
        Increment,
        [TokenInfo("--", TokenFamily.Operator, operatorType: OperatorType.Increment | OperatorType.Prefix | OperatorType.Postfix, unaryOperatorName: "op_Decrement")]
        Decrement,
        [TokenInfo("!", TokenFamily.Operator, operatorType: OperatorType.Prefix, unaryOperatorName: "op_LogicalNot")]
        LogicalNot,
        [TokenInfo("~", TokenFamily.Operator, operatorType: OperatorType.Prefix | OperatorType.Bitwise, unaryOperatorName: "op_OnesComplement")]
        BitwiseNot,

        // Precedence.Product
        [TokenInfo("*", TokenFamily.Operator, operatorType: OperatorType.Infix, infixOperatorName: "op_Multiply")]
        Mul,
        [TokenInfo("/", TokenFamily.Operator, operatorType: OperatorType.Infix, infixOperatorName: "op_Division")]
        Div,
        [TokenInfo("%", TokenFamily.Operator, operatorType: OperatorType.Infix, infixOperatorName: "op_Modulus")]
        Mod,

        // Precedence.Sum
        [TokenInfo("+", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Prefix, unaryOperatorName: "op_UnaryPlus", infixOperatorName: "op_Addition")]
        Add,
        [TokenInfo("-", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Prefix, unaryOperatorName: "op_UnaryNegation", infixOperatorName: "op_Subtraction")]
        Sub,

        // Precedence.Shift
        [TokenInfo("<<", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Bitwise | OperatorType.Shift, infixOperatorName: "op_LeftShift")]
        Lsh,
        [TokenInfo(">>", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Bitwise | OperatorType.Shift, infixOperatorName: "op_RightShift")]
        Rsh,

        // Precedence.Relational
        [TokenInfo("<", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Comparison, infixOperatorName: "op_LessThan")]
        Lt,
        [TokenInfo(">", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Comparison, infixOperatorName: "op_GreaterThan")]
        Gt,
        [TokenInfo("<=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Comparison, infixOperatorName: "op_LessThanOrEqual")]
        Le,
        [TokenInfo(">=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Comparison, infixOperatorName: "op_GreaterThanOrEqual")]
        Ge,

        [TokenInfo("is", TokenFamily.Keyword, operatorType: OperatorType.Infix | OperatorType.Comparison)]
        Is,
        [TokenInfo("as", TokenFamily.Keyword, operatorType: OperatorType.Infix | OperatorType.Comparison)]
        As,

        // Precedence.Equality
        [TokenInfo("==", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Comparison, infixOperatorName: "op_Equality")]
        Eq,
        [TokenInfo("!=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Comparison, infixOperatorName: "op_Inequality")]
        Ne,

        [TokenInfo("&", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Bitwise, infixOperatorName: "op_BitwiseAnd")]
        BitwiseAnd,
        [TokenInfo("^", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Bitwise, infixOperatorName: "op_BitwiseOr")]
        BitwiseXor,
        [TokenInfo("|", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Bitwise, infixOperatorName: "op_ExclusiveOr")]
        BitwiseOr,
        [TokenInfo("&&", TokenFamily.Operator, operatorType: OperatorType.Infix)]
        LogicalAnd,
        [TokenInfo("||", TokenFamily.Operator, operatorType: OperatorType.Infix)]
        LogicalOr,

        // Precedence.Compound
        [TokenInfo("=", TokenFamily.Operator, operatorType: OperatorType.Infix)]
        Assign,
        [TokenInfo("?", TokenFamily.Operator)]
        Condition,
        [TokenInfo(":", TokenFamily.Operator)]
        Colon,
        [TokenInfo("+=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound)]
        CompoundAdd,
        [TokenInfo("-=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound)]
        CompoundSub,
        [TokenInfo("*=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound)]
        CompoundMul,
        [TokenInfo("/=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound)]
        CompoundDiv,
        [TokenInfo("%=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound)]
        CompoundMod,
        [TokenInfo("<<=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound | OperatorType.Bitwise | OperatorType.Shift)]
        CompoundLsh,
        [TokenInfo(">>=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound | OperatorType.Bitwise | OperatorType.Shift)]
        CompoundRsh,
        [TokenInfo("&=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound | OperatorType.Bitwise)]
        CompoundAnd,
        [TokenInfo("^=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound | OperatorType.Bitwise)]
        CompoundXor,
        [TokenInfo("|=", TokenFamily.Operator, operatorType: OperatorType.Infix | OperatorType.Compound | OperatorType.Bitwise)]
        CompoundOr,

        // Precedence.Comma
        [TokenInfo(",", TokenFamily.Operator)]
        Comma,

        // Keywords
        [TokenInfo("null", TokenFamily.Keyword)]
        Null,
        [TokenInfo("true", TokenFamily.Keyword)]
        True,
        [TokenInfo("false", TokenFamily.Keyword)]
        False,
        [TokenInfo("typeof", TokenFamily.Keyword)]
        Typeof,

        [TokenInfo("bool", TokenFamily.Keyword)]
        Bool,
        [TokenInfo("char", TokenFamily.Keyword)]
        Char,
        [TokenInfo("void", TokenFamily.Keyword)]
        Void,
        [TokenInfo("byte", TokenFamily.Keyword)]
        Byte,
        [TokenInfo("sbyte", TokenFamily.Keyword)]
        SByte,
        [TokenInfo("short", TokenFamily.Keyword)]
        Short,
        [TokenInfo("ushort", TokenFamily.Keyword)]
        UShort,
        [TokenInfo("int", TokenFamily.Keyword)]
        Int,
        [TokenInfo("uint", TokenFamily.Keyword)]
        UInt,
        [TokenInfo("long", TokenFamily.Keyword)]
        Long,
        [TokenInfo("ulong", TokenFamily.Keyword)]
        ULong,
        [TokenInfo("float", TokenFamily.Keyword)]
        Float,
        [TokenInfo("double", TokenFamily.Keyword)]
        Double,
        [TokenInfo("decimal", TokenFamily.Keyword)]
        Decimal,
        [TokenInfo("string", TokenFamily.Keyword)]
        String,
        [TokenInfo("object", TokenFamily.Keyword)]
        Object,

        [TokenInfo("var", TokenFamily.Keyword)]
        Var,

        [TokenInfo("if", TokenFamily.Keyword)]
        If,
        [TokenInfo("else", TokenFamily.Keyword)]
        Else,
        [TokenInfo("for", TokenFamily.Keyword)]
        For,
        [TokenInfo("while", TokenFamily.Keyword)]
        While,
        [TokenInfo("foreach", TokenFamily.Keyword)]
        Foreach,
        [TokenInfo("break", TokenFamily.Keyword)]
        Break,
        [TokenInfo("continue", TokenFamily.Keyword)]
        Continue,
        [TokenInfo("in", TokenFamily.Keyword)]
        In,
        [TokenInfo("out", TokenFamily.Keyword)]
        Out,
        [TokenInfo("ref", TokenFamily.Keyword)]
        Ref,
        [TokenInfo("new", TokenFamily.Keyword)]
        New,

        [TokenInfo("not", TokenFamily.Keyword)]
        Not,

        // Names
        [TokenInfo(TokenFamily.Identifier)]
        Identifier,

        // Literals
        [TokenInfo(TokenFamily.Literal)]
        NumberLiteral,
        [TokenInfo(TokenFamily.Literal)]
        StringLiteral,
        [TokenInfo(TokenFamily.Literal)]
        CharLiteral,

        Invalid,
    }

    internal enum Decorator
    {
        None,
        In,
        Out,
        Ref,
    }

    internal class DebugConsoleException : Exception
    {
        public DebugConsoleException(string msg) : base(msg)
        {

        }
    }

    internal class InvalidTokenException : DebugConsoleException
    {
        public InvalidTokenException(Token token) : base($"Invalid token '{token.str}'")
        {

        }
    }

    internal class InvalidLiteralException : DebugConsoleException
    {
        public InvalidLiteralException(string literal) : base($"Invalid literal '{literal}'")
        {

        }
    }

    internal class UnexpectedTokenException : DebugConsoleException
    {
        public UnexpectedTokenException(TokenType tokenType) : base(tokenType == TokenType.Eol ? "Unexpected end of expression" : $"Unexpected token '{Token.Stringify(tokenType)}'")
        {

        }
        public UnexpectedTokenException(Token token) : base(token.type == TokenType.Eol ? "Unexpected end of expression" : $"Unexpected token '{Token.Stringify(token)}'")
        {

        }
        public UnexpectedTokenException(char c) : base($"Unexpected token '{c}'")
        {

        }
    }

    internal class UnexpectedEndOfExpressionException : DebugConsoleException
    {
        public UnexpectedEndOfExpressionException() : base("Unexpected end of expression")
        {

        }
    }

    internal class UnknownIdentifierException : DebugConsoleException
    {
        public UnknownIdentifierException(StringView name) : base($"Unknown identifier '{name}'")
        {

        }
        public UnknownIdentifierException(TokenType tokenType) : base($"Unexpected token '{Token.Stringify(tokenType)}'")
        {

        }
    }

    internal class VoidTypeException : DebugConsoleException
    {
        public VoidTypeException() : base("Void cannot be dereferenced or used as an argument")
        {

        }
    }

    internal class InvalidConversionException : DebugConsoleException
    {
        public InvalidConversionException(Type srcType, Type dstType) : base($"Cannot implicitly convert type '{srcType}' to '{dstType}'")
        {

        }
    }

    internal class NullResultException : DebugConsoleException
    {
        public NullResultException() : base("Object reference not set to an instance of an object")
        {

        }
    }

    internal readonly struct Token
    {
        public static readonly Token Empty = new Token(TokenType.Identifier, StringView.Empty);

        public Token(StringView identifier, uint line = 0)
        {
            type = TokenType.Identifier;
            str = identifier;
            this.line = line;
        }
        public Token(TokenType type, uint line = 0)
        {
            if (type >= TokenType.Identifier)
            {
                throw new ArgumentException($"Cannot construct token from TokenType.{type}");
            }

            this.type = type;
            str = Stringify(type);
            this.line = line;
        }
        public Token(TokenType type, StringView str, uint line = 0)
        {
            this.type = type;
            this.str = str;
            this.line = line;
        }

        public static implicit operator Token(string identifier)
        {
            return new Token(identifier);
        }
        public static implicit operator Token(StringView identifier)
        {
            return new Token(identifier);
        }
        public static implicit operator Token(TokenType type)
        {
            return new Token(type);
        }

        public readonly TokenType type;
        public readonly StringView str;
        public readonly uint line;

        public TokenFamily Family => TokenUtility.GetTokenInfo(type).family;

        public override bool Equals(object obj)
        {
            if (obj is Token token)
            {
                return Equals(token);
            }
            return false;
        }
        public bool Equals(Token token)
        {
            if (type != token.type)
            {
                return false;
            }

            if (type < TokenType.Identifier)
            {
                return true;
            }

            return str.Equals(token.str);
        }

        public static bool operator ==(Token lhs, Token rhs)
        {
            return lhs.Equals(rhs);
        }
        public static bool operator !=(Token lhs, Token rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override int GetHashCode()
        {
            int result = type.GetHashCode();
            if (type >= TokenType.Identifier)
            {
                result ^= str.GetHashCode();
            }
            return result;
        }

        public override string ToString()
        {
            return type >= TokenType.Identifier ? str.ToString() : Stringify(type);
        }
        public static string Stringify(Token token)
        {
            return token.ToString();
        }
        public static string Stringify(TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.Identifier:
                    return "identifier";
                case TokenType.NumberLiteral:
                case TokenType.StringLiteral:
                    return "literal";
                default:
                    return TokenUtility.GetTokenInfo(tokenType).str;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal class TokenInfoAttribute : Attribute
    {
        public TokenInfoAttribute(string str, TokenFamily family, OperatorType operatorType = OperatorType.None, string unaryOperatorName = null, string infixOperatorName = null)
        {
            this.str = str;
            this.family = family;
            this.operatorType = operatorType;
            this.unaryOperatorName = unaryOperatorName;
            this.infixOperatorName = infixOperatorName;
        }
        public TokenInfoAttribute(TokenFamily family, OperatorType operatorType = OperatorType.None, string unaryOperatorName = null, string infixOperatorName = null)
        {
            str = string.Empty;
            this.family = family;
            this.operatorType = operatorType;
            this.unaryOperatorName = unaryOperatorName;
            this.infixOperatorName = infixOperatorName;
        }

        public readonly string str;
        public readonly TokenFamily family;
        public readonly OperatorType operatorType;
        public readonly string unaryOperatorName;
        public readonly string infixOperatorName;
    }

    internal readonly struct TokenInfo
    {
        public TokenInfo(TokenType type, TokenInfoAttribute tokenInfoAttribute)
        {
            this.type = type;
            str = tokenInfoAttribute.str;
            family = tokenInfoAttribute.family;
            operatorType = tokenInfoAttribute.operatorType;
            unaryOperatorName = tokenInfoAttribute.unaryOperatorName;
            infixOperatorName = tokenInfoAttribute.infixOperatorName;
        }

        public readonly TokenType type;
        public readonly string str;
        public readonly TokenFamily family;
        public readonly OperatorType operatorType;
        public readonly string unaryOperatorName;
        public readonly string infixOperatorName;


        public bool IsPrefix => (operatorType & OperatorType.Prefix) != 0;
        public bool IsPostfix => (operatorType & OperatorType.Postfix) != 0;
        public bool IsInfix => (operatorType & OperatorType.Infix) != 0;
        public bool IsCompound => (operatorType & OperatorType.Compound) != 0;
        public bool IsComparison => (operatorType & OperatorType.Comparison) != 0;
        public bool IsBitwise => (operatorType & OperatorType.Bitwise) != 0;
        public bool IsShift => (operatorType & OperatorType.Shift) != 0;
        public bool IsIncrement => (operatorType & OperatorType.Increment) != 0;
    }

    internal readonly struct Keyword
    {
        public Keyword(string str, TokenType type)
        {
            this.str = str;
            this.type = type;
        }

        public readonly string str;
        public readonly TokenType type;
    }

    internal static class TokenUtility
    {
        static TokenUtility()
        {
            tokenInfo = new TokenInfo[(int)TokenType.Invalid];
            Type enumType = typeof(TokenType);
            string[] names = Enum.GetNames(enumType);
            keywords = new List<string>();
            for (int i = 0; i < tokenInfo.Length; i++)
            {
                FieldInfo field = enumType.GetField(names[i]);
                if (field == null) throw new NullReferenceException(names[i]);
                object[] attributes = field.GetCustomAttributes(typeof(TokenInfoAttribute), false);
                if (attributes.Length != 1) throw new NullReferenceException(names[i]);
                TokenInfoAttribute attribute = (TokenInfoAttribute)attributes[0];
                tokenInfo[i] = new TokenInfo((TokenType)i, attribute);
                if (attribute.family == TokenFamily.Keyword)
                {
                    keywords.Add(attribute.str);
                }
            }
            keywordLookupTable = new LookupTable(tokenInfo);

            baseTypeNames = new Dictionary<Type, string>();
            baseTypeNames.Add(typeof(bool), GetTokenInfo(TokenType.Bool).str);
            baseTypeNames.Add(typeof(char), GetTokenInfo(TokenType.Char).str);
            baseTypeNames.Add(typeof(void), GetTokenInfo(TokenType.Void).str);
            baseTypeNames.Add(typeof(byte), GetTokenInfo(TokenType.Byte).str);
            baseTypeNames.Add(typeof(sbyte), GetTokenInfo(TokenType.SByte).str);
            baseTypeNames.Add(typeof(short), GetTokenInfo(TokenType.Short).str);
            baseTypeNames.Add(typeof(ushort), GetTokenInfo(TokenType.UShort).str);
            baseTypeNames.Add(typeof(int), GetTokenInfo(TokenType.Int).str);
            baseTypeNames.Add(typeof(uint), GetTokenInfo(TokenType.UInt).str);
            baseTypeNames.Add(typeof(long), GetTokenInfo(TokenType.Long).str);
            baseTypeNames.Add(typeof(ulong), GetTokenInfo(TokenType.ULong).str);
            baseTypeNames.Add(typeof(float), GetTokenInfo(TokenType.Float).str);
            baseTypeNames.Add(typeof(double), GetTokenInfo(TokenType.Double).str);
            baseTypeNames.Add(typeof(decimal), GetTokenInfo(TokenType.Decimal).str);
            baseTypeNames.Add(typeof(string), GetTokenInfo(TokenType.String).str);
            baseTypeNames.Add(typeof(object), GetTokenInfo(TokenType.Object).str);
        }

        private static readonly TokenInfo[] tokenInfo;

        public static IReadOnlyList<TokenInfo> TokenInfoList => tokenInfo;
        public static TokenInfo GetTokenInfo(TokenType tokenType)
        {
            return tokenInfo[(int)tokenType];
        }


        private class LookupTable
        {
            private readonly struct Range
            {
                public Range(int beg, int end)
                {
                    this.beg = beg;
                    this.end = end;
                }

                public readonly int beg;
                public readonly int end;
            }

            public LookupTable(IReadOnlyList<TokenInfo> tokenInfo)
            {
                SortedDictionary<string, TokenType> sorted = new SortedDictionary<string, TokenType>();
                foreach (var info in tokenInfo)
                {
                    if (info.family == TokenFamily.Keyword)
                    {
                        sorted.Add(info.str, info.type);
                    }
                }

                lookup = new Range[('z' - 'a') + 1];
                List<Keyword> list = new List<Keyword>();
                int index = 0;
                int count = 0;
                char current = 'a';
                foreach (var keyword in sorted)
                {
                    char first = keyword.Key[0];
                    if (current != first)
                    {
                        lookup[current - 'a'] = new Range(index, index + count);
                        current = first;
                        index += count;
                        count = 0;
                    }
                    list.Add(new Keyword(keyword.Key, keyword.Value));
                    count++;
                }
                lookup[current - 'a'] = new Range(index, index + count);
                values = list.ToArray();
            }

            public bool TryGetKeyword(StringView str, out Keyword result)
            {
                if (str.Length > 0)
                {
                    char key = str[0];
                    if (key >= 'a' && key <= 'z')
                    {
                        Range range = lookup[key - 'a'];
                        for (int i = range.beg; i < range.end; i++)
                        {
                            if (values[i].str == str)
                            {
                                result = values[i];
                                return true;
                            }
                        }
                    }
                }
                result = new Keyword(string.Empty, TokenType.Invalid);
                return false;
            }

            private readonly Range[] lookup;
            private readonly Keyword[] values;
        }
        private static readonly LookupTable keywordLookupTable;
        private static List<string> keywords;
        public static IReadOnlyList<string> Keywords => keywords;

        public static bool TryGetKeyword(StringView str, out Keyword keyword)
        {
            return keywordLookupTable.TryGetKeyword(str, out keyword);
        }


        private static readonly Dictionary<Type, string> baseTypeNames;

        public static bool TryGetBaseTypeName(Type type, out string name)
        {
            return baseTypeNames.TryGetValue(type, out name);
        }


        public static bool IsInfixOperator(TokenType op)
        {
            return op == TokenType.Assign || (op >= TokenType.Mul && op <= TokenType.BitwiseOr) || (op >= TokenType.CompoundAdd && op <= TokenType.CompoundOr);
        }
    }
}
#endif