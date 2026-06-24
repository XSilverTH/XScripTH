using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandValueArgument(object? value) : ICommandArgument
{
    public object? Value { get; } = value;
}
