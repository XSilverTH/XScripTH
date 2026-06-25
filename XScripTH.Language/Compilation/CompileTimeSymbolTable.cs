using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language;

public sealed class CompileTimeSymbolTable : ICompileTimeSymbolTable
{
    private readonly ICompileTimeSymbolTable? _parent;
    private readonly Dictionary<string, Type> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type[]> _functions = new(StringComparer.Ordinal);

    public CompileTimeSymbolTable(ICompileTimeSymbolTable? parent = null)
    {
        _parent = parent;
    }

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
        if (_variables.TryGetValue(normalizedName, out var storedType))
        {
            type = storedType;
            return true;
        }
        if (_parent is not null)
        {
            return _parent.TryGetVariableType(name, out type);
        }
        type = null;
        return false;
    }

    public void DeclareFunction(string name, Type[] outputTypes)
    {
        var normalizedName = NormalizeFunctionName(name);
        ArgumentNullException.ThrowIfNull(outputTypes);

        if (!_functions.TryGetValue(normalizedName, out var existingOutputTypes))
        {
            _functions.Add(normalizedName, outputTypes);
            return;
        }

        if (existingOutputTypes.SequenceEqual(outputTypes))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Function '@{normalizedName}' is already declared with outputs '{FormatTypes(existingOutputTypes)}' and cannot be redeclared with outputs '{FormatTypes(outputTypes)}'.");
    }

    public bool TryGetFunctionOutputTypes(string name, out Type[]? outputTypes)
    {
        var normalizedName = NormalizeFunctionName(name);
        if (_functions.TryGetValue(normalizedName, out var storedOutputTypes))
        {
            outputTypes = storedOutputTypes;
            return true;
        }
        if (_parent is not null)
        {
            return _parent.TryGetFunctionOutputTypes(name, out outputTypes);
        }
        outputTypes = null;
        return false;
    }

    public ICompileTimeSymbolTable CreateChildScope()
    {
        return new CompileTimeSymbolTable(this);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));
        }

        return name[0] == '$' ? name[1..] : name;
    }

    private static string NormalizeFunctionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name must be non-empty.", nameof(name));
        }

        return name[0] == '@' ? name[1..] : name;
    }

    private static string FormatTypes(Type[] types) =>
        string.Join(", ", types.Select(type => type.FullName));
}
