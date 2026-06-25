namespace XScripTH.Language;

public sealed class XScriptFunctionResolutionException : Exception
{
    public XScriptFunctionResolutionException(string functionName)
        : base($"Function '@{functionName}' could not be resolved at compile time. Declare it with func before referencing it.")
    {
        FunctionName = functionName;
    }

    public string FunctionName { get; }
}
