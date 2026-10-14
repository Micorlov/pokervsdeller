#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Showdown.Domain;
using DomainBankroll = Showdown.Domain.Bankroll;

namespace Showdown.Game
{
    /// <summary>
    /// Owns the table and the clock — a port of <c>useShowdownTable</c> (src/state/useShowdownTable.ts). The domain
    /// settles a hand instantly; this spreads that instant across the beats the eye needs — deal, pick a kicker,
    /// watch the climb, act, reveal, pay — and fires the matching cue at each one.
    ///
    /// <see cref="Changed"/> plays the role of React's re-render: it fires once after every public call or timer
    /// beat that changed anything observable, never in the middle of one. The controller itself is stateful, but
    /// never mutates a domain record — every change replaces the round with a new value.
    ///
    /// Everything it touches is injected (stores, feedback, clock, seeds), so whole hands run without Unity.
    /// This file holds the state and the player's inputs; the beats live in <c>TableController.Beats.cs</c>.
    /// </summary>
    public sealed partial class TableController : ITableController, IDisposable
    {
        private const string HumanName = "You";
        private const int PressureArmedRound = ShowdownPayouts.PressureRound;

        private readonly BankrollStore bankrollStore;
        private readonly StatsStore statsStore;
        private readonly IFeedback feedback;
        private readonly IScheduler scheduler;
        private readonly Func<uint> seedSource;
        private readonly Func<long> epochMilliseconds;

        private ShowdownRoundState? round;
        private int? thinkingSeat;
        private bool kickerLocked;
        private bool forgeArming;
        private int? freshCardIndex;
        private bool pressureArmed;
        private bool revealed;
        private IReadOnlyList<Achievement> newAchievements = Array.Empty<Achievement>();
        private StreakPayout? streakBonus;

        /// <summary>The hand's own generator: bot decisions draw from it as well as the shuffle, so a seed replays a whole hand.</summary>
        private Func<double> rng = Rng.Mulberry32(0u);
        private uint? pinnedSeed;
        /// <summary>The settle beats are on the clock; a hand schedules them once.</summary>
        private bool settleScheduled;
        /// <summary>The highest dealer card already on the clock, so a round schedules one draw.</summary>
        private int drawScheduledFor;

        private int batchDepth;
        private bool dirty;

        /// <param name="seedSource">Seeds each hand's generator; defaults to <see cref="Rng.RandomSeed"/>.</param>
        /// <param name="epochMilliseconds">Stamps history entries; defaults to the system clock.</param>
        public TableController(
            BankrollStore bankrollStore,
            StatsStore statsStore,
            IFeedback feedback,
            IScheduler scheduler,
            Func<uint>? seedSource = null,
            Func<long>? epochMilliseconds = null)
        {
            this.bankrollStore = bankrollStore;
            this.statsStore = statsStore;
            this.feedback = feedback;
            this.scheduler = scheduler;
            this.seedSource = seedSource ?? Rng.RandomSeed;
            this.epochMilliseconds = epochMilliseconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public event Action? Changed;

        public ShowdownRoundState? Round => round;

        public int? ThinkingSeat => thinkingSeat;

        public bool KickerLocked => kickerLocked;

        public bool ForgeArming => forgeArming;

        public int? FreshCardIndex => freshCardIndex;

        public bool PressureArmed => pressureArmed;

        public bool Revealed => revealed;

        public IReadOnlyList<Achievement> NewAchievements => newAchievements;

        public StreakPayout? StreakBonus => streakBonus;

        /// <summary>During a hand the seat's stack IS the bankroll — no second ledger.</summary>
        public int Bankroll => round != null ? ShowdownRound.HumanSeat(round).Stack : bankrollStore.Value;

        public bool BankrollLoaded => bankrollStore.IsLoaded;

        public Statistics Stats => statsStore.Current;

        /// <summary>Abandons every pending beat (the hook's unmount cleanup).</summary>
        public void Dispose()
        {
            scheduler.CancelAll();
        }

        // ---- Change tracking -------------------------------------------------------------------

        /// <summary>Runs one entry point and announces the net result once, after it has finished.</summary>
        private void Batch(Action body)
        {
            batchDepth += 1;
            try
            {
                body();
            }
            finally
            {
                batchDepth -= 1;
                if (batchDepth == 0 && dirty)
                {
                    dirty = false;
                    Changed?.Invoke();
                }
            }
        }

        private void MarkChanged()
        {
            dirty = true;
        }

        /// <summary>A state setter that, like React's, ignores a write of the value already there.</summary>
        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            MarkChanged();
        }

        private void SetNewAchievements(IReadOnlyList<Achievement> value)
        {
            if (newAchievements.Count == 0 && value.Count == 0) return;
            newAchievements = value;
            MarkChanged();
        }

        /// <summary>Schedules a beat; its body runs as one batch, so a beat that changes several things redraws once.</summary>
        private void After(int delayMs, Action beat)
        {
            scheduler.Schedule(TableTiming.ToSeconds(delayMs), () => Batch(beat));
        }

        private static ShowdownRoundState WithHumanStack(ShowdownRoundState state, int stack) =>
            state with
            {
                Seats = state.Seats
                    .Select((seat, index) => index == ShowdownRound.HumanSeatIndex ? seat with { Stack = stack } : seat)
                    .ToArray(),
            };

        // ---- The player's inputs ---------------------------------------------------------------

        /// <summary>Take a seat from the home screen. Callers gate on <c>CanPlay</c> first; this refuses a short stack too.</summary>
        public void StartHand(int ante)
        {
            Batch(() =>
            {
                if (!bankrollStore.IsLoaded || !DomainBankroll.CanPlay(bankrollStore.Value, ante)) return;
                BeginRound(ShowdownRound.CreateShowdownTable(bankrollStore.Value, ante, HumanName), null);
            });
        }

        /// <summary>Marks (or re-marks) the human's kicker while the pick is still open.</summary>
        public void SelectKicker(int cardIndex)
        {
            Batch(() =>
            {
                var current = round;
                if (current == null || current.Phase != ShowdownPhase.Kicker || kickerLocked) return;
                if (cardIndex < 0 || cardIndex >= ShowdownRound.HumanSeat(current).Cards.Count) return;
                SetRound(ShowdownRound.SetKicker(current, ShowdownRound.HumanSeatIndex, cardIndex));
                feedback.Play(Cue.Tap);
            });
        }

        /// <summary>Locks the kicker in and puts the first dealer card on the clock.</summary>
        public void LockKicker()
        {
            Batch(() =>
            {
                var current = round;
                if (current == null || current.Phase != ShowdownPhase.Kicker || kickerLocked) return;
                if (ShowdownRound.HumanSeat(current).KickerIndex == null) return;
                Set(ref kickerLocked, true);
                drawScheduledFor = 1;
                feedback.Play(Cue.Chip);
                After(TableTiming.DealerCardBeatMs, PerformDraw);
            });
        }

        public void Hold()
        {
            Batch(() => SubmitMove(new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = PressureNow() }));
        }

        public void Forge(int discardIndex)
        {
            Batch(() =>
            {
                var current = round;
                if (current == null) return;
                var seat = ShowdownRound.HumanSeat(current);
                var pressure = PressureNow();
                if (discardIndex < 0 || discardIndex >= seat.Cards.Count || discardIndex == seat.KickerIndex) return;
                if (seat.ForgesUsed >= ShowdownPayouts.ForgeLimit || seat.Stack < current.Ante * (pressure ? 2 : 1)) return;

                var move = new ShowdownMove { Kind = ShowdownMoveKind.Forge, ForgeDiscardIndex = discardIndex, Pressure = pressure };
                // The ring marks a trade that really landed; a refused move must not light a card.
                if (!SubmitMove(move)) return;
                Set(ref freshCardIndex, discardIndex);
                After(TableTiming.FreshCardMs, () => Set(ref freshCardIndex, null));
            });
        }

        public void Bail()
        {
            Batch(() => SubmitMove(new ShowdownMove { Kind = ShowdownMoveKind.Bail, Pressure = false }));
        }

        public void BeginForge()
        {
            Batch(() => Set(ref forgeArming, true));
        }

        public void CancelForge()
        {
            Batch(() => Set(ref forgeArming, false));
        }

        public void SetPressureArmed(bool armed)
        {
            Batch(() => Set(ref pressureArmed, armed));
        }

        public void NextHand()
        {
            Batch(() =>
            {
                var current = round;
                if (current == null || current.Phase != ShowdownPhase.Settled) return;
                if (!DomainBankroll.CanPlay(ShowdownRound.HumanSeat(current).Stack, current.Ante)) return;
                BeginRound(current, null);
            });
        }

        /// <summary>
        /// The free top-up. <c>ReUp</c> is a no-op unless the stack really is short, so this stays correct even if a
        /// caller offers the button when it shouldn't. <paramref name="atAnte"/> is only consulted away from the
        /// table, where there is no round to read the stake from.
        /// </summary>
        public void ReUpNow(int atAnte)
        {
            Batch(() =>
            {
                var current = round;
                var ante = current != null ? current.Ante : atAnte;
                var before = current != null ? ShowdownRound.HumanSeat(current).Stack : bankrollStore.Value;
                var topped = DomainBankroll.ReUp(before, ante);
                if (topped == before) return;

                ChangeHumanStack(topped);
                statsStore.Update(previous => previous with { ReUps = previous.ReUps + 1 });
                MarkChanged();
                feedback.Play(Cue.Chip);
            });
        }

        /// <summary>
        /// The daily bonus drop. Eligibility lives in the daily-bonus store (the caller checks it and marks the
        /// claim); this just moves the chips, on or off the table.
        /// </summary>
        public void ClaimDailyBonus()
        {
            Batch(() =>
            {
                var before = round != null ? ShowdownRound.HumanSeat(round).Stack : bankrollStore.Value;
                ChangeHumanStack(before + DailyBonus.DailyBonusAmount);
                MarkChanged();
                feedback.Play(Cue.Chip);
            });
        }

        public void DismissAchievements()
        {
            Batch(() => SetNewAchievements(Array.Empty<Achievement>()));
        }

        public void DismissStreakBonus()
        {
            Batch(() => Set(ref streakBonus, null));
        }

        /// <summary>Leaving mid-hand just abandons the presentation — nothing was settled.</summary>
        public void LeaveTable()
        {
            Batch(() =>
            {
                scheduler.CancelAll();
                ResetPresentation();
                Set(ref round, null);
            });
        }

        public void ResetBankroll()
        {
            Batch(() =>
            {
                bankrollStore.Reset();
                MarkChanged();
            });
        }

        /// <summary>QA only: the next dealt hand uses this RNG seed, so a screenshot scenario is repeatable.</summary>
        public void PinNextSeed(uint seed)
        {
            pinnedSeed = seed;
        }

        // ---- Shared helpers --------------------------------------------------------------------

        /// <summary>Applies the human's move for this action round, with the pressure toggle folded in. False when it was not applied.</summary>
        private bool SubmitMove(ShowdownMove move)
        {
            var current = round;
            if (current == null || current.Phase != ShowdownPhase.Actions) return false;
            var seat = ShowdownRound.HumanSeat(current);
            if (seat.Status != SeatStatus.Active || seat.LastMove != null) return false;

            SetRound(ShowdownRound.ApplyMove(current, ShowdownRound.HumanSeatIndex, move));
            Set(ref forgeArming, false);
            Set(ref pressureArmed, false);
            feedback.Play(CueForMove(move, Cue.Tap));
            return true;
        }

        /// <summary>The cue for a move; <paramref name="otherwise"/> covers a plain hold (the human taps, the bots stay quiet).</summary>
        private static Cue CueForMove(ShowdownMove move, Cue otherwise)
        {
            if (move.Kind == ShowdownMoveKind.Bail) return Cue.Fold;
            if (move.Pressure) return Cue.Raise;
            return move.Kind == ShowdownMoveKind.Forge ? Cue.Flip : otherwise;
        }

        /// <summary>The pressure toggle only counts in the pressure round, once, and with the chips to back it.</summary>
        private bool PressureNow()
        {
            var current = round;
            if (current == null || !pressureArmed) return false;
            var seat = ShowdownRound.HumanSeat(current);
            return current.DealerCards.Count == PressureArmedRound && !seat.Pressured && seat.Stack >= current.Ante;
        }

        private void ChangeHumanStack(int stack)
        {
            if (round != null)
            {
                SetRound(WithHumanStack(round, stack));
            }

            bankrollStore.Save(stack);
        }

        private void ResetPresentation()
        {
            Set(ref thinkingSeat, null);
            Set(ref kickerLocked, false);
            Set(ref forgeArming, false);
            Set(ref freshCardIndex, null);
            Set(ref pressureArmed, false);
            Set(ref revealed, false);
            SetNewAchievements(Array.Empty<Achievement>());
            Set(ref streakBonus, null);
            settleScheduled = false;
            drawScheduledFor = 0;
        }
    }
}
