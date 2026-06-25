using System.Reflection;

namespace XScripTH.Contracts.Interfaces;

public interface ICommandRegistrar
{
    void Register(string name, Func<ICommand> factory);
    void RegisterAssembly(Assembly assembly);
}