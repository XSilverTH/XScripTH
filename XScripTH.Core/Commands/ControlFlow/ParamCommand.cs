using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.ControlFlow;

[Command("param")]
[CommandTypes([typeof(CommandVariableArgument), typeof(string)], [])]
[NoRuntimeInvocation]
public sealed class ParamCommand : ICommand, ICompileTimePhase
{
    private static readonly IReadOnlyDictionary<string, Type> TypeAliases = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["object"] = typeof(object),
        ["string"] = typeof(string),
        ["char"] = typeof(char),
        ["bool"] = typeof(bool),
        ["byte"] = typeof(byte),
        ["sbyte"] = typeof(sbyte),
        ["short"] = typeof(short),
        ["ushort"] = typeof(ushort),
        ["int"] = typeof(int),
        ["uint"] = typeof(uint),
        ["long"] = typeof(long),
        ["ulong"] = typeof(ulong),
        ["float"] = typeof(float),
        ["double"] = typeof(double),
        ["decimal"] = typeof(decimal)
    };

    public Task<ICommandOutput> ExecuteCompileTimeAsync(
        IReadOnlyList<ICommandArgument> arguments,
        ICompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        if (arguments.Count != 2)
            throw new ArgumentException("param requires exactly two arguments.", nameof(arguments));

        if (arguments[0] is not CommandVariableArgument target)
            throw new ArgumentException("param requires a variable target as its first argument.", nameof(arguments));

        if (arguments[1] is not CommandValueArgument { Value: string typeName })
            throw new ArgumentException("param requires a string type name as its second argument.", nameof(arguments));

        if (!TypeAliases.TryGetValue(typeName, out var type))
            throw new ArgumentException($"Unknown parameter type '{typeName}'.", nameof(arguments));

        context.Symbols.DeclareVariable(target.Name, type);
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([new CommandFunctionParameter(target.Name, type)]));
    }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        throw new InvalidOperationException("param is a compile-time declaration and must not execute at runtime.");
    }
}
