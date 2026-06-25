using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandValueArgument(object? value) : ICommandArgument
{
    public object? Value { get; } = value;

    public Task<ArgumentEvaluationResult> EvaluateAsync(
        ICommandExecutor executor,
        IExecutionContext executionContext,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ArgumentEvaluationResult(Value));
    }
}