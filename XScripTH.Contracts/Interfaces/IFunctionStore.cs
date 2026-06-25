using XScripTH.Contracts.Models;

namespace XScripTH.Contracts.Interfaces;

public interface IFunctionStore
{
    void Set(string name, CommandBlockArgument block);

    bool TryGet(string name, out CommandBlockArgument? block);
}
