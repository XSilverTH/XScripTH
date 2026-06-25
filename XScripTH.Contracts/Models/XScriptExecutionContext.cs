using System;
using System.Collections.Generic;
using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public class XScriptExecutionContext : IExecutionContext
{
    private readonly XScriptExecutionContext? _parent;
    private readonly Dictionary<string, object?> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandBlockArgument> _functions = new(StringComparer.Ordinal);

    public ICommandExecutor Executor { get; }

    public XScriptExecutionContext(ICommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        Executor = executor;
    }

    private XScriptExecutionContext(ICommandExecutor executor, XScriptExecutionContext parent) : this(executor)
    {
        _parent = parent;
    }

    public bool TryGetVariable(string name, out object? value)
    {
        var normalized = NormalizeVariable(name);
        if (_variables.TryGetValue(normalized, out value))
        {
            return true;
        }

        if (_parent != null)
        {
            return _parent.TryGetVariable(normalized, out value);
        }

        value = null;
        return false;
    }

    public void SetVariable(string name, object? value)
    {
        var normalized = NormalizeVariable(name);
        _variables[normalized] = value;
    }

    public bool TryGetFunction(string name, out CommandBlockArgument? block)
    {
        var normalized = NormalizeFunction(name);
        if (_functions.TryGetValue(normalized, out block))
        {
            return true;
        }

        if (_parent != null)
        {
            return _parent.TryGetFunction(normalized, out block);
        }

        block = null;
        return false;
    }

    public void SetFunction(string name, CommandBlockArgument block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var normalized = NormalizeFunction(name);
        _functions[normalized] = block;
    }

    public IExecutionContext CreateChildScope()
    {
        return new XScriptExecutionContext(Executor, this);
    }

    private static string NormalizeVariable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Variable name must be non-empty.", nameof(name));
        }

        return name[0] == '$' ? name[1..] : name;
    }

    private static string NormalizeFunction(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Function name must be non-empty.", nameof(name));
        }

        return name[0] == '@' ? name[1..] : name;
    }
}