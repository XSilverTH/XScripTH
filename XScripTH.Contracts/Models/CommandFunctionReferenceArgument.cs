using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandFunctionReferenceArgument : ICommandArgument
{
    public CommandFunctionReferenceArgument(string name, Type[] outputTypes, IFunctionStore functionStore)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(outputTypes);
        ArgumentNullException.ThrowIfNull(functionStore);

        Name = name[0] == '@' ? name[1..] : name;
        OutputTypes = outputTypes;
        FunctionStore = functionStore;
    }

    public string Name { get; }

    public Type[] OutputTypes { get; }

    public IFunctionStore FunctionStore { get; }
}
