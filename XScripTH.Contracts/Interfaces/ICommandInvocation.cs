namespace XScripTH.Contracts.Interfaces;

public interface ICommandInvocation
{
    Task<ICommand> CommandTask { get; }

    IReadOnlyList<ICommandArgument> Arguments { get; }
}
