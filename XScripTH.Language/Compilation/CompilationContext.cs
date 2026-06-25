using XScripTH.Contracts.Interfaces;

namespace XScripTH.Language.Compilation;

public sealed class CompilationContext : ICompilationContext
{
    public CompilationContext()
        : this(new CompileTimeSymbolTable())
    {
    }

    private CompilationContext(ICompileTimeSymbolTable symbols)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        Symbols = symbols;
    }

    public ICompileTimeSymbolTable Symbols { get; }

    public ICompilationContext CreateChildScope()
    {
        return new CompilationContext(Symbols.CreateChildScope());
    }
}