using XScripTH.Contracts.Enums;

namespace XScripTH.Contracts.Interfaces;

public interface ICommandOutput
{
    IReadOnlyList<object?>? Values { get; init; }
    CommandStatus Status { get; init; }
}