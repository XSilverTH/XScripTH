using XScripTH.Contracts.Enums;

namespace XScripTH.Contracts.Interfaces;

public interface ICommandOutput : ICommandIo
{
    public CommandStatus Status { get; init; }
}