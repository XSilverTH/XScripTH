using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandFunctionReferenceArgument : ICommandArgument
{
    public CommandFunctionReferenceArgument(string name, Type[] outputTypes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(outputTypes);

        Name = name[0] == '@' ? name[1..] : name;
        OutputTypes = outputTypes;
    }

    public string Name { get; }

    public Type[] OutputTypes { get; }

    public async Task<ArgumentEvaluationResult> EvaluateAsync(
        ICommandExecutor executor,
        IExecutionContext executionContext,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        if (!executionContext.TryGetFunction(Name, out var block) || block is null)
            throw new InvalidOperationException($"Function '@{Name}' has not been assigned.");

        return await block.EvaluateAsync(executor, executionContext, expectedInputType, cancellationToken)
            .ConfigureAwait(false);
    }
}