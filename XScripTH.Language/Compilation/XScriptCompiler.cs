using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Language.Validation;
using XScripTH.Language.Ast;

namespace XScripTH.Language;

public sealed class XScriptCompiler
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly ICommandTypeChecker _typeChecker;
    private readonly XScriptParser _parser;
    private readonly ICommandExecutor? _executor;

    public XScriptCompiler(
        ICommandRegistry commandRegistry,
        ICommandTypeChecker? typeChecker = null,
        XScriptParser? parser = null,
        ICommandExecutor? executor = null)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _commandRegistry = commandRegistry;
        _typeChecker = typeChecker ?? CommandTypeChecker.Default;
        _parser = parser ?? new XScriptParser();
        _executor = executor;
    }

    public async Task<IReadOnlyList<Task<ICommandInvocation>>> CompileAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var ast = _parser.Parse(source);
        return await CompileAsync(ast, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Task<ICommandInvocation>>> CompileAsync(
        XScriptProgramAst ast,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ast);

        var lowered = new List<(ICommandInvocation Invocation, XScriptCommandTerminator Terminator)>();
        foreach (var cmdAst in ast.Commands)
        {
            var invocation = LowerCommand(cmdAst);
            lowered.Add((invocation, cmdAst.Terminator));
        }

        // Validate type safety before execution so nested outputs match parent inputs.
        var invocationsToValidate = lowered.Select(l => Task.FromResult(l.Invocation)).ToList();
        await _typeChecker.EnsureValidAsync(invocationsToValidate, cancellationToken).ConfigureAwait(false);

        var result = new List<Task<ICommandInvocation>>();
        foreach (var (invocation, terminator) in lowered)
        {
            if (terminator == XScriptCommandTerminator.Await)
            {
                result.Add(Task.FromResult(invocation));
            }
            else // FireAndForget
            {
                var executor = _executor
                    ?? throw new InvalidOperationException("Fire-and-forget commands require an ICommandExecutor.");
                ICommand fireAndForgetCommand = new FireAndForgetCommand(invocation, executor);
                ICommandInvocation fireAndForgetInvocation = new CommandInvocation(
                    Task.FromResult(fireAndForgetCommand),
                    Array.Empty<ICommandArgument>()
                );
                result.Add(Task.FromResult(fireAndForgetInvocation));
            }
        }

        return result;
    }

    private ICommandInvocation LowerCommand(XScriptCommandAst commandAst)
    {
        if (!_commandRegistry.TryCreate(commandAst.Name, out var command) || command == null)
        {
            throw new XScriptCommandResolutionException(commandAst.Name);
        }

        var arguments = new List<ICommandArgument>();
        foreach (var argAst in commandAst.Arguments)
        {
            switch (argAst)
            {
                case XScriptLiteralArgumentAst literalArg:
                    arguments.Add(new CommandValueArgument(literalArg.Value));
                    break;
                case XScriptCommandArgumentAst commandArg:
                    var nestedInvocation = LowerCommand(commandArg.Command);
                    arguments.Add(new CommandInvocationArgument(nestedInvocation));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported AST argument type '{argAst?.GetType().FullName}'.");
            }
        }

        return new CommandInvocation(Task.FromResult(command), arguments);
    }
}
