﻿// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System.Collections.Generic;
using System.Text;

namespace AggroBird.Reflection
{
    internal sealed unsafe class Lexer : List<Token>
    {
        private enum CommentState
        {
            None,
            SingleLine,
            MultiLine,
        }

        public Lexer(string source)
        {
            this.source = source;

            fixed (char* ptr = source)
            {
                this.ptr = ptr;
                beg = ptr;
                end = ptr + source.Length;

                Parse();
            }
        }

        private char Peek(int offset = 0)
        {
            char* off = ptr + offset;
            return off < end ? *off : '\0';
        }
        private char Consume()
        {
            return *ptr++;
        }

        private void Parse()
        {
            TokenType stringState = TokenType.Invalid;
            char* stringStart = ptr;
            bool escape = false;
            while (ptr < end)
            {
                char c = Consume();
                if (stringState != TokenType.Invalid)
                {
                    if (!escape)
                    {
                        switch (c)
                        {
                            case '"':
                                if (stringState == TokenType.StringLiteral)
                                {
                                    Add(new Token(stringState, source.SubView((int)(stringStart - beg), (int)(ptr - stringStart)), lineNumber));
                                    stringState = TokenType.Invalid;
                                }
                                break;
                            case '\'':
                                if (stringState == TokenType.CharLiteral)
                                {
                                    Add(new Token(stringState, source.SubView((int)(stringStart - beg), (int)(ptr - stringStart)), lineNumber));
                                    stringState = TokenType.Invalid;
                                }
                                break;
                        }
                    }
                    escape = !escape && c == '\\';
                    continue;
                }

                switch (c)
                {
                    case '"':
                        stringState = TokenType.StringLiteral;
                        stringStart = ptr - 1;
                        break;
                    case '\'':
                        stringState = TokenType.CharLiteral;
                        stringStart = ptr - 1;
                        break;

                    case '(': MakeToken(c, TokenType.LParen); break;
                    case ')': MakeToken(c, TokenType.RParen); break;
                    case '{': MakeToken(c, TokenType.LBrace); break;
                    case '}': MakeToken(c, TokenType.RBrace); break;
                    case '[': MakeToken(c, TokenType.LBracket); break;
                    case ']': MakeToken(c, TokenType.RBracket); break;
                    case ';': MakeToken(c, TokenType.Semicolon); break;

                    case ':': MakeToken(c, TokenType.Colon); break;


                    case '.': MakeToken(c, TokenType.Period); break;

                    case ',': MakeToken(c, TokenType.Comma); break;

                    case '+':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '+': MakeToken(c, n, TokenType.Increment); break;
                            default: CheckCompound(c, TokenType.Add, TokenType.CompoundAdd); break;
                        }
                        break;
                    }
                    case '-':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '-': MakeToken(c, n, TokenType.Decrement); break;
                            default: CheckCompound(c, TokenType.Sub, TokenType.CompoundSub); break;
                        }
                        break;
                    }
                    case '*': CheckCompound(c, TokenType.Mul, TokenType.CompoundMul); break;
                    case '/':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            default: CheckCompound(c, TokenType.Div, TokenType.CompoundDiv); break;
                        }
                        break;
                    }
                    case '%': CheckCompound(c, TokenType.Mod, TokenType.CompoundMod); break;

                    case '<':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '<': CheckCompound(c, n, TokenType.Lsh, TokenType.CompoundLsh); break;
                            case '=': MakeToken(c, n, TokenType.Le); break;
                            default: MakeToken(c, TokenType.Lt); break;
                        }
                        break;
                    }

                    case '>':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '>': CheckCompound(c, n, TokenType.Rsh, TokenType.CompoundRsh); break;
                            case '=': MakeToken(c, n, TokenType.Ge); break;
                            default: MakeToken(c, TokenType.Gt); break;
                        }
                        break;
                    }

                    case '&':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '&': MakeToken(c, n, TokenType.LogicalAnd); break;
                            default: CheckCompound(c, TokenType.BitwiseAnd, TokenType.CompoundAnd); break;
                        }
                        break;
                    }

                    case '^': CheckCompound(c, TokenType.BitwiseXor, TokenType.CompoundXor); break;

                    case '|':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '|': MakeToken(c, n, TokenType.LogicalOr); break;
                            default: CheckCompound(c, TokenType.BitwiseOr, TokenType.CompoundOr); break;
                        }
                        break;
                    }

                    case '=':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '=': MakeToken(c, n, TokenType.Eq); break;
                            default: MakeToken(c, TokenType.Assign); break;
                        }
                        break;
                    }

                    case '!':
                    {
                        char n = Peek();
                        switch (n)
                        {
                            case '=': MakeToken(c, n, TokenType.Ne); break;
                            default: MakeToken(c, TokenType.LogicalNot); break;
                        }
                        break;
                    }

                    case '~': MakeToken(c, TokenType.BitwiseNot); break;

                    case '?':
                    {
                        MakeToken(c, TokenType.Condition);
                        break;
                    }

                    case '\n':
                        lineNumber++;
                        break;
                    case ' ':
                    case '\t':
                    case '\r':
                        break;

                    default:
                        if (IsDigit(c))
                        {
                            Add(ReadNumber(c));
                        }
                        else if (IsName(c))
                        {
                            Add(ReadName(c));
                        }
                        else
                        {
                            throw new DebugConsoleException($"Invalid character '{c}'");
                        }
                        break;
                }
            }

            if (stringState == TokenType.StringLiteral)
            {
                Add(new Token(stringState, source.SubView((int)(stringStart - beg), (int)(ptr - stringStart)), lineNumber));
            }
            else if (stringState == TokenType.CharLiteral)
            {
                Add(new Token(stringState, source.SubView((int)(stringStart - beg), (int)(ptr - stringStart)), lineNumber));
            }

            Add(new Token(TokenType.Eol, source.SubView(source.Length, 0), lineNumber));
        }

        private static bool IsAscii(char c)
        {
            return c <= 0x7Fu;
        }
        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
        private static bool IsName(char c)
        {
            if (IsAscii(c))
            {
                return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
            }
            return false;
        }

        private unsafe Token ReadNumber(char c)
        {
            char* start = ptr - 1;

            bool isAscii = IsAscii(c);

            bool isFloat = false;
            char prev = '\0';
            while (true)
            {
                c = Peek();
                if (c == '.')
                {
                    // Ensure single .
                    if (ptr < end - 1 && !IsDigit(*(ptr + 1))) break;
                    if (isFloat) break;
                    isFloat = true;
                }
                else
                {
                    if (c == '-')
                    {
                        // Ensure - after exponential
                        if (prev != 'e' && prev != 'E') break;
                    }
                    else
                    {
                        // Ensure valid numbers
                        if (!IsName(c) && !IsDigit(c)) break;
                    }
                }

                isAscii &= IsAscii(c);
                ptr++;
                prev = c;
            }
            if (!isAscii)
            {
                throw new DebugConsoleException($"Invalid character in literal");
            }

            StringView str = source.SubView((int)(start - beg), (int)(ptr - start));
            return new Token(TokenType.NumberLiteral, str, lineNumber);
        }
        private unsafe Token ReadName(char c)
        {
            char* start = ptr - 1;

            bool isAscii = IsAscii(c);

            while (true)
            {
                c = Peek();
                if (!IsName(c) && !IsDigit(c)) break;
                isAscii &= IsAscii(c);
                ptr++;
            }

            StringView str = source.SubView((int)(start - beg), (int)(ptr - start));
            if (isAscii)
            {
                if (TokenUtility.TryGetKeyword(str, out Keyword keyword))
                {
                    return new Token(keyword.type, str, lineNumber);
                }
            }

            return new Token(TokenType.Identifier, str, lineNumber);
        }

        private void CheckCompound(char c, TokenType type, TokenType compoundType)
        {
            char n = Peek();
            if (n == '=')
            {
                MakeToken(c, n, compoundType);
                return;
            }
            MakeToken(c, type);
        }
        private void CheckCompound(char c, char n, TokenType type, TokenType compoundType)
        {
            char l = Peek(1);
            if (l == '=')
            {
                MakeToken(c, n, l, compoundType);
                return;
            }
            MakeToken(c, n, type);
        }

        private void MakeToken(char c, TokenType tokenType)
        {
            Add(new Token(tokenType, source.SubView((int)(ptr - beg - 1), 1), lineNumber));
        }
        private void MakeToken(char c, char n, TokenType tokenType)
        {
            Add(new Token(tokenType, source.SubView((int)(ptr - beg - 1), 2), lineNumber));
            ptr++;
        }
        private void MakeToken(char c, char n, char l, TokenType tokenType)
        {
            Add(new Token(tokenType, source.SubView((int)(ptr - beg - 1), 3), lineNumber));
            ptr += 2;
        }


        private readonly string source;
        private char* ptr;
        private readonly char* beg;
        private readonly char* end;
        private uint lineNumber = 1;
        private readonly StringBuilder stringBuilder = new();
    }
}
#endif