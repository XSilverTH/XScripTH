using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("not-equal")]
[CommandTypes([typeof(object), typeof(object)], [typeof(bool)])]
public sealed class NotEqual : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([!ExpressionCommandRuntime.Equal(input.Values, "not-equal")]));
    }
}
