namespace XScripTH.Language;

public sealed class XScriptVariableResolutionException : Exception
{
    public XScriptVariableResolutionException(string variableName)
        : base($"Variable '${variableName}' could not be resolved.")
    {
        VariableName = variableName;
    }

    public string VariableName { get; }
}
