using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed class FunctionStore : IFunctionStore
{
    private readonly Dictionary<string, CommandBlockArgument> _blocks = new(StringComparer.Ordinal);

    public void Set(string name, CommandBlockArgument block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var normalizedName = NormalizeName(name);
        _blocks[normalizedName] = block;
    }

    public bool TryGet(string name, out CommandBlockArgument? block)
    {
        var normalizedName = NormalizeName(name);
        return _blocks.TryGetValue(normalizedName, out block);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name must be non-empty.", nameof(name));
        }

        return name[0] == '@' ? name[1..] : name;
    }
}
