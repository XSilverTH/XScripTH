using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Console;

[Command("print")]
[CommandTypes([typeof(string)], [])]
public sealed class Print : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        System.Console.WriteLine(input.Values![0] as string);

        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}