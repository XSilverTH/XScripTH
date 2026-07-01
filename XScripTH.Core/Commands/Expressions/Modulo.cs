using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("modulo")]
[CommandTypes([typeof(object), typeof(object)], [typeof(object)])]
public sealed class Modulo : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (left, right, type) = ExpressionCommandRuntime.RequireNumericPair(input.Values, "modulo");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([ExpressionCommandRuntime.Modulo(left, right, type)]));
    }
}