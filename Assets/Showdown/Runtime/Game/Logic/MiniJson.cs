#nullable enable
namespace Showdown.Game
{
    /// <summary>
    /// A small JSON reader/writer. No JSON package is installed and <c>JsonUtility</c> cannot do dictionaries,
    /// nested arrays or loose shapes, which is what the TypeScript app's stored values need.
    ///
    /// Parsed trees use only: <c>Dictionary&lt;string, object?&gt;</c> (objects, insertion-ordered),
    /// <c>List&lt;object?&gt;</c> (arrays), <c>double</c>, <c>string</c>, <c>bool</c> and <c>null</c> — the shapes
    /// <c>Stats.MigrateStatistics</c> accepts.
    /// </summary>
    public static class MiniJson
    {
        /// <summary>Nesting beyond this is treated as corrupt data rather than risking a stack overflow.</summary>
        public const int MaxDepth = 64;

        /// <summary>
        /// Parses strict JSON. Returns false (never throws) for anything malformed, including trailing text.
        /// A document that is the literal <c>null</c> parses successfully to a null <paramref name="value"/>.
        /// </summary>
        public static bool TryParse(string? text, out object? value)
        {
            return MiniJsonParser.TryParse(text, out value);
        }

        /// <summary>
        /// Writes a tree compactly, like <c>JSON.stringify</c>. Accepts the parser's shapes plus any CLR numeric
        /// type, any <c>IDictionary</c> and any <c>IEnumerable</c>. Throws <see cref="System.ArgumentException"/>
        /// for a value it cannot represent: that is a programming error in the caller, not bad stored data.
        /// </summary>
        public static string Serialize(object? value)
        {
            return MiniJsonWriter.Serialize(value);
        }
    }
}
