#nullable enable
using System.Collections.Generic;

namespace Showdown.Game
{
    /// <summary>
    /// String storage keyed by full key (<see cref="StorageKeys"/>). Implementations never throw: a read that
    /// fails behaves like an absent key and a write that fails is dropped, because a game that cannot save
    /// must still be playable (the same contract as <c>readJson</c>/<c>writeJson</c> in the TypeScript app).
    /// </summary>
    public interface IKeyValueStore
    {
        /// <summary>The stored text, or null when the key is absent or unreadable.</summary>
        string? Get(string key);

        void Set(string key, string value);

        void Remove(string key);
    }

    /// <summary>A dictionary-backed store: the test double, and the fallback when no persistent store is available.</summary>
    public sealed class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public string? Get(string key) => values.TryGetValue(key, out var value) ? value : null;

        public void Set(string key, string value)
        {
            values[key] = value;
        }

        public void Remove(string key)
        {
            values.Remove(key);
        }
    }
}
