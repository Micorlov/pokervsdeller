#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    /// <summary>Records every cue the table plays, in order, with the scheduler time it played at.</summary>
    internal sealed class RecordingFeedback : IFeedback
    {
        public readonly List<Cue> Played = new List<Cue>();
        public readonly List<double> PlayedAt = new List<double>();
        public Func<double> Clock = () => 0.0;

        public void Play(Cue cue)
        {
            Played.Add(cue);
            PlayedAt.Add(Clock());
        }

        /// <summary>The times a cue played at.</summary>
        public IReadOnlyList<double> TimesOf(Cue cue) =>
            Played.Select((played, index) => (played, index)).Where(pair => pair.played == cue).Select(pair => PlayedAt[pair.index]).ToArray();
    }

    /// <summary>One announced change: when it happened and what the table looked like.</summary>
    internal sealed class ChangeRecord
    {
        public ChangeRecord(double time, ShowdownPhase? phase, int dealerCards)
        {
            Time = time;
            Phase = phase;
            DealerCards = dealerCards;
        }

        public double Time { get; }
        public ShowdownPhase? Phase { get; }
        public int DealerCards { get; }
    }

    /// <summary>
    /// A table controller wired to in-memory storage, a manual clock and a recording feedback, plus a driver that
    /// plays hands by advancing that clock — the whole game layer with no Unity.
    /// </summary>
    internal sealed class TableRig
    {
        public const long FixedEpochMs = 1_760_000_000_000L;
        private const float StepSeconds = 0.01f;
        private const float DefaultTimeoutSeconds = 60f;

        public readonly MemoryKeyValueStore Storage;
        public readonly ManualScheduler Scheduler = new ManualScheduler();
        public readonly RecordingFeedback Feedback = new RecordingFeedback();
        public readonly BankrollStore BankrollStore;
        public readonly StatsStore StatsStore;
        public readonly TableController Table;
        public readonly List<ChangeRecord> Changes = new List<ChangeRecord>();
        public int ChangedCount;
        private object lastAnnounced;

        public TableRig(MemoryKeyValueStore? storage = null, Func<uint>? seeds = null)
        {
            Storage = storage ?? new MemoryKeyValueStore();
            BankrollStore = new BankrollStore(Storage);
            StatsStore = new StatsStore(Storage);
            var nextSeed = 1000u;
            Table = new TableController(BankrollStore, StatsStore, Feedback, Scheduler, seeds ?? (() => nextSeed++), () => FixedEpochMs);
            Feedback.Clock = () => Scheduler.Now;
            lastAnnounced = Snapshot();
            Table.Changed += () =>
            {
                ChangedCount += 1;
                Changes.Add(new ChangeRecord(Scheduler.Now, Table.Round?.Phase, Table.Round?.DealerCards.Count ?? -1));
                lastAnnounced = Snapshot();
            };
        }

        /// <summary>Every observable property of the controller, for comparing against what was last announced.</summary>
        private object Snapshot() => (
            Table.Round, Table.ThinkingSeat, Table.KickerLocked, Table.ForgeArming, Table.FreshCardIndex,
            Table.PressureArmed, Table.Revealed, Table.NewAchievements.Count, Table.StreakBonus, Table.Bankroll, Table.Stats);

        /// <summary>Fails if anything observable changed since the last <c>Changed</c> — the UI would never have redrawn for it.</summary>
        public void AssertAnnounced()
        {
            Assert.AreEqual(lastAnnounced, Snapshot(), "state changed without a Changed event");
        }

        /// <summary>The phases announced so far, consecutive repeats collapsed.</summary>
        public List<ShowdownPhase?> PhaseSequence()
        {
            var sequence = new List<ShowdownPhase?>();
            foreach (var change in Changes)
            {
                if (sequence.Count == 0 || sequence[sequence.Count - 1] != change.Phase) sequence.Add(change.Phase);
            }

            return sequence;
        }

        public static TableRig WithBankroll(int bankroll, Statistics? stats = null, uint? pinnedSeed = null)
        {
            var storage = new MemoryKeyValueStore();
            storage.Set(StorageKeys.Bankroll, "{\"bankroll\":" + bankroll + "}");
            if (stats != null) StoredJson.Write(storage, StorageKeys.Stats, StatisticsCodec.Encode(stats));
            var rig = new TableRig(storage);
            if (pinnedSeed.HasValue) rig.Table.PinNextSeed(pinnedSeed.Value);
            return rig;
        }

        public ShowdownRoundState Round => Table.Round!;

        public ShowdownSeatState Human => ShowdownRound.HumanSeat(Round);

        public void Advance(float seconds)
        {
            Scheduler.Advance(seconds);
        }

        /// <summary>Steps the clock in small increments until the condition holds, failing the test if it never does.</summary>
        public void RunUntil(Func<bool> condition, string what, float timeoutSeconds = DefaultTimeoutSeconds)
        {
            for (var elapsed = 0f; elapsed < timeoutSeconds; elapsed += StepSeconds)
            {
                AssertAnnounced();
                if (condition()) return;
                Scheduler.Advance(StepSeconds);
            }

            Assert.Fail("Timed out waiting for: " + what);
        }

        public bool HumanToMove(int dealerCards) =>
            Table.Round != null
            && Round.Phase == ShowdownPhase.Actions
            && Round.DealerCards.Count == dealerCards
            && Human.Status == SeatStatus.Active
            && Human.LastMove == null;

        public void DealToKickerPick()
        {
            RunUntil(() => Table.Round?.Phase == ShowdownPhase.Kicker, "the deal to settle into the kicker pick");
        }

        public void LockKicker(int cardIndex = 0)
        {
            Table.SelectKicker(cardIndex);
            Table.LockKicker();
        }

        public void RunToHumanTurn(int dealerCards)
        {
            RunUntil(() => HumanToMove(dealerCards), "the human's turn after dealer card " + dealerCards);
        }

        /// <summary>Starts a hand at the stored bankroll and plays to the human's first decision.</summary>
        public void StartAndReachFirstDecision(int ante = 10)
        {
            Table.StartHand(ante);
            DealToKickerPick();
            LockKicker();
            RunToHumanTurn(1);
        }

        /// <summary>Plays the whole hand out: kicker 0, hold every round, until the chips have moved.</summary>
        public void PlayOutHolding()
        {
            RunUntil(() =>
            {
                StepHoldingPolicy();
                return Table.Round?.Phase == ShowdownPhase.Settled;
            }, "the hand to settle");
        }

        private void StepHoldingPolicy()
        {
            var round = Table.Round;
            if (round == null) return;
            if (round.Phase == ShowdownPhase.Kicker && !Table.KickerLocked) LockKicker();
            if (HumanToMove(round.DealerCards.Count)) Table.Hold();
        }

        /// <summary>Plays one whole hand from the given seed with the holding policy.</summary>
        public static TableRig PlayHand(uint seed, int bankroll, int ante, Statistics? stats = null)
        {
            var rig = WithBankroll(bankroll, stats, seed);
            rig.Table.StartHand(ante);
            rig.PlayOutHolding();
            return rig;
        }

        /// <summary>The first seed in 1..limit whose holding-policy hand satisfies the predicate. Fails the test if none does.</summary>
        public static uint FindSeed(int bankroll, int ante, Statistics? stats, Func<TableRig, bool> predicate, uint limit = 400)
        {
            for (var seed = 1u; seed <= limit; seed += 1)
            {
                if (predicate(PlayHand(seed, bankroll, ante, stats))) return seed;
            }

            Assert.Fail("No seed up to " + limit + " produced the wanted hand.");
            return 0;
        }

        public int CountCues(Cue cue) => Feedback.Played.Count(played => played == cue);
    }
}
