// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;

namespace AggroBird.Reflection
{
    internal class ExpressionParser : TokenParser
    {
        // http://journal.stuffwithstuff.com/2011/03/19/pratt-parsers-expression-parsing-made-easy/

        internal enum Associativity
        {
            Left,
            Right,
        }

        internal readonly struct Grammar
        {
            public Grammar(Precedence precedence, Associativity associativity)
            {
                this.precedence = precedence;
                this.associativity = associativity;
            }

            public readonly Precedence precedence;
            public readonly Associativity associativity;

            // To handle right-associative operators we allow a slightly
            // lower precedence when parsing the right-hand side. This will let a
            // parselet with the same precedence appear on the right, which will then
            // take *this* parselet's result as its left-hand argument.
            public Precedence AssociativePrecedence => (Precedence)((int)precedence - associativity);
        }

        private delegate Expression PrefixFunc(ExpressionParser parser, Token token);
        private delegate Expression InfixFunc(ExpressionParser parser, Expression lhs, Token token);

        private readonly struct GrammarRule
        {
            public GrammarRule(TokenType type)
            {
                this.type = type;
                prefix = default;
                infix = default;
                grammar = default;
            }
            public GrammarRule(TokenType type, PrefixFunc prefix, InfixFunc infix, Precedence precedence, Associativity associativity)
            {
                this.type = type;
                this.prefix = prefix;
                this.infix = infix;
                grammar = new Grammar(precedence, associativity);
            }

            public readonly TokenType type;
            public readonly PrefixFunc prefix;
            public readonly InfixFunc infix;
            public readonly Grammar grammar;
        }

        private readonly struct MixedRule
        {
            public MixedRule(TokenType type, PrefixFunc prefix, InfixFunc infix, Precedence precedence, Associativity associativity)
            {
                rule = new GrammarRule(type, prefix, infix, precedence, associativity);
            }

            public static implicit operator GrammarRule(MixedRule mixedRule)
            {
                return mixedRule.rule;
            }

            private readonly GrammarRule rule;
        }

        private readonly struct PrefixRule
        {
            public PrefixRule(TokenType type, PrefixFunc prefix)
            {
                rule = new GrammarRule(type, prefix, null, Precedence.Root, Associativity.Right);
            }

            public static implicit operator GrammarRule(PrefixRule prefixRule)
            {
                return prefixRule.rule;
            }

            private readonly GrammarRule rule;
        }

        private readonly struct InfixRule
        {
            public InfixRule(TokenType type, InfixFunc infix, Precedence precedence, Associativity associativity)
            {
                rule = new GrammarRule(type, null, infix, precedence, associativity);
            }

            public static implicit operator GrammarRule(InfixRule infixRule)
            {
                return infixRule.rule;
            }

            private readonly GrammarRule rule;
        }

        public ExpressionParser(ArrayView<Token> tokens) : base(tokens)
        {

        }

        private static GrammarRule[] BuildGrammarRules()
        {
            GrammarRule[] result = new GrammarRule[(int)TokenType.Invalid + 1]
            {
                new GrammarRule(TokenType.Eol),

                // Seperators
                new MixedRule(TokenType.LParen, InvokeGroupCallback, InvokeCallCallback, Precedence.Call, Associativity.Left),
                new GrammarRule(TokenType.RParen),
                new GrammarRule(TokenType.LBrace),
                new GrammarRule(TokenType.RBrace),
                new InfixRule(TokenType.LBracket, InvokeSubscriptCallback, Precedence.Call, Associativity.Left),
                new GrammarRule(TokenType.RBracket),
                new GrammarRule(TokenType.Semicolon),

                // Operators
                new InfixRule(TokenType.Period, InvokeInfixCallback, Precedence.Primary, Associativity.Left),

                new MixedRule(TokenType.Increment, InvokePrefixCallback, InvokePostfixCallback, Precedence.Postfix, Associativity.Right),
                new MixedRule(TokenType.Decrement, InvokePrefixCallback, InvokePostfixCallback, Precedence.Postfix, Associativity.Right),
                new PrefixRule(TokenType.LogicalNot, InvokePrefixCallback),
                new PrefixRule(TokenType.BitwiseNot, InvokePrefixCallback),

                new MixedRule(TokenType.Mul, InvokePrefixCallback, InvokeInfixCallback, Precedence.Product, Associativity.Left),
                new InfixRule(TokenType.Div, InvokeInfixCallback, Precedence.Product, Associativity.Left),
                new InfixRule(TokenType.Mod, InvokeInfixCallback, Precedence.Product, Associativity.Left),

                new MixedRule(TokenType.Add, InvokePrefixCallback, InvokeInfixCallback, Precedence.Sum, Associativity.Left),
                new MixedRule(TokenType.Sub, InvokePrefixCallback, InvokeInfixCallback, Precedence.Sum, Associativity.Left),

                new InfixRule(TokenType.Lsh, InvokeInfixCallback, Precedence.Shift, Associativity.Left),
                new InfixRule(TokenType.Rsh, InvokeInfixCallback, Precedence.Shift, Associativity.Left),

                new InfixRule(TokenType.Lt, InvokeInfixCallback, Precedence.Relational, Associativity.Left),
                new InfixRule(TokenType.Gt, InvokeInfixCallback, Precedence.Relational, Associativity.Left),
                new InfixRule(TokenType.Le, InvokeInfixCallback, Precedence.Relational, Associativity.Left),
                new InfixRule(TokenType.Ge, InvokeInfixCallback, Precedence.Relational, Associativity.Left),

                new InfixRule(TokenType.Is, InvokeInfixCallback, Precedence.Relational, Associativity.Left),
                new InfixRule(TokenType.As, InvokeInfixCallback, Precedence.Relational, Associativity.Left),

                new InfixRule(TokenType.Eq, InvokeInfixCallback, Precedence.Equality, Associativity.Left),
                new InfixRule(TokenType.Ne, InvokeInfixCallback, Precedence.Equality, Associativity.Left),

                new MixedRule(TokenType.BitwiseAnd, InvokePrefixCallback, InvokeInfixCallback, Precedence.BitwiseAnd, Associativity.Left),
                new InfixRule(TokenType.BitwiseXor, InvokeInfixCallback, Precedence.BitwiseXor, Associativity.Left),
                new InfixRule(TokenType.BitwiseOr, InvokeInfixCallback, Precedence.BitwiseOr, Associativity.Left),

                new InfixRule(TokenType.LogicalAnd, InvokeInfixCallback, Precedence.LogicalAnd, Associativity.Left),
                new InfixRule(TokenType.LogicalOr, InvokeInfixCallback, Precedence.LogicalOr, Associativity.Left),

                new InfixRule(TokenType.Assign, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.Condition, InvokeConditionalCallback, Precedence.Compound, Associativity.Right),
                new GrammarRule(TokenType.Colon),

                new InfixRule(TokenType.CompoundAdd, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundSub, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundMul, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundDiv, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundMod, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundLsh, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundRsh, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundAnd, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundXor, InvokeInfixCallback, Precedence.Compound, Associativity.Right),
                new InfixRule(TokenType.CompoundOr, InvokeInfixCallback, Precedence.Compound, Associativity.Right),

                new InfixRule(TokenType.Comma, InvokeCommaCallback, Precedence.Comma, Associativity.Left),

                // Keywords
                new PrefixRule(TokenType.Null, InvokeKeywordCallback),
                new PrefixRule(TokenType.True, InvokeKeywordCallback),
                new PrefixRule(TokenType.False, InvokeKeywordCallback),
                new PrefixRule(TokenType.Typeof, InvokeKeywordCallback),

                new PrefixRule(TokenType.Bool, InvokeKeywordCallback),
                new PrefixRule(TokenType.Char, InvokeKeywordCallback),
                new PrefixRule(TokenType.Void, InvokeKeywordCallback),
                new PrefixRule(TokenType.Byte, InvokeKeywordCallback),
                new PrefixRule(TokenType.SByte, InvokeKeywordCallback),
                new PrefixRule(TokenType.Short, InvokeKeywordCallback),
                new PrefixRule(TokenType.UShort, InvokeKeywordCallback),
                new PrefixRule(TokenType.Int, InvokeKeywordCallback),
                new PrefixRule(TokenType.UInt, InvokeKeywordCallback),
                new PrefixRule(TokenType.Long, InvokeKeywordCallback),
                new PrefixRule(TokenType.ULong, InvokeKeywordCallback),
                new PrefixRule(TokenType.Float, InvokeKeywordCallback),
                new PrefixRule(TokenType.Double, InvokeKeywordCallback),
                new PrefixRule(TokenType.Decimal, InvokeKeywordCallback),
                new PrefixRule(TokenType.String, InvokeKeywordCallback),
                new PrefixRule(TokenType.Object, InvokeKeywordCallback),
                new PrefixRule(TokenType.Var, InvokeKeywordCallback),

                new PrefixRule(TokenType.If, InvokeKeywordCallback),
                new PrefixRule(TokenType.Else, InvokeKeywordCallback),
                new PrefixRule(TokenType.For, InvokeKeywordCallback),
                new PrefixRule(TokenType.While, InvokeKeywordCallback),
                new PrefixRule(TokenType.Foreach, InvokeKeywordCallback),
                new PrefixRule(TokenType.Break, InvokeKeywordCallback),
                new PrefixRule(TokenType.Continue, InvokeKeywordCallback),
                new PrefixRule(TokenType.In, InvokeKeywordCallback),
                new PrefixRule(TokenType.Out, InvokeKeywordCallback),
                new PrefixRule(TokenType.Ref, InvokeKeywordCallback),
                new PrefixRule(TokenType.New, InvokeKeywordCallback),

                new PrefixRule(TokenType.Not, InvokeKeywordCallback),
                
                // Names
                new PrefixRule(TokenType.Identifier, InvokeNameCallback),

                // Literals
                new PrefixRule(TokenType.NumberLiteral, InvokeLiteralCallback),
                new PrefixRule(TokenType.StringLiteral, InvokeLiteralCallback),
                new PrefixRule(TokenType.CharLiteral, InvokeLiteralCallback),

                new GrammarRule(TokenType.Invalid),
            };
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].type != (TokenType)i)
                {
                    throw new Exception($"Grammar rule type mismatch");
                }
            }
            return result;
        }

        private static readonly GrammarRule[] grammarRules = BuildGrammarRules();


        protected virtual Precedence PeekNextPrecedence(Expression lhs)
        {
            return grammarRules[(int)Peek()].grammar.precedence;
        }

        private GrammarRule GetRule(TokenType tokenType)
        {
            return grammarRules[(int)tokenType];
        }
        public Grammar GetGrammar(TokenType tokenType)
        {
            return grammarRules[(int)tokenType].grammar;
        }

        public Expression ParseNext(Precedence precedence = Precedence.Root)
        {
            Token token = Consume();
            if (token.type >= TokenType.Invalid) throw new InvalidTokenException(token);
            GrammarRule rule = GetRule(token.type);
            PrefixFunc prefix = rule.prefix;
            if (prefix == null) throw new UnexpectedTokenException(token);
            Expression lhs = prefix(this, token);

            return ParseNext(lhs, precedence);
        }
        public Expression ParseNext(Expression lhs, Precedence precedence = Precedence.Root)
        {
            while (precedence < PeekNextPrecedence(lhs))
            {
                Token token = Consume();
                if (token.type >= TokenType.Invalid) throw new InvalidTokenException(token);
                GrammarRule rule = GetRule(token.type);
                InfixFunc infix = rule.infix;
                if (infix == null) throw new UnexpectedTokenException(token);
                lhs = infix(this, lhs, token);
            }
            return lhs;
        }

        private static Expression InvokeNameCallback(ExpressionParser parser, Token token) => parser.NameCallback(token);
        private static Expression InvokeLiteralCallback(ExpressionParser parser, Token token) => parser.LiteralCallback(token);
        private static Expression InvokeKeywordCallback(ExpressionParser parser, Token token) => parser.KeywordCallback(token);
        private static Expression InvokePrefixCallback(ExpressionParser parser, Token token) => parser.PrefixCallback(token);
        private static Expression InvokePostfixCallback(ExpressionParser parser, Expression lhs, Token token) => parser.PostfixCallback(lhs, token);
        private static Expression InvokeInfixCallback(ExpressionParser parser, Expression lhs, Token token) => parser.InfixCallback(lhs, token);
        private static Expression InvokeGroupCallback(ExpressionParser parser, Token token) => parser.GroupCallback(token);
        private static Expression InvokeCallCallback(ExpressionParser parser, Expression lhs, Token token) => parser.CallCallback(lhs, token);
        private static Expression InvokeSubscriptCallback(ExpressionParser parser, Expression lhs, Token token) => parser.SubscriptCallback(lhs, token);
        private static Expression InvokeConditionalCallback(ExpressionParser parser, Expression lhs, Token token) => parser.ConditionalCallback(lhs, token);
        private static Expression InvokeCommaCallback(ExpressionParser parser, Expression lhs, Token token) => parser.CommaCallback(lhs, token);

        protected virtual Expression NameCallback(Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression LiteralCallback(Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression KeywordCallback(Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression PrefixCallback(Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression PostfixCallback(Expression lhs, Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression InfixCallback(Expression lhs, Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression GroupCallback(Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression CallCallback(Expression lhs, Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression SubscriptCallback(Expression lhs, Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression ConditionalCallback(Expression lhs, Token token)
        {
            throw new UnexpectedTokenException(token);
        }
        protected virtual Expression CommaCallback(Expression lhs, Token token)
        {
            throw new UnexpectedTokenException(token);
        }
    }
}
#endif