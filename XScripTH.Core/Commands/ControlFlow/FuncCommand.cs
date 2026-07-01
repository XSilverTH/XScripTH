using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.ControlFlow;

[Command("func")]
[CommandTypes([typeof(string), typeof(CommandBlockArgument)], [])]
public sealed class FuncCommand : ICommand, ICompileTimePhase
{
    public Task<ICommandOutput> ExecuteCompileTimeAsync(
        IReadOnlyList<ICommandArgument> arguments,
        ICompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        if (arguments.Count != 2)
            throw new ArgumentException("func requires exactly two arguments.", nameof(arguments));

        if (arguments[0] is not CommandValueArgument { Value: string name })
            throw new ArgumentException("func requires a string name as its first argument.", nameof(arguments));

        ValidateName(name);

        if (arguments[1] is not CommandBlockArgument block)
            throw new ArgumentException("func requires a command block as its second argument.", nameof(arguments));

        context.Symbols.DeclareFunction(name, new CommandFunctionSignature(block.Parameters, block.OutputTypes));
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        if (input.Values is not { Count: 2 })
            throw new ArgumentException("func requires exactly two input values.", nameof(input));

        if (input.Values[0] is not string name)
            throw new ArgumentException("func requires a string name as its first input value.", nameof(input));

        ValidateName(name);

        if (input.Values[1] is not CommandBlockArgument block)
            throw new ArgumentException("func requires a command block as its second input value.", nameof(input));

        var context = input.ExecutionContext ?? throw new InvalidOperationException("Execution context is required.");
        context.SetFunction(name,
            new CommandFunctionDefinition(block, new CommandFunctionSignature(block.Parameters, block.OutputTypes)));
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }


    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !(char.IsLetter(name[0]) || name[0] == '_'))
            throw new ArgumentException("Function name must match [A-Za-z_][A-Za-z0-9_-]*.", nameof(name));
        for (var index = 1; index < name.Length; index++)
        {
            var c = name[index];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                throw new ArgumentException("Function name must match [A-Za-z_][A-Za-z0-9_-]*.", nameof(name));
        }
    }
}