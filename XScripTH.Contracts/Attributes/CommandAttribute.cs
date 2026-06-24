namespace XScripTH.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute(string name) : Attribute
{
    public string Name => name;
}
