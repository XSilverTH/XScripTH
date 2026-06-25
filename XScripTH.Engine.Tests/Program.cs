using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Engine;

var tests = new (string Name, Func<Task> Run)[]
{
    ("resolves nested command input lazily at runtime", ResolvesNestedCommandInputLazilyAtRuntime),
    ("executes top-level commands independently", ExecutesTopLevelCommandsIndependently),
    ("supports recursive nested command inputs", SupportsRecursiveNestedCommandInputs),
    ("bubbles nested error output without executing parent", BubblesNestedErrorOutputWithoutExecutingParent),
    ("runtime executes valid direct command inputs", RuntimeExecutesValidDirectCommandInputs),
    ("runtime resolves variable command arguments", RuntimeResolvesVariableCommandArguments),
    ("runtime rejects missing variable command arguments", RuntimeRejectsMissingVariableCommandArguments),
    ("passing block to string executes lazily", PassingBlockToStringExecutesLazily),
    ("passing block as block does not execute first", PassingBlockAsBlockDoesNotExecuteFirst),
    ("function reference to primitive executes stored block", FunctionReferenceToPrimitiveExecutesStoredBlock),
    ("missing function reference throws name", MissingFunctionReferenceThrowsName)
};

foreach (var test in tests)
{
    await test.Run();
    System.Console.WriteLine($"PASS {test.Name}");
}

static async Task ResolvesNestedCommandInputLazilyAtRuntime()
{
    var literal = new LiteralStringCommand("hello");
    var parent = new StringLengthCommand();
    var invocation = Invoke(parent, Nested(Invoke(literal)));

    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(5, SingleValue<int>(result));
    AssertEqual(1, literal.ExecuteCount);
    AssertEqual(1, parent.ExecuteCount);
}

static async Task ExecutesTopLevelCommandsIndependently()
{
    var literal = new LiteralStringCommand("hello");
    var noInput = new NoInputCommand();

    var outputs = await new XScripTHEngine().ExecuteAllAsync(Program(
        Invoke(literal),
        Invoke(noInput)));

    AssertEqual(2, outputs.Count);
    AssertEqual(CommandStatus.Ok, outputs[0].Status);
    AssertEqual(CommandStatus.Ok, outputs[1].Status);
    AssertEqual(1, literal.ExecuteCount);
    AssertEqual(1, noInput.ExecuteCount);
}

static async Task SupportsRecursiveNestedCommandInputs()
{
    var invocation = Invoke(
        new StringLengthCommand(),
        Nested(Invoke(
            new SurroundCommand(),
            Nested(Invoke(new LiteralStringCommand("x"))))));

    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(3, SingleValue<int>(result));
}

static async Task BubblesNestedErrorOutputWithoutExecutingParent()
{
    var nested = new ErrorStringCommand();
    var parent = new StringLengthCommand();
    var invocation = Invoke(parent, Nested(Invoke(nested)));

    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Error, result.Status);
    AssertEqual("bad", SingleValue<string>(result));
    AssertEqual(1, nested.ExecuteCount);
    AssertEqual(0, parent.ExecuteCount);
}

static async Task RuntimeExecutesValidDirectCommandInputs()
{
    var invocation = Invoke(new StringLengthCommand(), Value("hello"));
    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(5, SingleValue<int>(result));
}

static async Task RuntimeResolvesVariableCommandArguments()
{
    var engine = new XScripTHEngine();
    var context = new XScriptExecutionContext(engine);
    context.SetVariable("message", "hello");
    var invocation = Invoke(new StringLengthCommand(), new CommandVariableArgument("message", typeof(string)));

    var result = await engine.ExecuteAsync(Program(invocation), context);

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(5, SingleValue<int>(result));
}

static async Task RuntimeRejectsMissingVariableCommandArguments()
{
    var engine = new XScripTHEngine();
    var context = new XScriptExecutionContext(engine);
    var invocation = Invoke(new StringLengthCommand(), new CommandVariableArgument("message", typeof(string)));

    var exception = await AssertThrowsAsync<InvalidOperationException>(async () =>
    {
        await engine.ExecuteAsync(Program(invocation), context);
    });

    if (!exception.Message.Contains("$message"))
    {
        throw new InvalidOperationException(
            $"Expected exception message containing '$message', but got '{exception.Message}'.");
    }
}

static async Task PassingBlockToStringExecutesLazily()
{
    var literal = new LiteralStringCommand("hello");
    var parent = new StringLengthCommand();
    var block = new CommandBlockArgument([Invoke(literal)], [typeof(string)]);
    var invocation = Invoke(parent, block);

    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(5, SingleValue<int>(result));
    AssertEqual(1, literal.ExecuteCount);
    AssertEqual(1, parent.ExecuteCount);
}

static async Task PassingBlockAsBlockDoesNotExecuteFirst()
{
    var literal = new LiteralStringCommand("hello");
    var parent = new BlockAcceptingCommand();
    var block = new CommandBlockArgument([Invoke(literal)], [typeof(string)]);
    var invocation = Invoke(parent, block);

    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(1, SingleValue<int>(result));
    AssertEqual(0, literal.ExecuteCount);
}

static async Task FunctionReferenceToPrimitiveExecutesStoredBlock()
{
    var literal = new LiteralStringCommand("hello");
    var engine = new XScripTHEngine();
    var context = new XScriptExecutionContext(engine);
    context.SetFunction("say", new CommandBlockArgument(Program(Invoke(literal)), [typeof(string)]));
    var invocation = Invoke(new StringLengthCommand(), new CommandFunctionReferenceArgument("say", [typeof(string)]));

    var result = await engine.ExecuteAsync(Program(invocation), context);

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(5, SingleValue<int>(result));
    AssertEqual(1, literal.ExecuteCount);
}

static async Task MissingFunctionReferenceThrowsName()
{
    var engine = new XScripTHEngine();
    var context = new XScriptExecutionContext(engine);
    var invocation = Invoke(new StringLengthCommand(),
        new CommandFunctionReferenceArgument("missing", [typeof(string)]));

    var exception = await AssertThrowsAsync<InvalidOperationException>(async () =>
    {
        await engine.ExecuteAsync(Program(invocation), context);
    });

    if (!exception.Message.Contains("@missing"))
    {
        throw new InvalidOperationException(
            $"Expected exception message containing '@missing', but got '{exception.Message}'.");
    }
}

static List<ICommandInvocation> Program(params ICommandInvocation[] invocations) => invocations.ToList();

static CommandValueArgument Value(object? value) => new(value);

static CommandInvocationArgument Nested(ICommandInvocation invocation) => new(invocation);

static CommandInvocation Invoke(ICommand command, params ICommandArgument[] arguments) =>
    CommandInvocation.FromCommand(command, arguments);

static T SingleValue<T>(ICommandOutput output)
{
    if (output.Values is not { Count: 1 })
    {
        throw new InvalidOperationException($"Expected exactly one value, got {output.Values?.Count ?? 0}.");
    }

    return (T)output.Values[0]!;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Expected exception {typeof(TException).FullName}, but caught {ex.GetType().FullName}: {ex.Message}");
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}.");
}

[CommandTypes([], [typeof(string)])]
sealed class LiteralStringCommand(string value) : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([value]));
    }
}

[CommandTypes([typeof(string)], [typeof(int)])]
sealed class StringLengthCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([((string)input.Values![0]!).Length]));
    }
}

[CommandTypes([typeof(string)], [typeof(string)])]
sealed class SurroundCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([$"<{(string)input.Values![0]!}>"]));
    }
}

[CommandTypes([], [])]
sealed class NoInputCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}

[CommandTypes([], [typeof(string)])]
sealed class ErrorStringCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Error(["bad"]));
    }
}

[CommandTypes([typeof(CommandBlockArgument)], [typeof(int)])]
sealed class BlockAcceptingCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandInput input)
    {
        var block = (CommandBlockArgument)input.Values![0]!;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([block.Invocations.Count]));
    }
}