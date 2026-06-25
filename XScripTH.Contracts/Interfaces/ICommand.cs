namespace XScripTH.Contracts.Interfaces;

public interface ICommand
{
    Task<ICommandOutput> Execute(ICommandInput input);
}