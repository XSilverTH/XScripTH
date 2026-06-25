using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.ControlFlow;

[Command("if")]
[CommandTypes([typeof(bool), typeof(CommandBlockArgument)], [])]
public sealed class IfCommand : ICommand
{
    private readonly ICommandExecutor _executor;

    public IfCommand(ICommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    public async Task<ICommandOutput> Execute(ICommandIo input)
    {
        if (input.Values is not { Count: 2 })
        {
            throw new ArgumentException("if requires exactly two input values.", nameof(input));
        }

        if (input.Values[0] is not bool condition)
        {
            throw new ArgumentException("if requires a boolean condition as its first input value.", nameof(input));
        }

        if (input.Values[1] is not CommandBlockArgument body)
        {
            throw new ArgumentException("if requires a command block as its second input value.", nameof(input));
        }

        return condition
            ? await _executor.ExecuteAsync(body.Invocations).ConfigureAwait(false)
            : CommandOutput.Ok();
    }
}
