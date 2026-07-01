using System.Reflection;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Language.Validation.Exceptions;
using XScripTH.Language.Validation.Models;

namespace XScripTH.Language.Validation;

public sealed class CommandTypeChecker : ICommandTypeChecker
{
    public static CommandTypeChecker Default { get; } = new();

    public async Task<CommandTypeCheckResult> ValidateAsync(
        IEnumerable<ICommandInvocation> invocations,
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
        var validation = await ValidateInvocationInternalAsync(invocation, Array.Empty<int>(), cancellationToken)
            .ConfigureAwait(false);
        return validation.Errors.Count == 0
            ? CommandTypeCheckResult.Valid
            : CommandTypeCheckResult.Invalid(validation.Errors);
    }

    public async Task EnsureValidAsync(
        IEnumerable<ICommandInvocation> invocations,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateProgramAsync(invocations, cancellationToken).ConfigureAwait(false);
        if (validation.Errors.Count == 0)
            return;

        var command = validation.FirstInvalidCommand
                      ?? throw new InvalidOperationException("Type check failed without an invalid command.");
        throw new CommandTypeCheckException(command.GetType(), GetCommandName(command.GetType()), validation.Errors);
    }

    public async Task EnsureInvocationValidAsync(
        ICommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var validation = await ValidateInvocationInternalAsync(invocation, Array.Empty<int>(), cancellationToken)
            .ConfigureAwait(false);
        if (validation.Errors.Count == 0)
            return;

        throw new CommandTypeCheckException(validation.Command.GetType(), GetCommandName(validation.Command.GetType()),
            validation.Errors);
    }

    private static async Task<ProgramValidation> ValidateProgramAsync(
        IEnumerable<ICommandInvocation> invocations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocations);

        var errors = new List<CommandTypeCheckError>();
        ICommand? firstInvalidCommand = null;

        foreach (var invocation in invocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var validation = await ValidateInvocationInternalAsync(invocation, Array.Empty<int>(), cancellationToken)
                .ConfigureAwait(false);
            if (validation.Errors.Count > 0 && firstInvalidCommand is null)
                firstInvalidCommand = validation.Command;

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

        var command = invocation.Command
                      ?? throw new InvalidOperationException("Command invocation returned null command.");
        var commandTypes = command.GetType().GetCustomAttribute<CommandTypesAttribute>();
        var inputs = invocation.StaticInputTypes ?? commandTypes?.Inputs;
        var outputs = GetInvocationOutputTypes(invocation, command);
        var errors = new List<CommandTypeCheckError>();

        if (inputs is null)
        {
            for (var index = 0; index < invocation.Arguments.Count; index++)
                await ValidateArgumentChildrenAsync(
                    errors,
                    invocation.Arguments[index],
                    AppendPath(path, index),
                    cancellationToken).ConfigureAwait(false);

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

                await ValidateArgumentChildrenAsync(
                    errors,
                    extraArgument,
                    currentPath,
                    cancellationToken).ConfigureAwait(false);

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
                        errors.Add(new CommandTypeCheckError(
                            currentPath,
                            expectedType,
                            valueArgument.Value?.GetType(),
                            $"Input {FormatPath(currentPath)} expected {FormatType(expectedType)}, but received {FormatType(valueArgument.Value?.GetType())}."));

                    break;

                case CommandVariableArgument variableArgument:
                    if (expectedType.IsAssignableFrom(typeof(CommandVariableArgument)))
                        break;

                    if (!MatchesExpectedOutputType(variableArgument.VariableType, expectedType))
                    {
                        errors.Add(new CommandTypeCheckError(
                            currentPath,
                            expectedType,
                            variableArgument.VariableType,
                            $"Input {FormatPath(currentPath)} expected {FormatType(expectedType)}, but received {FormatType(variableArgument.VariableType)}."));
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

                case CommandBlockArgument blockArgument:
                    await ValidateBlockChildrenAsync(errors, blockArgument, currentPath, cancellationToken)
                        .ConfigureAwait(false);
                    if (IsWhileConditionBlock(command, index))
                    {
                        AddNestedOutputCompatibilityErrors(errors, currentPath, typeof(bool),
                            blockArgument.OutputTypes);
                    }
                    else if (!IsBlockContainerExpected(expectedType))
                    {
                        AddNestedOutputCompatibilityErrors(errors, currentPath, expectedType,
                            blockArgument.OutputTypes);
                    }

                    break;

                case CommandFunctionReferenceArgument functionReferenceArgument:
                    if (IsWhileConditionBlock(command, index))
                    {
                        AddNestedOutputCompatibilityErrors(errors, currentPath, typeof(bool),
                            functionReferenceArgument.OutputTypes);
                    }
                    else if (!IsBlockContainerExpected(expectedType))
                    {
                        AddNestedOutputCompatibilityErrors(errors, currentPath, expectedType,
                            functionReferenceArgument.OutputTypes);
                    }

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
            return !targetType.IsValueType || Nullable.GetUnderlyingType(expectedType) is not null;

        return targetType.IsInstanceOfType(value);
    }

    private static bool MatchesExpectedOutputType(Type outputType, Type expectedType)
    {
        var expectedNonNullable = Nullable.GetUnderlyingType(expectedType) ?? expectedType;
        var outputNonNullable = Nullable.GetUnderlyingType(outputType) ?? outputType;
        return expectedNonNullable.IsAssignableFrom(outputNonNullable);
    }

    private static bool IsBlockContainerExpected(Type expectedType) =>
        expectedType != typeof(object) && expectedType.IsAssignableFrom(typeof(CommandBlockArgument));

    private static bool IsWhileConditionBlock(ICommand command, int argumentIndex) =>
        argumentIndex == 0 && GetCommandName(command.GetType()) == "while";

    private static Type? GetArgumentActualType(ICommandArgument argument) => argument switch
    {
        CommandValueArgument valueArgument => valueArgument.Value?.GetType(),
        CommandVariableArgument variableArgument => variableArgument.VariableType,
        CommandInvocationArgument => null,
        CommandBlockArgument => typeof(CommandBlockArgument),
        CommandFunctionReferenceArgument => typeof(CommandBlockArgument),
        _ => argument.GetType()
    };

    private static Type[]? GetInvocationOutputTypes(ICommandInvocation invocation, ICommand command) =>
        invocation.StaticOutputTypes ?? command.GetType().GetCustomAttribute<CommandTypesAttribute>()?.Outputs;

    private static async Task ValidateArgumentChildrenAsync(
        List<CommandTypeCheckError> errors,
        ICommandArgument argument,
        IReadOnlyList<int> path,
        CancellationToken cancellationToken)
    {
        switch (argument)
        {
            case CommandInvocationArgument nestedArgument:
            {
                var nestedValidation = await ValidateInvocationInternalAsync(
                    nestedArgument.Invocation,
                    path,
                    cancellationToken).ConfigureAwait(false);
                errors.AddRange(nestedValidation.Errors);
                break;
            }
            case CommandBlockArgument blockArgument:
                await ValidateBlockChildrenAsync(errors, blockArgument, path, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static async Task ValidateBlockChildrenAsync(
        List<CommandTypeCheckError> errors,
        CommandBlockArgument blockArgument,
        IReadOnlyList<int> path,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < blockArgument.Invocations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blockInvocation = blockArgument.Invocations[index];
            var blockValidation = await ValidateInvocationInternalAsync(
                blockInvocation,
                AppendPath(path, index),
                cancellationToken).ConfigureAwait(false);
            errors.AddRange(blockValidation.Errors);
        }
    }

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

    private sealed record InvocationValidation(
        IReadOnlyList<CommandTypeCheckError> Errors,
        ICommand Command,
        Type[]? Outputs);
}