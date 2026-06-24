using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XScripTH.Language.Ast;

namespace XScripTH.Language;

public sealed class XScriptParser
{
    private string source = string.Empty;
    private int position = 0;
    private int line = 1;
    private int column = 1;
    private Token currentToken = default!;

    public XScriptProgramAst Parse(string source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        this.source = source;
        this.position = 0;
        this.line = 1;
        this.column = 1;

        Consume(); // Load the first token

        var commands = new List<XScriptCommandAst>();
        while (currentToken.Type != TokenType.EndOfFile)
        {
            commands.Add(ParseTopCommand());
        }

        return new XScriptProgramAst(commands);
    }

    private XScriptCommandAst ParseTopCommand()
    {
        if (currentToken.Type == TokenType.Semicolon || currentToken.Type == TokenType.DoubleSemicolon)
        {
            throw new XScriptParseException(
                $"Empty statement is not allowed. Unexpected '{GetTokenName(currentToken)}', expected command identifier.",
                currentToken.Position,
                currentToken.Line,
                currentToken.Column);
        }

        var command = ParseCommandBody(isTopLevel: true);

        XScriptCommandTerminator terminator;
        if (currentToken.Type == TokenType.Semicolon)
        {
            terminator = XScriptCommandTerminator.Await;
            Consume();
        }
        else if (currentToken.Type == TokenType.DoubleSemicolon)
        {
            terminator = XScriptCommandTerminator.FireAndForget;
            Consume();
        }
        else
        {
            throw new XScriptParseException(
                $"Unexpected token '{GetTokenName(currentToken)}', expected ';' or ';;' to terminate the top-level command.",
                currentToken.Position,
                currentToken.Line,
                currentToken.Column);
        }

        return new XScriptCommandAst(command.Name, command.Arguments, terminator);
    }

    private (string Name, IReadOnlyList<XScriptArgumentAst> Arguments) ParseCommandBody(bool isTopLevel)
    {
        if (currentToken.Type != TokenType.Identifier)
        {
            throw new XScriptParseException(
                $"Unexpected token '{GetTokenName(currentToken)}', expected command identifier.",
                currentToken.Position,
                currentToken.Line,
                currentToken.Column);
        }

        string name = (string)currentToken.Value!;
        Consume(); // Consume Identifier

        var arguments = new List<XScriptArgumentAst>();

        if (IsArgumentStart(currentToken.Type))
        {
            arguments.Add(ParseArgument());

            while (true)
            {
                if (currentToken.Type == TokenType.Comma)
                {
                    Consume(); // Consume ','
                    if (!IsArgumentStart(currentToken.Type))
                    {
                        throw new XScriptParseException(
                            $"Unexpected token '{GetTokenName(currentToken)}' after comma, expected argument.",
                            currentToken.Position,
                            currentToken.Line,
                            currentToken.Column);
                    }
                    arguments.Add(ParseArgument());
                }
                else if (IsArgumentStart(currentToken.Type))
                {
                    throw new XScriptParseException(
                        $"Missing comma between arguments. Unexpected token '{GetTokenName(currentToken)}', expected ',' or command terminator.",
                        currentToken.Position,
                        currentToken.Line,
                        currentToken.Column);
                }
                else
                {
                    break;
                }
            }
        }

        return (name, arguments);
    }

    private XScriptArgumentAst ParseArgument()
    {
        if (currentToken.Type == TokenType.Identifier)
        {
            var nested = ParseCommandBody(isTopLevel: false);

            if (currentToken.Type == TokenType.DoubleSemicolon)
            {
                throw new XScriptParseException(
                    $"Nested command arguments must use ';'. Unexpected double semicolon ';;' at {currentToken.Line}:{currentToken.Column}.",
                    currentToken.Position,
                    currentToken.Line,
                    currentToken.Column);
            }

            if (currentToken.Type != TokenType.Semicolon)
            {
                throw new XScriptParseException(
                    $"Unexpected token '{GetTokenName(currentToken)}', expected ';' to terminate the nested command.",
                    currentToken.Position,
                    currentToken.Line,
                    currentToken.Column);
            }

            Consume(); // Consume ';'

            var nestedCommand = new XScriptCommandAst(nested.Name, nested.Arguments, XScriptCommandTerminator.Await);
            return new XScriptCommandArgumentAst(nestedCommand);
        }
        else if (currentToken.Type == TokenType.String ||
                 currentToken.Type == TokenType.Char ||
                 currentToken.Type == TokenType.Number ||
                 currentToken.Type == TokenType.Bool)
        {
            object value = currentToken.Value!;
            Consume(); // Consume literal
            return new XScriptLiteralArgumentAst(value);
        }
        else
        {
            throw new XScriptParseException(
                $"Unexpected token '{GetTokenName(currentToken)}', expected argument.",
                currentToken.Position,
                currentToken.Line,
                currentToken.Column);
        }
    }

    private bool IsArgumentStart(TokenType type)
    {
        return type == TokenType.String ||
               type == TokenType.Char ||
               type == TokenType.Number ||
               type == TokenType.Bool ||
               type == TokenType.Identifier;
    }

    private void Consume()
    {
        currentToken = NextToken();
    }

    private string GetTokenName(Token t)
    {
        return t.Type switch
        {
            TokenType.EndOfFile => "EOF",
            _ => t.Value?.ToString() ?? t.Type.ToString()
        };
    }

    private void Advance()
    {
        if (position < source.Length)
        {
            char c = source[position];
            position++;
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
    }

    private void SkipWhitespace()
    {
        while (position < source.Length && char.IsWhiteSpace(source[position]))
        {
            Advance();
        }
    }

    private Token NextToken()
    {
        SkipWhitespace();

        if (position >= source.Length)
        {
            return new Token(TokenType.EndOfFile, null, position, line, column);
        }

        int startPos = position;
        int startLine = line;
        int startCol = column;

        char c = source[position];

        if (c == ';')
        {
            Advance();
            if (position < source.Length && source[position] == ';')
            {
                Advance();
                return new Token(TokenType.DoubleSemicolon, ";;", startPos, startLine, startCol);
            }
            return new Token(TokenType.Semicolon, ";", startPos, startLine, startCol);
        }

        if (c == ',')
        {
            Advance();
            return new Token(TokenType.Comma, ",", startPos, startLine, startCol);
        }

        if (c == '"')
        {
            return ReadStringToken(startPos, startLine, startCol);
        }

        if (c == '\'')
        {
            return ReadCharToken(startPos, startLine, startCol);
        }

        if (char.IsDigit(c) || (c == '-' && position + 1 < source.Length && char.IsDigit(source[position + 1])))
        {
            return ReadNumberToken(startPos, startLine, startCol);
        }

        if (char.IsLetter(c) || c == '_')
        {
            return ReadIdentifierOrBoolToken(startPos, startLine, startCol);
        }

        throw new XScriptParseException($"Unexpected character '{c}'", startPos, startLine, startCol);
    }

    private Token ReadStringToken(int startPos, int startLine, int startCol)
    {
        Advance(); // Consume opening '"'
        var sb = new StringBuilder();

        while (position < source.Length)
        {
            char c = source[position];
            if (c == '"')
            {
                Advance(); // Consume closing '"'
                return new Token(TokenType.String, sb.ToString(), startPos, startLine, startCol);
            }
            if (c == '\\')
            {
                Advance(); // Consume '\\'
                if (position >= source.Length)
                {
                    throw new XScriptParseException("Unterminated string literal", startPos, startLine, startCol);
                }
                char esc = source[position];
                Advance(); // Consume escape character
                switch (esc)
                {
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '0': sb.Append('\0'); break;
                    default:
                        throw new XScriptParseException($"Invalid escape sequence '\\{esc}' in string literal", startPos, startLine, startCol);
                }
            }
            else
            {
                sb.Append(c);
                Advance();
            }
        }

        throw new XScriptParseException("Unterminated string literal", startPos, startLine, startCol);
    }

    private Token ReadCharToken(int startPos, int startLine, int startCol)
    {
        Advance(); // Consume opening '\''
        var sb = new StringBuilder();

        while (position < source.Length)
        {
            char c = source[position];
            if (c == '\'')
            {
                Advance(); // Consume closing '\''
                string val = sb.ToString();
                if (val.Length != 1)
                {
                    if (val.Length == 0)
                    {
                        throw new XScriptParseException("Empty char literal", startPos, startLine, startCol);
                    }
                    throw new XScriptParseException($"Multi-character char literal '{val}'", startPos, startLine, startCol);
                }
                return new Token(TokenType.Char, val[0], startPos, startLine, startCol);
            }
            if (c == '\\')
            {
                Advance(); // Consume '\\'
                if (position >= source.Length)
                {
                    throw new XScriptParseException("Unterminated char literal", startPos, startLine, startCol);
                }
                char esc = source[position];
                Advance(); // Consume escape character
                switch (esc)
                {
                    case '\\': sb.Append('\\'); break;
                    case '\'': sb.Append('\''); break;
                    case '"': sb.Append('"'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '0': sb.Append('\0'); break;
                    default:
                        throw new XScriptParseException($"Invalid escape sequence '\\{esc}' in char literal", startPos, startLine, startCol);
                }
            }
            else
            {
                sb.Append(c);
                Advance();
            }
        }

        throw new XScriptParseException("Unterminated char literal", startPos, startLine, startCol);
    }

    private Token ReadNumberToken(int startPos, int startLine, int startCol)
    {
        int numStart = position;
        if (source[position] == '-')
        {
            Advance();
        }
        while (position < source.Length && char.IsDigit(source[position]))
        {
            Advance();
        }

        bool hasDecimal = false;
        if (position + 1 < source.Length && source[position] == '.' && char.IsDigit(source[position + 1]))
        {
            hasDecimal = true;
            Advance(); // Consume '.'
            while (position < source.Length && char.IsDigit(source[position]))
            {
                Advance();
            }
        }

        int numEnd = position;
        string numStr = source.Substring(numStart, numEnd - numStart);

        int suffixStart = position;
        while (position < source.Length && char.IsLetter(source[position]))
        {
            Advance();
        }
        string suffix = source.Substring(suffixStart, position - suffixStart).ToLowerInvariant();

        if (hasDecimal)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                throw new XScriptParseException($"Decimal-point numeric literal '{numStr}' requires a suffix (f, d, or m)", startPos, startLine, startCol);
            }
            if (suffix != "f" && suffix != "d" && suffix != "m")
            {
                throw new XScriptParseException($"Integral suffix '{suffix}' is not allowed on decimal-point literal '{numStr}{suffix}'", startPos, startLine, startCol);
            }
        }
        else
        {
            var validSuffixes = new HashSet<string> { "", "i", "l", "s", "b", "sb", "u", "ul", "us", "f", "d", "m" };
            if (!validSuffixes.Contains(suffix))
            {
                throw new XScriptParseException($"Unknown suffix '{suffix}' on numeric literal '{numStr}{suffix}'", startPos, startLine, startCol);
            }
        }

        try
        {
            object val = suffix switch
            {
                "" or "i" => int.Parse(numStr, CultureInfo.InvariantCulture),
                "l" => long.Parse(numStr, CultureInfo.InvariantCulture),
                "s" => short.Parse(numStr, CultureInfo.InvariantCulture),
                "b" => byte.Parse(numStr, CultureInfo.InvariantCulture),
                "sb" => sbyte.Parse(numStr, CultureInfo.InvariantCulture),
                "u" => uint.Parse(numStr, CultureInfo.InvariantCulture),
                "ul" => ulong.Parse(numStr, CultureInfo.InvariantCulture),
                "us" => ushort.Parse(numStr, CultureInfo.InvariantCulture),
                "f" => float.Parse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture),
                "d" => double.Parse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture),
                "m" => decimal.Parse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture),
                _ => throw new XScriptParseException($"Unknown suffix '{suffix}'", startPos, startLine, startCol)
            };

            return new Token(TokenType.Number, val, startPos, startLine, startCol);
        }
        catch (OverflowException)
        {
            string typeName = suffix switch
            {
                "" or "i" => "int",
                "l" => "long",
                "s" => "short",
                "b" => "byte",
                "sb" => "sbyte",
                "u" => "uint",
                "ul" => "ulong",
                "us" => "ushort",
                "f" => "float",
                "d" => "double",
                "m" => "decimal",
                _ => "unknown"
            };
            throw new XScriptParseException($"Numeric literal '{numStr}{suffix}' overflowed for type '{typeName}'", startPos, startLine, startCol);
        }
        catch (FormatException exception)
        {
            throw new XScriptParseException(
                $"Invalid numeric literal '{numStr}{suffix}'.",
                startPos,
                startLine,
                startCol,
                exception);
        }
    }

    private Token ReadIdentifierOrBoolToken(int startPos, int startLine, int startCol)
    {
        while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] == '_' || source[position] == '-'))
        {
            Advance();
        }
        string val = source.Substring(startPos, position - startPos);
        if (val == "true")
        {
            return new Token(TokenType.Bool, true, startPos, startLine, startCol);
        }
        if (val == "false")
        {
            return new Token(TokenType.Bool, false, startPos, startLine, startCol);
        }
        return new Token(TokenType.Identifier, val, startPos, startLine, startCol);
    }

    private enum TokenType
    {
        Identifier,
        String,
        Char,
        Number,
        Bool,
        Comma,
        Semicolon,
        DoubleSemicolon,
        EndOfFile
    }

    private record Token(TokenType Type, object? Value, int Position, int Line, int Column);
}
