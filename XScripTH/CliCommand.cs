namespace XScripTH;

internal enum CliMode
{
    Help,
    Version,
    Run,
    Check,
    Repl
}

internal sealed record CliCommand(
    CliMode Mode,
    string? ScriptPath = null,
    bool Verbose = false,
    bool NoColor = false);