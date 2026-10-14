#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    /// <summary>The table's words: the table name, the status chip and the result sheet.</summary>
    [TestFixture]
    public class TableReportTests
    {
        private const int Ante = 10;
        private const int Duke = 1;
        private const int Gus = 2;

        // ---- builders ------------------------------------------------------------------------------

        private static List<Card> Hand(string spec) =>
            spec.Split(' ')
                .Select(token =>
                {
                    int rank = "23456789TJQKA".IndexOf(token[0]) + 2;
                    var suit = token[1] switch { 's' => Suit.Spades, 'h' => Suit.Hearts, 'd' => Suit.Diamonds, _ => Suit.Clubs };
                    return new Card { Rank = rank, Suit = suit, Id = Cards.CardId(rank, suit) };
                })
                .ToList();

        private static ShowdownRoundState Table(ShowdownPhase phase, string dealer = "")
        {
            var table = ShowdownRound.CreateShowdownTable(500, Ante);
            return table with
            {
                Phase = phase,
                DealerCards = dealer.Length == 0 ? new List<Card>() : Hand(dealer),
            };
        }

        private static ShowdownRoundState WithSeat(ShowdownRoundState round, int index, System.Func<ShowdownSeatState, ShowdownSeatState> change)
        {
            var seats = round.Seats.ToList();
            seats[index] = change(seats[index]);
            return round with { Seats = seats };
        }

        private static ShowdownRoundState Settled(ShowdownSettlement human, int humanStack = 560,
            ShowdownSettlement? duke = null, ShowdownSettlement? gus = null)
        {
            var round = Table(ShowdownPhase.Settled, "Ah Jd Ks 5d 3d");
            round = WithSeat(round, 0, s => s with { Cards = Hand("Kc 5s 8d As Ac"), Settlement = human, Stack = humanStack });
            round = WithSeat(round, Duke, s => s with
            {
                Status = SeatStatus.Bailed,
                Settlement = duke ?? new ShowdownSettlement { VsDealer = VsDealer.Bailed, Net = -10 },
            });
            return WithSeat(round, Gus, s => s with
            {
                Cards = Hand("Qc 2s 2c 7d 6c"),
                Settlement = gus ?? new ShowdownSettlement { VsDealer = VsDealer.Lose, Net = -20 },
            });
        }

        private static readonly ShowdownSettlement SweepWin = new ShowdownSettlement
        {
            VsDealer = VsDealer.Win, WonTable = true, Outright = true, Sweep = true,
            DealerDelta = 10, TableDelta = 20, SweepBonus = 50, Net = 80,
        };

        // ---- table name ----------------------------------------------------------------------------

        [Test]
        public void NamesEachStakeAfterAMetal()
        {
            Assert.AreEqual("Copper Table", TableReport.TableName(5));
            Assert.AreEqual("Bronze Table", TableReport.TableName(10));
            Assert.AreEqual("Gold Table", TableReport.TableName(25));
        }

        [Test]
        public void FallsBackToAPlainNameForAnUnknownStake()
        {
            Assert.AreEqual("Showdown Table", TableReport.TableName(7));
        }

        // ---- status chip ---------------------------------------------------------------------------

        [Test]
        public void AsksForTheKickerUntilItIsLocked()
        {
            var round = Table(ShowdownPhase.Kicker);
            Assert.AreEqual("Pick your kicker", TableReport.StatusChip(round, false, false, null));
            Assert.AreEqual("The house draws", TableReport.StatusChip(round, true, false, null));
        }

        [Test]
        public void SaysYourMoveWhenTheHumanOwesOne()
        {
            var round = Table(ShowdownPhase.Actions, "Ah Jd");
            Assert.AreEqual("Your move", TableReport.StatusChip(round, true, true, null));
        }

        [Test]
        public void CallsOutThePressureRoundWhileTheStakeIsStillSingle()
        {
            var round = Table(ShowdownPhase.Actions, "Ah Jd Ks");
            Assert.AreEqual("Pressure round", TableReport.StatusChip(round, true, true, null));

            var pressured = WithSeat(round, 0, s => s with { Pressured = true });
            Assert.AreEqual("Your move", TableReport.StatusChip(pressured, true, true, null));
        }

        [Test]
        public void SkipsThePressureRoundWhenTheStackCannotCoverIt()
        {
            var shortStack = WithSeat(Table(ShowdownPhase.Actions, "Ah Jd Ks"), 0, s => s with { Stack = Ante - 1 });
            Assert.AreEqual("Your move", TableReport.StatusChip(shortStack, true, true, null));
        }

        [Test]
        public void NamesTheBotThatIsThinking()
        {
            var round = Table(ShowdownPhase.Actions, "Ah Jd");
            Assert.AreEqual("Gus is playing", TableReport.StatusChip(round, true, false, Gus));
        }

        [Test]
        public void SaysYouBailedWhenNobodyIsThinking()
        {
            var round = WithSeat(Table(ShowdownPhase.Actions, "Ah Jd"), 0, s => s with { Status = SeatStatus.Bailed });
            Assert.AreEqual("You bailed", TableReport.StatusChip(round, true, false, null));
        }

        [Test]
        public void NamesTheDealTheShowdownAndTheResult()
        {
            Assert.AreEqual("Dealing", TableReport.StatusChip(Table(ShowdownPhase.Dealing), false, false, null));
            Assert.AreEqual("Showdown", TableReport.StatusChip(Table(ShowdownPhase.Reveal), true, false, null));
            Assert.AreEqual("Hand result", TableReport.StatusChip(Table(ShowdownPhase.Settled), true, false, null));
        }

        // ---- result sheet --------------------------------------------------------------------------

        [Test]
        public void HasNoReportBeforeTheHandSettles()
        {
            Assert.IsNull(TableReport.Settled(Table(ShowdownPhase.Reveal), null));
        }

        [Test]
        public void ASweepLeadsWithTheSweepAndItsBonusCard()
        {
            var report = TableReport.Settled(Settled(SweepWin), null)!;

            Assert.AreEqual("You sweep the table", report.Title);
            Assert.AreEqual(ReportTone.Win, report.Tone);
            Assert.AreEqual(80, report.Net);
            Assert.AreEqual("PAIR · HOUSE HIGH CARD", report.Matchup);
            Assert.AreEqual("Sweep: yours", report.Award!.Title);
            Assert.AreEqual(50, report.Award.Amount);
        }

        [Test]
        public void LedgerLinesAddUpToTheHandsNet()
        {
            var report = TableReport.Settled(Settled(SweepWin), null)!;

            CollectionAssert.AreEqual(new[] { "Table pot", "vs the house", "Sweep bonus" }, report.Lines.Select(l => l.Label));
            Assert.AreEqual(report.Net, report.Lines.Sum(l => l.Amount));
            Assert.AreEqual(560, report.Bankroll);
        }

        [Test]
        public void AStreakBonusJoinsTheLedgerTheHeadlineAndTheAwardCard()
        {
            var win = new ShowdownSettlement { VsDealer = VsDealer.Win, DealerDelta = 10, TableDelta = -10, Net = 0 };
            var report = TableReport.Settled(Settled(win), new StreakPayout(3, 20))!;

            Assert.AreEqual("Streak bonus", report.Lines.Last().Label);
            Assert.AreEqual(20, report.Net);
            Assert.AreEqual("3-win streak", report.Award!.Title);
            Assert.AreEqual("ON THE HOUSE", report.Award.Caption);
        }

        [Test]
        public void ABailShowsItsRefundAndReadsAsALoss()
        {
            var bail = new ShowdownSettlement
            {
                VsDealer = VsDealer.Bailed, DealerDelta = -10, TableDelta = -10, Net = -10,
            };
            var round = WithSeat(Settled(bail), 0, s => s with { Status = SeatStatus.Bailed, Refund = 10 });
            var report = TableReport.Settled(round, null)!;

            Assert.AreEqual("You bailed", report.Title);
            Assert.AreEqual(ReportTone.Lose, report.Tone);
            Assert.AreEqual("BAILED · HOUSE HIGH CARD", report.Matchup);
            Assert.AreEqual(new LedgerLine("Bail refund", 10), report.Lines.Last());
            Assert.AreEqual(report.Net, report.Lines.Sum(l => l.Amount));
        }

        [Test]
        public void ALossToTheHouseSaysSo()
        {
            var loss = new ShowdownSettlement { VsDealer = VsDealer.Lose, DealerDelta = -10, TableDelta = -10, Net = -20 };
            var report = TableReport.Settled(Settled(loss), null)!;

            Assert.AreEqual("The house takes it", report.Title);
            Assert.AreEqual(ReportTone.Lose, report.Tone);
            Assert.IsNull(report.Award);
        }

        [Test]
        public void AKickerStrikeIsNamedInTheTitle()
        {
            var strike = new ShowdownSettlement
            {
                VsDealer = VsDealer.Win, KickerStrike = true, DealerDelta = 20, TableDelta = -10, Net = 10,
            };
            Assert.AreEqual("Kicker strike ×2", TableReport.Settled(Settled(strike), null)!.Title);
        }

        [Test]
        public void ABreakEvenHandIsAPush()
        {
            var push = new ShowdownSettlement { VsDealer = VsDealer.Push, Net = 0 };
            var report = TableReport.Settled(Settled(push), null)!;

            Assert.AreEqual("Push", report.Title);
            Assert.AreEqual(ReportTone.Even, report.Tone);
        }

        [Test]
        public void ListsEachBotWithWhatTheHandDidToThem()
        {
            var gusWon = new ShowdownSettlement { VsDealer = VsDealer.Win, WonTable = true, Net = 30 };
            var report = TableReport.Settled(Settled(SweepWin, gus: gusWon), null)!;

            CollectionAssert.AreEqual(
                new[] { new SeatLine("Duke", "bailed", -10), new SeatLine("Gus", "won", 30) },
                report.Others);
        }

        // ---- amounts -------------------------------------------------------------------------------

        [Test]
        public void SignsAmountsWithATrueMinus()
        {
            Assert.AreEqual("+80", TableReport.Signed(80));
            Assert.AreEqual("−20", TableReport.Signed(-20));
            Assert.AreEqual("0", TableReport.Signed(0));
        }
    }
}
