using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandVariableArgument : ICommandArgument
{
    public CommandVariableArgument(string name, Type variableType, IVariableStore variableStore)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(variableType);
        ArgumentNullException.ThrowIfNull(variableStore);

        Name = name[0] == '$' ? name[1..] : name;
        VariableType = variableType;
        VariableStore = variableStore;
    }

    public string Name { get; }

    public Type VariableType { get; }

    public IVariableStore VariableStore { get; }
}
