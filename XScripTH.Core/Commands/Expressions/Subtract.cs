using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("subtract")]
[CommandTypes([typeof(object), typeof(object)], [typeof(object)])]
public sealed class Subtract : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (left, right, type) = ExpressionCommandRuntime.RequireNumericPair(input.Values, "subtract");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([ExpressionCommandRuntime.Subtract(left, right, type)]));
    }
}
