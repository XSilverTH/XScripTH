namespace XScripTH.Language.Parsing;

public class XScriptParseException : Exception
{
    public int Position { get; }
    public int Line { get; }
    public int Column { get; }

    public XScriptParseException(string message, int position, int line, int column)
        : base(message)
    {
        Position = position;
        Line = line;
        Column = column;
    }

    public XScriptParseException(string message, int position, int line, int column, Exception innerException)
        : base(message, innerException)
    {
        Position = position;
        Line = line;
        Column = column;
    }
}