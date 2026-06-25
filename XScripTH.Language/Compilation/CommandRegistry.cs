using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language.Compilation;

public sealed class CommandRegistry : ICommandRegistry, ICommandRegistrar
{
    private readonly Dictionary<string, Func<ICommand>> _factories = new(StringComparer.Ordinal);
    private readonly IServiceProvider _serviceProvider;

    private CommandRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton(this);
        services.AddSingleton<ICommandRegistry>(this);
        services.AddSingleton<ICommandRegistrar>(this);
        _serviceProvider = services.BuildServiceProvider();
    }

    public static CommandRegistry FromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var registry = new CommandRegistry();
        foreach (var assembly in assemblies)
            registry.RegisterAssembly(assembly);

        return registry;
    }

    public static CommandRegistry FromFactories(params (string Name, Func<ICommand> Factory)[] factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        var registry = new CommandRegistry();
        foreach (var (name, factory) in factories)
            registry.Register(name, factory);

        return registry;
    }

    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(ICommand).IsAssignableFrom(type))
                continue;

            var commandAttr = type.GetCustomAttribute<CommandAttribute>();
            var name = commandAttr?.Name ?? type.Name;

            Register(name, () => (ICommand)ActivatorUtilities.CreateInstance(_serviceProvider, type));
        }
    }

    public void Register(string name, Func<ICommand> factory)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(name, factory))
            throw new InvalidOperationException($"A command with the name '{name}' has already been registered.");
    }

    public bool TryCreate(string name, out ICommand? command)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_factories.TryGetValue(name, out var factory))
        {
            command = factory();
            return true;
        }

        command = null;
        return false;
    }
}