using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandInvocationArgument : ICommandArgument
{
    public CommandInvocationArgument(ICommandInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Invocation = invocation;
    }

    public ICommandInvocation Invocation { get; }
}