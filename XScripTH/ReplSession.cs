using XScripTH.Contracts.Models;
using XScripTH.Engine;
using XScripTH.Language.Compilation;

namespace XScripTH;

internal sealed class ReplSession(CliRuntime runtime, TextReader input, TextWriter output)
{
    public async Task<CliExitCode> RunAsync(CancellationToken cancellationToken)
    {
        var compiler = CliRuntime.CreateCompiler();
        var engine = new XScripTHEngine();
        var compilationContext = new CompilationContext();
        var executionContext = new XScriptExecutionContext(engine);

        output.WriteLine("XScripTH REPL. Enter script statements, :check <script>, :reset, :help, or :exit.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var entry = ReadEntry();
            if (entry is null)
                return CliExitCode.Success;

            var trimmed = entry.Trim();
            if (trimmed.Length == 0)
                continue;

            if (trimmed is ":exit" or ":quit")
                return CliExitCode.Success;

            if (trimmed == ":help")
            {
                WriteHelp();
                continue;
            }

            if (trimmed == ":reset")
            {
                compiler = CliRuntime.CreateCompiler();
                engine = new XScripTHEngine();
                compilationContext = new CompilationContext();
                executionContext = new XScriptExecutionContext(engine);
                output.WriteLine("State reset.");
                continue;
            }

            if (trimmed.StartsWith(":check ", StringComparison.Ordinal))
            {
                var source = trimmed[7..].TrimStart();
                var checkCode = await runtime.CompileOnlyAsync(compiler, source, new CompilationContext(), "<repl>", cancellationToken)
                    .ConfigureAwait(false);
                if (checkCode == CliExitCode.Success)
                    output.WriteLine("OK");
                continue;
            }

            if (trimmed.StartsWith(":load ", StringComparison.Ordinal))
            {
                var path = trimmed[6..].Trim();
                await runtime.RunFileAsync(path, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await runtime.CompileAndExecuteAsync(
                    compiler,
                    compilationContext,
                    engine,
                    executionContext,
                    entry,
                    "<repl>",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return CliExitCode.Canceled;
    }

    private string? ReadEntry()
    {
        output.Write("xscripth> ");
        var line = input.ReadLine();
        if (line is null)
            return null;

        var lines = new List<string> { line };
        var balance = GetBraceBalance(line);
        while (balance > 0)
        {
            output.Write("      ... ");
            line = input.ReadLine();
            if (line is null)
                break;

            lines.Add(line);
            balance += GetBraceBalance(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void WriteHelp()
    {
        output.WriteLine("REPL commands:");
        output.WriteLine("  <script>        Compile and execute one statement or balanced block.");
        output.WriteLine("  :check <script> Compile and type-check without runtime execution.");
        output.WriteLine("  :reset          Clear compile-time and runtime state.");
        output.WriteLine("  :exit           Leave the REPL.");
    }

    private static int GetBraceBalance(string line)
    {
        var balance = 0;
        var inString = false;
        var escaped = false;

        foreach (var ch in line)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            balance += ch switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };
        }

        return balance;
    }
}
