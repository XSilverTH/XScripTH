namespace XScripTH;

internal enum CliExitCode
{
    Success = 0,
    UsageError = 2,
    InputError = 3,
    SyntaxError = 10,
    TypeError = 11,
    SymbolError = 12,
    CompileError = 13,
    RuntimeError = 20,
    Canceled = 130
}
