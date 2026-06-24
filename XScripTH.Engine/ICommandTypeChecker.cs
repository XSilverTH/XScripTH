using XScripTH.Contracts.Interfaces;
using XScripTH.Engine.Models;

namespace XScripTH.Engine;

public interface ICommandTypeChecker
{
    Task<CommandTypeCheckResult> ValidateAsync(IEnumerable<Task<ICommandInvocation>> invocations, CancellationToken cancellationToken = default);

    Task<CommandTypeCheckResult> ValidateInvocationAsync(ICommandInvocation invocation, CancellationToken cancellationToken = default);

    Task EnsureValidAsync(IEnumerable<Task<ICommandInvocation>> invocations, CancellationToken cancellationToken = default);

    Task EnsureInvocationValidAsync(ICommandInvocation invocation, CancellationToken cancellationToken = default);
}
