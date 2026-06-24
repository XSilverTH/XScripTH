using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Engine;

public sealed class XScripTHEngine
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
        var values = new List<object?>();

        foreach (var argument in invocation.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (argument)
            {
                case CommandValueArgument valueArgument:
                    values.Add(valueArgument.Value);
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
