using XScripTH.Core.Commands.Console;
using XScripTH.Engine;
using XScripTH.Language.Compilation;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: XScripTH <script>");
    return 1;
}

// var source = string.Join(' ', args);
var source = File.ReadAllText(args[0]);
var engine = new XScripTHEngine();
var registry = CommandRegistry.FromAssemblies(typeof(Print).Assembly);
var compiler = new XScriptCompiler(registry);
var invocations = await compiler.CompileAsync(source);
var output = await engine.ExecuteAsync(invocations);

return (int)output.Status;