namespace XScripTH.Contracts.Interfaces;

public interface ICommandInvocation
{
    ICommand Command { get; }

    IReadOnlyList<ICommandArgument> Arguments { get; }

    Type[]? StaticInputTypes => null;

    Type[]? StaticOutputTypes => null;
}