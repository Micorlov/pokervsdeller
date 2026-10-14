#nullable enable
using System;
using Showdown.Domain;
using DomainBankroll = Showdown.Domain.Bankroll;

namespace Showdown.Game
{
    // The clock half of the controller: everything that happens because time passed rather than because the
    // player pressed something.
    public sealed partial class TableController
    {
        /// <summary>The single place the round changes; after each change, anything the new state now owes the clock is put on it.</summary>
        private void SetRound(ShowdownRoundState? next)
        {
            round = next;
            MarkChanged();
            if (next == null) return;
            ScheduleDealerDrawIfDue(next);
            ScheduleSettleIfRevealed(next);
        }

        /// <summary>A fresh hand: wipe the previous hand's presentation and clock, deal, and lay out the deal beats.</summary>
        private void BeginRound(ShowdownRoundState table, int? ante)
        {
            var handRng = Rng.Mulberry32(TakeSeed());
            var dealing = ShowdownRound.StartShowdownRound(table, handRng, ante);

            scheduler.CancelAll();
            ResetPresentation();
            rng = handRng;
            SetRound(dealing);
            feedback.Play(Cue.Chip);
            for (var beat = 0; beat < TableTiming.DealCardCount; beat += 1)
            {
                After(TableTiming.DealBeatMs * (beat + 1), () => feedback.Play(Cue.Deal));
            }

            After(TableTiming.DealBeatMs * TableTiming.DealCardCount + TableTiming.DealSettleMs, FinishDealingBeat);
        }

        /// <summary>The pinned seed (QA) is used once; every other hand takes a fresh one.</summary>
        private uint TakeSeed()
        {
            if (pinnedSeed.HasValue)
            {
                var seed = pinnedSeed.Value;
                pinnedSeed = null;
                return seed;
            }

            return seedSource();
        }

        /// <summary>Bots pick their kickers the moment the deal settles; the table then waits on the human's pick.</summary>
        private void FinishDealingBeat()
        {
            var current = round;
            if (current == null || current.Phase != ShowdownPhase.Dealing) return;

            var next = ShowdownRound.FinishDealing(current);
            for (var seatIndex = ShowdownRound.HumanSeatIndex + 1; seatIndex < ShowdownRound.SeatCount; seatIndex += 1)
            {
                next = ShowdownRound.SetKicker(next, seatIndex, Bots.ChooseBotKicker(next.Seats[seatIndex].Cards));
            }

            SetRound(next);
        }

        // Once every active seat has moved, the next card goes on the clock — exactly once per action round,
        // whichever seat happened to act last.
        private void ScheduleDealerDrawIfDue(ShowdownRoundState state)
        {
            if (state.Phase != ShowdownPhase.Actions || !ShowdownRound.AllActed(state)) return;
            var nextCard = state.DealerCards.Count + 1;
            if (drawScheduledFor >= nextCard) return;
            drawScheduledFor = nextCard;
            After(TableTiming.DealerCardBeatMs, PerformDraw);
        }

        /// <summary>Turns the next dealer card, once the table is ready for it.</summary>
        private void PerformDraw()
        {
            var current = round;
            if (current == null) return;
            if (current.Phase != ShowdownPhase.Kicker && current.Phase != ShowdownPhase.Actions) return;
            if (current.Phase == ShowdownPhase.Kicker && !ShowdownRound.AllKickersSet(current)) return;
            if (current.Phase == ShowdownPhase.Actions && !ShowdownRound.AllActed(current)) return;

            var next = ShowdownRound.DealDealerCard(current);
            SetRound(next);
            feedback.Play(Cue.Flip);
            if (next.Phase == ShowdownPhase.Actions) RunBotTurns();
        }

        /// <summary>
        /// Bots act one at a time around the table; the human can act at any point.
        ///
        /// The whole cascade is laid out on the clock up front rather than each seat calling the next: a
        /// self-referencing callback would close over a stale copy of itself, and one seat's timer failing would
        /// silently strand the rest of the table.
        /// </summary>
        private void RunBotTurns()
        {
            var elapsed = 0;
            for (var seatIndex = ShowdownRound.HumanSeatIndex + 1; seatIndex < ShowdownRound.SeatCount; seatIndex += 1)
            {
                var seat = seatIndex;
                var startsAt = elapsed;
                elapsed += TableTiming.BotThinkBaseMs + (seat % TableTiming.BotThinkSeatCycle) * TableTiming.BotThinkStepMs;
                After(startsAt, () => BeginThinking(seat));
                After(elapsed, () => BotActs(seat));
            }
        }

        private void BeginThinking(int seatIndex)
        {
            var current = round;
            if (current != null && current.Phase == ShowdownPhase.Actions
                && current.Seats[seatIndex].Status == SeatStatus.Active)
            {
                Set(ref thinkingSeat, seatIndex);
            }
        }

        private void BotActs(int seatIndex)
        {
            var current = round;
            Set(ref thinkingSeat, null);
            if (current == null || current.Phase != ShowdownPhase.Actions) return;

            var seat = current.Seats[seatIndex];
            if (seat.Status != SeatStatus.Active || seat.LastMove != null || seat.Persona == null) return;

            var move = Bots.DecideShowdownMove(seat, current.DealerCards, current.DealerCards.Count, current.Ante, seat.Persona, rng);
            SetRound(ShowdownRound.ApplyMove(current, seatIndex, move));
            // A bot holding says nothing; only the moves worth hearing get a cue.
            if (move.Kind == ShowdownMoveKind.Bail || move.Pressure || move.Kind == ShowdownMoveKind.Forge)
            {
                feedback.Play(CueForMove(move, Cue.Tap));
            }
        }

        // The fifth card flips the phase to Reveal; from there the hand plays itself out on a timer — the table's
        // hands turn, then the chips move.
        private void ScheduleSettleIfRevealed(ShowdownRoundState state)
        {
            if (state.Phase != ShowdownPhase.Reveal || settleScheduled) return;
            settleScheduled = true;

            After(TableTiming.RevealBeatMs, () =>
            {
                feedback.Play(Cue.Reveal);
                Set(ref revealed, true);
            });
            After(TableTiming.RevealBeatMs + TableTiming.SettleBeatMs, SettleHand);
        }

        /// <summary>
        /// Pays the table out and books the hand: stats, badges, the win-streak bonus, the bankroll and the bust cue.
        /// </summary>
        private void SettleHand()
        {
            var current = round;
            if (current == null || current.Phase != ShowdownPhase.Reveal) return;

            var settled = ShowdownRound.SettleShowdown(current);
            SetRound(settled);

            // The bankroll the player actually ends the hand on, streak bonus included.
            var effectiveBankroll = ShowdownRound.HumanSeat(settled).Stack;
            var tally = HandLedger.Tally(statsStore.Current, settled, epochMilliseconds());
            if (tally != null)
            {
                effectiveBankroll += BookHand(settled, tally);
            }

            bankrollStore.Save(effectiveBankroll);

            // Against the post-bonus figure: a thin win that lands a streak can clear the bust line only once the
            // bonus is paid, and cueing "broke" there would contradict the stack the player is looking at.
            if (DomainBankroll.IsBust(effectiveBankroll, settled.Ante))
            {
                After(TableTiming.BustCueDelayMs, () => feedback.Play(Cue.Bust));
            }
        }

        /// <summary>Records the tally and pays any streak bonus onto the human stack. Returns the bonus chips paid.</summary>
        private int BookHand(ShowdownRoundState settled, HandTally tally)
        {
            statsStore.Update(_ => tally.NextStats);
            MarkChanged();
            if (tally.Unlocked.Count > 0) SetNewAchievements(tally.Unlocked);

            var paid = 0;
            if (tally.Bonus != null)
            {
                paid = tally.Bonus.Amount;
                Set(ref streakBonus, tally.Bonus);
                SetRound(WithHumanStack(settled, ShowdownRound.HumanSeat(settled).Stack + paid));
            }

            feedback.Play(VerdictCue(ShowdownRound.HumanSeat(settled).Settlement!));
            return paid;
        }

        private static Cue VerdictCue(ShowdownSettlement settlement)
        {
            if (settlement.Sweep || settlement.KickerStrike) return Cue.WinBig;
            if (settlement.Net > 0) return Cue.Win;
            return settlement.Net < 0 ? Cue.Lose : Cue.Push;
        }
    }
}
