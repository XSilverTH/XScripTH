using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

var tests = new (string Name, Func<Task> Run)[]
{
    ("check succeeds without executing print", CheckSucceedsWithoutExecutingPrint),
    ("run executes print", RunExecutesPrint),
    ("syntax errors return syntax exit code and no stack trace", SyntaxErrorsReturnSyntaxExitCodeAndNoStacktrace),
    ("unknown commands return symbol exit code", UnknownCommandsReturnSymbolExitCode),
    ("type errors return type exit code", TypeErrorsReturnTypeExitCode),
    ("usage errors return usage exit code", UsageErrorsReturnUsageExitCode),
    ("expressions work through check and run", ExpressionsWorkThroughCheckAndRun)
};

foreach (var test in tests)
{
    await test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

static async Task CheckSucceedsWithoutExecutingPrint()
{
    var scriptPath = await CreateTemporaryScriptAsync("print \"hello\";");
    try
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "check", scriptPath });
        AssertEqual(0, exitCode);
        AssertContains($"OK {scriptPath}", stdout);
        AssertNotContains("hello", stdout);
        AssertEqual("", stderr.Trim());
    }
    finally
    {
        DeleteTemporaryScript(scriptPath);
    }
}

static async Task RunExecutesPrint()
{
    var scriptPath = await CreateTemporaryScriptAsync("print \"hello\";");
    try
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "run", scriptPath });
        AssertEqual(0, exitCode);
        AssertContains("hello", stdout);
        AssertEqual("", stderr.Trim());
    }
    finally
    {
        DeleteTemporaryScript(scriptPath);
    }
}

static async Task SyntaxErrorsReturnSyntaxExitCodeAndNoStacktrace()
{
    // Syntax error: 1.2 is missing float/double/decimal suffix
    var scriptPath = await CreateTemporaryScriptAsync("1.2;");
    try
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "run", scriptPath });
        AssertEqual(10, exitCode); // SyntaxError
        AssertContains("Script could not be parsed", stderr);
        
        // Assert no stack trace is printed.
        AssertNotContains("at XScripTH.", stderr);
        AssertNotContains("at System.", stderr);
        AssertNotContains("StackTrace", stderr);
    }
    finally
    {
        DeleteTemporaryScript(scriptPath);
    }
}

static async Task UnknownCommandsReturnSymbolExitCode()
{
    var scriptPath = await CreateTemporaryScriptAsync("nonexistent_command \"arg\";");
    try
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "run", scriptPath });
        AssertEqual(12, exitCode); // SymbolError
        AssertContains("Name resolution failed", stderr);
        AssertContains("nonexistent_command", stderr);
    }
    finally
    {
        DeleteTemporaryScript(scriptPath);
    }
}

static async Task TypeErrorsReturnTypeExitCode()
{
    var scriptPath = await CreateTemporaryScriptAsync("print 42;");
    try
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "check", scriptPath });
        AssertEqual(11, exitCode);
        AssertEqual("", stdout.Trim());
        AssertContains("static type validation", stderr);
        AssertContains("expected: System.String", stderr);
        AssertContains("actual: System.Int32", stderr);
        AssertNotContains("at XScripTH.", stderr);
    }
    finally
    {
        DeleteTemporaryScript(scriptPath);
    }
}

static async Task UsageErrorsReturnUsageExitCode()
{
    // Invalid command usage: "run" without script path
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "run" });
        AssertEqual(2, exitCode); // UsageError
        AssertContains("Usage error", stderr);
        AssertContains("Usage:", stderr);
    }

    // Invalid command usage: unrecognized command
    {
        var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "invalid_mode_here" });
        AssertEqual(2, exitCode); // UsageError
        AssertContains("Usage error", stderr);
        AssertContains("Usage:", stderr);
    }
}

static async Task ExpressionsWorkThroughCheckAndRun()
{
    var scriptPath = await CreateTemporaryScriptAsync("if ((1 + 2) == 3), { print \"ok\"; };");
    try
    {
        var (checkExitCode, checkStdout, checkStderr) = await RunCliAsync(new[] { "check", scriptPath });
        AssertEqual(0, checkExitCode);
        AssertContains($"OK {scriptPath}", checkStdout);
        AssertEqual("", checkStderr.Trim());

        var (runExitCode, runStdout, runStderr) = await RunCliAsync(new[] { "run", scriptPath });
        AssertEqual(0, runExitCode);
        AssertContains("ok", runStdout);
        AssertEqual("", runStderr.Trim());
    }
    finally
    {
        DeleteTemporaryScript(scriptPath);
    }
}

// Helpers
static async Task<string> CreateTemporaryScriptAsync(string content)
{
    var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xs");
    await File.WriteAllTextAsync(tempFile, content);
    return tempFile;
}

static void DeleteTemporaryScript(string path)
{
    try
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch
    {
        // Ignore cleanup errors in tests
    }
}

static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(string[] args)
{
    var cliTarget = GetCliPath();
    var processStartInfo = new ProcessStartInfo();
    processStartInfo.FileName = "dotnet";
    
    if (cliTarget.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
    {
        var newArgs = new List<string> { cliTarget };
        newArgs.AddRange(args);
        processStartInfo.Arguments = string.Join(" ", newArgs.Select(EscapeArgument));
    }
    else
    {
        var newArgs = new List<string> { "run", "--project", cliTarget, "--" };
        newArgs.AddRange(args);
        processStartInfo.Arguments = string.Join(" ", newArgs.Select(EscapeArgument));
    }
    
    processStartInfo.RedirectStandardOutput = true;
    processStartInfo.RedirectStandardError = true;
    processStartInfo.UseShellExecute = false;
    processStartInfo.CreateNoWindow = true;

    using var process = Process.Start(processStartInfo);
    if (process == null)
    {
        throw new Exception("Failed to start dotnet process.");
    }

    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    return (process.ExitCode, await stdoutTask, await stderrTask);
}

static string EscapeArgument(string arg)
{
    if (string.IsNullOrEmpty(arg)) return "\"\"";
    if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\\'))
    {
        var escaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
    return arg;
}

static string GetCliPath()
{
    var baseDir = AppContext.BaseDirectory;
    var dir = new DirectoryInfo(baseDir);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XScripTH.slnx")))
    {
        dir = dir.Parent;
    }

    if (dir == null)
    {
        throw new Exception("Could not find repository root containing XScripTH.slnx");
    }

    var repoRoot = dir.FullName;
    var config = "Debug";
    if (baseDir.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
    {
        config = "Release";
    }

    var dllPath = Path.Combine(repoRoot, "XScripTH", "bin", config, "net10.0", "XScripTH.dll");
    if (File.Exists(dllPath))
    {
        return dllPath;
    }

    var csprojPath = Path.Combine(repoRoot, "XScripTH", "XScripTH.csproj");
    if (File.Exists(csprojPath))
    {
        return csprojPath;
    }

    throw new Exception($"Could not find XScripTH.dll at {dllPath} or XScripTH.csproj at {csprojPath}");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"AssertEqual Failed:\nExpected: {expected}\nActual:   {actual}");
    }
}

static void AssertContains(string expectedSubstring, string actualString)
{
    if (actualString == null || !actualString.Contains(expectedSubstring))
    {
        throw new Exception($"AssertContains Failed:\nExpected substring: '{expectedSubstring}'\nActual string:      '{actualString}'");
    }
}

static void AssertNotContains(string unexpectedSubstring, string actualString)
{
    if (actualString != null && actualString.Contains(unexpectedSubstring))
    {
        throw new Exception($"AssertNotContains Failed:\nUnexpected substring: '{unexpectedSubstring}'\nActual string:        '{actualString}'");
    }
}
