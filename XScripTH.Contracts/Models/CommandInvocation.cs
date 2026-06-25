using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandInvocation : ICommandInvocation
{
    public CommandInvocation(
        Task<ICommand> commandTask,
        IReadOnlyList<ICommandArgument>? arguments = null,
        Type[]? staticOutputTypes = null)
    {
        ArgumentNullException.ThrowIfNull(commandTask);
        CommandTask = commandTask;
        Arguments = arguments ?? Array.Empty<ICommandArgument>();
        StaticOutputTypes = staticOutputTypes;
    }

    public Task<ICommand> CommandTask { get; }

    public IReadOnlyList<ICommandArgument> Arguments { get; }

    public Type[]? StaticOutputTypes { get; }

    public static CommandInvocation FromCommand(ICommand command, params ICommandArgument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandInvocation(Task.FromResult(command), arguments);
    }
}
