namespace XScripTH.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class CommandTypesAttribute(Type[]? inputs = null, Type[]? outputs = null) : Attribute
{
    public Type[]? Inputs { get; set; } = inputs;

    public Type[]? Outputs { get; set; } = outputs;
}