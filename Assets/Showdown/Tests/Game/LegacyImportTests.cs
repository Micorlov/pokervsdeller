#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class LegacyImportTests
    {
        private sealed class FakeSource : ILegacyRowSource
        {
            public IReadOnlyList<KeyValuePair<string, string>>? Rows;
            public Exception? Failure;
            public int Reads;

            public IReadOnlyList<KeyValuePair<string, string>>? ReadRows()
            {
                Reads += 1;
                if (Failure != null) throw Failure;
                return Rows;
            }
        }

        private static KeyValuePair<string, string> Row(string key, string value) => new KeyValuePair<string, string>(key, value);

        private MemoryKeyValueStore target = null!;
        private FakeSource source = null!;
        private List<string> warnings = null!;
        private LegacyImporter importer = null!;

        [SetUp]
        public void SetUp()
        {
            target = new MemoryKeyValueStore();
            source = new FakeSource();
            warnings = new List<string>();
            importer = new LegacyImporter(source, warnings.Add);
        }

        [Test]
        public void Copies_the_four_known_keys_into_an_empty_store()
        {
            source.Rows = new[]
            {
                Row(StorageKeys.Bankroll, "{\"bankroll\":725}"),
                Row(StorageKeys.Stats, "{\"handsPlayed\":9}"),
                Row(StorageKeys.Settings, "{\"soundOn\":false}"),
                Row(StorageKeys.DailyBonus, "{\"lastClaimDay\":\"2026-10-13\"}"),
            };

            var copied = importer.RunOnce(target);

            Assert.AreEqual(4, copied);
            Assert.AreEqual("{\"bankroll\":725}", target.Get(StorageKeys.Bankroll));
            Assert.AreEqual("{\"handsPlayed\":9}", target.Get(StorageKeys.Stats));
            Assert.AreEqual("{\"soundOn\":false}", target.Get(StorageKeys.Settings));
            Assert.AreEqual("{\"lastClaimDay\":\"2026-10-13\"}", target.Get(StorageKeys.DailyBonus));
            Assert.IsNotNull(target.Get(StorageKeys.LegacyImportDone));
        }

        [Test]
        public void Never_overwrites_a_key_the_new_store_already_has()
        {
            target.Set(StorageKeys.Bankroll, "{\"bankroll\":90}");
            source.Rows = new[]
            {
                Row(StorageKeys.Bankroll, "{\"bankroll\":725}"),
                Row(StorageKeys.Stats, "{\"handsPlayed\":9}"),
            };

            var copied = importer.RunOnce(target);

            Assert.AreEqual(1, copied);
            Assert.AreEqual("{\"bankroll\":90}", target.Get(StorageKeys.Bankroll));
            Assert.AreEqual("{\"handsPlayed\":9}", target.Get(StorageKeys.Stats));
        }

        [Test]
        public void Ignores_keys_outside_the_whitelist()
        {
            source.Rows = new[]
            {
                Row(StorageKeys.Namespace + ".somethingElse", "{\"a\":1}"),
                Row("other.app.bankroll", "{\"bankroll\":1}"),
                Row(StorageKeys.LegacyImportDone, "0"),
            };

            Assert.AreEqual(0, importer.RunOnce(target));
            Assert.IsNull(target.Get(StorageKeys.Namespace + ".somethingElse"));
            Assert.IsNull(target.Get("other.app.bankroll"));
            Assert.AreEqual("1", target.Get(StorageKeys.LegacyImportDone));
        }

        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("")]
        public void Skips_values_that_are_not_readable_json(string value)
        {
            source.Rows = new[] { Row(StorageKeys.Stats, value) };

            Assert.AreEqual(0, importer.RunOnce(target));
            Assert.IsNull(target.Get(StorageKeys.Stats));
        }

        [Test]
        public void Uses_the_first_row_when_a_key_appears_twice()
        {
            source.Rows = new[]
            {
                Row(StorageKeys.Bankroll, "{\"bankroll\":1}"),
                Row(StorageKeys.Bankroll, "{\"bankroll\":2}"),
            };

            Assert.AreEqual(1, importer.RunOnce(target));
            Assert.AreEqual("{\"bankroll\":1}", target.Get(StorageKeys.Bankroll));
        }

        [Test]
        public void Runs_only_once()
        {
            source.Rows = new[] { Row(StorageKeys.Bankroll, "{\"bankroll\":725}") };
            importer.RunOnce(target);
            target.Remove(StorageKeys.Bankroll);

            var second = importer.RunOnce(target);

            Assert.AreEqual(0, second);
            Assert.AreEqual(1, source.Reads);
            Assert.IsNull(target.Get(StorageKeys.Bankroll));
        }

        [Test]
        public void A_fresh_install_has_no_old_database_and_is_marked_done_without_a_warning()
        {
            source.Rows = null;

            var copied = importer.RunOnce(target);

            Assert.AreEqual(0, copied);
            Assert.IsNotNull(target.Get(StorageKeys.LegacyImportDone));
            Assert.IsEmpty(warnings);
        }

        [Test]
        public void A_read_failure_is_logged_swallowed_and_retried_next_launch()
        {
            source.Failure = new InvalidOperationException("database is locked");

            var copied = importer.RunOnce(target);

            Assert.AreEqual(0, copied);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("database is locked", warnings[0]);
            Assert.IsNull(target.Get(StorageKeys.LegacyImportDone));

            source.Failure = null;
            source.Rows = new[] { Row(StorageKeys.Bankroll, "{\"bankroll\":725}") };
            Assert.AreEqual(1, importer.RunOnce(target));
        }

        [Test]
        public void Imported_data_is_read_by_the_stores_exactly_like_native_data()
        {
            source.Rows = new[]
            {
                Row(StorageKeys.Bankroll, "{\"bankroll\":725}"),
                Row(StorageKeys.Settings, "{\"soundOn\":false,\"hapticsOn\":true,\"reducedMotion\":true,\"ante\":25}"),
            };

            importer.RunOnce(target);

            Assert.AreEqual(725, new BankrollStore(target).Value);
            Assert.IsTrue(new SettingsStore(target).Current.ReducedMotion);
        }
    }
}
