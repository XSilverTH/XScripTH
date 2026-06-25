using XScripTH.Core.Commands.Console;
using XScripTH.Engine;
using XScripTH.Language;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: XScripTH <script>");
    return 1;
}

var source = string.Join(' ', args);
var engine = new XScripTHEngine();
var registry = CommandRegistry.FromAssemblies(typeof(Print).Assembly);
var compiler = new XScriptCompiler(registry, executor: engine);
var invocations = await compiler.CompileAsync(source);
var output = await engine.ExecuteAsync(invocations.Select(t => t.Result));

return (int)output.Status;
