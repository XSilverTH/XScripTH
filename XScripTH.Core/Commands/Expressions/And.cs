using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("and")]
[CommandTypes([typeof(bool), typeof(bool)], [typeof(bool)])]
public sealed class And : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([ExpressionCommandRuntime.Bool(input.Values, "and", 0) && ExpressionCommandRuntime.Bool(input.Values, "and", 1)]));
    }
}
