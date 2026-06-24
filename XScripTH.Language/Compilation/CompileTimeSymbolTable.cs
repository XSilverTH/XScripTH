using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language;

public sealed class CompileTimeSymbolTable : ICompileTimeSymbolTable
{
    private readonly Dictionary<string, Type> _variables = new(StringComparer.Ordinal);

    public void DeclareVariable(string name, Type type)
    {
        var normalizedName = NormalizeName(name);
        ArgumentNullException.ThrowIfNull(type);

        if (!_variables.TryGetValue(normalizedName, out var existingType))
        {
            _variables.Add(normalizedName, type);
            return;
        }

        if (existingType == type)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Variable '${normalizedName}' is already declared as '{existingType.FullName}' and cannot be redeclared as '{type.FullName}'.");
    }

    public bool TryGetVariableType(string name, out Type? type)
    {
        var normalizedName = NormalizeName(name);
        var found = _variables.TryGetValue(normalizedName, out var storedType);
        type = storedType;
        return found;
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
