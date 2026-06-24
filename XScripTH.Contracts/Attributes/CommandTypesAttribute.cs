namespace XScripTH.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class CommandTypesAttribute(Type[]? inputs = null) : Attribute
{
    public Type[]? Inputs { get; set; } = inputs;
}