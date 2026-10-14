#nullable enable
using NUnit.Framework;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class SettingsStoreTests
    {
        private MemoryKeyValueStore storage = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new MemoryKeyValueStore();
        }

        [Test]
        public void Starts_with_the_defaults_when_nothing_is_stored()
        {
            var store = new SettingsStore(storage);

            Assert.IsTrue(store.IsLoaded);
            Assert.AreEqual(new AppSettings(), store.Current);
            Assert.IsTrue(store.Current.SoundOn);
            Assert.IsTrue(store.Current.HapticsOn);
            Assert.IsFalse(store.Current.ReducedMotion);
            Assert.AreEqual(10, store.Current.Ante);
        }

        [Test]
        public void Reads_the_shape_the_typescript_app_wrote()
        {
            storage.Set(StorageKeys.Settings, "{\"soundOn\":false,\"hapticsOn\":false,\"reducedMotion\":true,\"ante\":25}");

            var store = new SettingsStore(storage);

            Assert.AreEqual(new AppSettings { SoundOn = false, HapticsOn = false, ReducedMotion = true, Ante = 25 }, store.Current);
        }

        [Test]
        public void Missing_keys_keep_their_defaults_like_a_spread_over_the_defaults()
        {
            storage.Set(StorageKeys.Settings, "{\"soundOn\":false}");

            var store = new SettingsStore(storage);

            Assert.IsFalse(store.Current.SoundOn);
            Assert.IsTrue(store.Current.HapticsOn);
            Assert.AreEqual(10, store.Current.Ante);
        }

        [TestCase("{\"soundOn\":\"yes\",\"ante\":\"10\"}")]
        [TestCase("{\"ante\":7}")]
        [TestCase("{\"ante\":12.5}")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{{{ not json")]
        [TestCase("")]
        public void Wrong_types_and_corrupt_data_fall_back_to_the_defaults(string stored)
        {
            storage.Set(StorageKeys.Settings, stored);

            var store = new SettingsStore(storage);

            Assert.AreEqual(new AppSettings(), store.Current);
            Assert.IsTrue(store.IsLoaded);
        }

        [Test]
        public void Update_persists_the_exact_json_shape()
        {
            var store = new SettingsStore(storage);

            store.Update(current => current with { SoundOn = false, Ante = 5 });

            Assert.AreEqual("{\"soundOn\":false,\"hapticsOn\":true,\"reducedMotion\":false,\"ante\":5}", storage.Get(StorageKeys.Settings));
            Assert.AreEqual(new AppSettings { SoundOn = false, Ante = 5 }, new SettingsStore(storage).Current);
        }

        [Test]
        public void Changed_fires_after_every_update_and_after_a_reload()
        {
            var store = new SettingsStore(storage);
            var seen = 0;
            AppSettings? last = null;
            store.Changed += () =>
            {
                seen += 1;
                last = store.Current;
            };

            store.Update(current => current with { ReducedMotion = true });
            Assert.AreEqual(1, seen);
            Assert.IsTrue(last!.ReducedMotion);

            storage.Set(StorageKeys.Settings, "{\"reducedMotion\":false}");
            store.Load();
            Assert.AreEqual(2, seen);
            Assert.IsFalse(last!.ReducedMotion);
        }

        [Test]
        public void Update_ignores_a_patch_that_returns_nothing()
        {
            var store = new SettingsStore(storage);
            store.Update(current => current with { Ante = 25 });

            store.Update(_ => null!);

            Assert.AreEqual(25, store.Current.Ante);
        }
    }
}
