using System.Reflection;
using XScripTH.Contracts.Expressions;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Language.Ast;
using XScripTH.Language.Parsing;
using XScripTH.Language.Validation;

namespace XScripTH.Language.Compilation;

public sealed class XScriptCompiler
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly ICommandTypeChecker _typeChecker;
    private readonly XScriptParser _parser;

    public XScriptCompiler(
        ICommandRegistry commandRegistry,
        ICommandTypeChecker? typeChecker = null,
        XScriptParser? parser = null)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _commandRegistry = commandRegistry;
        _typeChecker = typeChecker ?? CommandTypeChecker.Default;
        _parser = parser ?? new XScriptParser();
    }

    public Task<IReadOnlyList<ICommandInvocation>> CompileAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        return CompileAsync(source, new CompilationContext(), cancellationToken);
    }

    public async Task<IReadOnlyList<ICommandInvocation>> CompileAsync(
        string source,
        ICompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        var ast = Parse(source);
        return await CompileAsync(ast, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ICommandInvocation>> CompileAsync(
        XScriptProgramAst ast,
        ICompilationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ast);
        var bound = await BindProgramAsync(ast.Commands, context, cancellationToken).ConfigureAwait(false);

        await TypeCheckAsync(bound, cancellationToken).ConfigureAwait(false);

        return EmitProgram(bound);
    }

    private XScriptProgramAst Parse(string source)
    {
        return XScriptParser.Parse(source);
    }

    private async Task<BoundProgram> BindProgramAsync(
        IReadOnlyList<XScriptCommandAst> commands,
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        var boundCommands = new List<BoundCommand>();
        foreach (var cmdAst in commands)
            boundCommands.Add(await BindCommandAsync(cmdAst, context, cancellationToken).ConfigureAwait(false));

        return new BoundProgram(boundCommands);
    }

    private async Task<BoundCommand> BindCommandAsync(
        XScriptCommandAst commandAst,
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        if (commandAst.Name[0] == '@')
            return await BindFunctionCallAsync(commandAst, context, cancellationToken).ConfigureAwait(false);

        if (!_commandRegistry.TryCreate(commandAst.Name, out var command) || command == null)
            throw new XScriptCommandResolutionException(commandAst.Name);

        var phase = command as ICompileTimePhase;
        var commandTypes = command.GetType().GetCustomAttribute<CommandTypesAttribute>();
        var emitsRuntimeInvocation = command.GetType().GetCustomAttribute<NoRuntimeInvocationAttribute>() is null;
        var inputs = commandTypes?.Inputs;
        var arguments = new List<ICommandArgument>(commandAst.Arguments.Count);
        for (var index = 0; index < commandAst.Arguments.Count; index++)
            arguments.Add(await LowerArgumentAsync(
                commandAst.Arguments[index],
                inputs is not null && index < inputs.Length ? inputs[index] : null,
                context,
                cancellationToken,
                allowFunctionParameters: commandAst.Name == "func" && index == 1).ConfigureAwait(false));

        Type[]? staticOutputTypes = null;
        if (TryGetExpressionCommandOutputTypes(commandAst.Name, arguments, out var expressionOutputTypes))
            staticOutputTypes = expressionOutputTypes;
        else if (commandAst.Name == "return")
            staticOutputTypes = commandAst.Arguments.Count > 0 ? GetArgumentOutputTypes(arguments[0]) : [];
        if (staticOutputTypes is { Length: 1 })
            staticOutputTypes = [staticOutputTypes[0]];

        var invocation = new CommandInvocation(command, arguments, staticOutputTypes);
        var outputTypes = GetInvocationOutputTypes(invocation, command);
        if (phase is null)
            return new BoundCommand(invocation, null, commandAst.Terminator, commandAst.Name, outputTypes);

        await _typeChecker.EnsureInvocationValidAsync(invocation, cancellationToken).ConfigureAwait(false);

        if (!emitsRuntimeInvocation)
            foreach (var argument in invocation.Arguments)
                if (argument is not CommandValueArgument && argument is not CommandVariableArgument)
                    throw new InvalidOperationException(
                        $"Compile-time command '{commandAst.Name}' cannot use dynamic argument '{argument.GetType().FullName}'.");

        var output = await phase.ExecuteCompileTimeAsync(arguments, context, cancellationToken).ConfigureAwait(false) ??
                     throw new InvalidOperationException(
                         $"Compile-time command '{commandAst.Name}' returned null output.");

        if (output.Status == CommandStatus.Error)
            throw new InvalidOperationException($"Compile-time command '{commandAst.Name}' returned an error status.");

        return emitsRuntimeInvocation
            ? new BoundCommand(invocation, null, commandAst.Terminator, commandAst.Name, outputTypes)
            : new BoundCommand(null, output, commandAst.Terminator, commandAst.Name, outputTypes);
    }

    private async Task<BoundCommand> BindFunctionCallAsync(
        XScriptCommandAst commandAst,
        ICompilationContext context,
        CancellationToken cancellationToken)
    {
        var functionName = commandAst.Name[1..];
        if (!context.Symbols.TryGetFunctionSignature(commandAst.Name, out var signature) || signature is null)
            throw new XScriptFunctionResolutionException(functionName);

        var arguments = new List<ICommandArgument>(commandAst.Arguments.Count);
        for (var index = 0; index < commandAst.Arguments.Count; index++)
            arguments.Add(await LowerArgumentAsync(
                commandAst.Arguments[index],
                index < signature.Parameters.Count ? signature.Parameters[index].Type : null,
                context,
                cancellationToken,
                allowFunctionParameters: false).ConfigureAwait(false));

        var invocation = new CommandInvocation(
            new FunctionCallCommand(functionName, signature),
            arguments,
            staticOutputTypes: signature.OutputTypes,
            staticInputTypes: signature.Parameters.Select(parameter => parameter.Type).ToArray());

        return new BoundCommand(invocation, null, commandAst.Terminator, commandAst.Name, signature.OutputTypes);
    }

    private async Task<ICommandArgument> LowerArgumentAsync(
        XScriptArgumentAst argumentAst,
        Type? expectedInputType,
        ICompilationContext context,
        CancellationToken cancellationToken,
        bool allowFunctionParameters)
    {
        switch (argumentAst)
        {
            case XScriptLiteralArgumentAst literalArg:
                return new CommandValueArgument(literalArg.Value);

            case XScriptVariableArgumentAst variableArg:
                if (context.Symbols.TryGetVariableType(variableArg.Name, out var variableType))
                    return new CommandVariableArgument(variableArg.Name, variableType!);

                return expectedInputType?.IsAssignableFrom(typeof(CommandVariableArgument)) == true
                    ? new CommandVariableArgument(variableArg.Name, typeof(object))
                    : throw new XScriptVariableResolutionException(variableArg.Name);

            case XScriptBlockArgumentAst blockArg:
                return await LowerBlockArgumentAsync(blockArg, context.CreateChildScope(), cancellationToken,
                        allowFunctionParameters)
                    .ConfigureAwait(false);

            case XScriptFunctionReferenceArgumentAst functionArg:
                if (!context.Symbols.TryGetFunctionSignature(functionArg.Name, out var signature))
                    throw new XScriptFunctionResolutionException(functionArg.Name);

                if (signature!.Parameters.Count != 0)
                    throw new InvalidOperationException(
                        $"Function '@{functionArg.Name}' requires positional arguments and must be called directly as '@{functionArg.Name} ...;'.");

                return new CommandFunctionReferenceArgument(functionArg.Name, signature.OutputTypes);

            case XScriptCommandArgumentAst commandArg:
                if (expectedInputType is not null && IsBlockContainerExpected(expectedInputType))
                {
                    var childContext = context.CreateChildScope();
                    var nested = await BindCommandAsync(commandArg.Command, childContext, cancellationToken)
                        .ConfigureAwait(false);
                    if (nested.RuntimeInvocation is null)
                        throw new InvalidOperationException(
                            $"Command '{nested.Name}' cannot be used as a deferred block because it has no runtime invocation.");

                    var invocation = EmitCommand(nested.RuntimeInvocation, commandArg.Command.Terminator);
                    return new CommandBlockArgument([invocation], nested.OutputTypes);
                }
                else
                {
                    var nested = await BindCommandAsync(commandArg.Command, context, cancellationToken)
                        .ConfigureAwait(false);
                    if (nested.CompileTimeOutput is null)
                        return new CommandInvocationArgument(nested.RuntimeInvocation!);
                    if (nested.CompileTimeOutput.Status != CommandStatus.Ok ||
                        nested.CompileTimeOutput.Values is not { Count: 1 })
                        throw new InvalidOperationException(
                            $"Compile-time command '{nested.Name}' used as an argument must complete successfully with exactly one output value.");

                    return new CommandValueArgument(nested.CompileTimeOutput.Values[0]);
                }
            case XScriptUnaryExpressionAst unaryExpression:
                return await LowerExpressionAsync(unaryExpression, expectedInputType, context, cancellationToken,
                        allowFunctionParameters)
                    .ConfigureAwait(false);

            case XScriptBinaryExpressionAst binaryExpression:
                return await LowerExpressionAsync(binaryExpression, expectedInputType, context, cancellationToken,
                        allowFunctionParameters)
                    .ConfigureAwait(false);

            default:
                throw new InvalidOperationException(
                    $"Unsupported AST argument type '{argumentAst.GetType().FullName}'.");
        }
    }

    private Task<ICommandArgument> LowerExpressionAsync(
        XScriptUnaryExpressionAst expression,
        Type? expectedInputType,
        ICompilationContext context,
        CancellationToken cancellationToken,
        bool allowFunctionParameters)
    {
        var commandAst = new XScriptCommandAst(
            GetExpressionCommandName(expression.Operator),
            [expression.Operand],
            XScriptCommandTerminator.Await);
        return LowerArgumentAsync(
            new XScriptCommandArgumentAst(commandAst),
            expectedInputType,
            context,
            cancellationToken,
            allowFunctionParameters);
    }

    private Task<ICommandArgument> LowerExpressionAsync(
        XScriptBinaryExpressionAst expression,
        Type? expectedInputType,
        ICompilationContext context,
        CancellationToken cancellationToken,
        bool allowFunctionParameters)
    {
        var commandAst = new XScriptCommandAst(
            GetExpressionCommandName(expression.Operator),
            [expression.Left, expression.Right],
            XScriptCommandTerminator.Await);
        return LowerArgumentAsync(
            new XScriptCommandArgumentAst(commandAst),
            expectedInputType,
            context,
            cancellationToken,
            allowFunctionParameters);
    }

    private static string GetExpressionCommandName(XScriptExpressionOperator expressionOperator) =>
        expressionOperator switch
        {
            XScriptExpressionOperator.Add => "add",
            XScriptExpressionOperator.Subtract => "subtract",
            XScriptExpressionOperator.Multiply => "multiply",
            XScriptExpressionOperator.Divide => "divide",
            XScriptExpressionOperator.Modulo => "modulo",
            XScriptExpressionOperator.Negate => "negate",
            XScriptExpressionOperator.Equal => "equal",
            XScriptExpressionOperator.NotEqual => "not-equal",
            XScriptExpressionOperator.LessThan => "less-than",
            XScriptExpressionOperator.LessThanOrEqual => "less-than-or-equal",
            XScriptExpressionOperator.GreaterThan => "greater-than",
            XScriptExpressionOperator.GreaterThanOrEqual => "greater-than-or-equal",
            XScriptExpressionOperator.And => "and",
            XScriptExpressionOperator.Or => "or",
            XScriptExpressionOperator.Not => "not",
            _ => throw new InvalidOperationException($"Unsupported expression operator '{expressionOperator}'.")
        };

    private async Task<CommandBlockArgument> LowerBlockArgumentAsync(
        XScriptBlockArgumentAst blockAst,
        ICompilationContext context,
        CancellationToken cancellationToken,
        bool allowFunctionParameters)
    {
        var boundProgram = await BindProgramAsync(blockAst.Commands, context, cancellationToken).ConfigureAwait(false);
        var parameters = CollectFunctionParameters(boundProgram, allowFunctionParameters);
        var invocations = EmitProgram(boundProgram);

        var outputTypes = boundProgram.Commands.Count == 0 ? [] : boundProgram.Commands[^1].OutputTypes;
        return new CommandBlockArgument(invocations, outputTypes, parameters);
    }

    private static IReadOnlyList<CommandFunctionParameter> CollectFunctionParameters(
        BoundProgram boundProgram,
        bool allowFunctionParameters)
    {
        var parameters = new List<CommandFunctionParameter>();
        var executableSeen = false;

        foreach (var command in boundProgram.Commands)
        {
            if (command.Name == "param")
            {
                if (!allowFunctionParameters)
                    throw new InvalidOperationException("param declarations are only allowed in a func block.");

                if (executableSeen)
                    throw new InvalidOperationException(
                        "param declarations must appear before executable commands in a function block.");

                if (command.CompileTimeOutput?.Values is { Count: 1 } values &&
                    values[0] is CommandFunctionParameter parameter)
                {
                    parameters.Add(parameter);
                }

                continue;
            }

            executableSeen = true;
        }

        return parameters;
    }

    private async Task TypeCheckAsync(BoundProgram bound, CancellationToken cancellationToken)
    {
        await _typeChecker.EnsureValidAsync(bound.RuntimeInvocations, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<ICommandInvocation> EmitProgram(BoundProgram bound)
    {
        var result = new List<ICommandInvocation>();
        foreach (var cmd in bound.Commands)
            if (cmd.RuntimeInvocation is not null)
                result.Add(EmitCommand(cmd.RuntimeInvocation, cmd.Terminator));

        return result;
    }

    private ICommandInvocation EmitCommand(ICommandInvocation invocation, XScriptCommandTerminator terminator)
    {
        if (terminator == XScriptCommandTerminator.Await)
            return invocation;

        ICommand fireAndForgetCommand = new FireAndForgetCommand(invocation);
        return new CommandInvocation(
            fireAndForgetCommand,
            []
        );
    }

    private Type[] GetInvocationOutputTypes(ICommandInvocation invocation, ICommand command) =>
        invocation.StaticOutputTypes ?? command.GetType().GetCustomAttribute<CommandTypesAttribute>()?.Outputs ??
        [];

    private static bool IsBlockContainerExpected(Type expectedInputType) =>
        expectedInputType != typeof(object) && expectedInputType.IsAssignableFrom(typeof(CommandBlockArgument));

    private bool TryGetExpressionCommandOutputTypes(
        string commandName,
        IReadOnlyList<ICommandArgument> arguments,
        out Type[] outputTypes)
    {
        outputTypes = [];

        switch (commandName)
        {
            case "add":
            case "subtract":
            case "multiply":
            case "divide":
            case "modulo":
            {
                RequireExpressionOperandCount(commandName, arguments, 2);
                var left = RequireSingleExpressionOperandType(commandName, arguments, 0);
                var right = RequireSingleExpressionOperandType(commandName, arguments, 1);
                RequireNumericExpressionOperands(commandName, left, right);
                outputTypes = [XScriptExpressionTypeRules.PromoteNumeric(left, right)];
                return true;
            }

            case "negate":
            {
                RequireExpressionOperandCount(commandName, arguments, 1);
                var operand = RequireSingleExpressionOperandType(commandName, arguments, 0);
                RequireNumericExpressionOperands(commandName, operand);
                outputTypes = [XScriptExpressionTypeRules.PromoteUnaryNumeric(operand)];
                return true;
            }

            case "less-than":
            case "less-than-or-equal":
            case "greater-than":
            case "greater-than-or-equal":
            {
                RequireExpressionOperandCount(commandName, arguments, 2);
                var left = RequireSingleExpressionOperandType(commandName, arguments, 0);
                var right = RequireSingleExpressionOperandType(commandName, arguments, 1);
                RequireNumericExpressionOperands(commandName, left, right);
                outputTypes = [typeof(bool)];
                return true;
            }

            case "equal":
            case "not-equal":
                RequireExpressionOperandCount(commandName, arguments, 2);
                _ = RequireSingleExpressionOperandType(commandName, arguments, 0);
                _ = RequireSingleExpressionOperandType(commandName, arguments, 1);
                outputTypes = [typeof(bool)];
                return true;

            case "and":
            case "or":
                RequireExpressionOperandCount(commandName, arguments, 2);
                outputTypes = [typeof(bool)];
                return true;

            case "not":
                RequireExpressionOperandCount(commandName, arguments, 1);
                outputTypes = [typeof(bool)];
                return true;

            default:
                return false;
        }
    }

    private static void RequireExpressionOperandCount(
        string commandName,
        IReadOnlyList<ICommandArgument> arguments,
        int expectedCount)
    {
        if (arguments.Count != expectedCount)
            throw new InvalidOperationException(
                $"Expression command '{commandName}' requires {expectedCount} operand(s).");
    }

    private Type RequireSingleExpressionOperandType(
        string commandName,
        IReadOnlyList<ICommandArgument> arguments,
        int index)
    {
        var outputTypes = GetArgumentOutputTypes(arguments[index]);
        if (outputTypes.Length != 1)
            throw new InvalidOperationException(
                $"Expression command '{commandName}' requires operand {index} to produce exactly one value.");

        return outputTypes[0];
    }

    private static void RequireNumericExpressionOperands(string commandName, params Type[] operandTypes)
    {
        if (operandTypes.Any(type => !XScriptExpressionTypeRules.IsNumeric(type)))
            throw new InvalidOperationException(
                $"Expression command '{commandName}' requires numeric operands.");
    }

    private Type[] GetArgumentOutputTypes(ICommandArgument argument)
    {
        return argument switch
        {
            CommandValueArgument valueArgument => [valueArgument.Value?.GetType() ?? typeof(object)],
            CommandVariableArgument variableArgument => [variableArgument.VariableType],
            CommandInvocationArgument invocationArgument => GetNestedInvocationOutputTypes(
                invocationArgument.Invocation),
            CommandBlockArgument blockArgument => blockArgument.OutputTypes,
            CommandFunctionReferenceArgument functionReferenceArgument => functionReferenceArgument.OutputTypes,
            _ => [argument.GetType()]
        };
    }

    private Type[] GetNestedInvocationOutputTypes(ICommandInvocation invocation)
    {
        var command = invocation.Command;
        return GetInvocationOutputTypes(invocation, command);
    }

    private sealed record BoundProgram(IReadOnlyList<BoundCommand> Commands)
    {
        public IReadOnlyList<ICommandInvocation> RuntimeInvocations =>
            Commands.Select(c => c.RuntimeInvocation).Where(i => i != null).Cast<ICommandInvocation>().ToList();
    }

    private sealed record BoundCommand(
        ICommandInvocation? RuntimeInvocation,
        ICommandOutput? CompileTimeOutput,
        XScriptCommandTerminator Terminator,
        string Name,
        Type[] OutputTypes);
}