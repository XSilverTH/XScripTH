using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("add")]
[CommandTypes([typeof(object), typeof(object)], [typeof(object)])]
public sealed class Add : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (left, right, type) = ExpressionCommandRuntime.RequireNumericPair(input.Values, "add");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([ExpressionCommandRuntime.Add(left, right, type)]));
    }
}