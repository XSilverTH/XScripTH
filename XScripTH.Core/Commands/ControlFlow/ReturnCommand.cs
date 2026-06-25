using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.ControlFlow;

[Command("return")]
[CommandTypes([typeof(object)], [typeof(object)])]
public sealed class ReturnCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        return input.Values is not { Count: 1 }
            ? throw new ArgumentException("return requires exactly one input value.", nameof(input))
            : Task.FromResult<ICommandOutput>(CommandOutput.Ok([input.Values[0]]));
    }
}