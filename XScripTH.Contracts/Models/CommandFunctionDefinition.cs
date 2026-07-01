namespace XScripTH.Contracts.Models;

public sealed class CommandFunctionDefinition
{
    public CommandFunctionDefinition(CommandBlockArgument block, CommandFunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(signature);

        Block = block;
        Signature = signature;
    }

    public CommandBlockArgument Block { get; }

    public CommandFunctionSignature Signature { get; }
}
