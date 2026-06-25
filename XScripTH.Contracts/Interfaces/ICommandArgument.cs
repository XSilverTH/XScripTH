using XScripTH.Contracts.Models;

namespace XScripTH.Contracts.Interfaces;

public interface ICommandArgument
{
    Task<ArgumentEvaluationResult> EvaluateAsync(
        ICommandExecutor executor,
        IExecutionContext executionContext,
        Type? expectedInputType,
        CancellationToken cancellationToken);
}