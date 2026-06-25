using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Engine;

public sealed class XScripTHEngine : ICommandExecutor
{
    public async Task<ICommandOutput> ExecuteAsync(
        IEnumerable<Task<ICommandInvocation>> commands,
        CancellationToken cancellationToken = default)
    {
        var outputs = await ExecuteAllAsync(commands, cancellationToken).ConfigureAwait(false);
        return outputs.Count > 0 ? outputs[^1] : CommandOutput.Ok();
    }

    public async Task<IReadOnlyList<ICommandOutput>> ExecuteAllAsync(
        IEnumerable<Task<ICommandInvocation>> commands,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var outputs = new List<ICommandOutput>();
        foreach (var invocationTask in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = await invocationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            var output = await ExecuteInvocationAsync(invocation, cancellationToken).ConfigureAwait(false);

            outputs.Add(output);
            if (output.Status == CommandStatus.Error)
            {
                break;
            }
        }

        return outputs;
    }

    public async Task<ICommandOutput> ExecuteInvocationAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var command = await invocation.CommandTask.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Command invocation returned null command.");
        var commandTypes = command.GetType().GetCustomAttribute<CommandTypesAttribute>();
        var inputs = commandTypes?.Inputs;
        var values = new List<object?>();

        for (var index = 0; index < invocation.Arguments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedInputType = inputs is not null && index < inputs.Length ? inputs[index] : null;
            var resolved = await ResolveArgumentAsync(invocation.Arguments[index], expectedInputType, cancellationToken).ConfigureAwait(false);
            if (resolved.ErrorOutput is not null)
            {
                return resolved.ErrorOutput;
            }

            values.Add(resolved.Value);
        }

        var input = new CommandInput(values);
        var outputTask = command.Execute(input)
            ?? throw new InvalidOperationException($"Command '{command.GetType().FullName}' returned null output task.");
        var output = await outputTask.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Command '{command.GetType().FullName}' returned null output.");

        return output;
    }

    private async Task<ArgumentResolution> ResolveArgumentAsync(
        ICommandArgument argument,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        switch (argument)
        {
            case CommandValueArgument valueArgument:
                return new ArgumentResolution(valueArgument.Value, null);

            case CommandVariableArgument variableArgument:
                if (expectedInputType?.IsAssignableFrom(typeof(CommandVariableArgument)) == true)
                {
                    return new ArgumentResolution(variableArgument, null);
                }

                if (!variableArgument.VariableStore.TryGet(variableArgument.Name, out var value))
                {
                    throw new InvalidOperationException($"Variable '${variableArgument.Name}' has not been assigned.");
                }

                return new ArgumentResolution(value, null);

            case CommandInvocationArgument invocationArgument:
                var nestedOutput = await ExecuteInvocationAsync(invocationArgument.Invocation, cancellationToken).ConfigureAwait(false);
                if (nestedOutput.Status == CommandStatus.Error)
                {
                    return new ArgumentResolution(null, nestedOutput);
                }

                return new ArgumentResolution(RequireSingleOutputValue(nestedOutput, "Nested command"), null);

            case CommandBlockArgument blockArgument:
                return await ResolveBlockArgumentAsync(blockArgument, expectedInputType, cancellationToken).ConfigureAwait(false);

            case CommandFunctionReferenceArgument functionReferenceArgument:
                if (!functionReferenceArgument.FunctionStore.TryGet(functionReferenceArgument.Name, out var block) || block is null)
                {
                    throw new InvalidOperationException($"Function '@{functionReferenceArgument.Name}' has not been assigned.");
                }

                return await ResolveBlockArgumentAsync(block, expectedInputType, cancellationToken).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Unsupported command argument type '{argument.GetType().FullName}'.");
        }
    }

    private async Task<ArgumentResolution> ResolveBlockArgumentAsync(
        CommandBlockArgument block,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        if (expectedInputType is not null && IsBlockContainerExpected(expectedInputType))
        {
            return new ArgumentResolution(block, null);
        }

        var output = await ExecuteAsync(block.Invocations, cancellationToken).ConfigureAwait(false);
        if (output.Status == CommandStatus.Error)
        {
            return new ArgumentResolution(null, output);
        }

        return new ArgumentResolution(RequireSingleOutputValue(output, "Command block"), null);
    }

    private static object? RequireSingleOutputValue(ICommandOutput output, string source)
    {
        if (output.Values is not { Count: 1 })
        {
            throw new InvalidOperationException($"{source} must produce exactly one output value.");
        }

        return output.Values[0];
    }

    private static bool IsBlockContainerExpected(Type expectedInputType) =>
        expectedInputType != typeof(object) && expectedInputType.IsAssignableFrom(typeof(CommandBlockArgument));

    private sealed record ArgumentResolution(object? Value, ICommandOutput? ErrorOutput);
}
