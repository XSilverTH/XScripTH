namespace XScripTH.Contracts.Interfaces;

public interface ICommand
{
    public ICommandOutput Execute(ICommandIo input);
}
