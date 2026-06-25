using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Language;

public sealed class CommandRegistry : ICommandRegistry, ICommandRegistrar
{
    private readonly Dictionary<string, Func<ICommand>> _factories = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, object> _services = new();

    public CommandRegistry()
    {
        var variableStore = new VariableStore();
        RegisterService(typeof(VariableStore), variableStore);
        RegisterService(typeof(IVariableStore), variableStore);
        var functionStore = new FunctionStore();
        RegisterService(typeof(FunctionStore), functionStore);
        RegisterService(typeof(IFunctionStore), functionStore);
        RegisterService(typeof(CommandRegistry), this);
        RegisterService(typeof(ICommandRegistry), this);
        RegisterService(typeof(ICommandRegistrar), this);
    }

    public static CommandRegistry FromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var registry = new CommandRegistry();
        foreach (var assembly in assemblies)
        {
            registry.RegisterAssembly(assembly);
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

    public void RegisterService<TService>(TService service) where TService : notnull =>
        RegisterService(typeof(TService), service);

    public void RegisterService(Type serviceType, object service)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(service);

        if (!serviceType.IsInstanceOfType(service))
        {
            throw new ArgumentException($"Service must be an instance of '{serviceType.FullName}'.", nameof(service));
        }

        if (_services.ContainsKey(serviceType))
        {
            throw new InvalidOperationException($"A service of type '{serviceType.FullName}' has already been registered.");
        }

        _services[serviceType] = service;
    }

    public bool TryGetService<TService>(out TService? service) where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out var value) && value is TService typedValue)
        {
            service = typedValue;
            return true;
        }

        service = null;
        return false;
    }

    public TService GetRequiredService<TService>() where TService : class
    {
        if (TryGetService<TService>(out var service))
        {
            return service!;
        }

        throw new InvalidOperationException($"A service of type '{typeof(TService).FullName}' has not been registered.");
    }

    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(ICommand).IsAssignableFrom(type))
            {
                continue;
            }

            var commandAttr = type.GetCustomAttribute<CommandAttribute>();
            var name = commandAttr?.Name ?? type.Name;

            Register(name, () =>
            {
                var constructor = SelectResolvableConstructor(type)
                    ?? throw new InvalidOperationException($"Command type '{type.FullName}' has no resolvable constructor.");
                var resolvedServices = constructor
                    .GetParameters()
                    .Select(parameter => _services[parameter.ParameterType])
                    .ToArray();
                return (ICommand)constructor.Invoke(resolvedServices);
            });
        }
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

    private ConstructorInfo? SelectResolvableConstructor(Type type)
    {
        var constructors = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor => constructor.GetParameters().All(parameter => _services.ContainsKey(parameter.ParameterType)))
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .ToArray();

        if (constructors.Length == 0)
        {
            return null;
        }

        var selected = constructors[0];
        if (constructors.Length > 1 &&
            constructors[1].GetParameters().Length == selected.GetParameters().Length)
        {
            throw new InvalidOperationException($"Command type '{type.FullName}' has multiple resolvable constructors.");
        }

        return selected;
    }
}
