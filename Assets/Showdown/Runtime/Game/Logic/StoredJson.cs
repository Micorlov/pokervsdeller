#nullable enable
using System.Collections.Generic;

namespace Showdown.Game
{
    /// <summary>
    /// JSON-valued storage over an <see cref="IKeyValueStore"/>: <c>readJson</c>/<c>writeJson</c> from
    /// src/storage/persistence.ts. Never throws; unreadable data reads as absent.
    /// </summary>
    public static class StoredJson
    {
        /// <summary>The parsed tree, or null when the key is absent, corrupt or holds the literal <c>null</c>.</summary>
        public static object? Read(IKeyValueStore store, string key)
        {
            var raw = store.Get(key);
            if (raw == null) return null;
            return MiniJson.TryParse(raw, out var tree) ? tree : null;
        }

        public static void Write(IKeyValueStore store, string key, object tree)
        {
            store.Set(key, MiniJson.Serialize(tree));
        }

        /// <summary>A field of a parsed object, or null when the tree is not an object or lacks the field.</summary>
        public static object? Field(object? tree, string name) =>
            tree is IDictionary<string, object?> fields && fields.TryGetValue(name, out var value) ? value : null;

        /// <summary>A whole-number field, or null when absent, not a number, or not an integer in range.</summary>
        public static int? IntField(object? tree, string name)
        {
            if (!(Field(tree, name) is double number)) return null;
            if (number != System.Math.Floor(number) || number < int.MinValue || number > int.MaxValue) return null;
            return (int)number;
        }

        public static bool? BoolField(object? tree, string name) =>
            Field(tree, name) is bool flag ? flag : (bool?)null;

        public static string? StringField(object? tree, string name) =>
            Field(tree, name) as string;
    }
}
