using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandInvocationArgument : ICommandArgument
{
    public CommandInvocationArgument(ICommandInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Invocation = invocation;
    }

    public ICommandInvocation Invocation { get; }

    public async Task<ArgumentEvaluationResult> EvaluateAsync(
        ICommandExecutor executor,
        IExecutionContext executionContext,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        var nestedOutput = await executor.ExecuteInvocationAsync(Invocation, executionContext, cancellationToken)
            .ConfigureAwait(false);
        if (nestedOutput.Status == CommandStatus.Error)
            return new ArgumentEvaluationResult(null, nestedOutput);

        return nestedOutput.Values is not { Count: 1 }
            ? throw new InvalidOperationException("Nested command must produce exactly one output value.")
            : new ArgumentEvaluationResult(nestedOutput.Values[0]);
    }
}