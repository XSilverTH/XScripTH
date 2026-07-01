using System.Globalization;
using XScripTH.Contracts.Expressions;

namespace XScripTH.Core.Commands.Expressions;

internal static class ExpressionCommandRuntime
{
    public static IReadOnlyList<object?> RequireInputCount(IReadOnlyList<object?>? input, string commandName, int expectedCount)
    {
        if (input is null || input.Count != expectedCount)
            throw new ArgumentException($"{commandName} requires exactly {expectedCount} input value(s).", nameof(input));

        return input;
    }

    public static (object Left, object Right, Type PromotedType) RequireNumericPair(
        IReadOnlyList<object?>? input,
        string commandName)
    {
        var values = RequireInputCount(input, commandName, 2);
        var left = values[0];
        var right = values[1];
        if (left is null || right is null
            || !XScriptExpressionTypeRules.IsNumeric(left.GetType())
            || !XScriptExpressionTypeRules.IsNumeric(right.GetType()))
            throw new ArgumentException($"{commandName} requires numeric input values.", nameof(input));

        var promotedType = XScriptExpressionTypeRules.PromoteNumeric(left.GetType(), right.GetType());
        return (Convert.ChangeType(left, promotedType, CultureInfo.InvariantCulture),
            Convert.ChangeType(right, promotedType, CultureInfo.InvariantCulture),
            promotedType);
    }

    public static (object Operand, Type PromotedType) RequireNumericOperand(
        IReadOnlyList<object?>? input,
        string commandName)
    {
        var values = RequireInputCount(input, commandName, 1);
        var operand = values[0];
        if (operand is null || !XScriptExpressionTypeRules.IsNumeric(operand.GetType()))
            throw new ArgumentException($"{commandName} requires numeric input values.", nameof(input));

        var promotedType = XScriptExpressionTypeRules.PromoteUnaryNumeric(operand.GetType());
        return (Convert.ChangeType(operand, promotedType, CultureInfo.InvariantCulture), promotedType);
    }

    public static object Add(object left, object right, Type type)
    {
        checked
        {
            if (type == typeof(decimal)) return (decimal)left + (decimal)right;
            if (type == typeof(double)) return (double)left + (double)right;
            if (type == typeof(float)) return (float)left + (float)right;
            if (type == typeof(ulong)) return (ulong)left + (ulong)right;
            if (type == typeof(long)) return (long)left + (long)right;
            if (type == typeof(uint)) return (uint)left + (uint)right;
            return (int)left + (int)right;
        }
    }

    public static object Subtract(object left, object right, Type type)
    {
        checked
        {
            if (type == typeof(decimal)) return (decimal)left - (decimal)right;
            if (type == typeof(double)) return (double)left - (double)right;
            if (type == typeof(float)) return (float)left - (float)right;
            if (type == typeof(ulong)) return (ulong)left - (ulong)right;
            if (type == typeof(long)) return (long)left - (long)right;
            if (type == typeof(uint)) return (uint)left - (uint)right;
            return (int)left - (int)right;
        }
    }

    public static object Multiply(object left, object right, Type type)
    {
        checked
        {
            if (type == typeof(decimal)) return (decimal)left * (decimal)right;
            if (type == typeof(double)) return (double)left * (double)right;
            if (type == typeof(float)) return (float)left * (float)right;
            if (type == typeof(ulong)) return (ulong)left * (ulong)right;
            if (type == typeof(long)) return (long)left * (long)right;
            if (type == typeof(uint)) return (uint)left * (uint)right;
            return (int)left * (int)right;
        }
    }

    public static object Divide(object left, object right, Type type)
    {
        if (IsZero(right, type))
            throw new DivideByZeroException("Expression division by zero.");

        checked
        {
            if (type == typeof(decimal)) return (decimal)left / (decimal)right;
            if (type == typeof(double)) return (double)left / (double)right;
            if (type == typeof(float)) return (float)left / (float)right;
            if (type == typeof(ulong)) return (ulong)left / (ulong)right;
            if (type == typeof(long)) return (long)left / (long)right;
            if (type == typeof(uint)) return (uint)left / (uint)right;
            return (int)left / (int)right;
        }
    }

    public static object Modulo(object left, object right, Type type)
    {
        if (IsZero(right, type))
            throw new DivideByZeroException("Expression division by zero.");

        checked
        {
            if (type == typeof(decimal)) return (decimal)left % (decimal)right;
            if (type == typeof(double)) return (double)left % (double)right;
            if (type == typeof(float)) return (float)left % (float)right;
            if (type == typeof(ulong)) return (ulong)left % (ulong)right;
            if (type == typeof(long)) return (long)left % (long)right;
            if (type == typeof(uint)) return (uint)left % (uint)right;
            return (int)left % (int)right;
        }
    }

    public static object Negate(object operand, Type type)
    {
        checked
        {
            if (type == typeof(decimal)) return -(decimal)operand;
            if (type == typeof(double)) return -(double)operand;
            if (type == typeof(float)) return -(float)operand;
            if (type == typeof(long)) return -(long)operand;
            return -(int)operand;
        }
    }

    public static int Compare(object left, object right, Type type) => ((IComparable)left).CompareTo(right);

    public static bool Equal(IReadOnlyList<object?>? input, string commandName)
    {
        var values = RequireInputCount(input, commandName, 2);
        var left = values[0];
        var right = values[1];
        if (left is not null && right is not null
            && XScriptExpressionTypeRules.IsNumeric(left.GetType())
            && XScriptExpressionTypeRules.IsNumeric(right.GetType()))
        {
            var promotedType = XScriptExpressionTypeRules.PromoteNumeric(left.GetType(), right.GetType());
            var convertedLeft = Convert.ChangeType(left, promotedType, CultureInfo.InvariantCulture);
            var convertedRight = Convert.ChangeType(right, promotedType, CultureInfo.InvariantCulture);
            return Equals(convertedLeft, convertedRight);
        }

        return Equals(left, right);
    }

    public static bool Bool(IReadOnlyList<object?>? input, string commandName, int index)
    {
        var expectedCount = commandName == "not" ? 1 : 2;
        var values = RequireInputCount(input, commandName, expectedCount);
        return (bool)values[index]!;
    }

    private static bool IsZero(object value, Type type)
    {
        if (type == typeof(decimal)) return (decimal)value == 0m;
        if (type == typeof(double)) return (double)value == 0d;
        if (type == typeof(float)) return (float)value == 0f;
        if (type == typeof(ulong)) return (ulong)value == 0UL;
        if (type == typeof(long)) return (long)value == 0L;
        if (type == typeof(uint)) return (uint)value == 0U;
        return (int)value == 0;
    }
}
