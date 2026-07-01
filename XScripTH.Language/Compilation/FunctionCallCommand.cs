using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Language.Compilation;

internal sealed class FunctionCallCommand : ICommand
{
    private readonly CommandFunctionSignature _signature;

    public FunctionCallCommand(string name, CommandFunctionSignature signature)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name must be non-empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(signature);

        Name = name[0] == '@' ? name[1..] : name;
        _signature = signature;
    }

    public string Name { get; }

    public async Task<ICommandOutput> Execute(ICommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var context = input.ExecutionContext ?? throw new InvalidOperationException("Execution context is required.");
        var values = input.Values;
        if (values?.Count != _signature.Parameters.Count)
            throw new ArgumentException(
                $"Function '@{Name}' expects {_signature.Parameters.Count} argument(s), but received {values?.Count ?? 0}.",
                nameof(input));

        if (!context.TryGetFunction(Name, out var function) || function is null)
            throw new InvalidOperationException($"Function '@{Name}' has not been assigned.");

        var child = context.CreateChildScope();
        for (var index = 0; index < _signature.Parameters.Count; index++)
            child.SetVariable(_signature.Parameters[index].Name, values[index]);

        return await context.Executor.ExecuteAsync(function.Block.Invocations, child, CancellationToken.None)
            .ConfigureAwait(false);
    }
}