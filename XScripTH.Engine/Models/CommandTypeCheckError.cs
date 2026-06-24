namespace XScripTH.Engine.Models;

public sealed record CommandTypeCheckError(
    IReadOnlyList<int> Path,
    Type? ExpectedType,
    Type? ActualType,
    string Message);
