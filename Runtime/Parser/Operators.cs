// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AggroBird.DebugConsole
{
    internal struct UnaryOperatorFunction
    {
        private delegate object SingleOperatorDelegate(ExecutionContext context, Expression arg);

        public static UnaryOperatorFunction MakeOperator<RetType>(Func<ExecutionContext, Expression, RetType> func)
        {
            return new UnaryOperatorFunction((ExecutionContext context, Expression arg) => { return func(context, arg); }, typeof(RetType));
        }

        private UnaryOperatorFunction(SingleOperatorDelegate func, Type returnType)
        {
            this.func = func;
            this.returnType = returnType;
        }

        private readonly SingleOperatorDelegate func;
        private readonly Type returnType;

        public Type ReturnType => returnType;

        public object Invoke(ExecutionContext context, Expression arg)
        {
            return func(context, arg);
        }
    }

    internal struct InfixOperatorFunction
    {
        private delegate object InfixOperatorDelegate(ExecutionContext context, Expression lhs, Expression rhs);

        public static InfixOperatorFunction MakeOperator<RetType>(Func<ExecutionContext, Expression, Expression, RetType> func)
        {
            return new InfixOperatorFunction((ExecutionContext context, Expression lhs, Expression rhs) => { return func(context, lhs, rhs); }, typeof(RetType));
        }

        private InfixOperatorFunction(InfixOperatorDelegate func, Type returnType)
        {
            this.func = func;
            this.returnType = returnType;
        }

        private readonly InfixOperatorDelegate func;
        private readonly Type returnType;

        public Type ReturnType => returnType;

        public object Invoke(ExecutionContext context, Expression lhs, Expression rhs)
        {
            return func(context, lhs, rhs);
        }
    }

    internal static class Operators
    {
        public static bool IsArithmetic(TypeCode type)
        {
            switch (type)
            {
                case TypeCode.Char:
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
                    return true;
            }
            return false;
        }
        public static bool IsUnsigned(TypeCode type)
        {
            switch (type)
            {
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
            }
            return false;
        }
        public static bool IsBoolean(TypeCode type)
        {
            return type == TypeCode.Boolean;
        }
        public static bool IsIntegral(TypeCode type)
        {
            switch (type)
            {
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return true;
            }
            return false;
        }
        public static bool IsFloatingPoint(TypeCode type)
        {
            switch (type)
            {
                case TypeCode.Single:
                case TypeCode.Double:
                    return true;
            }
            return false;
        }

        private static TypeCode Max(TypeCode lhs, TypeCode rhs)
        {
            return (TypeCode)Math.Max((int)lhs, (int)rhs);
        }

        private static bool CheckAssignmentImplicitConversion(TypeCode destination, TypeCode operand)
        {
            if (destination == operand) return true;
            if (destination == TypeCode.Double) return operand <= TypeCode.Double;
            if (destination == TypeCode.Single) return operand <= TypeCode.Single;
            if (IsUnsigned(destination)) return IsUnsigned(operand) && operand <= destination;
            return operand <= destination;
        }
        private static bool CheckArithmeticImplicitConversion(TypeCode lhs, TypeCode rhs, out TypeCode cast)
        {
            cast = default;

            if (IsArithmetic(lhs) && IsArithmetic(rhs))
            {
                if (lhs != rhs)
                {
                    if (lhs == TypeCode.Double || rhs == TypeCode.Double)
                        cast = TypeCode.Double;
                    else if (lhs == TypeCode.Single || rhs == TypeCode.Single)
                        cast = TypeCode.Single;
                    else if (lhs <= TypeCode.Int32 && rhs <= TypeCode.Int32)
                        cast = TypeCode.Int32;
                    else if (lhs <= TypeCode.Int64 && rhs <= TypeCode.Int64)
                        cast = TypeCode.Int64;
                    else if (IsUnsigned(lhs) == IsUnsigned(rhs))
                        cast = Max(Max(lhs, rhs), TypeCode.Int32);
                    else
                        return false;

                    return true;
                }
                else
                {
                    cast = Max(lhs, TypeCode.Int32);
                    return true;
                }
            }
            else if (lhs == TypeCode.Boolean && rhs == TypeCode.Boolean)
            {
                cast = TypeCode.Boolean;
                return true;
            }

            return false;
        }
        private static bool CheckShiftOperand(TypeCode operand)
        {
            switch (operand)
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                    return true;
            }
            return false;
        }


        public static Expression MakeUnaryOperator(TokenType op, Expression arg)
        {
            TokenInfo info = TokenUtility.GetTokenInfo(op);

            if (TryFindUserOperator(info, arg, out Expression expr))
            {
                return expr;
            }

            return new UnaryOperator(arg, MakeUnary(op, arg.GetTypeCode(), arg));
        }
        private static UnaryOperatorFunction MakeUnary(TokenType op, TypeCode type, Expression argExpr)
        {
            if (op != TokenType.Increment && op != TokenType.Decimal)
            {
                // Upgrade to at least int if using smaller types
                switch (type)
                {
                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                        type = TypeCode.Int32;
                        break;
                }
            }

            switch (type)
            {
                case TypeCode.Boolean:
                    switch (op)
                    {
                        case TokenType.LogicalNot: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return !Expression.ToBool(arg.Execute(context)); });
                    }
                    break;

                case TypeCode.SByte:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (sbyte)(Expression.ToSByte(arg.Execute(context)) + 1); });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (sbyte)(Expression.ToSByte(arg.Execute(context)) - 1); });
                    }
                    break;

                case TypeCode.Byte:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (byte)(Expression.ToByte(arg.Execute(context)) + 1); });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (byte)(Expression.ToByte(arg.Execute(context)) - 1); });
                    }
                    break;

                case TypeCode.Int16:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (short)(Expression.ToInt16(arg.Execute(context)) + 1); });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (short)(Expression.ToInt16(arg.Execute(context)) - 1); });
                    }
                    break;

                case TypeCode.UInt16:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (ushort)(Expression.ToUInt16(arg.Execute(context)) + 1); });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return (ushort)(Expression.ToUInt16(arg.Execute(context)) - 1); });
                    }
                    break;

                case TypeCode.Int32:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return ~Expression.ToInt32(arg.Execute(context)); });
                        case TokenType.Sub: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return -Expression.ToInt32(arg.Execute(context)); });
                        case TokenType.Add: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToInt32(arg.Execute(context)); });
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToInt32(arg.Execute(context)) + 1; });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToInt32(arg.Execute(context)) - 1; });
                    }
                    break;
                case TypeCode.UInt32:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return ~Expression.ToUInt32(arg.Execute(context)); });
                        case TokenType.Sub: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return -Expression.ToUInt32(arg.Execute(context)); });
                        case TokenType.Add: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToUInt32(arg.Execute(context)); });
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToUInt32(arg.Execute(context)) + 1; });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToUInt32(arg.Execute(context)) - 1; });
                    }
                    break;
                case TypeCode.Int64:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return ~Expression.ToInt64(arg.Execute(context)); });
                        case TokenType.Sub: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return -Expression.ToInt64(arg.Execute(context)); });
                        case TokenType.Add: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToInt64(arg.Execute(context)); });
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToInt64(arg.Execute(context)) + 1; });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToInt64(arg.Execute(context)) - 1; });
                    }
                    break;
                case TypeCode.UInt64:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return ~Expression.ToUInt64(arg.Execute(context)); });
                        case TokenType.Add: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToUInt64(arg.Execute(context)); });
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToUInt64(arg.Execute(context)) + 1; });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToUInt64(arg.Execute(context)) - 1; });
                    }
                    break;
                case TypeCode.Single:
                    switch (op)
                    {
                        case TokenType.Sub: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return -Expression.ToSingle(arg.Execute(context)); });
                        case TokenType.Add: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToSingle(arg.Execute(context)); });
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToSingle(arg.Execute(context)) + 1; });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToSingle(arg.Execute(context)) - 1; });
                    }
                    break;
                case TypeCode.Double:
                    switch (op)
                    {
                        case TokenType.Sub: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return -Expression.ToDouble(arg.Execute(context)); });
                        case TokenType.Add: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToDouble(arg.Execute(context)); });
                        case TokenType.Increment: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToDouble(arg.Execute(context)) + 1; });
                        case TokenType.Decrement: return UnaryOperatorFunction.MakeOperator((ExecutionContext context, Expression arg) => { return Expression.ToDouble(arg.Execute(context)) - 1; });
                    }
                    break;
            }

            throw new DebugConsoleException($"Operator '{TokenUtility.GetTokenInfo(op).str}' cannot be applied to operand of type '{argExpr.ResultType}'");
        }

        public static Expression MakeInfixOperator(TokenType op, Expression lhsExpr, Expression rhsExpr)
        {
            TokenInfo info = TokenUtility.GetTokenInfo(op);

            TypeCode lhsType = lhsExpr.GetTypeCode();
            TypeCode rhsType = rhsExpr.GetTypeCode();

            if (info.IsShift && IsIntegral(lhsType) && CheckShiftOperand(rhsType))
            {
                return new InfixOperator(lhsExpr, rhsExpr, MakeInfix(op, Max(lhsType, TypeCode.Int32), lhsExpr, rhsExpr));
            }
            else if (CheckArithmeticImplicitConversion(lhsType, rhsType, out TypeCode cast))
            {
                return new InfixOperator(lhsExpr, rhsExpr, MakeInfix(op, cast, lhsExpr, rhsExpr));
            }
            else if (TryFindUserOperator(info, lhsExpr, rhsExpr, out Expression expr))
            {
                return expr;
            }
            else if (op == TokenType.Add && (lhsType == TypeCode.String || rhsType == TypeCode.String))
            {
                return new InfixOperator(lhsExpr, rhsExpr, InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Stringify(lhs.Execute(context)) + Stringify(rhs.Execute(context)); }));
            }
            else if (!lhsExpr.ResultType.IsValueType && !rhsExpr.ResultType.IsValueType)
            {
                switch (op)
                {
                    case TokenType.Eq: return new InfixOperator(lhsExpr, rhsExpr, InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return ReferenceEquals(lhs.Execute(context), rhs.Execute(context)); }));
                    case TokenType.Ne: return new InfixOperator(lhsExpr, rhsExpr, InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return !ReferenceEquals(lhs.Execute(context), rhs.Execute(context)); }));
                }
            }

            throw new DebugConsoleException($"Operator '{info.str}' cannot be applied to operands of type '{lhsExpr.ResultType}' and '{rhsExpr.ResultType}'");
        }
        private static InfixOperatorFunction MakeInfix(TokenType op, TypeCode type, Expression lhsExpr, Expression rhsExpr)
        {
            switch (type)
            {
                case TypeCode.Boolean:
                    switch (op)
                    {
                        case TokenType.BitwiseAnd: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToBool(lhs.Execute(context)) & Expression.ToBool(rhs.Execute(context)); });
                        case TokenType.BitwiseXor: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToBool(lhs.Execute(context)) ^ Expression.ToBool(rhs.Execute(context)); });
                        case TokenType.BitwiseOr: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToBool(lhs.Execute(context)) | Expression.ToBool(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToBool(lhs.Execute(context)) == Expression.ToBool(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToBool(lhs.Execute(context)) != Expression.ToBool(rhs.Execute(context)); });
                    }
                    break;

                case TypeCode.Int32:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) * Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Div: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) / Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Mod: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) % Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Add: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) + Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Sub: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) - Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Lsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) << Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Rsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) >> Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Lt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) < Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Gt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) > Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Le: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) <= Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Ge: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) >= Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) == Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) != Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.BitwiseAnd: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) & Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.BitwiseXor: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) ^ Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.BitwiseOr: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt32(lhs.Execute(context)) | Expression.ToInt32(rhs.Execute(context)); });
                    }
                    break;
                case TypeCode.UInt32:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) * Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Div: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) / Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Mod: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) % Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Add: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) + Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Sub: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) - Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Lsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) << Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Rsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) >> Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Lt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) < Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Gt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) > Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Le: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) <= Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Ge: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) >= Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) == Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) != Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.BitwiseAnd: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) & Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.BitwiseXor: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) ^ Expression.ToUInt32(rhs.Execute(context)); });
                        case TokenType.BitwiseOr: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt32(lhs.Execute(context)) | Expression.ToUInt32(rhs.Execute(context)); });
                    }
                    break;
                case TypeCode.Int64:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) * Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Div: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) / Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Mod: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) % Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Add: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) + Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Sub: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) - Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Lsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) << Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Rsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) >> Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Lt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) < Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Gt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) > Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Le: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) <= Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Ge: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) >= Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) == Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) != Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.BitwiseAnd: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) & Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.BitwiseXor: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) ^ Expression.ToInt64(rhs.Execute(context)); });
                        case TokenType.BitwiseOr: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToInt64(lhs.Execute(context)) | Expression.ToInt64(rhs.Execute(context)); });
                    }
                    break;
                case TypeCode.UInt64:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) * Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Div: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) / Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Mod: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) % Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Add: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) + Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Sub: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) - Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Lsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) << Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Rsh: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) >> Expression.ToInt32(rhs.Execute(context)); });
                        case TokenType.Lt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) < Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Gt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) > Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Le: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) <= Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Ge: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) >= Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) == Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) != Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.BitwiseAnd: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) & Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.BitwiseXor: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) ^ Expression.ToUInt64(rhs.Execute(context)); });
                        case TokenType.BitwiseOr: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToUInt64(lhs.Execute(context)) | Expression.ToUInt64(rhs.Execute(context)); });
                    }
                    break;
                case TypeCode.Single:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) * Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Div: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) / Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Mod: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) % Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Add: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) + Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Sub: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) - Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Lt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) < Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Gt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) > Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Le: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) <= Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Ge: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) >= Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) == Expression.ToSingle(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToSingle(lhs.Execute(context)) != Expression.ToSingle(rhs.Execute(context)); });
                    }
                    break;
                case TypeCode.Double:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) * Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Div: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) / Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Mod: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) % Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Add: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) + Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Sub: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) - Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Lt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) < Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Gt: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) > Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Le: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) <= Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Ge: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) >= Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Eq: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) == Expression.ToDouble(rhs.Execute(context)); });
                        case TokenType.Ne: return InfixOperatorFunction.MakeOperator((ExecutionContext context, Expression lhs, Expression rhs) => { return Expression.ToDouble(lhs.Execute(context)) != Expression.ToDouble(rhs.Execute(context)); });
                    }
                    break;
            }

            throw new DebugConsoleException($"Operator '{TokenUtility.GetTokenInfo(op).str}' cannot be applied to operands of type '{lhsExpr.ResultType}' and '{rhsExpr.ResultType}'");
        }

        private static bool TryFindUserOperator(TokenInfo info, Expression arg, out Expression expr)
        {
            if (!string.IsNullOrEmpty(info.unaryOperatorName))
            {
                List<MethodInfo> overloads = new List<MethodInfo>();

                Expression[] args = new Expression[1] { arg };

                foreach (MemberInfo member in arg.ResultType.GetMember(info.unaryOperatorName, MemberTypes.Method, BindingFlags.Public | BindingFlags.Static))
                {
                    if (member is MethodInfo method && Expression.IsCompatibleOverload(method, args))
                    {
                        overloads.Add(method);
                    }
                }

                MethodInfo[] optimal = Expression.GetOptimalOverloads(overloads, args);

                if (optimal.Length > 0)
                {
                    if (optimal.Length > 1) throw new DebugConsoleException($"Operator '{info.str}' is ambiguous on operand of type '{arg.ResultType}'");
                    expr = new Method(optimal[0], args);
                    return true;
                }
            }

            expr = null;
            return false;
        }
        private static bool TryFindUserOperator(TokenInfo info, Expression lhs, Expression rhs, out Expression expr)
        {
            if (!string.IsNullOrEmpty(info.infixOperatorName))
            {
                List<MethodInfo> overloads = new List<MethodInfo>();

                Expression[] args = new Expression[2] { lhs, rhs };

                foreach (MemberInfo member in lhs.ResultType.GetMember(info.infixOperatorName, MemberTypes.Method, BindingFlags.Public | BindingFlags.Static))
                {
                    if (member is MethodInfo method && Expression.IsCompatibleOverload(method, args))
                    {
                        overloads.Add(method);
                    }
                }

                // Try all operands
                if (lhs.ResultType != rhs.ResultType)
                {
                    foreach (MemberInfo member in rhs.ResultType.GetMember(info.infixOperatorName, MemberTypes.Method, BindingFlags.Public | BindingFlags.Static))
                    {
                        if (member is MethodInfo method && Expression.IsCompatibleOverload(method, args))
                        {
                            overloads.Add(method);
                        }
                    }
                }

                MethodInfo[] optimal = Expression.GetOptimalOverloads(overloads, args);

                if (optimal.Length > 0)
                {
                    if (optimal.Length > 1) throw new DebugConsoleException($"Operator '{info.str}' is ambiguous on operands of type '{lhs.ResultType}' and '{rhs.ResultType}'");
                    expr = new Method(optimal[0], args);
                    return true;
                }
            }

            expr = null;
            return false;
        }

        private static string Stringify(object obj)
        {
            if (obj == null) return $"{null}";
            return obj.ToString();
        }
    }
}
#endif