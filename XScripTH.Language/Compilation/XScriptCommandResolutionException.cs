namespace XScripTH.Language.Compilation;

public sealed class XScriptCommandResolutionException : Exception
{
    public XScriptCommandResolutionException(string commandName)
        : base($"Command '{commandName}' could not be resolved.")
    {
        CommandName = commandName;
    }

    public XScriptCommandResolutionException(string commandName, string message)
        : base(message)
    {
        CommandName = commandName;
    }

    public XScriptCommandResolutionException(string commandName, string message, Exception innerException)
        : base(message, innerException)
    {
        CommandName = commandName;
    }

    public string CommandName { get; }
}