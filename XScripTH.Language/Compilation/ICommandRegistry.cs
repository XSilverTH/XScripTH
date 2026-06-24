using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language;

public interface ICommandRegistry
{
    bool TryCreate(string name, out ICommand? command);
}
