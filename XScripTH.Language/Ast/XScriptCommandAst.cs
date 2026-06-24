namespace XScripTH.Language.Ast;

public sealed record XScriptCommandAst(
    string Name,
    IReadOnlyList<XScriptArgumentAst> Arguments,
    XScriptCommandTerminator Terminator);
