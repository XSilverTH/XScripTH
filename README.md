# XScripTH

A simple, strongly-typed, command-based scripting engine for .NET.

---

## How It Works

1. **Parser**: Lexes and parses script text into an Abstract Syntax Tree (AST).
2. **Registry**: Discovers command definitions in loaded assemblies via the `[Command]` attribute.
3. **Compiler**: Lowers the AST into command invocations, runs compile-time phases via `ICompileTimePhase`, omits phases marked `[NoRuntimeInvocation]`, and runs a static type check before execution.
4. **Type Checker**: Statically validates that argument types (both literals and nested command outputs) match the expected input types declared on each command.
5. **Engine**: Executes command invocations sequentially. Supports synchronous (`;`) and asynchronous/fire-and-forget (`;;`) execution pipelines.

---

## Syntax Guide

A script consists of one or more command calls separated by a semicolon (`;`) or a double semicolon (`;;`).

### Command Calls
```
CommandName [Arg1, Arg2, ...];
CommandName [Arg1, Arg2, ...];;
```
* **`;` (Synchronous / Await)**: Blocks execution until the command completes.
* **`;;` (Asynchronous / Fire-and-Forget)**: Starts the command in the background and immediately proceeds to the next command.

### Literals
Arguments can be literals. Suffixes determine their C# type:
* **String**: `"Hello, World"` (`string`)
* **Char**: `'A'` (`char`)
* **Boolean**: `true` or `false` (`bool`)
* **Int**: `42` (`int`)
* **Long**: `42l` or `42L` (`long`)
* **Float**: `3.14f` or `3.14F` (`float`)
* **Double**: `3.14d` or `3.14D` (`double`)
* **Decimal**: `3.14m` or `3.14M` (`decimal`)

### Nested Commands
Commands can receive the output of other commands as arguments by writing them inside the argument list. Nested commands must end with a single semicolon (`;`).
```
// Evaluates 'text' first and passes its output to 'length'
length text; ;

// Nested command among other literal arguments
surround "[", text;, "]";
```

### Variables
Declare variables with `var $name, value;` and pass them to later commands with `$name`.
```
var $message, "Hello";
print $message;
```

### Deferred Blocks and Functions
Use `{ ... }` to pass a command block without executing it immediately. Commands inside the block still use normal `;` or `;;` terminators.
```
if { return true; }, { print "yes"; };
```

`return` echoes one value from a block so it can be used where a primitive value is expected.
```
print { return "hello"; };
```

Declare reusable blocks with `func`, then reference them with `@name`. Functions must be declared before any `@name` reference.
```
func "say", { return "hello"; };
print @say;

func "body", { print "tick"; };
if true, @body;
```

`while` takes a condition block and a body block. The condition block is re-executed each iteration and must return one `bool`.
```
while { return true; }, { print "loop"; };
```

---

## Defining Commands in C#

To add a command to the engine, implement `ICommand` and decorate it with attributes:

```csharp
using XScripTH.Contracts.Attributes;
using XScripTH.Contracts.Interfaces;
using XScripTH.Contracts.Models;

[Command("print")]
[CommandTypes([typeof(string)], [])] // Input types, Output types
public sealed class PrintCommand : ICommand
{
    public Task<ICommandOutput> Execute(ICommandIo input)
    {
        // Access input parameters via input.Values
        string message = (string)input.Values![0]!;
        Console.WriteLine(message);

        return Task.FromResult<ICommandOutput>(CommandOutput.Ok());
    }
}
```

---

## Usage

Compile and run a script using `XScriptCompiler` and `XScripTHEngine`:

```csharp
using XScripTH.Engine;
using XScripTH.Language;

string script = "print \"Hello, World!\";";

var registry = CommandRegistry.FromAssemblies(typeof(PrintCommand).Assembly);
var engine = new XScripTHEngine();
var compiler = new XScriptCompiler(registry, executor: engine);

// Compile & Type Check
var invocations = await compiler.CompileAsync(script);

// Execute
var output = await engine.ExecuteAsync(invocations);
```
