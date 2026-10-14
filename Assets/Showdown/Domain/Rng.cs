#nullable enable
using System;
using System.Collections.Generic;

namespace Showdown.Domain
{
    /// <summary>
    /// Seeded randomness.
    ///
    /// Every shuffle and every bot decision draws from an injected generator
    /// (a <c>Func&lt;double&gt;</c> returning [0, 1)) rather than a global random source,
    /// so a round can be replayed exactly from its seed — which is what makes the
    /// engine testable at all.
    /// </summary>
    public static class Rng
    {
        /// <summary>
        /// mulberry32: small, fast, and good enough for dealing cards. Bit-for-bit the
        /// same sequence as the TypeScript original (Math.imul is an unchecked 32-bit
        /// multiply, &gt;&gt;&gt; a logical shift, state a uint).
        /// </summary>
        public static Func<double> Mulberry32(uint seed)
        {
            var state = seed;
            return () =>
            {
                unchecked
                {
                    state += 0x6d2b79f5u;
                    var t = state;
                    t = (t ^ (t >> 15)) * (t | 1u);
                    t ^= t + (t ^ (t >> 7)) * (t | 61u);
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            };
        }

        /// <summary>Same as the uint overload; a negative seed wraps like the original's <c>seed &gt;&gt;&gt; 0</c>.</summary>
        public static Func<double> Mulberry32(int seed) => Mulberry32(unchecked((uint)seed));

        /// <summary>Fisher-Yates. Returns a new list — the input is never touched.</summary>
        public static IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> items, Func<double> rng)
        {
            var output = new T[items.Count];
            for (var i = 0; i < output.Length; i += 1)
            {
                output[i] = items[i];
            }

            for (var i = output.Length - 1; i > 0; i -= 1)
            {
                var j = (int)Math.Floor(rng() * (i + 1));
                var swap = output[i];
                output[i] = output[j];
                output[j] = swap;
            }

            return output;
        }

        /// <summary>
        /// A fresh seed for a real session, where replayability is not wanted. Callers
        /// that want determinism simply pass their own seed to <see cref="Mulberry32(uint)"/>.
        /// </summary>
        public static uint RandomSeed() => unchecked((uint)Guid.NewGuid().GetHashCode());
    }
}
