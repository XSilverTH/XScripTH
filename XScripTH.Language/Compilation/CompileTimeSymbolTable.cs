using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Language.Compilation;

public sealed class CompileTimeSymbolTable(ICompileTimeSymbolTable? parent = null) : ICompileTimeSymbolTable
{
    private readonly Dictionary<string, Type> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandFunctionSignature> _functions = new(StringComparer.Ordinal);

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
            return;

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

        if (parent is not null)
            return parent.TryGetVariableType(name, out type);

        type = null;
        return false;
    }

    public void DeclareFunction(string name, CommandFunctionSignature signature)
    {
        var normalizedName = NormalizeFunctionName(name);
        ArgumentNullException.ThrowIfNull(signature);

        if (!_functions.TryGetValue(normalizedName, out var existingSignature))
        {
            _functions.Add(normalizedName, signature);
            return;
        }

        if (SignaturesEqual(existingSignature, signature))
            return;

        throw new InvalidOperationException(
            $"Function '@{normalizedName}' is already declared with inputs '{FormatParameters(existingSignature.Parameters)}' and outputs '{FormatTypes(existingSignature.OutputTypes)}' and cannot be redeclared with inputs '{FormatParameters(signature.Parameters)}' and outputs '{FormatTypes(signature.OutputTypes)}'.");
    }

    public bool TryGetFunctionSignature(string name, out CommandFunctionSignature? signature)
    {
        var normalizedName = NormalizeFunctionName(name);
        if (_functions.TryGetValue(normalizedName, out var storedSignature))
        {
            signature = storedSignature;
            return true;
        }

        if (parent is not null)
            return parent.TryGetFunctionSignature(name, out signature);

        signature = null;
        return false;
    }

    public ICompileTimeSymbolTable CreateChildScope()
    {
        return new CompileTimeSymbolTable(this);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));

        return name[0] == '$' ? name[1..] : name;
    }

    private static string NormalizeFunctionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name must be non-empty.", nameof(name));

        return name[0] == '@' ? name[1..] : name;
    }

    private static bool SignaturesEqual(CommandFunctionSignature left, CommandFunctionSignature right) =>
        left.Parameters.Select(parameter => (parameter.Name, parameter.Type))
            .SequenceEqual(right.Parameters.Select(parameter => (parameter.Name, parameter.Type))) &&
        left.OutputTypes.SequenceEqual(right.OutputTypes);

    private static string FormatParameters(IReadOnlyList<CommandFunctionParameter> parameters) =>
        string.Join(", ", parameters.Select(parameter => $"${parameter.Name}: {parameter.Type.FullName}"));

    private static string FormatTypes(Type[] types) =>
        string.Join(", ", types.Select(type => type.FullName));
}