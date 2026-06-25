namespace XScripTH.Contracts.Interfaces;

public interface ICommandExecutor
{
    Task<ICommandOutput> ExecuteAsync(
        IEnumerable<Task<ICommandInvocation>> commands,
        CancellationToken cancellationToken = default);

    Task<ICommandOutput> ExecuteAsync(
        IEnumerable<Task<ICommandInvocation>> commands,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ICommandOutput>> ExecuteAllAsync(
        IEnumerable<Task<ICommandInvocation>> commands,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ICommandOutput>> ExecuteAllAsync(
        IEnumerable<Task<ICommandInvocation>> commands,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default);

    Task<ICommandOutput> ExecuteInvocationAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default);

    Task<ICommandOutput> ExecuteInvocationAsync(
        ICommandInvocation invocation,
        IExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
