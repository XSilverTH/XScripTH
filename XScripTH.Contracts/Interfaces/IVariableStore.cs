namespace XScripTH.Contracts.Interfaces;

public interface IVariableStore
{
    void Set(string name, object? value);
    bool TryGet(string name, out object? value);
}
