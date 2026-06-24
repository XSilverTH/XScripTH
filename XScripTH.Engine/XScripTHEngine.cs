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
            var argument = invocation.Arguments[index];
            var expectedInputType = inputs is not null && index < inputs.Length ? inputs[index] : null;
            switch (argument)
            {
                case CommandValueArgument valueArgument:
                    values.Add(valueArgument.Value);
                    break;

                case CommandVariableArgument variableArgument:
                    if (expectedInputType?.IsAssignableFrom(typeof(CommandVariableArgument)) == true)
                    {
                        values.Add(variableArgument);
                        break;
                    }

                    if (!variableArgument.VariableStore.TryGet(variableArgument.Name, out var value))
                    {
                        throw new InvalidOperationException($"Variable '${variableArgument.Name}' has not been assigned.");
                    }

                    values.Add(value);
                    break;

                case CommandInvocationArgument invocationArgument:
                    var nestedOutput = await ExecuteInvocationAsync(invocationArgument.Invocation, cancellationToken).ConfigureAwait(false);
                    if (nestedOutput.Status == CommandStatus.Error)
                    {
                        return nestedOutput;
                    }

                    values.Add(nestedOutput.Values![0]);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported command argument type '{argument.GetType().FullName}'.");
            }
        }

        var input = new CommandInput(values);
        var outputTask = command.Execute(input)
            ?? throw new InvalidOperationException($"Command '{command.GetType().FullName}' returned null output task.");
        var output = await outputTask.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Command '{command.GetType().FullName}' returned null output.");

        return output;
    }
}
