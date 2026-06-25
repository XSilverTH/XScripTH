using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandBlockArgument : ICommandArgument
{
    public CommandBlockArgument(IReadOnlyList<Task<ICommandInvocation>> invocations, Type[]? outputTypes = null)
    {
        ArgumentNullException.ThrowIfNull(invocations);

        Invocations = invocations;
        OutputTypes = outputTypes ?? Array.Empty<Type>();
    }

    public IReadOnlyList<Task<ICommandInvocation>> Invocations { get; }

    public Type[] OutputTypes { get; }
}
