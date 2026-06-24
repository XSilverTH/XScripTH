namespace XScripTH.Contracts.Interfaces;

public interface ICompileTimeSymbolTable
{
    void DeclareVariable(string name, Type type);
    bool TryGetVariableType(string name, out Type? type);
}
