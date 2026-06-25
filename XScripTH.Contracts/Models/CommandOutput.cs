using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public class CommandOutput(IReadOnlyList<object?>? values = null, CommandStatus status = CommandStatus.Ok)
    : ICommandOutput
{
    public IReadOnlyList<object?>? Values { get; init; } = values;
    public CommandStatus Status { get; init; } = status;
    public IExecutionContext? ExecutionContext { get; init; } = null;
    public static CommandOutput Ok(IReadOnlyList<object?> values) => new(values);
    public static CommandOutput Error(IReadOnlyList<object?> values) => new(values, CommandStatus.Error);
    public static CommandOutput Ok() => new();
    public static CommandOutput Error() => new(status: CommandStatus.Error);
}