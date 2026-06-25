using XScripTH.Contracts.Models;

namespace XScripTH.Contracts.Interfaces;

public interface IExecutionContext
{
    ICommandExecutor Executor { get; }
    bool TryGetVariable(string name, out object? value);
    void SetVariable(string name, object? value);
    bool TryGetFunction(string name, out CommandBlockArgument? block);
    void SetFunction(string name, CommandBlockArgument block);
    IExecutionContext CreateChildScope();
}
