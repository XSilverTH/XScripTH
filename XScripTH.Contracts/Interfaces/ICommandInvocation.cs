namespace XScripTH.Contracts.Interfaces;

public interface ICommandInvocation
{
    ICommand Command { get; }

    IReadOnlyList<ICommandArgument> Arguments { get; }

    Type[]? StaticOutputTypes => null;
}