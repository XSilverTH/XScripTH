using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("less-than")]
[CommandTypes([typeof(object), typeof(object)], [typeof(bool)])]
public sealed class LessThan : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (left, right, type) = ExpressionCommandRuntime.RequireNumericPair(input.Values, "less-than");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([
            ExpressionCommandRuntime.Compare(left, right, type) < 0
        ]));
    }
}