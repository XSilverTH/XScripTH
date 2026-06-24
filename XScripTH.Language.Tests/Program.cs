using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;
using XScripTH.Engine;
using XScripTH.Language;
using XScripTH.Language.Ast;
using XScripTH.Language.Validation;
using XScripTH.Language.Validation.Exceptions;

var tests = new (string Name, Func<Task> Run)[]
{
    ("parses literals and executes command inputs", LiteralsExecution),
    ("parses nested command input and typechecks output", NestedCommandInputTypechecks),
    ("rejects nested command output mismatch during language typecheck", NestedOutputMismatch),
    ("type checker accepts recursive command inputs without executing them", TypeCheckerAcceptsRecursiveCommandInputsWithoutExecutingThem),
    ("type checker rejects top-level output chaining", TypeCheckerRejectsTopLevelOutputChaining),
    ("type checker rejects nested command output mismatch", TypeCheckerRejectsNestedCommandOutputMismatch),
    ("type checker supports recursive nested command inputs", TypeCheckerSupportsRecursiveNestedCommandInputs),
    ("supports comma separated nested command among literals", CommaSeparatedNestedCommandAmongLiterals),
    ("double semicolon runs without waiting", DoubleSemicolonRunsWithoutWaiting),
    ("nested double semicolon is rejected", NestedDoubleSemicolonIsRejected),
    ("unknown command fails during compile", UnknownCommandFailsDuringCompile)
};

foreach (var test in tests)
{
    await test.Run();
    System.Console.WriteLine($"PASS {test.Name}");
}

static async Task LiteralsExecution()
{
    CapturedValues.Reset();
    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry);
    var invocationTasks = await compiler.CompileAsync("capture \"hi\",'x',5,5l,5.25f,5.25d,5.25m,true;");
    var engine = new XScripTHEngine();
    var outputs = await engine.ExecuteAllAsync(invocationTasks);

    AssertEqual(1, CapturedValues.ExecuteCount);
    AssertEqual("hi", CapturedValues.StringValue);
    AssertEqual('x', CapturedValues.CharValue);
    AssertEqual(5, CapturedValues.IntValue);
    AssertEqual(5L, CapturedValues.LongValue);
    AssertEqual(5.25f, CapturedValues.FloatValue);
    AssertEqual(5.25d, CapturedValues.DoubleValue);
    AssertEqual(5.25m, CapturedValues.DecimalValue);
    AssertEqual(true, CapturedValues.BoolValue);
}

static async Task NestedCommandInputTypechecks()
{
    CommandCounts.Reset();
    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry);
    var invocationTasks = await compiler.CompileAsync("length text; ;");
    var engine = new XScripTHEngine();
    var outputs = await engine.ExecuteAllAsync(invocationTasks);

    AssertEqual(1, outputs.Count);
    AssertEqual(5, (int)outputs[0].Values![0]!);
    AssertEqual(1, CommandCounts.TextCount);
    AssertEqual(1, CommandCounts.LengthCount);
}

static async Task NestedOutputMismatch()
{
    CommandCounts.Reset();
    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry);

    var exception = await AssertThrowsAsync<CommandTypeCheckException>(async () =>
    {
        await compiler.CompileAsync("length number; ;");
    });

    AssertEqual(1, exception.Errors.Count);
    var error = exception.Errors[0];
    AssertPath([0], error.Path);
    AssertEqual(typeof(string), error.ExpectedType);
    AssertEqual(typeof(int), error.ActualType);

    AssertEqual(0, CommandCounts.TextCount);
    AssertEqual(0, CommandCounts.NumberCount);
    AssertEqual(0, CommandCounts.LengthCount);
}

static async Task TypeCheckerAcceptsRecursiveCommandInputsWithoutExecutingThem()
{
    CommandCounts.Reset();
    var invocation = Invoke(new LengthCommand(), Nested(Invoke(new TextCommand())));

    await CommandTypeChecker.Default.EnsureValidAsync(Program(invocation));

    AssertEqual(0, CommandCounts.TextCount);
    AssertEqual(0, CommandCounts.LengthCount);
}

static async Task TypeCheckerRejectsTopLevelOutputChaining()
{
    var invocation = Invoke(new LengthCommand());
    var exception = await AssertThrowsAsync<CommandTypeCheckException>(() =>
        CommandTypeChecker.Default.EnsureValidAsync(Program(
            Invoke(new TextCommand()),
            invocation)));

    AssertEqual(typeof(LengthCommand), exception.CommandType);
    AssertEqual(1, exception.Errors.Count);
    AssertPath([0], exception.Errors[0].Path);
    AssertEqual(typeof(string), exception.Errors[0].ExpectedType);
    AssertEqual(null, exception.Errors[0].ActualType);
}

static async Task TypeCheckerRejectsNestedCommandOutputMismatch()
{
    CommandCounts.Reset();
    var invocation = Invoke(new LengthCommand(), Nested(Invoke(new NumberCommand())));

    var exception = await AssertThrowsAsync<CommandTypeCheckException>(() =>
        CommandTypeChecker.Default.EnsureValidAsync(Program(invocation)));

    AssertEqual(1, exception.Errors.Count);
    AssertPath([0], exception.Errors[0].Path);
    AssertEqual(typeof(string), exception.Errors[0].ExpectedType);
    AssertEqual(typeof(int), exception.Errors[0].ActualType);
    AssertEqual(0, CommandCounts.NumberCount);
    AssertEqual(0, CommandCounts.LengthCount);
}

static async Task TypeCheckerSupportsRecursiveNestedCommandInputs()
{
    var invocation = Invoke(
        new LengthCommand(),
        Nested(Invoke(
            new SurroundCommand(),
            new CommandValueArgument("<"),
            Nested(Invoke(new TextCommand())),
            new CommandValueArgument(">"))));

    await CommandTypeChecker.Default.EnsureValidAsync(Program(invocation));
}

static async Task CommaSeparatedNestedCommandAmongLiterals()
{
    CommandCounts.Reset();
    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry);
    var invocationTasks = await compiler.CompileAsync("surround \"<\",text;,\">\";");
    var engine = new XScripTHEngine();
    var outputs = await engine.ExecuteAllAsync(invocationTasks);

    AssertEqual(1, outputs.Count);
    AssertEqual("<hello>", (string)outputs[0].Values![0]!);
    AssertEqual(1, CommandCounts.TextCount);
    AssertEqual(1, CommandCounts.SurroundCount);
}

static async Task DoubleSemicolonRunsWithoutWaiting()
{
    BlockingGate.Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    CommandCounts.Reset();

    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry, executor: new XScripTHEngine());
    var invocationTasks = await compiler.CompileAsync("block;; mark;");
    var engine = new XScripTHEngine();

    var executeTask = engine.ExecuteAllAsync(invocationTasks);

    // Let the tasks advance to ensure execution scheduler picks up the invocations
    await Task.Delay(100);

    AssertEqual(1, CommandCounts.MarkCount);

    BlockingGate.Tcs.SetResult(true);

    var outputs = await executeTask;
    AssertEqual(2, outputs.Count);
}

static async Task NestedDoubleSemicolonIsRejected()
{
    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry);
    var exception = await AssertThrowsAsync<XScriptParseException>(async () =>
    {
        await compiler.CompileAsync("length text;; ;");
    });

    if (!exception.Message.Contains("Nested command arguments must use ';'"))
    {
        throw new InvalidOperationException($"Expected exception message containing 'Nested command arguments must use ';'', but got '{exception.Message}'.");
    }
}

static async Task UnknownCommandFailsDuringCompile()
{
    var registry = CommandRegistry.FromAssemblies(typeof(TextCommand).Assembly);
    var compiler = new XScriptCompiler(registry);
    var exception = await AssertThrowsAsync<XScriptCommandResolutionException>(async () =>
    {
        await compiler.CompileAsync("missing;");
    });

    AssertEqual("missing", exception.CommandName);
}

static List<Task<ICommandInvocation>> Program(params ICommandInvocation[] invocations) => invocations.Select(Task.FromResult).ToList();

static CommandInvocationArgument Nested(ICommandInvocation invocation) => new(invocation);

static CommandInvocation Invoke(ICommand command, params ICommandArgument[] arguments) => CommandInvocation.FromCommand(command, arguments);

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
        throw new InvalidOperationException($"Expected exception {typeof(TException).FullName}, but caught {ex.GetType().FullName}: {ex.Message}");
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

public static class CapturedValues
{
    public static string? StringValue;
    public static char CharValue;
    public static int IntValue;
    public static long LongValue;
    public static float FloatValue;
    public static double DoubleValue;
    public static decimal DecimalValue;
    public static bool BoolValue;
    public static int ExecuteCount;

    public static void Reset()
    {
        StringValue = null;
        CharValue = default;
        IntValue = default;
        LongValue = default;
        FloatValue = default;
        DoubleValue = default;
        DecimalValue = default;
        BoolValue = default;
        ExecuteCount = 0;
    }
}

public static class CommandCounts
{
    public static int TextCount;
    public static int LengthCount;
    public static int NumberCount;
    public static int SurroundCount;
    public static int MarkCount;

    public static void Reset()
    {
        TextCount = 0;
        LengthCount = 0;
        NumberCount = 0;
        SurroundCount = 0;
        MarkCount = 0;
    }
}

public static class BlockingGate
{
    public static TaskCompletionSource<bool> Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

[Command("text")]
[CommandTypes([], [typeof(string)])]
sealed class TextCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        CommandCounts.TextCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok(["hello"]));
    }
}

[Command("number")]
[CommandTypes([], [typeof(int)])]
sealed class NumberCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        CommandCounts.NumberCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([42]));
    }
}

[Command("length")]
[CommandTypes([typeof(string)], [typeof(int)])]
sealed class LengthCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        CommandCounts.LengthCount++;
        var str = (string)input.Values![0]!;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([str.Length]));
    }
}

[Command("capture")]
[CommandTypes([typeof(string), typeof(char), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal), typeof(bool)], [])]
sealed class CaptureCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        CapturedValues.ExecuteCount++;
        CapturedValues.StringValue = (string)input.Values![0]!;
        CapturedValues.CharValue = (char)input.Values[1]!;
        CapturedValues.IntValue = (int)input.Values[2]!;
        CapturedValues.LongValue = (long)input.Values[3]!;
        CapturedValues.FloatValue = (float)input.Values[4]!;
        CapturedValues.DoubleValue = (double)input.Values[5]!;
        CapturedValues.DecimalValue = (decimal)input.Values[6]!;
        CapturedValues.BoolValue = (bool)input.Values[7]!;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}

[Command("block")]
[CommandTypes([], [])]
sealed class BlockingCommand : ICommand
{
    public async Task<ICommandOutput> Execute(ICommandIo input)
    {
        await BlockingGate.Tcs.Task;
        return CommandOutput.Ok();
    }
}

[Command("mark")]
[CommandTypes([], [])]
sealed class MarkCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        CommandCounts.MarkCount++;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}

[Command("surround")]
[CommandTypes([typeof(string), typeof(string), typeof(string)], [typeof(string)])]
sealed class SurroundCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        CommandCounts.SurroundCount++;
        var val1 = (string)input.Values![0]!;
        var val2 = (string)input.Values[1]!;
        var val3 = (string)input.Values[2]!;
        return Task.FromResult<ICommandOutput>(CommandOutput.Ok([val1 + val2 + val3]));
    }
}
