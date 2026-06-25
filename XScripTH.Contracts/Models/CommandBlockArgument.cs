using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class CommandBlockArgument : ICommandArgument
{
    public CommandBlockArgument(IReadOnlyList<ICommandInvocation> invocations, Type[]? outputTypes = null)
    {
        ArgumentNullException.ThrowIfNull(invocations);

        Invocations = invocations;
        OutputTypes = outputTypes ?? Array.Empty<Type>();
    }

    public IReadOnlyList<ICommandInvocation> Invocations { get; }

    public Type[] OutputTypes { get; }
}
