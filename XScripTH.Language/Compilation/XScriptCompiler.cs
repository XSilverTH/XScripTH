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
        if (commandRegistry is CommandRegistry registry)
        {
            if (executor is not null && !registry.TryGetService<ICommandExecutor>(out _))
            {
                registry.RegisterService<ICommandExecutor>(executor);
            }
        }
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

        var lowered = await LowerCommandListAsync(ast.Commands, context, cancellationToken).ConfigureAwait(false);

        // Validate type safety before execution so nested outputs match parent inputs.
        var invocationsToValidate = lowered.Select(l => l.Invocation).ToList();
        await _typeChecker.EnsureValidAsync(invocationsToValidate, cancellationToken).ConfigureAwait(false);

        var result = new List<Task<ICommandInvocation>>();
        foreach (var (invocation, terminator, _) in lowered)
        {
            result.Add(await ApplyTerminatorAsync(invocation, terminator).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<List<(ICommandInvocation Invocation, XScriptCommandTerminator Terminator, Type[] OutputTypes)>> LowerCommandListAsync(
        IReadOnlyList<XScriptCommandAst> commands,
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        var lowered = new List<(ICommandInvocation Invocation, XScriptCommandTerminator Terminator, Type[] OutputTypes)>();
        foreach (var cmdAst in commands)
        {
            var loweredCommand = await LowerCommandAsync(cmdAst, context, cancellationToken).ConfigureAwait(false);
            if (loweredCommand.RuntimeInvocation is not null)
            {
                lowered.Add((loweredCommand.RuntimeInvocation, cmdAst.Terminator, loweredCommand.OutputTypes));
            }
        }

        return lowered;
    }

    private Task<Task<ICommandInvocation>> ApplyTerminatorAsync(
        ICommandInvocation invocation,
        XScriptCommandTerminator terminator)
    {
        if (terminator == XScriptCommandTerminator.Await)
        {
            return Task.FromResult(Task.FromResult(invocation));
        }

        ICommand fireAndForgetCommand = new FireAndForgetCommand(invocation);
        ICommandInvocation fireAndForgetInvocation = new CommandInvocation(
            fireAndForgetCommand,
            Array.Empty<ICommandArgument>()
        );
        return Task.FromResult(Task.FromResult(fireAndForgetInvocation));
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

        var outputTypes = commandTypes?.Outputs ?? Array.Empty<Type>();
        var staticOutputTypes = commandAst.Name == "return"
            ? commandAst.Arguments.Count > 0 ? GetArgumentOutputTypes(arguments[0]) : Array.Empty<Type>()
            : null;
        if (staticOutputTypes is { Length: 1 })
        {
            staticOutputTypes = [staticOutputTypes[0]];
        }

        var invocation = new CommandInvocation(command, arguments, staticOutputTypes);
        outputTypes = GetInvocationOutputTypes(invocation, command);
        if (phase is null)
        {
            return new LoweredCommand(invocation, null, commandAst.Name, outputTypes);
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
            ? new LoweredCommand(invocation, null, commandAst.Name, outputTypes)
            : new LoweredCommand(null, output, commandAst.Name, outputTypes);
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
                    return new CommandVariableArgument(variableArg.Name, variableType!);
                }

                if (expectedInputType?.IsAssignableFrom(typeof(CommandVariableArgument)) == true)
                {
                    return new CommandVariableArgument(variableArg.Name, typeof(object));
                }

                throw new XScriptVariableResolutionException(variableArg.Name);

            case XScriptBlockArgumentAst blockArg:
                return await LowerBlockArgumentAsync(blockArg, context.CreateChildScope(), cancellationToken).ConfigureAwait(false);

            case XScriptFunctionReferenceArgumentAst functionArg:
                if (context.Symbols.TryGetFunctionOutputTypes(functionArg.Name, out var outputTypes))
                {
                    return new CommandFunctionReferenceArgument(functionArg.Name, outputTypes!);
                }

                throw new XScriptFunctionResolutionException(functionArg.Name);

            case XScriptCommandArgumentAst commandArg:
                if (expectedInputType is not null && IsBlockContainerExpected(expectedInputType))
                {
                    var childContext = context.CreateChildScope();
                    var nested = await LowerCommandAsync(commandArg.Command, childContext, cancellationToken).ConfigureAwait(false);
                    if (nested.RuntimeInvocation is null)
                    {
                        throw new InvalidOperationException($"Command '{nested.Name}' cannot be used as a deferred block because it has no runtime invocation.");
                    }

                    var invocationTaskTask = await ApplyTerminatorAsync(nested.RuntimeInvocation, commandArg.Command.Terminator).ConfigureAwait(false);
                    var invocation = await invocationTaskTask.ConfigureAwait(false);
                    return new CommandBlockArgument([invocation], nested.OutputTypes);
                }
                else
                {
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
                }
            default:
                throw new InvalidOperationException($"Unsupported AST argument type '{argumentAst?.GetType().FullName}'.");
        }
    }

    private async Task<CommandBlockArgument> LowerBlockArgumentAsync(
        XScriptBlockArgumentAst blockAst,
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        var lowered = await LowerCommandListAsync(blockAst.Commands, context, cancellationToken).ConfigureAwait(false);
        var invocations = new List<ICommandInvocation>(lowered.Count);
        foreach (var (invocation, terminator, _) in lowered)
        {
            var invocationTaskTask = await ApplyTerminatorAsync(invocation, terminator).ConfigureAwait(false);
            var commandInvocation = await invocationTaskTask.ConfigureAwait(false);
            invocations.Add(commandInvocation);
        }

        var outputTypes = lowered.Count == 0 ? Array.Empty<Type>() : lowered[^1].OutputTypes;
        return new CommandBlockArgument(invocations, outputTypes);
    }

    private Type[] GetInvocationOutputTypes(ICommandInvocation invocation, ICommand command) =>
        invocation.StaticOutputTypes ?? command.GetType().GetCustomAttribute<CommandTypesAttribute>()?.Outputs ?? Array.Empty<Type>();

    private static bool IsBlockContainerExpected(Type expectedInputType) =>
        expectedInputType != typeof(object) && expectedInputType.IsAssignableFrom(typeof(CommandBlockArgument));

    private Type[] GetArgumentOutputTypes(ICommandArgument argument)
    {
        return argument switch
        {
            CommandValueArgument valueArgument => [valueArgument.Value?.GetType() ?? typeof(object)],
            CommandVariableArgument variableArgument => [variableArgument.VariableType],
            CommandInvocationArgument invocationArgument => GetNestedInvocationOutputTypes(invocationArgument.Invocation),
            CommandBlockArgument blockArgument => blockArgument.OutputTypes,
            CommandFunctionReferenceArgument functionReferenceArgument => functionReferenceArgument.OutputTypes,
            _ => [argument.GetType()]
        };
    }

    private Type[] GetNestedInvocationOutputTypes(ICommandInvocation invocation)
    {
        var command = invocation.Command;
        return GetInvocationOutputTypes(invocation, command);
    }


    private sealed record LoweredCommand(
        ICommandInvocation? RuntimeInvocation,
        ICommandOutput? CompileTimeOutput,
        string Name,
        Type[] OutputTypes);
}
