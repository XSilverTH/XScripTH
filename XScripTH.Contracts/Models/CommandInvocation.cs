using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandInvocation : ICommandInvocation
{
    public CommandInvocation(Task<ICommand> commandTask, IReadOnlyList<ICommandArgument>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(commandTask);
        CommandTask = commandTask;
        Arguments = arguments ?? Array.Empty<ICommandArgument>();
    }

    public Task<ICommand> CommandTask { get; }

    public IReadOnlyList<ICommandArgument> Arguments { get; }

    public static CommandInvocation FromCommand(ICommand command, params ICommandArgument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandInvocation(Task.FromResult(command), arguments);
    }
}
