using System.Linq;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.ControlFlow;

[Command("while")]
[CommandTypes([typeof(CommandBlockArgument), typeof(CommandBlockArgument)], [])]
public sealed class WhileCommand : ICommand
{
    public async Task<ICommandOutput> Execute(ICommandIo input)
    {
        if (input.Values is not { Count: 2 })
        {
            throw new ArgumentException("while requires exactly two input values.", nameof(input));
        }

        if (input.Values[0] is not CommandBlockArgument condition)
        {
            throw new ArgumentException("while requires a condition command block as its first input value.", nameof(input));
        }

        if (input.Values[1] is not CommandBlockArgument body)
        {
            throw new ArgumentException("while requires a body command block as its second input value.", nameof(input));
        }
        var context = input.ExecutionContext ?? throw new InvalidOperationException("Execution context is required.");
        ICommandOutput? lastBodyOutput = null;
        while (true)
        {
            var conditionOutput = await context.Executor.ExecuteAsync(condition.Invocations, context.CreateChildScope()).ConfigureAwait(false);
            if (conditionOutput.Status == CommandStatus.Error)
            {
                return conditionOutput;
            }

            if (conditionOutput.Values is not { Count: 1 } || conditionOutput.Values[0] is not bool shouldContinue)
            {
                throw new InvalidOperationException("while condition block must produce exactly one boolean output value.");
            }

            if (!shouldContinue)
            {
                return lastBodyOutput ?? CommandOutput.Ok();
            }

            lastBodyOutput = await context.Executor.ExecuteAsync(body.Invocations, context.CreateChildScope()).ConfigureAwait(false);
            if (lastBodyOutput.Status == CommandStatus.Error)
            {
                return lastBodyOutput;
            }
        }
    }
}
