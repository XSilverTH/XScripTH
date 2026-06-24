namespace XScripTH.Language.Ast;

public abstract record XScriptArgumentAst;

public sealed record XScriptLiteralArgumentAst(object Value) : XScriptArgumentAst;

public sealed record XScriptCommandArgumentAst(XScriptCommandAst Command) : XScriptArgumentAst;

public sealed record XScriptVariableArgumentAst(string Name) : XScriptArgumentAst;
