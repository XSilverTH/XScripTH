using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandInput(IReadOnlyList<object?>? values = null, IExecutionContext? executionContext = null)
    : ICommandIo
{
    public IReadOnlyList<object?>? Values { get; init; } = values;
    public IExecutionContext? ExecutionContext { get; init; } = executionContext;

    public static CommandInput Empty { get; } = new(Array.Empty<object?>());

    public static CommandInput FromValues(params object?[] values) => new(values);

    public static CommandInput FromValues(IReadOnlyList<object?> values) => new(values);

    public static CommandInput FromValues(IExecutionContext executionContext, params object?[] values) =>
        new(values, executionContext);
}