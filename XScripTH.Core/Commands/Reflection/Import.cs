using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Reflection;

[Command("import")]
[CommandTypes([typeof(string)], [])]
[NoRuntimeInvocation]
public sealed class Import : ICommand, ICompileTimePhase
{
    private readonly ICommandRegistrar _registrar;

    public Import(ICommandRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        _registrar = registrar;
    }


    public Task<ICommandOutput> ExecuteCompileTimeAsync(
        IReadOnlyList<ICommandArgument> arguments,
        ICompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        var rawPath = arguments is [CommandValueArgument { Value: string value }]
            ? value
            : null;
        return string.IsNullOrWhiteSpace(rawPath) ? throw new ArgumentException("Import path must be a non-empty string.", nameof(arguments)) : Execute(new CommandInput([rawPath]));
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
