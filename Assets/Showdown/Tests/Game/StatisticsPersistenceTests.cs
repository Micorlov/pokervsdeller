#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class StatisticsPersistenceTests
    {
        /// <summary>A stats blob as src/state/statsStore.ts would have written it: every field, three history entries.</summary>
        private const string TypescriptBlob =
            "{\"handsPlayed\":42,\"handsWon\":19,\"biggestWin\":135,\"reUps\":2,\"currentStreak\":1,\"bestStreak\":4,"
            + "\"bestHandCategory\":\"fullHouse\","
            + "\"categoryCounts\":{\"highCard\":5,\"pair\":17,\"twoPair\":9,\"trips\":5,\"straight\":2,\"flush\":1,\"fullHouse\":3},"
            + "\"sweeps\":3,\"kickerStrikes\":6,\"bails\":7,\"pressures\":11,\"history\":["
            + "{\"category\":\"fullHouse\",\"outcome\":\"sweep\",\"kickerStrike\":true,\"pressured\":true,\"forges\":1,\"net\":135,\"at\":1760400000000},"
            + "{\"category\":\"pair\",\"outcome\":\"bail\",\"kickerStrike\":false,\"pressured\":false,\"forges\":0,\"net\":-8,\"at\":1760399000000},"
            + "{\"category\":\"highCard\",\"outcome\":\"lose\",\"kickerStrike\":false,\"pressured\":false,\"forges\":2,\"net\":-20,\"at\":1760398000000}]}";

        private static Statistics Load(string stored)
        {
            var storage = new MemoryKeyValueStore();
            storage.Set(StorageKeys.Stats, stored);
            return new StatsStore(storage).Current;
        }

        [Test]
        public void Reads_a_realistic_blob_the_typescript_app_wrote()
        {
            var stats = Load(TypescriptBlob);

            Assert.AreEqual(42, stats.HandsPlayed);
            Assert.AreEqual(19, stats.HandsWon);
            Assert.AreEqual(135, stats.BiggestWin);
            Assert.AreEqual(2, stats.ReUps);
            Assert.AreEqual(1, stats.CurrentStreak);
            Assert.AreEqual(4, stats.BestStreak);
            Assert.AreEqual(HandCategory.FullHouse, stats.BestHandCategory);
            Assert.AreEqual(3, stats.Sweeps);
            Assert.AreEqual(6, stats.KickerStrikes);
            Assert.AreEqual(7, stats.Bails);
            Assert.AreEqual(11, stats.Pressures);
        }

        [Test]
        public void Reads_category_counts_keyed_by_category_id()
        {
            var counts = Load(TypescriptBlob).CategoryCounts;

            Assert.AreEqual(7, counts.Count);
            Assert.AreEqual(17, counts[HandCategory.Pair]);
            Assert.AreEqual(3, counts[HandCategory.FullHouse]);
            Assert.IsFalse(counts.ContainsKey(HandCategory.RoyalFlush));
        }

        [Test]
        public void Reads_history_entries_newest_first_with_every_field()
        {
            var history = Load(TypescriptBlob).History;

            Assert.AreEqual(3, history.Count);
            var first = history[0];
            Assert.AreEqual(HandCategory.FullHouse, first.Category);
            Assert.AreEqual(ShowdownOutcome.Sweep, first.Outcome);
            Assert.IsTrue(first.KickerStrike);
            Assert.IsTrue(first.Pressured);
            Assert.AreEqual(1, first.Forges);
            Assert.AreEqual(135, first.Net);
            Assert.AreEqual(1760400000000L, first.At);
            Assert.AreEqual(ShowdownOutcome.Bail, history[1].Outcome);
            Assert.AreEqual(-20, history[2].Net);
        }

        [Test]
        public void Writing_what_was_read_reproduces_the_typescript_json_byte_for_byte()
        {
            var storage = new MemoryKeyValueStore();
            storage.Set(StorageKeys.Stats, TypescriptBlob);
            var store = new StatsStore(storage);

            store.Update(stats => stats);

            Assert.AreEqual(TypescriptBlob, storage.Get(StorageKeys.Stats));
        }

        [Test]
        public void Round_trips_a_record_built_by_the_domain_reducer()
        {
            var storage = new MemoryKeyValueStore();
            var store = new StatsStore(storage);
            var entry = new HandHistoryEntry
            {
                Category = HandCategory.Flush,
                Outcome = ShowdownOutcome.BeatBoth,
                KickerStrike = true,
                Pressured = false,
                Forges = 2,
                Net = 55,
                At = 1760000000123L,
            };

            store.Update(stats => Stats.RecordHandResult(Stats.RecordHandResult(stats, entry), entry with { Net = -10, Outcome = ShowdownOutcome.Lose }));
            var reloaded = new StatsStore(storage).Current;

            Assert.AreEqual(store.Current.HandsPlayed, reloaded.HandsPlayed);
            Assert.AreEqual(2, reloaded.CategoryCounts[HandCategory.Flush]);
            CollectionAssert.AreEqual(store.Current.History, reloaded.History);
            Assert.AreEqual(HandCategory.Flush, reloaded.BestHandCategory);
            Assert.AreEqual(0, reloaded.CurrentStreak);
        }

        [Test]
        public void A_fresh_install_has_the_default_record_and_writes_null_for_no_best_hand()
        {
            var storage = new MemoryKeyValueStore();
            var store = new StatsStore(storage);
            Assert.AreEqual(0, store.Current.HandsPlayed);
            Assert.IsNull(store.Current.BestHandCategory);

            store.Update(stats => stats);

            Assert.AreEqual(
                "{\"handsPlayed\":0,\"handsWon\":0,\"biggestWin\":0,\"reUps\":0,\"currentStreak\":0,\"bestStreak\":0,"
                + "\"bestHandCategory\":null,\"categoryCounts\":{},\"sweeps\":0,\"kickerStrikes\":0,\"bails\":0,\"pressures\":0,\"history\":[]}",
                storage.Get(StorageKeys.Stats));
        }

        [Test]
        public void Translates_history_entries_written_by_the_previous_game()
        {
            var stats = Load(
                "{\"handsPlayed\":3,\"history\":["
                + "{\"category\":\"pair\",\"outcome\":\"fold\",\"net\":-5,\"at\":1700000000000},"
                + "{\"category\":\"flush\",\"outcome\":\"win\",\"net\":20,\"at\":1700000001000}]}");

            Assert.AreEqual(3, stats.HandsPlayed);
            Assert.AreEqual(ShowdownOutcome.Bail, stats.History[0].Outcome);
            Assert.AreEqual(ShowdownOutcome.BeatDealer, stats.History[1].Outcome);
        }

        [Test]
        public void Drops_unreadable_history_entries_and_keeps_the_rest()
        {
            var stats = Load(
                "{\"handsPlayed\":5,\"history\":[7,{\"category\":\"nonsense\",\"outcome\":\"lose\",\"net\":1,\"at\":2},"
                + "{\"category\":\"pair\",\"outcome\":\"lose\",\"net\":-5,\"at\":3}]}");

            Assert.AreEqual(5, stats.HandsPlayed);
            Assert.AreEqual(1, stats.History.Count);
        }

        [TestCase("not json at all")]
        [TestCase("[1,2,3]")]
        [TestCase("null")]
        [TestCase("\"string\"")]
        [TestCase("{\"handsPlayed\":")]
        public void Corrupt_data_falls_back_to_the_default_record_without_throwing(string stored)
        {
            var stats = Load(stored);

            Assert.AreEqual(0, stats.HandsPlayed);
            Assert.IsEmpty(stats.History);
        }

        [Test]
        public void Wrongly_typed_aggregates_fall_back_field_by_field()
        {
            var stats = Load("{\"handsPlayed\":\"many\",\"handsWon\":4,\"bestHandCategory\":\"splendid\"}");

            Assert.AreEqual(0, stats.HandsPlayed);
            Assert.AreEqual(4, stats.HandsWon);
            Assert.IsNull(stats.BestHandCategory);
        }

        [Test]
        public void History_is_capped_at_the_limit_when_loaded()
        {
            var entries = Enumerable.Range(0, Stats.HistoryLimit + 5)
                .Select(i => "{\"category\":\"pair\",\"outcome\":\"lose\",\"net\":-5,\"at\":" + i + "}");
            var stats = Load("{\"history\":[" + string.Join(",", entries) + "]}");

            Assert.AreEqual(Stats.HistoryLimit, stats.History.Count);
        }

        [Test]
        public void Every_category_id_survives_a_round_trip()
        {
            var counts = new Dictionary<HandCategory, int>();
            foreach (HandCategory category in System.Enum.GetValues(typeof(HandCategory)))
            {
                counts[category] = (int)category + 1;
            }

            var encoded = MiniJson.Serialize(StatisticsCodec.Encode(Statistics.Default with { CategoryCounts = counts }));
            Assert.IsTrue(MiniJson.TryParse(encoded, out var tree));
            var decoded = StatisticsCodec.Decode(tree);

            CollectionAssert.AreEquivalent(counts, decoded.CategoryCounts);
        }
    }
}
