namespace XScripTH.Language.Compilation;

public sealed class XScriptVariableResolutionException(string variableName)
    : Exception($"Variable '${variableName}' could not be resolved.")
{
    public string VariableName { get; } = variableName;
}