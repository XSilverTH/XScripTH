using System.Globalization;
using Pidgin;
using XScripTH.Language.Ast;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace XScripTH.Language.Parsing;

public sealed class XScriptParser
{
    public static XScriptProgramAst Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = ParserGrammar.Program.Parse(source);
        if (result.Success) return result.Value;
        var error = result.Error!;
        var pos = error.ErrorPos;
        throw new XScriptParseException(
            error.RenderErrorMessage(),
            0,
            pos.Line,
            pos.Col);
    }

    private static class ParserGrammar
    {
        private static readonly Parser<char, Unit> SkipWs = Whitespace.SkipMany();

        private static Parser<char, T> Tok<T>(Parser<char, T> p) => p.Before(SkipWs);
        private static Parser<char, char> Tok(char c) => Char(c).Before(SkipWs);
        private static Parser<char, string> Tok(string s) => String(s).Before(SkipWs);

        private static void ThrowException(string message, SourcePos pos)
        {
            throw new XScriptParseException(message, 0, pos.Line, pos.Col);
        }

        private static Parser<char, T> ThrowParseError<T>(string message) =>
            CurrentPos.Bind(pos =>
            {
                ThrowException(message, pos);
                return Return(default(T)!);
            });

        // Terminals
        private static readonly Parser<char, XScriptCommandTerminator> Terminator =
            Try(Tok(";;")).Map(_ => XScriptCommandTerminator.FireAndForget)
                .Or(Tok(';').Map(_ => XScriptCommandTerminator.Await));

        private static readonly Parser<char, string> IdentifierStr =
            Map((first, rest) => first + rest,
                Letter.Or(Char('_')).Or(Char('-')),
                LetterOrDigit.Or(Char('_')).Or(Char('-')).ManyString()
            );

        private static readonly Parser<char, string> Identifier = Tok(IdentifierStr);

        // Keywords
        private static readonly Parser<char, bool> BoolTrue = String("true").Map(_ => true);
        private static readonly Parser<char, bool> BoolFalse = String("false").Map(_ => false);

        private static readonly Parser<char, bool> BooleanToken =
            Try(BoolTrue.Or(BoolFalse).Before(Not(LetterOrDigit.Or(Char('_')).Or(Char('-'))))).Before(SkipWs);

        private static readonly Parser<char, string> VariableName =
            Try(Char('$')).Then(IdentifierStr).Before(SkipWs);

        private static readonly Parser<char, string> FunctionRefName =
            Try(Char('@')).Then(IdentifierStr).Before(SkipWs);

        // String
        private static readonly Parser<char, char> StringEscape =
            Char('\\').Then(
                Char('\\').Map(_ => '\\')
                    .Or(Char('"').Map(_ => '"'))
                    .Or(Char('n').Map(_ => '\n'))
                    .Or(Char('r').Map(_ => '\r'))
                    .Or(Char('t').Map(_ => '\t'))
                    .Or(Char('0').Map(_ => '\0'))
            );

        private static readonly Parser<char, string> StringLiteral =
            Try(Tok(
                Char('"').Then(AnyCharExcept('"', '\\').Or(StringEscape).ManyString()).Before(Char('"'))
            ));

        // Char
        private static readonly Parser<char, char> CharEscape =
            Char('\\').Then(
                Char('\\').Map(_ => '\\')
                    .Or(Char('\'').Map(_ => '\''))
                    .Or(Char('"').Map(_ => '"'))
                    .Or(Char('n').Map(_ => '\n'))
                    .Or(Char('r').Map(_ => '\r'))
                    .Or(Char('t').Map(_ => '\t'))
                    .Or(Char('0').Map(_ => '\0'))
            );

        private static readonly Parser<char, char> CharLiteral =
            Try(Tok(
                Char('\'').Then(AnyCharExcept('\'', '\\').Or(CharEscape)).Before(Char('\''))
            ));

        // Numbers
        private static readonly Parser<char, object> NumberLiteral =
            CurrentPos.Bind(pos =>
                Try(Tok(
                    Map((sign, intPart, fracPart, suffix) =>
                        {
                            var isNegative = sign is { HasValue: true, Value: '-' };
                            var numStr = (isNegative ? "-" : "") + intPart + fracPart.GetValueOrDefault("");
                            var suf = suffix.GetValueOrDefault("").ToLowerInvariant();
                            var hasDecimal = fracPart.HasValue;

                            if (hasDecimal)
                            {
                                if (string.IsNullOrEmpty(suf))
                                    throw new XScriptParseException(
                                        $"Decimal-point numeric literal '{numStr}' requires a suffix (f, d, or m)", 0,
                                        pos.Line, pos.Col);
                                if (suf != "f" && suf != "d" && suf != "m")
                                    throw new XScriptParseException(
                                        $"Integral suffix '{suf}' is not allowed on decimal-point literal '{numStr}{suf}'",
                                        0, pos.Line, pos.Col);
                            }
                            else
                            {
                                var validSuffixes = new HashSet<string>
                                    { "", "i", "l", "s", "b", "sb", "u", "ul", "us", "f", "d", "m" };
                                if (!validSuffixes.Contains(suf))
                                    throw new XScriptParseException(
                                        $"Unknown suffix '{suf}' on numeric literal '{numStr}{suf}'", 0, pos.Line,
                                        pos.Col);
                            }

                            try
                            {
                                return suf switch
                                {
                                    "" or "i" => (object)int.Parse(numStr, CultureInfo.InvariantCulture),
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
                                    _ => throw new XScriptParseException($"Unknown suffix '{suf}'", 0, pos.Line,
                                        pos.Col)
                                };
                            }
                            catch (OverflowException)
                            {
                                throw new XScriptParseException($"Numeric literal '{numStr}{suf}' overflowed", 0,
                                    pos.Line, pos.Col);
                            }
                            catch (FormatException ex)
                            {
                                throw new XScriptParseException($"Invalid numeric literal '{numStr}{suf}'.", 0,
                                    pos.Line, pos.Col, ex);
                            }
                        },
                        Char('-').Optional(),
                        Digit.AtLeastOnceString(),
                        Char('.').Then(Digit.AtLeastOnceString()).Map(d => "." + d).Optional(),
                        Letter.ManyString().Optional()
                    )
                ))
            );

        private static readonly Parser<char, XScriptCommandAst> TopCommand;
        private static readonly Parser<char, XScriptArgumentAst> Argument;

        private static readonly Parser<char, Unit> NestedTerminator =
            CurrentPos.Bind(pos =>
                Try(String(";;")).Bind(_ =>
                        ThrowParseError<Unit>(
                            $"Nested command arguments must use ';'. Unexpected double semicolon ';;' at {pos.Line}:{pos.Col}."))
                    .Or(
                        Try(
                            Char(';').Before(SkipWs).Bind(_ =>
                                Lookahead(Any).Optional().Bind(next =>
                                    (next.HasValue && (next.Value == ',' || next.Value == ';'))
                                        ? Return(Unit.Value)
                                        : Parser<char>.Fail<Unit>("Don't consume")
                                )
                            )
                        )
                    )
                    .Or(Lookahead(Char(';')).Map(_ => Unit.Value))
            );

        static ParserGrammar()
        {
            var argumentParser = Rec(() => Argument!);
            var topCommandParser = Rec(() => TopCommand!);

            var argumentsParser = argumentParser.Separated(Tok(','));

            var nestedCommand = Map(XScriptArgumentAst (name, args, _) =>
                    new XScriptCommandArgumentAst(new XScriptCommandAst(name, args.ToList(),
                        XScriptCommandTerminator.Await)),
                Identifier,
                argumentsParser,
                NestedTerminator
            );

            var block = Map(XScriptArgumentAst (cmds) => new XScriptBlockArgumentAst(cmds.ToList()),
                Tok('{').Then(topCommandParser.Many()).Before(Tok('}'))
            );

            var literalArg =
                Try(StringLiteral).Map<XScriptArgumentAst>(v => new XScriptLiteralArgumentAst(v))
                    .Or(Try(CharLiteral).Map<XScriptArgumentAst>(v => new XScriptLiteralArgumentAst(v)))
                    .Or(Try(NumberLiteral).Map<XScriptArgumentAst>(v => new XScriptLiteralArgumentAst(v)))
                    .Or(Try(BooleanToken).Map<XScriptArgumentAst>(v => new XScriptLiteralArgumentAst(v)));

            var varArg = VariableName.Map<XScriptArgumentAst>(v => new XScriptVariableArgumentAst(v));
            var funcRefArg = FunctionRefName.Map<XScriptArgumentAst>(v => new XScriptFunctionReferenceArgumentAst(v));

            Argument = literalArg
                .Or(varArg)
                .Or(funcRefArg)
                .Or(block)
                .Or(nestedCommand);

            TopCommand = Map((name, args, term) => new XScriptCommandAst(name, args.ToList(), term),
                Identifier,
                argumentsParser,
                Terminator
            );

            Program = SkipWs.Then(TopCommand.Many()).Before(End).Map(cmds => new XScriptProgramAst(cmds.ToList()));
        }

        public static readonly Parser<char, XScriptProgramAst> Program;
    }
}