namespace XScripTH.Contracts.Interfaces;

public interface ICompileTimePhase
{
    Task<ICommandOutput> ExecuteCompileTimeAsync(
        IReadOnlyList<ICommandArgument> arguments,
        ICompilationContext context,
        CancellationToken cancellationToken = default);
}
