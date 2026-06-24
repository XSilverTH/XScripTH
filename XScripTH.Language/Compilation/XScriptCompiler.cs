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
    private readonly IVariableStore _variableStore;

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
        _variableStore = commandRegistry is CommandRegistry registry
            ? registry.GetRequiredService<IVariableStore>()
            : new VariableStore();
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

        var context = new CompilationContext();
        var lowered = new List<(ICommandInvocation Invocation, XScriptCommandTerminator Terminator)>();
        foreach (var cmdAst in ast.Commands)
        {
            var loweredCommand = await LowerCommandAsync(cmdAst, context, cancellationToken).ConfigureAwait(false);
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
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        if (!_commandRegistry.TryCreate(commandAst.Name, out var command) || command == null)
        {
            throw new XScriptCommandResolutionException(commandAst.Name);
        }

        var phase = command as ICompileTimePhase;
        var commandTypes = command.GetType().GetCustomAttribute<CommandTypesAttribute>();
        var emitsRuntimeInvocation = command.GetType().GetCustomAttribute<NoRuntimeInvocationAttribute>() is null;
        var inputs = commandTypes?.Inputs;
        var arguments = new List<ICommandArgument>(commandAst.Arguments.Count);
        for (var index = 0; index < commandAst.Arguments.Count; index++)
        {
            var expectedInputType = inputs is not null && index < inputs.Length ? inputs[index] : null;
            arguments.Add(await LowerArgumentAsync(
                commandAst.Arguments[index],
                commandAst.Name,
                phase,
                expectedInputType,
                context,
                cancellationToken).ConfigureAwait(false));
        }

        var invocation = new CommandInvocation(Task.FromResult(command), arguments);
        if (phase is null)
        {
            return new LoweredCommand(invocation, null, commandAst.Name);
        }

        await _typeChecker.EnsureInvocationValidAsync(invocation, cancellationToken).ConfigureAwait(false);

        if (!emitsRuntimeInvocation)
        {
            foreach (var argument in invocation.Arguments)
            {
                if (argument is not CommandValueArgument)
                {
                    throw new InvalidOperationException($"Compile-time command '{commandAst.Name}' cannot use dynamic argument '{argument.GetType().FullName}'.");
                }
            }
        }

        var output = await phase.ExecuteCompileTimeAsync(arguments, context, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Compile-time command '{commandAst.Name}' returned null output.");
        if (output.Status == CommandStatus.Error)
        {
            throw new InvalidOperationException($"Compile-time command '{commandAst.Name}' returned an error status.");
        }

        return emitsRuntimeInvocation
            ? new LoweredCommand(invocation, null, commandAst.Name)
            : new LoweredCommand(null, output, commandAst.Name);
    }

    private async Task<ICommandArgument> LowerArgumentAsync(
        XScriptArgumentAst argumentAst,
        string parentCommandName,
        ICompileTimePhase? parentPhase,
        Type? expectedInputType,
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        switch (argumentAst)
        {
            case XScriptLiteralArgumentAst literalArg:
                return new CommandValueArgument(literalArg.Value);

            case XScriptVariableArgumentAst variableArg:
                if (context.Symbols.TryGetVariableType(variableArg.Name, out var variableType))
                {
                    return new CommandVariableArgument(variableArg.Name, variableType!, _variableStore);
                }

                if (expectedInputType?.IsAssignableFrom(typeof(CommandVariableArgument)) == true)
                {
                    return new CommandVariableArgument(variableArg.Name, typeof(object), _variableStore);
                }

                throw new XScriptVariableResolutionException(variableArg.Name);

            case XScriptCommandArgumentAst commandArg:
                var nested = await LowerCommandAsync(commandArg.Command, context, cancellationToken).ConfigureAwait(false);
                if (nested.CompileTimeOutput is not null)
                {
                    if (nested.CompileTimeOutput.Status != CommandStatus.Ok ||
                        nested.CompileTimeOutput.Values is not { Count: 1 })
                    {
                        throw new InvalidOperationException($"Compile-time command '{nested.Name}' used as an argument must complete successfully with exactly one output value.");
                    }

                    return new CommandValueArgument(nested.CompileTimeOutput.Values[0]);
                }

                return new CommandInvocationArgument(nested.RuntimeInvocation!);

            default:
                throw new InvalidOperationException($"Unsupported AST argument type '{argumentAst?.GetType().FullName}'.");
        }
    }


    private sealed record LoweredCommand(
        ICommandInvocation? RuntimeInvocation,
        ICommandOutput? CompileTimeOutput,
        string Name);
}
