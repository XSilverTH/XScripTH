using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Engine.Exceptions;
using XScripTH.Engine.Models;

namespace XScripTH.Engine;

public sealed class CommandTypeChecker : ICommandTypeChecker
{
    public static CommandTypeChecker Default { get; } = new();

    public async Task<CommandTypeCheckResult> ValidateAsync(
        IEnumerable<Task<ICommandInvocation>> invocations,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateProgramAsync(invocations, cancellationToken).ConfigureAwait(false);
        return validation.Errors.Count == 0
            ? CommandTypeCheckResult.Valid
            : CommandTypeCheckResult.Invalid(validation.Errors);
    }

    public async Task<CommandTypeCheckResult> ValidateInvocationAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var validation = await ValidateInvocationInternalAsync(invocation, Array.Empty<int>(), cancellationToken).ConfigureAwait(false);
        return validation.Errors.Count == 0
            ? CommandTypeCheckResult.Valid
            : CommandTypeCheckResult.Invalid(validation.Errors);
    }

    public async Task EnsureValidAsync(
        IEnumerable<Task<ICommandInvocation>> invocations,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateProgramAsync(invocations, cancellationToken).ConfigureAwait(false);
        if (validation.Errors.Count == 0)
        {
            return;
        }

        var command = validation.FirstInvalidCommand
            ?? throw new InvalidOperationException("Type check failed without an invalid command.");
        throw new CommandTypeCheckException(command.GetType(), GetCommandName(command.GetType()), validation.Errors);
    }

    public async Task EnsureInvocationValidAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var validation = await ValidateInvocationInternalAsync(invocation, Array.Empty<int>(), cancellationToken).ConfigureAwait(false);
        if (validation.Errors.Count == 0)
        {
            return;
        }

        throw new CommandTypeCheckException(validation.Command.GetType(), GetCommandName(validation.Command.GetType()), validation.Errors);
    }

    private static async Task<ProgramValidation> ValidateProgramAsync(
        IEnumerable<Task<ICommandInvocation>> invocations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocations);

        var errors = new List<CommandTypeCheckError>();
        ICommand? firstInvalidCommand = null;

        foreach (var invocationTask in invocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = await invocationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            var validation = await ValidateInvocationInternalAsync(invocation, Array.Empty<int>(), cancellationToken).ConfigureAwait(false);
            if (validation.Errors.Count > 0 && firstInvalidCommand is null)
            {
                firstInvalidCommand = validation.Command;
            }

            errors.AddRange(validation.Errors);
        }

        return new ProgramValidation(errors, firstInvalidCommand);
    }

    private static async Task<InvocationValidation> ValidateInvocationInternalAsync(
        ICommandInvocation invocation,
        IReadOnlyList<int> path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        var command = await invocation.CommandTask.WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Command invocation returned null command.");
        var commandTypes = command.GetType().GetCustomAttribute<CommandTypesAttribute>();
        var inputs = commandTypes?.Inputs;
        var outputs = commandTypes?.Outputs;
        var errors = new List<CommandTypeCheckError>();

        if (inputs is null)
        {
            for (var index = 0; index < invocation.Arguments.Count; index++)
            {
                if (invocation.Arguments[index] is CommandInvocationArgument nestedArgument)
                {
                    var nestedValidation = await ValidateInvocationInternalAsync(
                        nestedArgument.Invocation,
                        AppendPath(path, index),
                        cancellationToken).ConfigureAwait(false);
                    errors.AddRange(nestedValidation.Errors);
                }
            }

            return new InvocationValidation(errors, command, outputs);
        }

        var itemCount = Math.Max(inputs.Length, invocation.Arguments.Count);
        for (var index = 0; index < itemCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentPath = AppendPath(path, index);

            if (index >= inputs.Length)
            {
                var extraArgument = invocation.Arguments[index];
                errors.Add(new CommandTypeCheckError(
                    currentPath,
                    null,
                    GetArgumentActualType(extraArgument),
                    $"Input {FormatPath(currentPath)} was provided, but the command only accepts {inputs.Length} value(s)."));

                if (extraArgument is CommandInvocationArgument extraNestedArgument)
                {
                    var nestedValidation = await ValidateInvocationInternalAsync(
                        extraNestedArgument.Invocation,
                        currentPath,
                        cancellationToken).ConfigureAwait(false);
                    errors.AddRange(nestedValidation.Errors);
                }

                continue;
            }

            var expectedType = inputs[index];
            if (index >= invocation.Arguments.Count)
            {
                errors.Add(new CommandTypeCheckError(
                    currentPath,
                    expectedType,
                    null,
                    $"Input {FormatPath(currentPath)} is missing; expected {FormatType(expectedType)}."));
                continue;
            }

            var argument = invocation.Arguments[index];
            switch (argument)
            {
                case CommandValueArgument valueArgument:
                    if (!MatchesExpectedType(valueArgument.Value, expectedType))
                    {
                        errors.Add(new CommandTypeCheckError(
                            currentPath,
                            expectedType,
                            valueArgument.Value?.GetType(),
                            $"Input {FormatPath(currentPath)} expected {FormatType(expectedType)}, but received {FormatType(valueArgument.Value?.GetType())}."));
                    }
                    break;

                case CommandInvocationArgument invocationArgument:
                    var nestedValidation = await ValidateInvocationInternalAsync(
                        invocationArgument.Invocation,
                        currentPath,
                        cancellationToken).ConfigureAwait(false);
                    errors.AddRange(nestedValidation.Errors);
                    AddNestedOutputCompatibilityErrors(errors, currentPath, expectedType, nestedValidation.Outputs);
                    break;
            }
        }

        return new InvocationValidation(errors, command, outputs);
    }

    private static void AddNestedOutputCompatibilityErrors(
        List<CommandTypeCheckError> errors,
        IReadOnlyList<int> path,
        Type expectedType,
        Type[]? outputs)
    {
        if (outputs is null)
        {
            errors.Add(new CommandTypeCheckError(
                path,
                expectedType,
                null,
                $"Input {FormatPath(path)} is a command invocation whose output types are not declared."));
            return;
        }

        if (outputs.Length != 1)
        {
            errors.Add(new CommandTypeCheckError(
                path,
                expectedType,
                null,
                $"Input {FormatPath(path)} expected one command output assignable to {FormatType(expectedType)}, but the nested command declares {outputs.Length} output value(s)."));
            return;
        }

        var outputType = outputs[0];
        if (!MatchesExpectedOutputType(outputType, expectedType))
        {
            errors.Add(new CommandTypeCheckError(
                path,
                expectedType,
                outputType,
                $"Input {FormatPath(path)} expected {FormatType(expectedType)}, but the nested command declares {FormatType(outputType)}."));
        }
    }

    private static bool MatchesExpectedType(object? value, Type expectedType)
    {
        var targetType = Nullable.GetUnderlyingType(expectedType) ?? expectedType;
        if (value is null)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(expectedType) is not null;
        }

        return targetType.IsInstanceOfType(value);
    }

    private static bool MatchesExpectedOutputType(Type outputType, Type expectedType)
    {
        var expectedNonNullable = Nullable.GetUnderlyingType(expectedType) ?? expectedType;
        var outputNonNullable = Nullable.GetUnderlyingType(outputType) ?? outputType;
        return expectedNonNullable.IsAssignableFrom(outputNonNullable);
    }

    private static Type? GetArgumentActualType(ICommandArgument argument) => argument switch
    {
        CommandValueArgument valueArgument => valueArgument.Value?.GetType(),
        CommandInvocationArgument => null,
        _ => argument.GetType()
    };

    private static IReadOnlyList<int> AppendPath(IReadOnlyList<int> path, int index)
    {
        var result = new int[path.Count + 1];
        for (var i = 0; i < path.Count; i++)
        {
            result[i] = path[i];
        }

        result[^1] = index;
        return result;
    }

    private static string FormatPath(IReadOnlyList<int> path) => string.Join('.', path.Select(index => $"[{index}]"));

    private static string GetCommandName(Type commandType) =>
        commandType.GetCustomAttribute<CommandAttribute>()?.Name ?? commandType.Name;

    private static string FormatType(Type? type) => type?.FullName ?? "null";

    private sealed record ProgramValidation(IReadOnlyList<CommandTypeCheckError> Errors, ICommand? FirstInvalidCommand);

    private sealed record InvocationValidation(IReadOnlyList<CommandTypeCheckError> Errors, ICommand Command, Type[]? Outputs);
}
