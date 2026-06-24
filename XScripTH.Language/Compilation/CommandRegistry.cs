using System;
using System.Collections.Generic;
using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language;

public sealed class CommandRegistry : ICommandRegistry
{
    private readonly Dictionary<string, Func<ICommand>> _factories = new(StringComparer.Ordinal);

    public static CommandRegistry FromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var registry = new CommandRegistry();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(ICommand).IsAssignableFrom(type))
                {
                    continue;
                }

                // Check for public parameterless constructor
                var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (ctor == null)
                {
                    continue;
                }

                var commandAttr = type.GetCustomAttribute<CommandAttribute>();
                var name = commandAttr?.Name ?? type.Name;

                registry.Register(name, () => (ICommand)Activator.CreateInstance(type)!);
            }
        }
        return registry;
    }

    public static CommandRegistry FromFactories(params (string Name, Func<ICommand> Factory)[] factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        var registry = new CommandRegistry();
        foreach (var (name, factory) in factories)
        {
            registry.Register(name, factory);
        }
        return registry;
    }

    public void Register(string name, Func<ICommand> factory)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(factory);

        if (_factories.ContainsKey(name))
        {
            throw new InvalidOperationException($"A command with the name '{name}' has already been registered.");
        }
        _factories[name] = factory;
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
