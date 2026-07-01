using System.Reflection;

namespace XScripTH;

internal sealed class CliApplication(TextReader input, TextWriter output, TextWriter error)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        CliCommand command;
        try
        {
            command = CliParser.Parse(args);
        }
        catch (CliUsageException exception)
        {
            var diagnostics = new DiagnosticWriter(error, ColorEnabled(noColor: false));
            diagnostics.WriteError("Usage error", exception.Message);
            WriteUsage(error);
            return (int)CliExitCode.UsageError;
        }

        var diagnosticsForCommand = new DiagnosticWriter(error, ColorEnabled(command.NoColor));
        var runtime = new CliRuntime(diagnosticsForCommand, output);

        try
        {
            var exitCode = command.Mode switch
            {
                CliMode.Help => WriteUsage(output),
                CliMode.Version => WriteVersion(output),
                CliMode.Run => await runtime.RunFileAsync(command.ScriptPath!, cancellationToken).ConfigureAwait(false),
                CliMode.Check => await runtime.CheckFileAsync(command.ScriptPath!, cancellationToken)
                    .ConfigureAwait(false),
                CliMode.Repl => await new ReplSession(runtime, input, output).RunAsync(cancellationToken)
                    .ConfigureAwait(false),
                _ => CliExitCode.UsageError
            };

            return (int)exitCode;
        }
        catch (Exception exception)
        {
            diagnosticsForCommand.WriteError("Unexpected failure", exception.Message);
            if (command.Verbose)
                diagnosticsForCommand.WriteVerbose(exception);
            return (int)CliExitCode.RuntimeError;
        }
    }

    private static CliExitCode WriteUsage(TextWriter writer)
    {
        writer.WriteLine("XScripTH - command-line script runner");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  xscripth run <script.xs>      Compile and execute a script file.");
        writer.WriteLine("  xscripth check <script.xs>    Parse and type-check without runtime execution.");
        writer.WriteLine("  xscripth repl                 Start an interactive shell.");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -h, --help                    Show this help.");
        writer.WriteLine("      --version                 Show version information.");
        writer.WriteLine("  -v, --verbose                 Include stack traces for unexpected failures.");
        writer.WriteLine("      --no-color                Disable ANSI diagnostic color.");
        writer.WriteLine();
        writer.WriteLine("Exit codes:");
        writer.WriteLine("  0   Success");
        writer.WriteLine("  2   Usage error");
        writer.WriteLine("  3   Input file error");
        writer.WriteLine("  10  Syntax error");
        writer.WriteLine("  11  Static type error");
        writer.WriteLine("  12  Symbol resolution error");
        writer.WriteLine("  13  Compile-time execution error");
        writer.WriteLine("  20  Runtime execution error");
        writer.WriteLine("  130 Canceled");
        return CliExitCode.Success;
    }

    private static CliExitCode WriteVersion(TextWriter writer)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        writer.WriteLine($"XScripTH {version}");
        return CliExitCode.Success;
    }

    private static bool ColorEnabled(bool noColor)
    {
        return !noColor && !Console.IsErrorRedirected &&
               string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }
}