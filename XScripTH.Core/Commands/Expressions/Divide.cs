using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("divide")]
[CommandTypes([typeof(object), typeof(object)], [typeof(object)])]
public sealed class Divide : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (left, right, type) = ExpressionCommandRuntime.RequireNumericPair(input.Values, "divide");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([ExpressionCommandRuntime.Divide(left, right, type)]));
    }
}