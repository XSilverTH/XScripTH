using System.Linq;
using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Engine;

public sealed class XScripTHEngine : ICommandExecutor
{
    public async Task<ICommandOutput> ExecuteAsync(
        IEnumerable<ICommandInvocation> commands,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(commands, new XScriptExecutionContext(this), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ICommandOutput> ExecuteAsync(
        IEnumerable<ICommandInvocation> commands,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        var outputs = await ExecuteAllAsync(commands, executionContext, cancellationToken).ConfigureAwait(false);
        return outputs.Count > 0 ? outputs[^1] : CommandOutput.Ok();
    }

    public async Task<IReadOnlyList<ICommandOutput>> ExecuteAllAsync(
        IEnumerable<ICommandInvocation> commands,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAllAsync(commands, new XScriptExecutionContext(this), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ICommandOutput>> ExecuteAllAsync(
        IEnumerable<ICommandInvocation> commands,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(executionContext);

        var outputs = new List<ICommandOutput>();
        foreach (var invocation in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = await ExecuteInvocationAsync(invocation, executionContext, cancellationToken)
                .ConfigureAwait(false);

            outputs.Add(output);
            if (output.Status == CommandStatus.Error)
                break;
        }

        return outputs;
    }

    public async Task<ICommandOutput> ExecuteInvocationAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInvocationAsync(invocation, new XScriptExecutionContext(this), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ICommandOutput> ExecuteInvocationAsync(
        ICommandInvocation invocation,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(executionContext);

        var command = invocation.Command
                      ?? throw new InvalidOperationException("Command invocation returned null command.");
        var commandTypes = command.GetType().GetCustomAttribute<CommandTypesAttribute>();
        var inputs = commandTypes?.Inputs;
        var values = new List<object?>();

        for (var index = 0; index < invocation.Arguments.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedInputType = inputs is not null && index < inputs.Length ? inputs[index] : null;
            var resolved = await invocation.Arguments[index]
                .EvaluateAsync(this, executionContext, expectedInputType, cancellationToken)
                .ConfigureAwait(false);
            if (resolved.ErrorOutput is not null)
                return resolved.ErrorOutput;

            values.Add(resolved.Value);
        }

        var input = new CommandInput(values, executionContext);
        var outputTask = command.Execute(input) ??
                         throw new InvalidOperationException(
                             $"Command '{command.GetType().FullName}' returned null output task.");
        var output = await outputTask.WaitAsync(cancellationToken).ConfigureAwait(false) ??
                     throw new InvalidOperationException(
                         $"Command '{command.GetType().FullName}' returned null output.");

        return output;
    }

}