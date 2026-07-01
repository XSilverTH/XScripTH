using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("multiply")]
[CommandTypes([typeof(object), typeof(object)], [typeof(object)])]
public sealed class Multiply : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (left, right, type) = ExpressionCommandRuntime.RequireNumericPair(input.Values, "multiply");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([
            ExpressionCommandRuntime.Multiply(left, right, type)
        ]));
    }
}