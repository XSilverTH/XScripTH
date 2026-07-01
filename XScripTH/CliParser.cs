namespace XScripTH;

internal static class CliParser
{
    public static CliCommand Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return new CliCommand(CliMode.Help);

        var verbose = false;
        var noColor = false;
        var positionals = new List<string>(args.Count);

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "-h" or "--help":
                    return new CliCommand(CliMode.Help, Verbose: verbose, NoColor: noColor);
                case "--version":
                    return new CliCommand(CliMode.Version, Verbose: verbose, NoColor: noColor);
                case "-v" or "--verbose":
                    verbose = true;
                    break;
                case "--no-color":
                    noColor = true;
                    break;
                default:
                    positionals.Add(arg);
                    break;
            }
        }

        if (positionals.Count == 0)
            return new CliCommand(CliMode.Help, Verbose: verbose, NoColor: noColor);

        return positionals[0] switch
        {
            "run" when positionals.Count == 2 => new CliCommand(CliMode.Run, positionals[1], verbose, noColor),
            "check" when positionals.Count == 2 => new CliCommand(CliMode.Check, positionals[1], verbose, noColor),
            "repl" when positionals.Count == 1 => new CliCommand(CliMode.Repl, Verbose: verbose, NoColor: noColor),
            "help" when positionals.Count == 1 => new CliCommand(CliMode.Help, Verbose: verbose, NoColor: noColor),
            _ => throw new CliUsageException($"Invalid command line: {string.Join(' ', args)}")
        };
    }
}

internal sealed class CliUsageException(string message) : Exception(message);
