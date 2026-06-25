using XScripTH.Language.Validation.Models;

namespace XScripTH.Language.Validation.Exceptions;

public sealed class CommandTypeCheckException : Exception
{
    public CommandTypeCheckException(Type commandType, string commandName, IReadOnlyList<CommandTypeCheckError> errors)
        : base(CreateMessage(commandName, errors))
    {
        CommandType = commandType;
        CommandName = commandName;
        Errors = errors;
    }

    public Type CommandType { get; }

    public string CommandName { get; }

    public IReadOnlyList<CommandTypeCheckError> Errors { get; }

    private static string CreateMessage(string commandName, IReadOnlyList<CommandTypeCheckError> errors)
    {
        var details = string.Join("; ", errors.Select(error => error.Message));
        return $"Type check failed for command '{commandName}': {details}";
    }
}