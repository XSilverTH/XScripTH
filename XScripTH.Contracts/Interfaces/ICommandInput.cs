namespace XScripTH.Contracts.Interfaces;

public interface ICommandInput
{
    IReadOnlyList<object?>? Values { get; init; }
    IExecutionContext? ExecutionContext { get; init; }
}