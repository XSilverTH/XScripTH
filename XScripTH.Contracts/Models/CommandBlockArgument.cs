using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandBlockArgument : ICommandArgument
{
    public CommandBlockArgument(IReadOnlyList<ICommandInvocation> invocations, Type[]? outputTypes = null)
    {
        ArgumentNullException.ThrowIfNull(invocations);

        Invocations = invocations;
        OutputTypes = outputTypes ?? Array.Empty<Type>();
    }

    public IReadOnlyList<ICommandInvocation> Invocations { get; }

    public Type[] OutputTypes { get; }

    public async Task<ArgumentEvaluationResult> EvaluateAsync(
        ICommandExecutor executor,
        IExecutionContext executionContext,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        if (expectedInputType is not null &&
            expectedInputType != typeof(object) &&
            expectedInputType.IsAssignableFrom(typeof(CommandBlockArgument)))
        {
            return new ArgumentEvaluationResult(this);
        }

        var output = await executor.ExecuteAsync(Invocations, executionContext.CreateChildScope(), cancellationToken)
            .ConfigureAwait(false);
        if (output.Status == CommandStatus.Error)
            return new ArgumentEvaluationResult(null, output);

        return output.Values is not { Count: 1 }
            ? throw new InvalidOperationException("Command block must produce exactly one output value.")
            : new ArgumentEvaluationResult(output.Values[0]);
    }
}