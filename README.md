# XScripTH

XScripTH is a statically-typed, command-based scripting language and execution engine built in C# for .NET. It is designed to evaluate scripts through a strict, sequential pipeline that parses text, validates types at compile-time, and executes asynchronous commands.

This document explains the internal mechanics of the engine and provides a comprehensive guide to the syntax structure of the language.

---

## How the Engine Works

The lifecycle of an XScripTH script goes through four distinct phases before and during execution:

1. **Parsing (AST Generation)**
The `XScriptParser` lexes the raw script string into tokens and constructs an Abstract Syntax Tree (AST). It identifies command invocations, nested commands, blocks, and arguments.
2. **Compilation & Compile-Time Execution**
The `XScriptCompiler` traverses the AST and binds text identifiers to actual C# classes using the `CommandRegistry`. During this phase, commands that implement `ICompileTimePhase` (such as `var`, `func`, or `import`) are executed. This allows the compiler to populate the static symbol table (variables and function signatures) before the script ever runs.
3. **Static Type Checking**
Before execution, the `CommandTypeChecker` statically analyzes the bound program. Every C# command defines its expected inputs and outputs via the `[CommandTypes]` attribute. The type checker traverses the script to ensure that literals, variables, and nested command outputs strictly match the expected input types of their parent commands. If a type mismatch occurs, compilation fails immediately.
4. **Runtime Execution**
The `XScripTHEngine` executes the compiled `ICommandInvocation` list. It processes commands sequentially, resolving nested commands first, and maintains an `XScriptExecutionContext` to handle variable state and child scoping (e.g., inside `if` or `while` blocks).

---

## Syntax Structure & Language Rules

XScripTH is purely command-driven. There are no operators like `+` or `=` natively in the language grammar; everything is a command that takes arguments (comma-separated) and returns outputs.

### 1. Commands and Terminators

Every statement in XScripTH is a command invocation. Commands are invoked by their name, followed by an optional, comma-separated list of arguments, and must be explicitly terminated.

There are two terminators:

* **`;` (Await):** Blocks the execution pipeline until the command completes.
* **`;;` (Fire-and-Forget):** Dispatches the command asynchronously in the background and immediately moves to the next instruction.

```xscript
// Synchronous execution
print "Hello World";

// Asynchronous background execution
long-running-task;; 
mark;

```

### 2. Literals and Arguments

Arguments are separated by commas `,`. The parser supports standard primitives, and infers their underlying .NET types. Numeric literals support C#-style suffixes to enforce specific types during static type checking.

* **Strings:** `"Hello"` (`string`)
* **Chars:** `'A'` (`char`)
* **Booleans:** `true`, `false` (`bool`)
* **Integers:** `42` (Defaults to `int`)
* **Longs:** `42l` or `42L` (`long`)
* **Floats:** `3.14f` (`float`)
* **Doubles:** `3.14d` (`double`)
* **Decimals:** `3.14m` (`decimal`)

```xscript
// Calling a 'capture' command with mixed literal arguments
capture "text", 'c', 42, 42l, 3.14f, true;

```

### 3. Nested Commands

The output of one command can be passed directly as an argument to another command. Nested commands are written inline and must be terminated by a single semicolon `;` to indicate the end of the nested expression.

```xscript
// 'text' executes first. Its output is passed to 'length'.
// The first ';' closes the 'text' command. The second ';' closes the 'length' command.
length text; ;

// Nested commands can be mixed with literal arguments
surround "[", text;, "]";

```

### 4. Variables

Variables are strongly typed and resolved during the compile-time phase.

* You declare and assign a variable using the `var` command.
* You reference a variable using the `$` prefix.

Because XScripTH evaluates `var` at compile-time, the type checker automatically knows what type `$name` is based on what you assigned to it.

```xscript
// Declare a variable. The type is inferred as 'string'
var $message, "Hello";

// Use the variable
print $message;

// Assigning the output of a command to a variable
var $size, length $message; ;

```

### 5. Deferred Blocks and Returns

Blocks `{ ... }` allow you to group commands without executing them immediately. They are treated as an argument type (`CommandBlockArgument`) and are passed into control flow commands.

If a block needs to resolve to a value, you use the `return` command inside it.

```xscript
// A block that echoes a single value
{ return 42; }

// A block containing multiple statements
{ 
    print "Working...";
    return true; 
}

```

### 6. Functions

Functions in XScripTH are named, reusable deferred blocks.

* Declare a function using the `func` command, providing a string name and a block.
* Reference a declared function using the `@` prefix.

Functions must be declared before they are referenced, as the compile-time symbol table reads top-to-bottom.

```xscript
// Declare a function named "say_hello"
func "say_hello", { 
    return "Hello from function"; 
};

// Use the function reference as an argument
print @say_hello;

```

Direct calls pass positional parameters to a declared function. Parameter declarations are prefix commands inside the function block, and each declaration creates a local variable for the rest of that block.

Supported parameter type aliases are `object`, `string`, `char`, `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, and `decimal`.

```xscript
func "greet_user", {
    param $name, "string";
    param $age, "int";
    print $name;
};

// Await the function call.
@greet_user "Alice", 25;

// Fire and forget.
@greet_user "Bob", 30;;
```

### 7. Control Flow

Standard control flow is implemented as built-in commands that accept blocks as inputs.

**If Command:**
Takes a boolean condition and a block to execute if the condition is true.

```xscript
if true, { 
    print "Condition met!"; 
};

```

**While Command:**
Takes two blocks: a condition block (which must return a `bool`) and a body block. The condition block is re-evaluated before every iteration.

```xscript
var $counter, 0;

// Note: You would need custom commands to manipulate $counter, 
// but the structural syntax looks like this:
while { return true; }, { 
    print "Infinite Loop"; 
};

```

### Scoping Rules

XScripTH supports block-level scoping via `XScriptExecutionContext`. Variables declared inside a `{ ... }` block shadow outer variables but do not leak into the parent scope once the block finishes executing.
