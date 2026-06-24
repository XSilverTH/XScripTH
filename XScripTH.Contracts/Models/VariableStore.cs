using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class VariableStore : IVariableStore
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public void Set(string name, object? value)
    {
        var normalizedName = NormalizeName(name);
        _values[normalizedName] = value;
    }

    public bool TryGet(string name, out object? value)
    {
        var normalizedName = NormalizeName(name);
        return _values.TryGetValue(normalizedName, out value);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));
        }

        return name[0] == '$' ? name[1..] : name;
    }
}
