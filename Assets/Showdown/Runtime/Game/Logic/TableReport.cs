#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Showdown.Domain;

namespace Showdown.Game
{
    public enum ReportTone
    {
        Win,
        Lose,
        Even,
    }

    /// <summary>One row of the result ledger; <c>Amount</c> is signed.</summary>
    public sealed record LedgerLine(string Label, int Amount);

    /// <summary>What the hand did to one of the other seats.</summary>
    public sealed record SeatLine(string Name, string Verb, int Net);

    /// <summary>The gold card on the result sheet: the one payout worth celebrating.</summary>
    public sealed record ReportAward(string Title, string Caption, int Amount);

    /// <summary>Everything the result sheet says about a settled hand.</summary>
    public sealed record HandReport
    {
        public string Title { get; init; } = "";
        public ReportTone Tone { get; init; }
        /// <summary>The hand's whole change to the bankroll, streak bonus included.</summary>
        public int Net { get; init; }
        /// <summary>"PAIR · HOUSE HIGH CARD".</summary>
        public string Matchup { get; init; } = "";
        /// <summary>Signed rows that add up to <see cref="Net"/>.</summary>
        public IReadOnlyList<LedgerLine> Lines { get; init; } = new List<LedgerLine>();
        public int Bankroll { get; init; }
        public ReportAward? Award { get; init; }
        public IReadOnlyList<SeatLine> Others { get; init; } = new List<SeatLine>();
    }

    /// <summary>
    /// The table's words in the walnut design: the stake's table name, the status chip under the title and the
    /// result sheet. Kept engine-free so the wording is tested without building a screen.
    /// </summary>
    public static class TableReport
    {
        private const string Minus = "−";
        private const string Dot = " · ";

        public static string TableName(int ante)
        {
            switch (ante)
            {
                case 5: return "Copper Table";
                case 10: return "Bronze Table";
                case 25: return "Gold Table";
                default: return "Showdown Table";
            }
        }

        /// <summary>"+80", "−20" with a true minus sign, "0".</summary>
        public static string Signed(int amount)
        {
            string digits = System.Math.Abs(amount).ToString(CultureInfo.InvariantCulture);
            if (amount > 0) return "+" + digits;
            return amount < 0 ? Minus + digits : digits;
        }

        /// <param name="humanPending">True while the human owes a move this action round.</param>
        /// <param name="thinkingSeat">Index into <c>round.Seats</c> of the bot thinking, or null.</param>
        public static string StatusChip(ShowdownRoundState round, bool kickerLocked, bool humanPending, int? thinkingSeat)
        {
            switch (round.Phase)
            {
                case ShowdownPhase.Kicker:
                    return kickerLocked ? "The house draws" : "Pick your kicker";
                case ShowdownPhase.Actions:
                    return ActionsChip(round, humanPending, thinkingSeat);
                case ShowdownPhase.Reveal:
                    return "Showdown";
                case ShowdownPhase.Settled:
                    return "Hand result";
                default:
                    return "Dealing";
            }
        }

        private static string ActionsChip(ShowdownRoundState round, bool humanPending, int? thinkingSeat)
        {
            var human = ShowdownRound.HumanSeat(round);
            if (humanPending)
            {
                // Matches when the Pressure button shows: the third card, a single stake, and chips to double it.
                bool pressureRound = round.DealerCards.Count == ShowdownPayouts.PressureRound && !human.Pressured
                    && human.Stack >= round.Ante;
                return pressureRound ? "Pressure round" : "Your move";
            }
            if (thinkingSeat.HasValue && thinkingSeat.Value < round.Seats.Count)
            {
                return round.Seats[thinkingSeat.Value].Name + " is playing";
            }
            return human.Status == SeatStatus.Bailed ? "You bailed" : "Around the table";
        }

        /// <summary>The result sheet for a settled hand; null until the human seat has a settlement.</summary>
        public static HandReport? Settled(ShowdownRoundState round, StreakPayout? streak)
        {
            var human = ShowdownRound.HumanSeat(round);
            var settlement = human.Settlement;
            if (round.Phase != ShowdownPhase.Settled || settlement == null) return null;

            int streakAmount = streak?.Amount ?? 0;
            int net = settlement.Net + streakAmount;
            return new HandReport
            {
                Title = TitleFor(settlement),
                Tone = net > 0 ? ReportTone.Win : net < 0 ? ReportTone.Lose : ReportTone.Even,
                Net = net,
                Matchup = MatchupFor(round, human),
                Lines = LinesFor(settlement, human, streakAmount),
                Bankroll = human.Stack,
                Award = AwardFor(settlement, streak),
                Others = OthersFor(round),
            };
        }

        private static string TitleFor(ShowdownSettlement s)
        {
            if (s.Sweep) return "You sweep the table";
            if (s.KickerStrike) return "Kicker strike ×2";
            if (s.VsDealer == VsDealer.Bailed) return "You bailed";
            if (s.VsDealer == VsDealer.Win) return s.WonTable ? "You beat them all" : "You beat the house";
            if (s.WonTable && s.Net >= 0) return "You took the table";
            if (s.Net == 0) return "Push";
            if (s.Net > 0) return "You come out ahead";
            return s.VsDealer == VsDealer.Lose ? "The house takes it" : "Not this hand";
        }

        private static string MatchupFor(ShowdownRoundState round, ShowdownSeatState human)
        {
            string yours = human.Status == SeatStatus.Bailed || human.Cards.Count != HandRanks.HandSize
                ? "BAILED"
                : HandRanks.HandName(HandRanks.EvaluateHand(human.Cards)).ToUpperInvariant();
            var dealer = ShowdownRound.DealerRankOf(round);
            return dealer == null ? yours : yours + Dot + "HOUSE " + HandRanks.HandName(dealer).ToUpperInvariant();
        }

        private static IReadOnlyList<LedgerLine> LinesFor(ShowdownSettlement s, ShowdownSeatState human, int streakAmount)
        {
            var lines = new List<LedgerLine>
            {
                new LedgerLine("Table pot", s.TableDelta),
                new LedgerLine("vs the house", s.DealerDelta),
            };
            if (s.SweepBonus > 0) lines.Add(new LedgerLine("Sweep bonus", s.SweepBonus));
            if (s.VsDealer == VsDealer.Bailed && human.Refund > 0) lines.Add(new LedgerLine("Bail refund", human.Refund));
            if (streakAmount > 0) lines.Add(new LedgerLine("Streak bonus", streakAmount));
            return lines;
        }

        private static ReportAward? AwardFor(ShowdownSettlement s, StreakPayout? streak)
        {
            if (streak != null && streak.Amount > 0) return new ReportAward(streak.Streak + "-win streak", "ON THE HOUSE", streak.Amount);
            if (s.Sweep && s.SweepBonus > 0) return new ReportAward("Sweep: yours", "BEAT TABLE + HOUSE", s.SweepBonus);
            return null;
        }

        private static IReadOnlyList<SeatLine> OthersFor(ShowdownRoundState round)
        {
            var others = new List<SeatLine>();
            foreach (var seat in round.Seats)
            {
                if (seat.IsHuman) continue;
                int net = seat.Settlement?.Net ?? 0;
                string verb = seat.Status == SeatStatus.Bailed ? "bailed" : net > 0 ? "won" : net < 0 ? "lost" : "broke even";
                others.Add(new SeatLine(seat.Name, verb, net));
            }
            return others;
        }
    }
}
