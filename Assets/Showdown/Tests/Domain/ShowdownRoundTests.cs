#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    /// <summary>The scripted policy for the human seat: what it plays in a given action round.</summary>
    internal delegate ShowdownMove HumanPolicy(ShowdownRoundState state, int round);

    internal static class RoundHarness
    {
        public const int Ante = 10;
        public const int HumanStack = 500;

        /// <summary>
        /// The ledger every reducer must respect: chips on the table are only ever
        /// moved, never made. Mid-hand the pots and stakes are still in play; once
        /// settled they have all landed in stacks or on the house ledger.
        /// </summary>
        public static int ChipTotal(ShowdownRoundState state)
        {
            var stacks = state.Seats.Sum(seat => seat.Stack);
            if (state.Phase == ShowdownPhase.Settled || state.Phase == ShowdownPhase.Ante)
            {
                return stacks + state.HouseNet;
            }

            // A bailed seat's stake already lives in the table pot, so only active
            // stakes are still outstanding against the house.
            var stakes = state.Seats.Sum(seat => seat.Status == SeatStatus.Active ? seat.DealerStake : 0);
            return stacks + state.TablePot + stakes + state.HouseNet;
        }

        public static readonly HumanPolicy AlwaysHold = (state, round) =>
            new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = false };

        /// <summary>Forges early, pressures in round three — touches every mechanic.</summary>
        public static readonly HumanPolicy Adventurous = (state, round) =>
        {
            var seat = ShowdownRound.HumanSeat(state);
            if (round <= 2 && seat.ForgesUsed < 2 && seat.KickerIndex != null && seat.Stack >= state.Ante)
            {
                return new ShowdownMove
                {
                    Kind = ShowdownMoveKind.Forge,
                    ForgeDiscardIndex = Bots.ChooseForgeDiscard(seat.Cards, seat.KickerIndex.Value),
                    Pressure = false,
                };
            }

            if (round == 3 && seat.Stack >= state.Ante)
            {
                return new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = true };
            }

            return new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = false };
        };

        /// <summary>Plays one full hand from a seed, checking the ledger after every reducer.</summary>
        public static ShowdownRoundState PlayHand(int seed, HumanPolicy humanPolicy)
        {
            var rng = Rng.Mulberry32(seed);
            var state = ShowdownRound.CreateShowdownTable(HumanStack, Ante);
            var initial = ChipTotal(state);

            ShowdownRoundState Step(ShowdownRoundState next)
            {
                Assert.AreEqual(initial, ChipTotal(next));
                return next;
            }

            state = Step(ShowdownRound.StartShowdownRound(state, rng));
            state = Step(ShowdownRound.FinishDealing(state));
            for (var i = 0; i < ShowdownRound.SeatCount; i += 1)
            {
                state = Step(ShowdownRound.SetKicker(state, i, Bots.ChooseBotKicker(state.Seats[i].Cards)));
            }

            Assert.IsTrue(ShowdownRound.AllKickersSet(state));

            while (state.Phase == ShowdownPhase.Kicker || state.Phase == ShowdownPhase.Actions)
            {
                state = Step(ShowdownRound.DealDealerCard(state));
                if (state.Phase != ShowdownPhase.Actions) break;
                var round = state.DealerCards.Count;
                for (var i = 0; i < ShowdownRound.SeatCount; i += 1)
                {
                    var seat = state.Seats[i];
                    if (seat.Status == SeatStatus.Bailed) continue;
                    var move = seat.IsHuman
                        ? humanPolicy(state, round)
                        : Bots.DecideShowdownMove(seat, state.DealerCards, round, state.Ante, seat.Persona!, rng);
                    state = Step(ShowdownRound.ApplyMove(state, i, move));
                }

                Assert.IsTrue(ShowdownRound.AllActed(state));
            }

            Assert.AreEqual(ShowdownPhase.Reveal, state.Phase);
            return Step(ShowdownRound.SettleShowdown(state));
        }
    }

    [TestFixture]
    public class FullHandOfShowdownTests
    {
        private static int PlayersNet(ShowdownRoundState settled) =>
            settled.Seats.Sum(seat => seat.Settlement?.Net ?? 0);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void ConservesEveryChip(int seed)
        {
            var settled = RoundHarness.PlayHand(seed, RoundHarness.AlwaysHold);
            Assert.AreEqual(ShowdownPhase.Settled, settled.Phase);

            Assert.AreEqual(0, settled.HouseNet + PlayersNet(settled));

            foreach (var seat in settled.Seats)
            {
                Assert.IsNotNull(seat.Settlement);
            }
        }

        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        [TestCase(19)]
        [TestCase(20)]
        public void SurvivesForgesAndPressure(int seed)
        {
            var settled = RoundHarness.PlayHand(seed, RoundHarness.Adventurous);
            Assert.AreEqual(0, settled.HouseNet + PlayersNet(settled));
        }

        [Test]
        public void DealsOnlyUniqueCardsToTheShowdown()
        {
            var settled = RoundHarness.PlayHand(7, RoundHarness.Adventurous);
            var shown = settled.DealerCards.Select(card => card.Id)
                .Concat(settled.Seats.SelectMany(seat => seat.Cards.Select(card => card.Id)))
                .Concat(settled.Stock.Select(card => card.Id))
                .ToList();
            Assert.AreEqual(shown.Count, shown.Distinct().Count());
            Assert.AreEqual(5, settled.DealerCards.Count);
        }

        [Test]
        public void ReplaysIdenticallyFromTheSameSeed()
        {
            TestSupport.AssertDeepEqual(
                RoundHarness.PlayHand(42, RoundHarness.Adventurous),
                RoundHarness.PlayHand(42, RoundHarness.Adventurous));
        }

        [Test]
        public void CarriesTheHumanStackThroughBothAntesAndBackOut()
        {
            var settled = RoundHarness.PlayHand(3, RoundHarness.AlwaysHold);
            var seat = ShowdownRound.HumanSeat(settled);
            Assert.IsNotNull(seat.Settlement);
            Assert.AreEqual(RoundHarness.HumanStack + (seat.Settlement?.Net ?? 0), seat.Stack);
        }
    }

    [TestFixture]
    public class TableProtocolTests
    {
        private const int Ante = RoundHarness.Ante;
        private const int HumanStack = RoundHarness.HumanStack;

        private static readonly ShowdownMove Hold = new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = false };

        /// <summary>State advanced to the first action round, human kicker on card 0.</summary>
        private static ShowdownRoundState FirstAction(int seed = 5)
        {
            var rng = Rng.Mulberry32(seed);
            var state = ShowdownRound.StartShowdownRound(ShowdownRound.CreateShowdownTable(HumanStack, Ante), rng);
            state = ShowdownRound.FinishDealing(state);
            for (var i = 0; i < ShowdownRound.SeatCount; i += 1)
            {
                state = ShowdownRound.SetKicker(state, i, i == 0 ? 0 : Bots.ChooseBotKicker(state.Seats[i].Cards));
            }

            return ShowdownRound.DealDealerCard(state);
        }

        private static ShowdownRoundState HoldEveryoneElse(ShowdownRoundState state)
        {
            var next = state;
            for (var i = 1; i < ShowdownRound.SeatCount; i += 1)
            {
                if (next.Seats[i].Status == SeatStatus.Active)
                {
                    next = ShowdownRound.ApplyMove(next, i, Hold);
                }
            }

            return next;
        }

        [Test]
        public void RefusesToDrawBeforeEveryKickerIsSet()
        {
            var rng = Rng.Mulberry32(5);
            var state = ShowdownRound.StartShowdownRound(ShowdownRound.CreateShowdownTable(HumanStack, Ante), rng);
            state = ShowdownRound.FinishDealing(state);
            TestSupport.AssertThrowsMatching(() => ShowdownRound.DealDealerCard(state), "kicker");
        }

        [Test]
        public void RefusesToDrawWhileSeatsStillOweAMove()
        {
            var state = FirstAction();
            Assert.AreEqual(ShowdownPhase.Actions, state.Phase);
            TestSupport.AssertThrowsMatching(() => ShowdownRound.DealDealerCard(state), "owe a move");
        }

        [Test]
        public void RefusesToForgeTheKickerAway()
        {
            var state = FirstAction();
            TestSupport.AssertThrowsMatching(
                () => ShowdownRound.ApplyMove(
                    state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Forge, ForgeDiscardIndex = 0, Pressure = false }),
                "kicker");
        }

        [Test]
        public void RefusesAThirdForge()
        {
            var state = FirstAction();
            state = ShowdownRound.ApplyMove(state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Forge, ForgeDiscardIndex = 1, Pressure = false });
            state = HoldEveryoneElse(state);
            state = ShowdownRound.DealDealerCard(state);
            state = ShowdownRound.ApplyMove(state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Forge, ForgeDiscardIndex = 2, Pressure = false });
            state = HoldEveryoneElse(state);
            state = ShowdownRound.DealDealerCard(state);
            var last = state;
            TestSupport.AssertThrowsMatching(
                () => ShowdownRound.ApplyMove(
                    last, 0, new ShowdownMove { Kind = ShowdownMoveKind.Forge, ForgeDiscardIndex = 3, Pressure = false }),
                "no forges left");
        }

        [Test]
        public void OnlyAllowsPressureInTheThirdActionRound()
        {
            var state = FirstAction();
            TestSupport.AssertThrowsMatching(
                () => ShowdownRound.ApplyMove(state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Hold, Pressure = true }),
                "card 3");
        }

        [Test]
        public void NeverLetsABailCarryPressure()
        {
            var state = FirstAction();
            state = HoldEveryoneElse(state);
            state = ShowdownRound.ApplyMove(state, 0, Hold);
            state = ShowdownRound.DealDealerCard(state);
            state = HoldEveryoneElse(state);
            state = ShowdownRound.ApplyMove(state, 0, Hold);
            state = ShowdownRound.DealDealerCard(state);
            var last = state;
            TestSupport.AssertThrowsMatching(
                () => ShowdownRound.ApplyMove(last, 0, new ShowdownMove { Kind = ShowdownMoveKind.Bail, Pressure = true }),
                "pressure");
        }

        [Test]
        public void HandsTheBailRefundOverImmediatelyAndPoursTheStakeIntoThePot()
        {
            var state = FirstAction();
            var before = ShowdownRound.HumanSeat(state);
            var bailed = ShowdownRound.ApplyMove(state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Bail, Pressure = false });
            var after = ShowdownRound.HumanSeat(bailed);
            Assert.AreEqual(SeatStatus.Bailed, after.Status);
            Assert.AreEqual(Ante, after.Refund);
            Assert.AreEqual(before.Stack + Ante, after.Stack);
            Assert.AreEqual(state.HouseNet - Ante, bailed.HouseNet);
            Assert.AreEqual(state.TablePot + before.DealerStake, bailed.TablePot);
        }

        [Test]
        public void IgnoresASecondMoveInTheSameRound()
        {
            var state = FirstAction();
            state = ShowdownRound.ApplyMove(state, 0, Hold);
            Assert.AreSame(state, ShowdownRound.ApplyMove(state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Bail, Pressure = false }));
        }

        [Test]
        public void IgnoresAMoveFromABailedSeat()
        {
            var state = FirstAction();
            state = ShowdownRound.ApplyMove(state, 0, new ShowdownMove { Kind = ShowdownMoveKind.Bail, Pressure = false });
            state = HoldEveryoneElse(state);
            state = ShowdownRound.DealDealerCard(state);
            Assert.AreSame(state, ShowdownRound.ApplyMove(state, 0, Hold));
        }

        [Test]
        public void TreatsASettleOutsideTheRevealAsANoOp()
        {
            var state = FirstAction();
            Assert.AreSame(state, ShowdownRound.SettleShowdown(state));
        }
    }
}
