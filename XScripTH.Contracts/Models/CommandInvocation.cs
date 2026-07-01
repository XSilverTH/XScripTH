using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandInvocation : ICommandInvocation
{
    public CommandInvocation(
        ICommand command,
        IReadOnlyList<ICommandArgument>? arguments = null,
        Type[]? staticOutputTypes = null,
        Type[]? staticInputTypes = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        Command = command;
        Arguments = arguments ?? Array.Empty<ICommandArgument>();
        StaticOutputTypes = staticOutputTypes;
        StaticInputTypes = staticInputTypes;
    }

    public ICommand Command { get; }

    public IReadOnlyList<ICommandArgument> Arguments { get; }

    public Type[]? StaticInputTypes { get; }

    public Type[]? StaticOutputTypes { get; }

    public static CommandInvocation FromCommand(ICommand command, params ICommandArgument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandInvocation(command, arguments);
    }
}