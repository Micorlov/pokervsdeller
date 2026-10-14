#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Showdown.Game
{
    /// <summary>Recursive-descent parser behind <see cref="MiniJson.TryParse"/>.</summary>
    internal sealed class MiniJsonParser
    {
        private const int UnicodeEscapeLength = 4;

        private readonly string text;
        private int position;

        private MiniJsonParser(string text)
        {
            this.text = text;
        }

        public static bool TryParse(string? text, out object? value)
        {
            value = null;
            if (text == null) return false;

            var parser = new MiniJsonParser(text);
            try
            {
                var parsed = parser.ParseValue(0);
                parser.SkipWhitespace();
                if (parser.position != text.Length) return false;
                value = parsed;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static FormatException Fail(string reason) => new FormatException(reason);

        private void SkipWhitespace()
        {
            while (position < text.Length && IsWhitespace(text[position]))
            {
                position += 1;
            }
        }

        private static bool IsWhitespace(char c) => c == ' ' || c == '\t' || c == '\n' || c == '\r';

        private object? ParseValue(int depth)
        {
            if (depth > MiniJson.MaxDepth) throw Fail("nesting too deep");
            SkipWhitespace();
            if (position >= text.Length) throw Fail("unexpected end");

            switch (text[position])
            {
                case '{': return ParseObject(depth);
                case '[': return ParseArray(depth);
                case '"': return ParseString();
                case 't': ExpectWord("true"); return true;
                case 'f': ExpectWord("false"); return false;
                case 'n': ExpectWord("null"); return null;
                default: return ParseNumber();
            }
        }

        private void ExpectWord(string word)
        {
            if (string.CompareOrdinal(text, position, word, 0, word.Length) != 0) throw Fail("bad literal");
            position += word.Length;
        }

        private Dictionary<string, object?> ParseObject(int depth)
        {
            var result = new Dictionary<string, object?>();
            position += 1;
            SkipWhitespace();
            if (TryConsume('}')) return result;

            while (true)
            {
                SkipWhitespace();
                if (position >= text.Length || text[position] != '"') throw Fail("object key expected");
                var key = ParseString();
                SkipWhitespace();
                if (!TryConsume(':')) throw Fail("':' expected");
                result[key] = ParseValue(depth + 1);
                SkipWhitespace();
                if (TryConsume('}')) return result;
                if (!TryConsume(',')) throw Fail("',' or '}' expected");
            }
        }

        private List<object?> ParseArray(int depth)
        {
            var result = new List<object?>();
            position += 1;
            SkipWhitespace();
            if (TryConsume(']')) return result;

            while (true)
            {
                result.Add(ParseValue(depth + 1));
                SkipWhitespace();
                if (TryConsume(']')) return result;
                if (!TryConsume(',')) throw Fail("',' or ']' expected");
            }
        }

        private bool TryConsume(char expected)
        {
            if (position >= text.Length || text[position] != expected) return false;
            position += 1;
            return true;
        }

        private string ParseString()
        {
            position += 1;
            var builder = new StringBuilder();
            while (position < text.Length)
            {
                var c = text[position];
                position += 1;
                if (c == '"') return builder.ToString();
                if (c < ' ') throw Fail("control character in string");
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                builder.Append(ParseEscape());
            }

            throw Fail("unterminated string");
        }

        private char ParseEscape()
        {
            if (position >= text.Length) throw Fail("unterminated escape");
            var c = text[position];
            position += 1;
            switch (c)
            {
                case '"': return '"';
                case '\\': return '\\';
                case '/': return '/';
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'u': return ParseUnicodeEscape();
                default: throw Fail("bad escape");
            }
        }

        private char ParseUnicodeEscape()
        {
            if (position + UnicodeEscapeLength > text.Length) throw Fail("short unicode escape");
            var digits = text.Substring(position, UnicodeEscapeLength);
            // AllowHexSpecifier alone rejects signs and whitespace, so only four hex digits get through.
            if (!int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
            {
                throw Fail("bad unicode escape");
            }

            position += UnicodeEscapeLength;
            return (char)code;
        }

        private double ParseNumber()
        {
            var start = position;
            TryConsume('-');
            if (TryConsume('0'))
            {
                // A leading zero may not be followed by more digits.
            }
            else if (ConsumeDigits() == 0)
            {
                throw Fail("number expected");
            }

            if (TryConsume('.') && ConsumeDigits() == 0) throw Fail("digits expected after '.'");

            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                position += 1;
                if (!TryConsume('+')) TryConsume('-');
                if (ConsumeDigits() == 0) throw Fail("digits expected in exponent");
            }

            return ToDouble(text.Substring(start, position - start));
        }

        private int ConsumeDigits()
        {
            var count = 0;
            while (position < text.Length && text[position] >= '0' && text[position] <= '9')
            {
                position += 1;
                count += 1;
            }

            return count;
        }

        private static double ToDouble(string literal)
        {
            if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                || double.IsInfinity(number) || double.IsNaN(number))
            {
                throw Fail("number out of range");
            }

            return number;
        }
    }
}
