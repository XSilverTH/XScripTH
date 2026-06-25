using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandFunctionReferenceArgument : ICommandArgument
{
    public CommandFunctionReferenceArgument(string name, Type[] outputTypes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(outputTypes);

        Name = name[0] == '@' ? name[1..] : name;
        OutputTypes = outputTypes;
    }

    public string Name { get; }

    public Type[] OutputTypes { get; }
}
