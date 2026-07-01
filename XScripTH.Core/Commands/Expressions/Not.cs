using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Expressions;

[Command("not")]
[CommandTypes([typeof(bool)], [typeof(bool)])]
public sealed class Not : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        return Task.FromResult<ICommandOutput>(
            CommandOutput.Ok([!ExpressionCommandRuntime.Bool(input.Values, "not", 0)]));
    }
}