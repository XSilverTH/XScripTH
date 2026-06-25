using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Core.Commands.Variables;

[Command("var")]
[CommandTypes([typeof(CommandVariableArgument), typeof(object)], [])]
public sealed class Var : ICommand, ICompileTimePhase
{


    public async Task<ICommandOutput> ExecuteCompileTimeAsync(
        IReadOnlyList<ICommandArgument> arguments,
        ICompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        if (arguments.Count != 2)
        {
            throw new ArgumentException("var requires exactly two arguments.", nameof(arguments));
        }

        if (arguments[0] is not CommandVariableArgument target)
        {
            throw new ArgumentException("var requires a variable target as its first argument.", nameof(arguments));
        }

        var inferredType = await InferValueTypeAsync(arguments[1], cancellationToken).ConfigureAwait(false);
        context.Symbols.DeclareVariable(target.Name, inferredType);
        return CommandOutput.Ok();
    }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        if (input.Values is not { Count: 2 })
        {
            throw new ArgumentException("var requires exactly two input values.", nameof(input));
        }

        var target = (CommandVariableArgument)input.Values[0]!;
        var context = input.ExecutionContext ?? throw new InvalidOperationException("Execution context is required.");
        context.SetVariable(target.Name, input.Values[1]);
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }

    private static async Task<Type> InferValueTypeAsync(ICommandArgument argument, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (argument)
        {
            case CommandValueArgument valueArgument:
                return valueArgument.Value?.GetType() ?? typeof(object);

            case CommandVariableArgument variableArgument:
                return variableArgument.VariableType;

            case CommandInvocationArgument invocationArgument:
                var command = invocationArgument.Invocation.Command
                    ?? throw new InvalidOperationException("var requires its value expression to resolve to a command.");
                return RequireSingleOutputType(
                    invocationArgument.Invocation.StaticOutputTypes ?? command.GetType().GetCustomAttribute<CommandTypesAttribute>()?.Outputs);

            case CommandBlockArgument blockArgument:
                return RequireSingleOutputType(blockArgument.OutputTypes);

            case CommandFunctionReferenceArgument functionReferenceArgument:
                return RequireSingleOutputType(functionReferenceArgument.OutputTypes);

            default:
                throw new InvalidOperationException($"var does not support value argument type '{argument.GetType().FullName}'.");
        }
    }

    private static Type RequireSingleOutputType(Type[]? outputs)
    {
        if (outputs is not { Length: 1 })
        {
            throw new InvalidOperationException("var requires its value expression to have exactly one declared output type.");
        }

        return outputs[0];
    }
}
