using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language.Compilation;

public interface ICommandRegistry
{
    bool TryCreate(string name, out ICommand? command);
}