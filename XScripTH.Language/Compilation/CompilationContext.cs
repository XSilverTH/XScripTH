using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language;

public sealed class CompilationContext : ICompilationContext
{
    public CompilationContext()
        : this(new CompileTimeSymbolTable())
    {
    }

    public CompilationContext(ICompileTimeSymbolTable symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        Symbols = symbols;
    }

    public ICompileTimeSymbolTable Symbols { get; }
}
