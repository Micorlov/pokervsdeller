#nullable enable
using System;
using System.Collections.Generic;
using Showdown.Domain;

namespace Showdown.Game
{
    /// <summary>
    /// Remembers the calendar day of the last claim (<c>{"lastClaimDay":"2026-10-14"}</c>). Eligibility is derived
    /// at read time so a session left open overnight becomes claimable again without a restart.
    /// </summary>
    public sealed class DailyBonusStore : IDailyBonusStore
    {
        private readonly IKeyValueStore storage;
        private readonly Func<DateTime> clock;
        private string? lastClaimDay;
        private bool isLoaded;

        /// <param name="clock">The local date and time; injected so tests can cross midnight.</param>
        public DailyBonusStore(IKeyValueStore storage, Func<DateTime> clock)
        {
            this.storage = storage;
            this.clock = clock;
            Load();
        }

        /// <summary>
        /// Only claimable once storage has answered — a flash of "claim me" that disappears when the stored day
        /// loads would read as a broken button.
        /// </summary>
        public bool CanClaim => isLoaded && DailyBonus.CanClaimDailyBonus(lastClaimDay, clock());

        public event Action? Changed;

        /// <summary>(Re)reads storage and announces the result; see <see cref="SettingsStore.Load"/>.</summary>
        public void Load()
        {
            lastClaimDay = StoredJson.StringField(StoredJson.Read(storage, StorageKeys.DailyBonus), "lastClaimDay");
            isLoaded = true;
            Changed?.Invoke();
        }

        public void MarkClaimed()
        {
            lastClaimDay = DailyBonus.DayKeyOf(clock());
            StoredJson.Write(storage, StorageKeys.DailyBonus, new Dictionary<string, object?> { { "lastClaimDay", lastClaimDay } });
            Changed?.Invoke();
        }
    }
}
