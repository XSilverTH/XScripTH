namespace XScripTH.Contracts.Interfaces;

public interface ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input);
}
