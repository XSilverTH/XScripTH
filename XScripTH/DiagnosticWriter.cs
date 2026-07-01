using XScripTH.Language.Compilation;
using XScripTH.Language.Parsing;
using XScripTH.Language.Validation.Exceptions;

namespace XScripTH;

internal sealed class DiagnosticWriter(TextWriter error, bool colorEnabled)
{
    private const string Reset = "\e[0m";
    private const string Red = "\e[31m";
    private const string Yellow = "\e[33m";
    private const string Dim = "\e[2m";

    public void WriteError(string title, string message)
    {
        WriteHeading("error", title);
        error.WriteLine(message);
    }

    public void WriteParseError(XScriptParseException exception, string? source = null, string? path = null)
    {
        WriteHeading("syntax", "Script could not be parsed");
        WriteLocation(path, exception.Line, exception.Column);
        error.WriteLine(exception.Message);
        WriteSourceLine(source, exception.Line, exception.Column);
    }

    public void WriteTypeError(CommandTypeCheckException exception)
    {
        WriteHeading("type", $"Command '{exception.CommandName}' failed static type validation");
        foreach (var typeError in exception.Errors)
        {
            var path = typeError.Path.Count == 0 ? "<root>" : string.Join(" -> ", typeError.Path);
            error.WriteLine($"  at {path}: {typeError.Message}");
            if (typeError.ExpectedType is null && typeError.ActualType is null) continue;
            error.WriteLine($"     expected: {FormatType(typeError.ExpectedType)}");
            error.WriteLine($"       actual: {FormatType(typeError.ActualType)}");
        }
    }

    public void WriteSymbolError(Exception exception)
    {
        WriteHeading("symbol", "Name resolution failed");
        error.WriteLine(exception.Message);
    }

    public void WriteCompileError(Exception exception)
    {
        WriteHeading("compile", "Compilation failed");
        error.WriteLine(exception.Message);
    }

    public void WriteRuntimeError(Exception exception)
    {
        WriteHeading("runtime", "Execution failed");
        error.WriteLine(exception.Message);
    }

    public void WriteRuntimeStatusError()
    {
        WriteHeading("runtime", "Script returned an error status");
        error.WriteLine("A command completed with CommandStatus.Error.");
    }

    public void WriteVerbose(Exception exception)
    {
        error.WriteLine();
        error.WriteLine(Color(Dim, exception.ToString()));
    }

    private void WriteHeading(string category, string title)
    {
        error.Write(Color(Red, $"XScripTH {category}: "));
        error.WriteLine(title);
    }

    private void WriteLocation(string? path, int line, int column)
    {
        if (line <= 0 || column <= 0)
            return;

        var file = string.IsNullOrWhiteSpace(path) ? "<input>" : path;
        error.WriteLine(Color(Yellow, $" --> {file}:{line}:{column}"));
    }

    private void WriteSourceLine(string? source, int line, int column)
    {
        if (string.IsNullOrEmpty(source) || line <= 0)
            return;

        using var reader = new StringReader(source);
        string? current = null;
        for (var index = 1; index <= line; index++)
        {
            current = reader.ReadLine();
            if (current is null)
                return;
        }

        error.WriteLine($" {line,4} | {current}");
        error.WriteLine($"      | {new string(' ', Math.Max(0, column - 1))}{Color(Red, "^")}");
    }

    private string Color(string code, string value) => colorEnabled ? code + value + Reset : value;

    private static string FormatType(Type? type) => type?.FullName ?? "<unknown>";
}