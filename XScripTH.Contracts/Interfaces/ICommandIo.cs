namespace XScripTH.Contracts.Interfaces;

public interface ICommandIo
{
    public IReadOnlyList<object?>? Values { get; init; }
    public IExecutionContext? ExecutionContext { get; init; }
}
