namespace XScripTH.Contracts.Interfaces;

public interface ICompileTimeSymbolTable
{
    void DeclareVariable(string name, Type type);
    bool TryGetVariableType(string name, out Type? type);
    void DeclareFunction(string name, Type[] outputTypes);
    bool TryGetFunctionOutputTypes(string name, out Type[]? outputTypes);
}
