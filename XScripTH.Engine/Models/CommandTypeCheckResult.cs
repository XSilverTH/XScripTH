namespace XScripTH.Engine.Models;

public sealed class CommandTypeCheckResult
{
    private static readonly CommandTypeCheckResult ValidResult = new(Array.Empty<CommandTypeCheckError>());

    public CommandTypeCheckResult(IReadOnlyList<CommandTypeCheckError> errors) => Errors = errors;

    public IReadOnlyList<CommandTypeCheckError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static CommandTypeCheckResult Valid => ValidResult;

    public static CommandTypeCheckResult Invalid(IReadOnlyList<CommandTypeCheckError> errors) => new(errors);
}
