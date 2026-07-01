namespace XScripTH.Language.Ast;

public abstract record XScriptArgumentAst;

public sealed record XScriptLiteralArgumentAst(object Value) : XScriptArgumentAst;

public sealed record XScriptCommandArgumentAst(XScriptCommandAst Command) : XScriptArgumentAst;

public sealed record XScriptVariableArgumentAst(string Name) : XScriptArgumentAst;

public sealed record XScriptBlockArgumentAst(IReadOnlyList<XScriptCommandAst> Commands) : XScriptArgumentAst;

public sealed record XScriptFunctionReferenceArgumentAst(string Name) : XScriptArgumentAst;

public abstract record XScriptExpressionArgumentAst : XScriptArgumentAst;

public sealed record XScriptUnaryExpressionAst(
    XScriptExpressionOperator Operator,
    XScriptArgumentAst Operand) : XScriptExpressionArgumentAst;

public sealed record XScriptBinaryExpressionAst(
    XScriptExpressionOperator Operator,
    XScriptArgumentAst Left,
    XScriptArgumentAst Right) : XScriptExpressionArgumentAst;

public enum XScriptExpressionOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Negate,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    And,
    Or,
    Not
}