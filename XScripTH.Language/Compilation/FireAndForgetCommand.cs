using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

namespace XScripTH.Language.Compilation;

[CommandTypes([], [])]
internal sealed class FireAndForgetCommand : ICommand
{
    private readonly ICommandInvocation _capturedInvocation;

    public FireAndForgetCommand(ICommandInvocation capturedInvocation)
    {
        ArgumentNullException.ThrowIfNull(capturedInvocation);
        _capturedInvocation = capturedInvocation;
    }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var context = input.ExecutionContext ?? throw new InvalidOperationException("Execution context is required.");
        _ = context.Executor.ExecuteInvocationAsync(_capturedInvocation, context, CancellationToken.None)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _ = t.Exception;
            }, TaskScheduler.Default);

        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}