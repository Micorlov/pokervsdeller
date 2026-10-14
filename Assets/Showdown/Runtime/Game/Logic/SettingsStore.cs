#nullable enable
using System;

namespace Showdown.Game
{
    /// <summary>Player preferences, persisted on every change (src/state/settingsStore.ts).</summary>
    public sealed class SettingsStore : ISettingsStore
    {
        private readonly IKeyValueStore storage;

        public SettingsStore(IKeyValueStore storage)
        {
            this.storage = storage;
            Load();
        }

        public AppSettings Current { get; private set; } = new AppSettings();

        public bool IsLoaded { get; private set; }

        public event Action? Changed;

        /// <summary>
        /// (Re)reads storage and announces the result. The constructor calls this once; call it again after
        /// something else wrote the key (the legacy import). Corrupt or missing data yields the defaults.
        /// </summary>
        public void Load()
        {
            Current = SettingsCodec.Decode(StoredJson.Read(storage, StorageKeys.Settings));
            IsLoaded = true;
            Changed?.Invoke();
        }

        public void Update(Func<AppSettings, AppSettings> patch)
        {
            var next = patch(Current);
            if (next == null) return;

            Current = next;
            StoredJson.Write(storage, StorageKeys.Settings, SettingsCodec.Encode(next));
            Changed?.Invoke();
        }
    }
}
