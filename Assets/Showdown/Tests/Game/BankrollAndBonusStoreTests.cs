#nullable enable
using System;
using NUnit.Framework;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class BankrollStoreTests
    {
        [Test]
        public void Starts_at_the_starting_bankroll()
        {
            var store = new BankrollStore(new MemoryKeyValueStore());

            Assert.IsTrue(store.IsLoaded);
            Assert.AreEqual(500, store.Value);
        }

        [Test]
        public void Reads_the_typescript_shape()
        {
            var storage = new MemoryKeyValueStore();
            storage.Set(StorageKeys.Bankroll, "{\"bankroll\":1275}");

            Assert.AreEqual(1275, new BankrollStore(storage).Value);
        }

        [TestCase("{\"bankroll\":\"lots\"}")]
        [TestCase("{\"bankroll\":-5}")]
        [TestCase("{\"bankroll\":12.5}")]
        [TestCase("{}")]
        [TestCase("garbage")]
        [TestCase("null")]
        public void Corrupt_or_unusable_values_read_as_the_starting_bankroll(string stored)
        {
            var storage = new MemoryKeyValueStore();
            storage.Set(StorageKeys.Bankroll, stored);

            Assert.AreEqual(500, new BankrollStore(storage).Value);
        }

        [Test]
        public void Save_writes_the_exact_json_and_survives_a_restart()
        {
            var storage = new MemoryKeyValueStore();
            var store = new BankrollStore(storage);

            store.Save(420);

            Assert.AreEqual("{\"bankroll\":420}", storage.Get(StorageKeys.Bankroll));
            Assert.AreEqual(420, new BankrollStore(storage).Value);
        }

        [Test]
        public void Reset_restores_and_persists_the_starting_bankroll()
        {
            var storage = new MemoryKeyValueStore();
            var store = new BankrollStore(storage);
            store.Save(35);

            store.Reset();

            Assert.AreEqual(500, store.Value);
            Assert.AreEqual("{\"bankroll\":500}", storage.Get(StorageKeys.Bankroll));
        }
    }

    [TestFixture]
    public class DailyBonusStoreTests
    {
        private MemoryKeyValueStore storage = null!;
        private DateTime now;

        [SetUp]
        public void SetUp()
        {
            storage = new MemoryKeyValueStore();
            now = new DateTime(2026, 10, 14, 9, 30, 0);
        }

        private DailyBonusStore Create() => new DailyBonusStore(storage, () => now);

        [Test]
        public void Can_claim_when_nothing_was_ever_claimed()
        {
            Assert.IsTrue(Create().CanClaim);
        }

        [Test]
        public void Marking_a_claim_blocks_the_rest_of_the_day_and_writes_the_day_key()
        {
            var store = Create();

            store.MarkClaimed();

            Assert.IsFalse(store.CanClaim);
            Assert.AreEqual("{\"lastClaimDay\":\"2026-10-14\"}", storage.Get(StorageKeys.DailyBonus));
            now = new DateTime(2026, 10, 14, 23, 59, 59);
            Assert.IsFalse(store.CanClaim);
        }

        [Test]
        public void Becomes_claimable_again_across_midnight_in_the_same_session()
        {
            var store = Create();
            store.MarkClaimed();

            now = new DateTime(2026, 10, 15, 0, 0, 1);

            Assert.IsTrue(store.CanClaim);
        }

        [Test]
        public void The_claim_survives_a_restart_on_the_same_day_only()
        {
            Create().MarkClaimed();

            Assert.IsFalse(Create().CanClaim);
            now = now.AddDays(1);
            Assert.IsTrue(Create().CanClaim);
        }

        [Test]
        public void Reads_the_typescript_shape_including_a_null_day()
        {
            storage.Set(StorageKeys.DailyBonus, "{\"lastClaimDay\":\"2026-10-14\"}");
            Assert.IsFalse(Create().CanClaim);

            storage.Set(StorageKeys.DailyBonus, "{\"lastClaimDay\":null}");
            Assert.IsTrue(Create().CanClaim);
        }

        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("{\"lastClaimDay\":20261014}")]
        public void Corrupt_data_reads_as_never_claimed(string stored)
        {
            storage.Set(StorageKeys.DailyBonus, stored);

            Assert.IsTrue(Create().CanClaim);
        }

        [Test]
        public void Changed_fires_when_a_claim_is_marked()
        {
            var store = Create();
            var seen = 0;
            store.Changed += () => seen += 1;

            store.MarkClaimed();

            Assert.AreEqual(1, seen);
        }
    }
}
