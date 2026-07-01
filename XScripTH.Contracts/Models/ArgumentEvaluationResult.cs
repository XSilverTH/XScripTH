using XScripTH.Contracts.Interfaces;

namespace XScripTH.Contracts.Models;

public sealed record ArgumentEvaluationResult(object? Value, ICommandOutput? ErrorOutput = null);