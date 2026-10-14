#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace Showdown.Game
{
    /// <summary>Compact serializer behind <see cref="MiniJson.Serialize"/>.</summary>
    internal static class MiniJsonWriter
    {
        /// <summary>Doubles below this that are whole numbers print as plain integers (no exponent, no ".0").</summary>
        private const double MaxPlainInteger = 1e15;

        public static string Serialize(object? value)
        {
            var builder = new StringBuilder();
            Write(builder, value, 0);
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, object? value, int depth)
        {
            if (depth > MiniJson.MaxDepth) throw new ArgumentException("Value nests too deeply to serialize.");

            switch (value)
            {
                case null:
                    builder.Append("null");
                    return;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    return;
                case string text:
                    WriteString(builder, text);
                    return;
                case char character:
                    WriteString(builder, character.ToString());
                    return;
                case double number:
                    WriteDouble(builder, number);
                    return;
                case float single:
                    WriteDouble(builder, single);
                    return;
                case decimal money:
                    builder.Append(money.ToString(CultureInfo.InvariantCulture));
                    return;
                case IFormattable integer when IsIntegral(value):
                    builder.Append(integer.ToString(null, CultureInfo.InvariantCulture));
                    return;
                case IDictionary dictionary:
                    WriteObject(builder, dictionary, depth);
                    return;
                case IEnumerable sequence:
                    WriteArray(builder, sequence, depth);
                    return;
                default:
                    throw new ArgumentException("Cannot serialize a value of type " + value.GetType().Name);
            }
        }

        private static bool IsIntegral(object value) =>
            value is sbyte || value is byte || value is short || value is ushort
            || value is int || value is uint || value is long || value is ulong;

        private static void WriteDouble(StringBuilder builder, double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                // JSON.stringify writes non-finite numbers as null; so do we.
                builder.Append("null");
                return;
            }

            if (Math.Floor(number) == number && Math.Abs(number) < MaxPlainInteger)
            {
                builder.Append(((long)number).ToString(CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder builder, IDictionary dictionary, int depth)
        {
            builder.Append('{');
            var first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first) builder.Append(',');
                first = false;
                WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? "");
                builder.Append(':');
                Write(builder, entry.Value, depth + 1);
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable sequence, int depth)
        {
            builder.Append('[');
            var first = true;
            foreach (var item in sequence)
            {
                if (!first) builder.Append(',');
                first = false;
                Write(builder, item, depth + 1);
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (var c in text)
            {
                AppendEscaped(builder, c);
            }

            builder.Append('"');
        }

        private static void AppendEscaped(StringBuilder builder, char c)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); return;
                case '\\': builder.Append("\\\\"); return;
                case '\b': builder.Append("\\b"); return;
                case '\f': builder.Append("\\f"); return;
                case '\n': builder.Append("\\n"); return;
                case '\r': builder.Append("\\r"); return;
                case '\t': builder.Append("\\t"); return;
            }

            if (c < ' ')
            {
                builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(c);
        }
    }
}
