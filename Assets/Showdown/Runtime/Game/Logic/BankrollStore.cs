#nullable enable
using System.Collections.Generic;
using Showdown.Domain;

namespace Showdown.Game
{
    /// <summary>
    /// The one number that survives between sessions: the player's chips (<c>{"bankroll":500}</c>). Bots rebuy
    /// silently and the dealer is a bottomless house, so nothing else needs to persist (src/state/bankrollStore.ts).
    /// </summary>
    public sealed class BankrollStore
    {
        private readonly IKeyValueStore storage;

        public BankrollStore(IKeyValueStore storage)
        {
            this.storage = storage;
            Load();
        }

        public int Value { get; private set; } = Domain.Bankroll.StartingBankroll;

        public bool IsLoaded { get; private set; }

        /// <summary>(Re)reads storage. A missing, corrupt or negative value reads as the starting bankroll.</summary>
        public void Load()
        {
            var stored = StoredJson.IntField(StoredJson.Read(storage, StorageKeys.Bankroll), "bankroll");
            Value = stored.HasValue && stored.Value >= 0 ? stored.Value : Domain.Bankroll.StartingBankroll;
            IsLoaded = true;
        }

        public void Save(int value)
        {
            Value = value;
            StoredJson.Write(storage, StorageKeys.Bankroll, new Dictionary<string, object?> { { "bankroll", value } });
        }

        public void Reset() => Save(Domain.Bankroll.StartingBankroll);
    }
}
