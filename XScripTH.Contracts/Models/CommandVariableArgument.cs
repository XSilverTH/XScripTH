using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandVariableArgument : ICommandArgument
{
    public CommandVariableArgument(string name, Type variableType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(variableType);

        Name = name[0] == '$' ? name[1..] : name;
        VariableType = variableType;
    }

    public string Name { get; }

    public Type VariableType { get; }

    public Task<ArgumentEvaluationResult> EvaluateAsync(
        ICommandExecutor executor,
        IExecutionContext executionContext,
        Type? expectedInputType,
        CancellationToken cancellationToken)
    {
        if (expectedInputType?.IsAssignableFrom(typeof(CommandVariableArgument)) == true)
            return Task.FromResult(new ArgumentEvaluationResult(this));

        return !executionContext.TryGetVariable(Name, out var value)
            ? throw new InvalidOperationException($"Variable '${Name}' has not been assigned.")
            : Task.FromResult(new ArgumentEvaluationResult(value));
    }
}