using XScripTH.Contracts.Models;

namespace XScripTH.Contracts.Interfaces;

public interface ICompileTimeSymbolTable
{
    void DeclareVariable(string name, Type type);
    bool TryGetVariableType(string name, out Type? type);
    void DeclareFunction(string name, CommandFunctionSignature signature);
    bool TryGetFunctionSignature(string name, out CommandFunctionSignature? signature);
    ICompileTimeSymbolTable CreateChildScope();
}