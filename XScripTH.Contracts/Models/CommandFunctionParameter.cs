namespace XScripTH.Contracts.Models;

public sealed record CommandFunctionParameter
{
    public CommandFunctionParameter(string name, Type type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter name must be non-empty.", nameof(name));

        var normalizedName = name[0] == '$' ? name[1..] : name;
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("Parameter name must be non-empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(type);

        Name = normalizedName;
        Type = type;
    }

    public string Name { get; }

    public Type Type { get; }
}
