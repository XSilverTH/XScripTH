using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Console;

[Command("print")]
[CommandTypes([typeof(string)])]
public class Print:ICommand
{
    public ICommandOutput Execute(ICommandIo input)
    {
        System.Console.WriteLine(input.Values![0] as string);

        return CommandOutput.Ok();
    }
}
