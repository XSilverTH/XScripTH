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
    ("runtime executes valid direct command inputs", RuntimeExecutesValidDirectCommandInputs)
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

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

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
    public int ExecuteCount { get; private set; }

    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        ExecuteCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
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
