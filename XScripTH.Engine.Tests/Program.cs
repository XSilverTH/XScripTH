using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Enums;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Engine;
using XScripTH.Engine.Exceptions;

var tests = new (string Name, Func<Task> Run)[]
{
    ("type checker accepts recursive command inputs without executing them", TypeCheckerAcceptsRecursiveCommandInputsWithoutExecutingThem),
    ("resolves nested command input lazily at runtime", ResolvesNestedCommandInputLazilyAtRuntime),
    ("does not chain top-level command outputs", DoesNotChainTopLevelCommandOutputs),
    ("rejects nested command output type mismatch during populate", RejectsNestedCommandOutputTypeMismatchDuringPopulate),
    ("supports recursive nested command inputs", SupportsRecursiveNestedCommandInputs),
    ("bubbles nested error output without executing parent", BubblesNestedErrorOutputWithoutExecutingParent),
    ("runtime does not invoke type checker", RuntimeDoesNotInvokeTypeChecker)
};

foreach (var test in tests)
{
    await test.Run();
    System.Console.WriteLine($"PASS {test.Name}");
}

static async Task TypeCheckerAcceptsRecursiveCommandInputsWithoutExecutingThem()
{
    var literal = new LiteralStringCommand("hello");
    var parent = new StringLengthCommand();
    var invocation = Invoke(parent, Nested(Invoke(literal)));

    await CommandTypeChecker.Default.EnsureValidAsync(Program(invocation));

    AssertEqual(0, literal.ExecuteCount);
    AssertEqual(0, parent.ExecuteCount);
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

static async Task DoesNotChainTopLevelCommandOutputs()
{
    var invocation = Invoke(new StringLengthCommand());
    var exception = await AssertThrowsAsync<CommandTypeCheckException>(() =>
        CommandTypeChecker.Default.EnsureValidAsync(Program(
            Invoke(new LiteralStringCommand("hello")),
            invocation)));

    AssertEqual(typeof(StringLengthCommand), exception.CommandType);
    AssertEqual(1, exception.Errors.Count);
    AssertPath([0], exception.Errors[0].Path);
    AssertEqual(typeof(string), exception.Errors[0].ExpectedType);
    AssertEqual(null, exception.Errors[0].ActualType);
}

static async Task RejectsNestedCommandOutputTypeMismatchDuringPopulate()
{
    var nested = new LiteralIntCommand(42);
    var parent = new StringLengthCommand();
    var invocation = Invoke(parent, Nested(Invoke(nested)));

    var exception = await AssertThrowsAsync<CommandTypeCheckException>(() =>
        CommandTypeChecker.Default.EnsureValidAsync(Program(invocation)));

    AssertEqual(1, exception.Errors.Count);
    AssertPath([0], exception.Errors[0].Path);
    AssertEqual(typeof(string), exception.Errors[0].ExpectedType);
    AssertEqual(typeof(int), exception.Errors[0].ActualType);
    AssertEqual(0, nested.ExecuteCount);
    AssertEqual(0, parent.ExecuteCount);
}

static async Task SupportsRecursiveNestedCommandInputs()
{
    var invocation = Invoke(
        new StringLengthCommand(),
        Nested(Invoke(
            new SurroundCommand(),
            Nested(Invoke(new LiteralStringCommand("x"))))));

    await CommandTypeChecker.Default.EnsureValidAsync(Program(invocation));
    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(3, SingleValue<int>(result));
}

static async Task BubblesNestedErrorOutputWithoutExecutingParent()
{
    var nested = new ErrorStringCommand();
    var parent = new StringLengthCommand();
    var invocation = Invoke(parent, Nested(Invoke(nested)));

    await CommandTypeChecker.Default.EnsureValidAsync(Program(invocation));
    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Error, result.Status);
    AssertEqual("bad", SingleValue<string>(result));
    AssertEqual(1, nested.ExecuteCount);
    AssertEqual(0, parent.ExecuteCount);
}

static async Task RuntimeDoesNotInvokeTypeChecker()
{
    var invocation = Invoke(new StringLengthCommand(), Value("hello"));
    var result = await new XScripTHEngine().ExecuteAsync(Program(invocation));

    AssertEqual(CommandStatus.Ok, result.Status);
    AssertEqual(5, SingleValue<int>(result));
}

static List<Task<ICommandInvocation>> Program(params ICommandInvocation[] invocations) => invocations.Select(Task.FromResult).ToList();

static CommandValueArgument Value(object? value) => new(value);

static CommandInvocationArgument Nested(ICommandInvocation invocation) => new(invocation);

static CommandInvocation Invoke(ICommand command, params ICommandArgument[] arguments) => CommandInvocation.FromCommand(command, arguments);

static T SingleValue<T>(ICommandIo io)
{
    if (io.Values is not { Count: 1 })
    {
        throw new InvalidOperationException($"Expected exactly one value, got {io.Values?.Count ?? 0}.");
    }

    return (T)io.Values[0]!;
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

    throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}.");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void AssertPath(IReadOnlyList<int> expected, IReadOnlyList<int> actual)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"Expected path {FormatPath(expected)}, got {FormatPath(actual)}.");
    }
}

static string FormatPath(IReadOnlyList<int> path) => string.Join('.', path.Select(index => $"[{index}]"));

[CommandTypes([], [typeof(string)])]
sealed class LiteralStringCommand(string value) : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([value]));
    }
}

[CommandTypes([], [typeof(int)])]
sealed class LiteralIntCommand(int value) : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([value]));
    }
}

[CommandTypes([typeof(string)], [typeof(int)])]
sealed class StringLengthCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([((string)input.Values![0]!).Length]));
    }
}

[CommandTypes([typeof(string)], [typeof(string)])]
sealed class SurroundCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([$"<{(string)input.Values![0]!}>"]));
    }
}

[CommandTypes([], [])]
sealed class NoInputCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input) => Task.FromResult<ICommandOutput>(CommandOutput.Ok());
}

[CommandTypes([], [typeof(string)])]
sealed class ErrorStringCommand : ICommand
{
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Error(["bad"]));
    }
}
