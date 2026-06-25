namespace XScripTH.Contracts.Interfaces;

public interface ICompilationContext
{
    ICompileTimeSymbolTable Symbols { get; }
    ICompilationContext CreateChildScope();
}
