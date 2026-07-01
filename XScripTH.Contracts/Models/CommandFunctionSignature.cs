namespace XScripTH.Contracts.Models;

public sealed record CommandFunctionSignature
{
    public CommandFunctionSignature(IReadOnlyList<CommandFunctionParameter> parameters, Type[] outputTypes)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(outputTypes);

        Parameters = parameters;
        OutputTypes = outputTypes;
    }

    public IReadOnlyList<CommandFunctionParameter> Parameters { get; }

    public Type[] OutputTypes { get; }
}