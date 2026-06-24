namespace XScripTH.Language.Validation.Models;

public sealed record CommandTypeCheckError(
    IReadOnlyList<int> Path,
    Type? ExpectedType,
    Type? ActualType,
    string Message);
