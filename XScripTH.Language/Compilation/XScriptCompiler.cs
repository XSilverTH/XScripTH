using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Language.Validation;
using XScripTH.Language.Ast;

namespace XScripTH.Language;

public sealed class XScriptCompiler
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly ICommandTypeChecker _typeChecker;
    private readonly XScriptParser _parser;
    private readonly ICommandExecutor? _executor;

    public XScriptCompiler(
        ICommandRegistry commandRegistry,
        ICommandTypeChecker? typeChecker = null,
        XScriptParser? parser = null,
        ICommandExecutor? executor = null)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _commandRegistry = commandRegistry;
        _typeChecker = typeChecker ?? CommandTypeChecker.Default;
        _parser = parser ?? new XScriptParser();
        _executor = executor;
    }

    public async Task<IReadOnlyList<Task<ICommandInvocation>>> CompileAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var ast = _parser.Parse(source);
        return await CompileAsync(ast, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Task<ICommandInvocation>>> CompileAsync(
        XScriptProgramAst ast,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ast);

        var lowered = new List<(ICommandInvocation Invocation, XScriptCommandTerminator Terminator)>();
        foreach (var cmdAst in ast.Commands)
        {
            var loweredCommand = await LowerCommandAsync(cmdAst, cancellationToken).ConfigureAwait(false);
            if (loweredCommand.RuntimeInvocation is not null)
            {
                lowered.Add((loweredCommand.RuntimeInvocation, cmdAst.Terminator));
            }
        }

        // Validate type safety before execution so nested outputs match parent inputs.
        var invocationsToValidate = lowered.Select(l => Task.FromResult(l.Invocation)).ToList();
        await _typeChecker.EnsureValidAsync(invocationsToValidate, cancellationToken).ConfigureAwait(false);

        var result = new List<Task<ICommandInvocation>>();
        foreach (var (invocation, terminator) in lowered)
        {
            if (terminator == XScriptCommandTerminator.Await)
            {
                result.Add(Task.FromResult(invocation));
            }
            else // FireAndForget
            {
                var executor = _executor
                    ?? throw new InvalidOperationException("Fire-and-forget commands require an ICommandExecutor.");
                ICommand fireAndForgetCommand = new FireAndForgetCommand(invocation, executor);
                ICommandInvocation fireAndForgetInvocation = new CommandInvocation(
                    Task.FromResult(fireAndForgetCommand),
                    Array.Empty<ICommandArgument>()
                );
                result.Add(Task.FromResult(fireAndForgetInvocation));
            }
        }

        return result;
    }

    private async Task<LoweredCommand> LowerCommandAsync(
        XScriptCommandAst commandAst,
        CancellationToken cancellationToken)
    {
        if (!_commandRegistry.TryCreate(commandAst.Name, out var command) || command == null)
        {
            throw new XScriptCommandResolutionException(commandAst.Name);
        }

        var isCompileTime = IsCompileTime(command);
        var arguments = new List<ICommandArgument>();
        foreach (var argAst in commandAst.Arguments)
        {
            switch (argAst)
            {
                case XScriptLiteralArgumentAst literalArg:
                    arguments.Add(new CommandValueArgument(literalArg.Value));
                    break;
                case XScriptCommandArgumentAst commandArg:
                    var nested = await LowerCommandAsync(commandArg.Command, cancellationToken).ConfigureAwait(false);
                    if (nested.CompileTimeOutput is not null)
                    {
                        if (nested.CompileTimeOutput.Status != CommandStatus.Ok ||
                            nested.CompileTimeOutput.Values is not { Count: 1 })
                        {
                            throw new InvalidOperationException($"Compile-time command '{nested.Name}' used as an argument must complete successfully with exactly one output value.");
                        }

                        arguments.Add(new CommandValueArgument(nested.CompileTimeOutput.Values[0]));
                    }
                    else if (isCompileTime)
                    {
                        throw new InvalidOperationException($"Compile-time command '{commandAst.Name}' cannot use runtime command argument '{commandArg.Command.Name}'.");
                    }
                    else
                    {
                        arguments.Add(new CommandInvocationArgument(nested.RuntimeInvocation!));
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported AST argument type '{argAst?.GetType().FullName}'.");
            }
        }

        var invocation = new CommandInvocation(Task.FromResult(command), arguments);
        if (!isCompileTime)
        {
            return new LoweredCommand(invocation, null, commandAst.Name);
        }

        await _typeChecker.EnsureInvocationValidAsync(invocation, cancellationToken).ConfigureAwait(false);

        var values = invocation.Arguments
            .Cast<CommandValueArgument>()
            .Select(argument => argument.Value)
            .ToList();
        var outputTask = command.Execute(new CommandInput(values))
            ?? throw new InvalidOperationException($"Command '{command.GetType().FullName}' returned null output task.");
        var output = await outputTask.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Command '{command.GetType().FullName}' returned null output.");

        if (output.Status == CommandStatus.Error)
        {
            throw new InvalidOperationException($"Compile-time command '{commandAst.Name}' returned an error status.");
        }

        return new LoweredCommand(null, output, commandAst.Name);
    }

    private static bool IsCompileTime(ICommand command) =>
        command.GetType().GetCustomAttribute<CompileTimeAttribute>() is not null;

    private sealed record LoweredCommand(
        ICommandInvocation? RuntimeInvocation,
        ICommandOutput? CompileTimeOutput,
        string Name);
}
