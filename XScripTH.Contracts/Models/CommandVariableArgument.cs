using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandVariableArgument : ICommandArgument
{
    public CommandVariableArgument(string name, Type variableType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(variableType);

        Name = name[0] == '$' ? name[1..] : name;
        VariableType = variableType;
    }

    public string Name { get; }

    public Type VariableType { get; }
}
