using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Reflection;

[Command("import")]
[CommandTypes([typeof(string)], [])]
[CompileTime]
public sealed class Import : ICommand
{
    private readonly ICommandRegistrar _registrar;

    public Import(ICommandRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        _registrar = registrar;
    }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        var rawPath = input.Values is { Count: 1 } ? input.Values[0] as string : null;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ArgumentException("Import path must be a non-empty string.", nameof(input));
        }

        var fullPath = Path.GetFullPath(rawPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Import DLL was not found.", fullPath);
        }

        var assembly = Assembly.LoadFrom(fullPath);
        _registrar.RegisterAssembly(assembly);
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}
