using System;
using System.Threading;
using System.Threading.Tasks;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Language;

[CommandTypes([], [])]
internal sealed class FireAndForgetCommand : ICommand
{
    private readonly ICommandInvocation _capturedInvocation;
    private readonly ICommandExecutor _executor;

    public FireAndForgetCommand(ICommandInvocation capturedInvocation, ICommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(capturedInvocation);
        ArgumentNullException.ThrowIfNull(executor);
        _capturedInvocation = capturedInvocation;
        _executor = executor;
    }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        _ = _executor.ExecuteInvocationAsync(_capturedInvocation, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _ = t.Exception;
                }
            }, TaskScheduler.Default);

        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}
