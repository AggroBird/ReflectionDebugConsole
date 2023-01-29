// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AggroBird.Reflection
{
    internal static class Operators
    {
        private readonly struct UnaryFunc
        {
            private delegate object SingleOperatorDelegate(ExecutionContext context, Expression arg);

            public static UnaryFunc MakeOperator<RetType>(Func<ExecutionContext, Expression, RetType> func)
            {
                return new UnaryFunc((ExecutionContext context, Expression arg) => func(context, arg), typeof(RetType));
            }

            private UnaryFunc(SingleOperatorDelegate func, Type returnType)
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

        private readonly struct InfixFunc
        {
            private delegate object InfixOperatorDelegate(ExecutionContext context, Expression lhs, Expression rhs);

            public static InfixFunc MakeOperator<RetType>(Func<ExecutionContext, Expression, Expression, RetType> func)
            {
                return new InfixFunc((ExecutionContext context, Expression lhs, Expression rhs) => func(context, lhs, rhs), typeof(RetType));
            }

            private InfixFunc(InfixOperatorDelegate func, Type returnType)
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

        private class UnaryOperator : Expression
        {
            public UnaryOperator(Expression arg, UnaryFunc func)
            {
                this.arg = arg;
                this.func = func;
            }

            public readonly Expression arg;
            public readonly UnaryFunc func;

            public override bool IsConstant => arg.IsConstant;


            public override object Execute(ExecutionContext context) => func.Invoke(context, arg);
            public override Type ResultType => func.ReturnType;
        }

        private class InfixOperator : Expression
        {
            public InfixOperator(Expression lhs, Expression rhs, InfixFunc func)
            {
                this.lhs = lhs;
                this.rhs = rhs;
                this.func = func;
            }

            public readonly Expression lhs;
            public readonly Expression rhs;
            public readonly InfixFunc func;

            public override bool IsConstant => lhs.IsConstant && rhs.IsConstant;


            public override object Execute(ExecutionContext context) => func.Invoke(context, lhs, rhs);
            public override Type ResultType => func.ReturnType;
        }

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


        private const string OpImplicit = "op_Implicit";
        private const string OpExplicit = "op_Explicit";

        public static bool ToBool(object val)
        {
            Type srcType = val.GetType();
            if (srcType == typeof(bool)) return (bool)val;
            throw new InvalidCastException(srcType, typeof(bool));
        }
        public static char ToChar(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (char)(sbyte)val;
                    case TypeCode.Byte: return (char)(byte)val;
                    case TypeCode.Int16: return (char)(short)val;
                    case TypeCode.UInt16: return (char)(ushort)val;
                    case TypeCode.Int32: return (char)(int)val;
                    case TypeCode.UInt32: return (char)(uint)val;
                    case TypeCode.Int64: return (char)(long)val;
                    case TypeCode.UInt64: return (char)(ulong)val;
                    case TypeCode.Single: return (char)(float)val;
                    case TypeCode.Double: return (char)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(double));
        }
        public static sbyte ToSByte(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (sbyte)(char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (sbyte)(byte)val;
                    case TypeCode.Int16: return (sbyte)(short)val;
                    case TypeCode.UInt16: return (sbyte)(ushort)val;
                    case TypeCode.Int32: return (sbyte)(int)val;
                    case TypeCode.UInt32: return (sbyte)(uint)val;
                    case TypeCode.Int64: return (sbyte)(long)val;
                    case TypeCode.UInt64: return (sbyte)(ulong)val;
                    case TypeCode.Single: return (sbyte)(float)val;
                    case TypeCode.Double: return (sbyte)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(sbyte));
        }
        public static byte ToByte(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (byte)(char)val;
                    case TypeCode.SByte: return (byte)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (byte)(short)val;
                    case TypeCode.UInt16: return (byte)(ushort)val;
                    case TypeCode.Int32: return (byte)(int)val;
                    case TypeCode.UInt32: return (byte)(uint)val;
                    case TypeCode.Int64: return (byte)(long)val;
                    case TypeCode.UInt64: return (byte)(ulong)val;
                    case TypeCode.Single: return (byte)(float)val;
                    case TypeCode.Double: return (byte)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(byte));
        }
        public static short ToInt16(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (short)(char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (short)(ushort)val;
                    case TypeCode.Int32: return (short)(int)val;
                    case TypeCode.UInt32: return (short)(uint)val;
                    case TypeCode.Int64: return (short)(long)val;
                    case TypeCode.UInt64: return (short)(ulong)val;
                    case TypeCode.Single: return (short)(float)val;
                    case TypeCode.Double: return (short)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(short));
        }
        public static ushort ToUInt16(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (ushort)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (ushort)(short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (ushort)(int)val;
                    case TypeCode.UInt32: return (ushort)(uint)val;
                    case TypeCode.Int64: return (ushort)(long)val;
                    case TypeCode.UInt64: return (ushort)(ulong)val;
                    case TypeCode.Single: return (ushort)(float)val;
                    case TypeCode.Double: return (ushort)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(ushort));
        }
        public static int ToInt32(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (int)(uint)val;
                    case TypeCode.Int64: return (int)(long)val;
                    case TypeCode.UInt64: return (int)(ulong)val;
                    case TypeCode.Single: return (int)(float)val;
                    case TypeCode.Double: return (int)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(int));
        }
        public static uint ToUInt32(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (uint)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (uint)(short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (uint)(int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (uint)(long)val;
                    case TypeCode.UInt64: return (uint)(ulong)val;
                    case TypeCode.Single: return (uint)(float)val;
                    case TypeCode.Double: return (uint)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(uint));
        }
        public static long ToInt64(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (long)val;
                    case TypeCode.UInt64: return (long)(ulong)val;
                    case TypeCode.Single: return (long)(float)val;
                    case TypeCode.Double: return (long)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(long));
        }
        public static ulong ToUInt64(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (ulong)(sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (ulong)(short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (ulong)(int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (ulong)(long)val;
                    case TypeCode.UInt64: return (ulong)val;
                    case TypeCode.Single: return (ulong)(float)val;
                    case TypeCode.Double: return (ulong)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(ulong));
        }
        public static float ToSingle(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (long)val;
                    case TypeCode.UInt64: return (ulong)val;
                    case TypeCode.Single: return (float)val;
                    case TypeCode.Double: return (float)(double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(float));
        }
        public static double ToDouble(object val)
        {
            Type srcType = val.GetType();
            unchecked
            {
                switch (Type.GetTypeCode(srcType))
                {
                    case TypeCode.Char: return (char)val;
                    case TypeCode.SByte: return (sbyte)val;
                    case TypeCode.Byte: return (byte)val;
                    case TypeCode.Int16: return (short)val;
                    case TypeCode.UInt16: return (ushort)val;
                    case TypeCode.Int32: return (int)val;
                    case TypeCode.UInt32: return (uint)val;
                    case TypeCode.Int64: return (long)val;
                    case TypeCode.UInt64: return (ulong)val;
                    case TypeCode.Single: return (float)val;
                    case TypeCode.Double: return (double)val;
                }
            }
            throw new InvalidCastException(srcType, typeof(double));
        }

        private static Expression MakeOperator(Expression arg, UnaryFunc func)
        {
            if (arg.IsConstant)
            {
                return new BoxedObject(func.Invoke(null, arg));
            }

            return new UnaryOperator(arg, func);
        }
        private static Expression MakeOperator(Expression lhs, Expression rhs, InfixFunc func)
        {
            if (lhs.IsConstant && rhs.IsConstant)
            {
                return new BoxedObject(func.Invoke(null, lhs, rhs));
            }

            return new InfixOperator(lhs, rhs, func);
        }

        public static Expression MakeUnaryOperator(TokenType op, Expression arg)
        {
            TokenInfo info = TokenUtility.GetTokenInfo(op);

            if (TryFindUserOperator(info, arg, out Expression expr))
            {
                return expr;
            }

            return MakeOperator(arg, MakeUnary(op, arg.GetTypeCode(), arg));
        }
        private static UnaryFunc MakeUnary(TokenType op, TypeCode type, Expression argExpr)
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
                        case TokenType.LogicalNot: return UnaryFunc.MakeOperator((c, a) => !ToBool(a.Execute(c)));
                    }
                    break;

                case TypeCode.SByte:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => (sbyte)(ToSByte(a.Execute(c)) + 1));
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => (sbyte)(ToSByte(a.Execute(c)) - 1));
                    }
                    break;

                case TypeCode.Byte:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => (byte)(ToByte(a.Execute(c)) + 1));
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => (byte)(ToByte(a.Execute(c)) - 1));
                    }
                    break;

                case TypeCode.Int16:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => (short)(ToInt16(a.Execute(c)) + 1));
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => (short)(ToInt16(a.Execute(c)) - 1));
                    }
                    break;

                case TypeCode.UInt16:
                    switch (op)
                    {
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => (ushort)(ToUInt16(a.Execute(c)) + 1));
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => (ushort)(ToUInt16(a.Execute(c)) - 1));
                    }
                    break;

                case TypeCode.Int32:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryFunc.MakeOperator((c, a) => ~ToInt32(a.Execute(c)));
                        case TokenType.Sub: return UnaryFunc.MakeOperator((c, a) => -ToInt32(a.Execute(c)));
                        case TokenType.Add: return UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c)));
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c)) + 1);
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c)) - 1);
                    }
                    break;
                case TypeCode.UInt32:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryFunc.MakeOperator((c, a) => ~ToUInt32(a.Execute(c)));
                        case TokenType.Sub: return UnaryFunc.MakeOperator((c, a) => -ToUInt32(a.Execute(c)));
                        case TokenType.Add: return UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c)));
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c)) + 1);
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c)) - 1);
                    }
                    break;
                case TypeCode.Int64:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryFunc.MakeOperator((c, a) => ~ToInt64(a.Execute(c)));
                        case TokenType.Sub: return UnaryFunc.MakeOperator((c, a) => -ToInt64(a.Execute(c)));
                        case TokenType.Add: return UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c)));
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c)) + 1);
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c)) - 1);
                    }
                    break;
                case TypeCode.UInt64:
                    switch (op)
                    {
                        case TokenType.BitwiseNot: return UnaryFunc.MakeOperator((c, a) => ~ToUInt64(a.Execute(c)));
                        case TokenType.Add: return UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c)));
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c)) + 1);
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c)) - 1);
                    }
                    break;
                case TypeCode.Single:
                    switch (op)
                    {
                        case TokenType.Sub: return UnaryFunc.MakeOperator((c, a) => -ToSingle(a.Execute(c)));
                        case TokenType.Add: return UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c)));
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c)) + 1);
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c)) - 1);
                    }
                    break;
                case TypeCode.Double:
                    switch (op)
                    {
                        case TokenType.Sub: return UnaryFunc.MakeOperator((c, a) => -ToDouble(a.Execute(c)));
                        case TokenType.Add: return UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c)));
                        case TokenType.Increment: return UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c)) + 1);
                        case TokenType.Decrement: return UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c)) - 1);
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
                return MakeOperator(lhsExpr, rhsExpr, MakeInfix(op, Max(lhsType, TypeCode.Int32), lhsExpr, rhsExpr));
            }
            else if (CheckArithmeticImplicitConversion(lhsType, rhsType, out TypeCode cast))
            {
                return MakeOperator(lhsExpr, rhsExpr, MakeInfix(op, cast, lhsExpr, rhsExpr));
            }
            else if (TryFindUserOperator(info, lhsExpr, rhsExpr, out Expression expr))
            {
                return expr;
            }
            else if (op == TokenType.Add && (lhsType == TypeCode.String || rhsType == TypeCode.String))
            {
                return MakeOperator(lhsExpr, rhsExpr, InfixFunc.MakeOperator((c, l, r) => Stringify(l.Execute(c)) + Stringify(r.Execute(c))));
            }
            else if (!lhsExpr.ResultType.IsValueType && !rhsExpr.ResultType.IsValueType)
            {
                switch (op)
                {
                    case TokenType.Eq: return MakeOperator(lhsExpr, rhsExpr, InfixFunc.MakeOperator((c, l, r) => ReferenceEquals(l.Execute(c), r.Execute(c))));
                    case TokenType.Ne: return MakeOperator(lhsExpr, rhsExpr, InfixFunc.MakeOperator((c, l, r) => !ReferenceEquals(l.Execute(c), r.Execute(c))));
                }
            }

            throw new DebugConsoleException($"Operator '{info.str}' cannot be applied to operands of type '{lhsExpr.ResultType}' and '{rhsExpr.ResultType}'");
        }
        private static InfixFunc MakeInfix(TokenType op, TypeCode type, Expression lhsExpr, Expression rhsExpr)
        {
            switch (type)
            {
                case TypeCode.Boolean:
                    switch (op)
                    {
                        case TokenType.BitwiseAnd: return InfixFunc.MakeOperator((c, l, r) => ToBool(l.Execute(c)) & ToBool(r.Execute(c)));
                        case TokenType.BitwiseXor: return InfixFunc.MakeOperator((c, l, r) => ToBool(l.Execute(c)) ^ ToBool(r.Execute(c)));
                        case TokenType.BitwiseOr: return InfixFunc.MakeOperator((c, l, r) => ToBool(l.Execute(c)) | ToBool(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToBool(l.Execute(c)) == ToBool(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToBool(l.Execute(c)) != ToBool(r.Execute(c)));
                    }
                    break;

                case TypeCode.Int32:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) * ToInt32(r.Execute(c)));
                        case TokenType.Div: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) / ToInt32(r.Execute(c)));
                        case TokenType.Mod: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) % ToInt32(r.Execute(c)));
                        case TokenType.Add: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) + ToInt32(r.Execute(c)));
                        case TokenType.Sub: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) - ToInt32(r.Execute(c)));
                        case TokenType.Lsh: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) << ToInt32(r.Execute(c)));
                        case TokenType.Rsh: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) >> ToInt32(r.Execute(c)));
                        case TokenType.Lt: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) < ToInt32(r.Execute(c)));
                        case TokenType.Gt: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) > ToInt32(r.Execute(c)));
                        case TokenType.Le: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) <= ToInt32(r.Execute(c)));
                        case TokenType.Ge: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) >= ToInt32(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) == ToInt32(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) != ToInt32(r.Execute(c)));
                        case TokenType.BitwiseAnd: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) & ToInt32(r.Execute(c)));
                        case TokenType.BitwiseXor: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) ^ ToInt32(r.Execute(c)));
                        case TokenType.BitwiseOr: return InfixFunc.MakeOperator((c, l, r) => ToInt32(l.Execute(c)) | ToInt32(r.Execute(c)));
                    }
                    break;
                case TypeCode.UInt32:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) * ToUInt32(r.Execute(c)));
                        case TokenType.Div: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) / ToUInt32(r.Execute(c)));
                        case TokenType.Mod: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) % ToUInt32(r.Execute(c)));
                        case TokenType.Add: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) + ToUInt32(r.Execute(c)));
                        case TokenType.Sub: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) - ToUInt32(r.Execute(c)));
                        case TokenType.Lsh: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) << ToInt32(r.Execute(c)));
                        case TokenType.Rsh: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) >> ToInt32(r.Execute(c)));
                        case TokenType.Lt: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) < ToUInt32(r.Execute(c)));
                        case TokenType.Gt: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) > ToUInt32(r.Execute(c)));
                        case TokenType.Le: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) <= ToUInt32(r.Execute(c)));
                        case TokenType.Ge: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) >= ToUInt32(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) == ToUInt32(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) != ToUInt32(r.Execute(c)));
                        case TokenType.BitwiseAnd: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) & ToUInt32(r.Execute(c)));
                        case TokenType.BitwiseXor: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) ^ ToUInt32(r.Execute(c)));
                        case TokenType.BitwiseOr: return InfixFunc.MakeOperator((c, l, r) => ToUInt32(l.Execute(c)) | ToUInt32(r.Execute(c)));
                    }
                    break;
                case TypeCode.Int64:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) * ToInt64(r.Execute(c)));
                        case TokenType.Div: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) / ToInt64(r.Execute(c)));
                        case TokenType.Mod: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) % ToInt64(r.Execute(c)));
                        case TokenType.Add: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) + ToInt64(r.Execute(c)));
                        case TokenType.Sub: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) - ToInt64(r.Execute(c)));
                        case TokenType.Lsh: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) << ToInt32(r.Execute(c)));
                        case TokenType.Rsh: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) >> ToInt32(r.Execute(c)));
                        case TokenType.Lt: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) < ToInt64(r.Execute(c)));
                        case TokenType.Gt: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) > ToInt64(r.Execute(c)));
                        case TokenType.Le: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) <= ToInt64(r.Execute(c)));
                        case TokenType.Ge: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) >= ToInt64(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) == ToInt64(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) != ToInt64(r.Execute(c)));
                        case TokenType.BitwiseAnd: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) & ToInt64(r.Execute(c)));
                        case TokenType.BitwiseXor: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) ^ ToInt64(r.Execute(c)));
                        case TokenType.BitwiseOr: return InfixFunc.MakeOperator((c, l, r) => ToInt64(l.Execute(c)) | ToInt64(r.Execute(c)));
                    }
                    break;
                case TypeCode.UInt64:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) * ToUInt64(r.Execute(c)));
                        case TokenType.Div: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) / ToUInt64(r.Execute(c)));
                        case TokenType.Mod: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) % ToUInt64(r.Execute(c)));
                        case TokenType.Add: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) + ToUInt64(r.Execute(c)));
                        case TokenType.Sub: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) - ToUInt64(r.Execute(c)));
                        case TokenType.Lsh: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) << ToInt32(r.Execute(c)));
                        case TokenType.Rsh: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) >> ToInt32(r.Execute(c)));
                        case TokenType.Lt: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) < ToUInt64(r.Execute(c)));
                        case TokenType.Gt: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) > ToUInt64(r.Execute(c)));
                        case TokenType.Le: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) <= ToUInt64(r.Execute(c)));
                        case TokenType.Ge: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) >= ToUInt64(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) == ToUInt64(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) != ToUInt64(r.Execute(c)));
                        case TokenType.BitwiseAnd: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) & ToUInt64(r.Execute(c)));
                        case TokenType.BitwiseXor: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) ^ ToUInt64(r.Execute(c)));
                        case TokenType.BitwiseOr: return InfixFunc.MakeOperator((c, l, r) => ToUInt64(l.Execute(c)) | ToUInt64(r.Execute(c)));
                    }
                    break;
                case TypeCode.Single:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) * ToSingle(r.Execute(c)));
                        case TokenType.Div: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) / ToSingle(r.Execute(c)));
                        case TokenType.Mod: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) % ToSingle(r.Execute(c)));
                        case TokenType.Add: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) + ToSingle(r.Execute(c)));
                        case TokenType.Sub: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) - ToSingle(r.Execute(c)));
                        case TokenType.Lt: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) < ToSingle(r.Execute(c)));
                        case TokenType.Gt: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) > ToSingle(r.Execute(c)));
                        case TokenType.Le: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) <= ToSingle(r.Execute(c)));
                        case TokenType.Ge: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) >= ToSingle(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) == ToSingle(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToSingle(l.Execute(c)) != ToSingle(r.Execute(c)));
                    }
                    break;
                case TypeCode.Double:
                    switch (op)
                    {
                        case TokenType.Mul: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) * ToDouble(r.Execute(c)));
                        case TokenType.Div: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) / ToDouble(r.Execute(c)));
                        case TokenType.Mod: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) % ToDouble(r.Execute(c)));
                        case TokenType.Add: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) + ToDouble(r.Execute(c)));
                        case TokenType.Sub: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) - ToDouble(r.Execute(c)));
                        case TokenType.Lt: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) < ToDouble(r.Execute(c)));
                        case TokenType.Gt: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) > ToDouble(r.Execute(c)));
                        case TokenType.Le: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) <= ToDouble(r.Execute(c)));
                        case TokenType.Ge: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) >= ToDouble(r.Execute(c)));
                        case TokenType.Eq: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) == ToDouble(r.Execute(c)));
                        case TokenType.Ne: return InfixFunc.MakeOperator((c, l, r) => ToDouble(l.Execute(c)) != ToDouble(r.Execute(c)));
                    }
                    break;
            }

            throw new DebugConsoleException($"Operator '{TokenUtility.GetTokenInfo(op).str}' cannot be applied to operands of type '{lhsExpr.ResultType}' and '{rhsExpr.ResultType}'");
        }

        public static bool TryMakeImplicitCastOperator(Expression expr, Type dstType, out Expression castExpr)
        {
            if (TryMakeImplicitCast(expr, dstType, out UnaryFunc func))
            {
                castExpr = MakeOperator(expr, func);
                return true;
            }

            if (TryFindUserCast(OpImplicit, expr.ResultType, dstType, out MethodInfo castMethod))
            {
                castExpr = new MethodMember(castMethod, new Expression[] { expr });
                return true;
            }

            castExpr = expr;
            return false;
        }
        private static bool TryMakeImplicitCast(Expression expr, Type dstType, out UnaryFunc func)
        {
            Type srcType = expr.ResultType;

            TypeCode srcTypeCode = Type.GetTypeCode(srcType);
            TypeCode dstTypeCode = Type.GetTypeCode(dstType);

            // Only language native implicit casts
            switch (srcTypeCode)
            {
                case TypeCode.Char:
                    switch (dstTypeCode)
                    {
                        case TypeCode.UInt16: func = UnaryFunc.MakeOperator((c, a) => ToUInt16(a.Execute(c))); return true;
                        case TypeCode.Int32: func = UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c))); return true;
                        case TypeCode.UInt32: func = UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c))); return true;
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.UInt64: func = UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.SByte:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int16: func = UnaryFunc.MakeOperator((c, a) => ToInt16(a.Execute(c))); return true;
                        case TypeCode.Int32: func = UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c))); return true;
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.Byte:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int16: func = UnaryFunc.MakeOperator((c, a) => ToInt16(a.Execute(c))); return true;
                        case TypeCode.UInt16: func = UnaryFunc.MakeOperator((c, a) => ToUInt16(a.Execute(c))); return true;
                        case TypeCode.Int32: func = UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c))); return true;
                        case TypeCode.UInt32: func = UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c))); return true;
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.UInt64: func = UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.Int16:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int32: func = UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c))); return true;
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.UInt16:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int32: func = UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c))); return true;
                        case TypeCode.UInt32: func = UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c))); return true;
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.UInt64: func = UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.Int32:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.UInt32:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.UInt64: func = UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.Int64:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.UInt64:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
                case TypeCode.Single:
                    switch (dstTypeCode)
                    {
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
            }

            func = default;
            return false;
        }

        public static bool TryMakeExplicitCastOperator(Expression expr, Type dstType, out Expression castExpr)
        {
            if (TryMakeExplicitCast(expr, dstType, out UnaryFunc func))
            {
                castExpr = MakeOperator(expr, func);
                return true;
            }

            if (TryFindUserCast(OpExplicit, expr.ResultType, dstType, out MethodInfo castMethod))
            {
                castExpr = new MethodMember(castMethod, new Expression[] { expr });
                return true;
            }

            castExpr = expr;
            return false;
        }
        private static bool TryMakeExplicitCast(Expression expr, Type dstType, out UnaryFunc func)
        {
            Type srcType = expr.ResultType;

            TypeCode srcTypeCode = Type.GetTypeCode(srcType);
            TypeCode dstTypeCode = Type.GetTypeCode(dstType);

            // Regular typecast
            switch (srcTypeCode)
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
                    switch (dstTypeCode)
                    {
                        case TypeCode.Char: func = UnaryFunc.MakeOperator((c, a) => ToChar(a.Execute(c))); return true;
                        case TypeCode.SByte: func = UnaryFunc.MakeOperator((c, a) => ToSByte(a.Execute(c))); return true;
                        case TypeCode.Byte: func = UnaryFunc.MakeOperator((c, a) => ToByte(a.Execute(c))); return true;
                        case TypeCode.Int16: func = UnaryFunc.MakeOperator((c, a) => ToInt16(a.Execute(c))); return true;
                        case TypeCode.UInt16: func = UnaryFunc.MakeOperator((c, a) => ToUInt16(a.Execute(c))); return true;
                        case TypeCode.Int32: func = UnaryFunc.MakeOperator((c, a) => ToInt32(a.Execute(c))); return true;
                        case TypeCode.UInt32: func = UnaryFunc.MakeOperator((c, a) => ToUInt32(a.Execute(c))); return true;
                        case TypeCode.Int64: func = UnaryFunc.MakeOperator((c, a) => ToInt64(a.Execute(c))); return true;
                        case TypeCode.UInt64: func = UnaryFunc.MakeOperator((c, a) => ToUInt64(a.Execute(c))); return true;
                        case TypeCode.Single: func = UnaryFunc.MakeOperator((c, a) => ToSingle(a.Execute(c))); return true;
                        case TypeCode.Double: func = UnaryFunc.MakeOperator((c, a) => ToDouble(a.Execute(c))); return true;
                    }
                    break;
            }

            func = default;
            return false;
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
                    expr = new MethodMember(optimal[0], args);
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
                    expr = new MethodMember(optimal[0], args);
                    return true;
                }
            }

            expr = null;
            return false;
        }

        private static bool TryFindUserCast(string name, Type srcType, Type dstType, out MethodInfo result)
        {
            MethodInfo[] srcCasts = srcType.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy) as MethodInfo[];
            for (int i = 0; i < srcCasts.Length; i++)
            {
                MethodInfo castMethod = srcCasts[i];
                ParameterInfo[] parameters = castMethod.GetParameters();
                if (parameters.Length == 1 && castMethod.ReturnType.Equals(dstType) && parameters[0].ParameterType.IsAssignableFrom(srcType))
                {
                    result = castMethod;
                    return true;
                }
            }

            MethodInfo[] dstCasts = dstType.GetMember(name, MemberTypes.Method, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy) as MethodInfo[];
            for (int i = 0; i < dstCasts.Length; i++)
            {
                MethodInfo castMethod = dstCasts[i];
                ParameterInfo[] parameters = castMethod.GetParameters();
                if (parameters.Length == 1 && castMethod.ReturnType.Equals(dstType) && parameters[0].ParameterType.IsAssignableFrom(srcType))
                {
                    result = castMethod;
                    return true;
                }
            }

            result = null;
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