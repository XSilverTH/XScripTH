using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("negate")]
[CommandTypes([typeof(object)], [typeof(object)])]
public sealed class Negate : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var (operand, type) = ExpressionCommandRuntime.RequireNumericOperand(input.Values, "negate");
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([ExpressionCommandRuntime.Negate(operand, type)]));
    }
}