#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class ShowdownPayoutsTests
    {
        private const int Ante = 10;

        /// <summary>The dealer shows down a pair of nines; the last card off the deck is the 2 of clubs.</summary>
        private static readonly HandRank DealerRank = HandRanks.EvaluateHand(TestSupport.Hand("9c 9d 5h 3s 2c"));

        private static readonly Card FinalCard = TestSupport.Hand("2c")[0];

        private const string Trips = "Qc Qd Qh 3s 2s";
        private const string TripsTwin = "Qs Qh Qd 3h 2h";
        private const string AceHigh = "Ac Kd 9h 5s 4c";
        private const string KingHigh = "Kc Qd 9d 5c 4d";
        private const string DealerTwin = "9h 9s 5d 3c 2d";

        private static SettleInput SeatOf(
            HandRank? rank = null,
            int kickerRank = 7,
            int dealerStake = Ante,
            int tableContrib = Ante,
            bool pressured = false,
            bool bailed = false,
            int refund = 0) =>
            new SettleInput
            {
                Rank = rank,
                KickerRank = kickerRank,
                DealerStake = dealerStake,
                TableContrib = tableContrib,
                Pressured = pressured,
                Bailed = bailed,
                Refund = refund,
            };

        private static SettleInput Survivor(
            string spec,
            int kickerRank = 7,
            int dealerStake = Ante,
            bool pressured = false) =>
            SeatOf(
                rank: HandRanks.EvaluateHand(TestSupport.Hand(spec)),
                kickerRank: kickerRank,
                dealerStake: dealerStake,
                pressured: pressured);

        /// <summary>Chips created must equal chips destroyed, in every single scenario.</summary>
        private static void ExpectConserved(
            IReadOnlyList<SettleInput> seats,
            IReadOnlyList<ShowdownSettlement> settlements,
            int houseDelta)
        {
            var refunds = seats.Sum(seat => seat.Refund);
            var nets = settlements.Sum(settlement => settlement.Net);
            Assert.AreEqual(refunds - nets, houseDelta);
        }

        private static SettleSeatsResult Settle(IReadOnlyList<SettleInput> seats, int tablePot)
        {
            var result = ShowdownPayouts.SettleSeats(seats, DealerRank, FinalCard, tablePot, Ante);
            ExpectConserved(seats, result.Settlements, result.HouseDelta);
            return result;
        }

        [Test]
        public void BailRefundReturnsHalfOfEverythingCommittedRoundedDown()
        {
            Assert.AreEqual(10, ShowdownPayouts.BailRefund(20));
            Assert.AreEqual(12, ShowdownPayouts.BailRefund(25));
            Assert.AreEqual(0, ShowdownPayouts.BailRefund(0));
        }

        // ---- the dealer battle ----

        [Test]
        public void PaysAPlainDealerBeatAtEvenMoney()
        {
            var seats = new[] { Survivor(Trips), Survivor(TripsTwin) };
            var settlements = Settle(seats, 20).Settlements;
            foreach (var s in settlements)
            {
                Assert.AreEqual(VsDealer.Win, s.VsDealer);
                Assert.AreEqual(Ante, s.DealerDelta);
            }
        }

        [Test]
        public void TakesTheStakeOnALossAndPushesATie()
        {
            var seats = new[] { Survivor(AceHigh), Survivor(DealerTwin) };
            var settlements = Settle(seats, 20).Settlements;
            Assert.AreEqual(VsDealer.Lose, settlements[0].VsDealer);
            Assert.AreEqual(-Ante, settlements[0].DealerDelta);
            Assert.AreEqual(VsDealer.Push, settlements[1].VsDealer);
            Assert.AreEqual(0, settlements[1].DealerDelta);
        }

        [Test]
        public void PaysAPressuredWinningStakeAtThreeToOne()
        {
            var seats = new[] { Survivor(Trips, pressured: true, dealerStake: Ante * 2), Survivor(TripsTwin) };
            var settlements = Settle(seats, 20).Settlements;
            Assert.AreEqual(Ante * 2 * 3, settlements[0].DealerDelta);
            Assert.AreEqual(Ante, settlements[1].DealerDelta);
        }

        // ---- the kicker strike ----

        [Test]
        public void DoublesTheDealerWinningsWhenTheKickerMatchesTheLastCard()
        {
            var seats = new[] { Survivor(Trips, kickerRank: 2), Survivor(TripsTwin) };
            var settlements = Settle(seats, 20).Settlements;
            Assert.IsTrue(settlements[0].KickerStrike);
            Assert.AreEqual(Ante * 2, settlements[0].DealerDelta);
            Assert.IsFalse(settlements[1].KickerStrike);
        }

        [Test]
        public void KickerStrikeStacksWithPressureForSixTimesTheStake()
        {
            var seats = new[]
            {
                Survivor(Trips, kickerRank: 2, pressured: true, dealerStake: Ante * 2),
                Survivor(TripsTwin),
            };
            var settlements = Settle(seats, 20).Settlements;
            Assert.AreEqual(Ante * 2 * 3 * 2, settlements[0].DealerDelta);
        }

        [Test]
        public void KickerStrikeNeverFiresOnALossOrAPush()
        {
            var seats = new[] { Survivor(AceHigh, kickerRank: 2), Survivor(DealerTwin, kickerRank: 2) };
            var settlements = Settle(seats, 20).Settlements;
            foreach (var s in settlements)
            {
                Assert.IsFalse(s.KickerStrike);
            }
        }

        // ---- the table battle ----

        [Test]
        public void HandsThePotPlusTheBeatenStakeToTheSingleBestHand()
        {
            var seats = new[] { Survivor(Trips), Survivor(AceHigh) };
            var settlements = Settle(seats, 20).Settlements;
            Assert.IsTrue(settlements[0].WonTable);
            Assert.IsTrue(settlements[0].Outright);
            // The loser's stake joined the pot: 20 on the table + 10 beaten stake.
            Assert.AreEqual(30 - Ante, settlements[0].TableDelta);
            Assert.IsFalse(settlements[1].WonTable);
            Assert.AreEqual(-Ante, settlements[1].TableDelta);
        }

        [Test]
        public void SplitsATiedPotSpareChipsToTheEarlierSeat()
        {
            // The bailed seat's stake was poured into the pot when it quit: 25 + 10.
            var seats = new[]
            {
                Survivor(Trips),
                Survivor(TripsTwin),
                SeatOf(bailed: true, tableContrib: 5, refund: 7),
            };
            var settlements = Settle(seats, 35).Settlements;
            Assert.AreEqual(18 - Ante, settlements[0].TableDelta);
            Assert.AreEqual(17 - Ante, settlements[1].TableDelta);
            Assert.IsFalse(settlements[0].Outright);
            Assert.IsFalse(settlements[1].Outright);
        }

        [Test]
        public void LetsASoleSurvivorTakeThePotEvenWhileLosingToTheDealer()
        {
            // Pot at settle already holds both bailers' stakes: 30 antes + 2 x 10.
            var seats = new[]
            {
                Survivor(KingHigh),
                SeatOf(bailed: true, refund: 10),
                SeatOf(bailed: true, refund: 10),
            };
            var settlements = Settle(seats, 50).Settlements;
            Assert.AreEqual(VsDealer.Lose, settlements[0].VsDealer);
            Assert.IsTrue(settlements[0].WonTable);
            // Their own beaten stake fell into the pot they collect: 50 + 10 back out.
            Assert.AreEqual(60 - Ante, settlements[0].TableDelta);
            Assert.AreEqual(60 - Ante - Ante, settlements[0].Net);
            Assert.IsFalse(settlements[0].Sweep);
        }

        [Test]
        public void SendsAnUnclaimedPotToTheHouseWhenEveryoneBailed()
        {
            // Both stakes already sit in the pot: 20 antes + 2 x 10 stakes.
            var seats = new[] { SeatOf(bailed: true, refund: 10), SeatOf(bailed: true, refund: 10) };
            var result = Settle(seats, 40);
            foreach (var s in result.Settlements)
            {
                Assert.AreEqual(VsDealer.Bailed, s.VsDealer);
                Assert.IsFalse(s.WonTable);
                Assert.AreEqual(10 - 20, s.Net);
            }

            Assert.AreEqual(40, result.HouseDelta);
        }

        // ---- the sweep ----

        [Test]
        public void PaysFiveAntesForBeatingDealerAndTableInOneHand()
        {
            var seats = new[] { Survivor(Trips), Survivor(AceHigh) };
            var settlements = Settle(seats, 20).Settlements;
            Assert.IsTrue(settlements[0].Sweep);
            Assert.AreEqual(5 * Ante, settlements[0].SweepBonus);
            // Winnings + pot-with-beaten-stake + sweep: 10 + (30 - 10) + 50.
            Assert.AreEqual(Ante + 2 * Ante + 5 * Ante, settlements[0].Net);
        }

        [Test]
        public void SweepNeedsTheOutrightTableWinASplitIsNotASweep()
        {
            var seats = new[] { Survivor(Trips), Survivor(TripsTwin) };
            var settlements = Settle(seats, 20).Settlements;
            foreach (var s in settlements)
            {
                Assert.IsFalse(s.Sweep);
                Assert.AreEqual(0, s.SweepBonus);
            }
        }

        [Test]
        public void SweepNeedsTheDealerBeatTakingTheTableOnALosingHandIsNotASweep()
        {
            var seats = new[] { Survivor(KingHigh), Survivor(AceHigh) };
            var settlements = Settle(seats, 20).Settlements;
            Assert.IsTrue(settlements[1].WonTable);
            Assert.AreEqual(VsDealer.Lose, settlements[1].VsDealer);
            Assert.IsFalse(settlements[1].Sweep);
            // Both beaten stakes fell into the pot the ace-high still collects.
            Assert.AreEqual(40 - Ante, settlements[1].TableDelta);
        }
    }
}
