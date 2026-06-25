namespace XScripTH.Language.Compilation;

public sealed class XScriptFunctionResolutionException(string functionName) : Exception(
    $"Function '@{functionName}' could not be resolved at compile time. Declare it with func before referencing it.")
{
    public string FunctionName { get; } = functionName;
}