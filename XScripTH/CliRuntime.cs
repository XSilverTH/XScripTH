using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Core.Commands.Console;
using XScripTH.Engine;
using XScripTH.Language.Compilation;
using XScripTH.Language.Parsing;
using XScripTH.Language.Validation.Exceptions;

namespace XScripTH;

internal sealed class CliRuntime(DiagnosticWriter diagnostics, TextWriter output)
{
    public async Task<CliExitCode> RunFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!TryReadScript(path, out var source, out var exitCode))
            return exitCode;

        var compiler = CreateCompiler();
        var engine = new XScripTHEngine();
        var context = new XScriptExecutionContext(engine);
        return await CompileAndExecuteAsync(compiler, engine, context, source, path, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CliExitCode> CheckFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!TryReadScript(path, out var source, out var exitCode))
            return exitCode;

        var compiler = CreateCompiler();
        var context = new CompilationContext();
        var result = await CompileOnlyAsync(compiler, source, context, path, cancellationToken).ConfigureAwait(false);
        if (result != CliExitCode.Success)
            return result;

        output.WriteLine($"OK {path}");
        return CliExitCode.Success;
    }

    public static XScriptCompiler CreateCompiler()
    {
        var registry = CommandRegistry.FromAssemblies(typeof(Print).Assembly);
        return new XScriptCompiler(registry);
    }

    public async Task<CliExitCode> CompileOnlyAsync(
        XScriptCompiler compiler,
        string source,
        ICompilationContext compilationContext,
        string? path,
        CancellationToken cancellationToken)
    {
        try
        {
            await compiler.CompileAsync(source, compilationContext, cancellationToken).ConfigureAwait(false);
            return CliExitCode.Success;
        }
        catch (Exception exception) when (TryRenderCompileException(exception, source, path, out var code))
        {
            return code;
        }
    }

    public async Task<CliExitCode> CompileAndExecuteAsync(
        XScriptCompiler compiler,
        XScripTHEngine engine,
        IExecutionContext executionContext,
        string source,
        string? path,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ICommandInvocation> invocations;
        try
        {
            invocations = await compiler.CompileAsync(source, new CompilationContext(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (TryRenderCompileException(exception, source, path, out var code))
        {
            return code;
        }

        return await ExecuteAsync(engine, executionContext, invocations, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CliExitCode> CompileAndExecuteAsync(
        XScriptCompiler compiler,
        ICompilationContext compilationContext,
        XScripTHEngine engine,
        IExecutionContext executionContext,
        string source,
        string? path,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ICommandInvocation> invocations;
        try
        {
            invocations = await compiler.CompileAsync(source, compilationContext, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (TryRenderCompileException(exception, source, path, out var code))
        {
            return code;
        }

        return await ExecuteAsync(engine, executionContext, invocations, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CliExitCode> ExecuteAsync(
        XScripTHEngine engine,
        IExecutionContext executionContext,
        IReadOnlyList<ICommandInvocation> invocations,
        CancellationToken cancellationToken)
    {
        try
        {
            var outputs = await engine.ExecuteAllAsync(invocations, executionContext, cancellationToken)
                .ConfigureAwait(false);
            if (outputs.Any(output => output.Status == CommandStatus.Error))
            {
                diagnostics.WriteRuntimeStatusError();
                return CliExitCode.RuntimeError;
            }

            return CliExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            diagnostics.WriteError("Canceled", "Operation canceled.");
            return CliExitCode.Canceled;
        }
        catch (Exception exception)
        {
            diagnostics.WriteRuntimeError(exception);
            return CliExitCode.RuntimeError;
        }
    }

    private bool TryReadScript(string path, out string source, out CliExitCode exitCode)
    {
        source = string.Empty;
        exitCode = CliExitCode.Success;

        try
        {
            source = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostics.WriteError("Input file could not be read", exception.Message);
            exitCode = CliExitCode.InputError;
            return false;
        }
    }

    private bool TryRenderCompileException(Exception exception, string source, string? path, out CliExitCode code)
    {
        switch (exception)
        {
            case XScriptParseException parseException:
                diagnostics.WriteParseError(parseException, source, path);
                code = CliExitCode.SyntaxError;
                return true;
            case CommandTypeCheckException typeException:
                diagnostics.WriteTypeError(typeException);
                code = CliExitCode.TypeError;
                return true;
            case XScriptCommandResolutionException or XScriptVariableResolutionException or XScriptFunctionResolutionException:
                diagnostics.WriteSymbolError(exception);
                code = CliExitCode.SymbolError;
                return true;
            case OperationCanceledException:
                diagnostics.WriteError("Canceled", "Operation canceled.");
                code = CliExitCode.Canceled;
                return true;
            case InvalidOperationException:
                diagnostics.WriteCompileError(exception);
                code = CliExitCode.CompileError;
                return true;
            default:
                code = CliExitCode.CompileError;
                return false;
        }
    }
}
