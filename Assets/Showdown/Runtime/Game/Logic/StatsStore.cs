#nullable enable
using System;
using Showdown.Domain;

namespace Showdown.Game
{
    /// <summary>Lifetime table stats — small enough to write whole on every change (src/state/statsStore.ts).</summary>
    public sealed class StatsStore
    {
        private readonly IKeyValueStore storage;

        public StatsStore(IKeyValueStore storage)
        {
            this.storage = storage;
            Load();
        }

        public Statistics Current { get; private set; } = Statistics.Default;

        /// <summary>
        /// (Re)reads storage. Stats written by older builds — including the previous game — are translated to the
        /// current shape before they touch the UI; anything unreadable reads as a fresh record.
        /// </summary>
        public void Load()
        {
            Current = StatisticsCodec.Decode(StoredJson.Read(storage, StorageKeys.Stats));
        }

        public void Update(Func<Statistics, Statistics> patch)
        {
            Current = patch(Current);
            StoredJson.Write(storage, StorageKeys.Stats, StatisticsCodec.Encode(Current));
        }
    }
}
