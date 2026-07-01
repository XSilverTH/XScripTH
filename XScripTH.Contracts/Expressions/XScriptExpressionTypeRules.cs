namespace XScripTH.Contracts.Expressions;

public static class XScriptExpressionTypeRules
{
    public static bool IsNumeric(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;
        return unwrapped == typeof(byte)
            || unwrapped == typeof(sbyte)
            || unwrapped == typeof(short)
            || unwrapped == typeof(ushort)
            || unwrapped == typeof(int)
            || unwrapped == typeof(uint)
            || unwrapped == typeof(long)
            || unwrapped == typeof(ulong)
            || unwrapped == typeof(float)
            || unwrapped == typeof(double)
            || unwrapped == typeof(decimal);
    }

    public static Type PromoteUnaryNumeric(Type operandType)
    {
        var type = Nullable.GetUnderlyingType(operandType) ?? operandType;
        if (!IsNumeric(type))
            throw new InvalidOperationException("Unary numeric expression requires a numeric operand.");

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal) || type == typeof(long))
            return type;

        if (type == typeof(ulong))
            return typeof(long);

        return typeof(int);
    }

    public static Type PromoteNumeric(Type leftType, Type rightType)
    {
        var left = Nullable.GetUnderlyingType(leftType) ?? leftType;
        var right = Nullable.GetUnderlyingType(rightType) ?? rightType;

        if (!IsNumeric(left) || !IsNumeric(right))
            throw new InvalidOperationException("Expression numeric promotion requires numeric operands.");

        if ((left == typeof(decimal) && (right == typeof(float) || right == typeof(double)))
            || (right == typeof(decimal) && (left == typeof(float) || left == typeof(double))))
            throw new InvalidOperationException("Cannot mix decimal operands with float or double operands in an expression.");

        if (left == typeof(decimal) || right == typeof(decimal))
            return typeof(decimal);

        if (left == typeof(double) || right == typeof(double))
            return typeof(double);

        if (left == typeof(float) || right == typeof(float))
            return typeof(float);

        if (left == typeof(ulong) || right == typeof(ulong))
        {
            var other = left == typeof(ulong) ? right : left;
            if (other == typeof(byte) || other == typeof(ushort) || other == typeof(uint) || other == typeof(ulong))
                return typeof(ulong);

            throw new InvalidOperationException("Cannot mix ulong with signed integral operands in an expression.");
        }

        if (left == typeof(long) || right == typeof(long))
            return typeof(long);

        if (left == typeof(uint) || right == typeof(uint))
        {
            var other = left == typeof(uint) ? right : left;
            return other == typeof(sbyte) || other == typeof(short) || other == typeof(int)
                ? typeof(long)
                : typeof(uint);
        }

        return typeof(int);
    }
}
