using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("or")]
[CommandTypes([typeof(bool), typeof(bool)], [typeof(bool)])]
public sealed class Or : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([
            ExpressionCommandRuntime.Bool(input.Values, "or", 0) || ExpressionCommandRuntime.Bool(input.Values, "or", 1)
        ]));
    }
}