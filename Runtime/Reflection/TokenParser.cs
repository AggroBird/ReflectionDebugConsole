// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

namespace AggroBird.Reflection
{
    internal class TokenParser
    {
        public TokenParser(ArrayView<Token> tokens)
        {
            this.tokens = tokens;
        }

        public readonly ArrayView<Token> tokens;
        private int position = 0;
        public int Position => position;
        public Token CurrentToken => tokens[position];


        public virtual void Advance()
        {
            position++;
        }
        public virtual TokenType Peek()
        {
            return PeekAt(position);
        }
        public virtual Token Consume()
        {
            Token result = tokens[position];
            Advance();
            return result;
        }

        public Token Consume(TokenType expected)
        {
            TokenType next = Peek();
            if (next == TokenType.Eol) throw new UnexpectedEndOfExpressionException();
            if (next != expected) throw new DebugConsoleException($"Unexpected token '{Token.Stringify(next)}' (expected '{Token.Stringify(expected)}')");
            if (next <= TokenType.Eol) return Token.Empty;
            return Consume();
        }
        public bool Match(TokenType expected)
        {
            if (Peek() == expected)
            {
                Advance();
                return true;
            }
            return false;
        }
        public bool Match(TokenType expected, out Token token)
        {
            if (Peek() == expected)
            {
                token = Consume();
                return true;
            }
            token = default;
            return false;
        }

        public TokenType PeekAt(int idx)
        {
            return (idx < tokens.Length) ? tokens[idx].type : TokenType.Eol;
        }

        public ArrayView<Token> CopyTokens(int start, int len)
        {
            return tokens.SubView(start, len);
        }
    }
}
#endif