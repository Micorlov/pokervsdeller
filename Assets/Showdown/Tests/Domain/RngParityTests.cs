#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    /// <summary>
    /// Values pinned from the TypeScript original (src/domain run under Node), so a drift in the
    /// RNG, the shuffle, the bot arithmetic or the reducers shows up as a failing test rather than
    /// a quietly different game. The TypeScript suite itself pins no seeded outcomes.
    /// </summary>
    [TestFixture]
    public class RngParityTests
    {
        [TestCase(1, 0.6270739405881613, 0.002735721180215478, 0.5274470399599522, 0.9810509674716741, 0.9683778982143849, 0.281103502959013)]
        [TestCase(2, 0.7342509443406016, 0.32499843230471015, 0.28529605525545776, 0.5379551574587822, 0.8752879470121115, 0.6308333419729024)]
        [TestCase(75, 0.2425106316804886, 0.04455898655578494, 0.8669660727027804, 0.000024772249162197113, 0.39284360548481345, 0.6153577708173543)]
        public void Mulberry32ReproducesTheOriginalSequenceBitForBit(
            int seed, double v1, double v2, double v3, double v4, double v5, double v6)
        {
            var rng = Rng.Mulberry32(seed);
            var expected = new[] { v1, v2, v3, v4, v5, v6 };
            foreach (var value in expected)
            {
                Assert.AreEqual(value, rng());
            }
        }

        [Test]
        public void ANegativeSeedWrapsLikeTheOriginalsUnsignedShift()
        {
            var signed = Rng.Mulberry32(-1);
            var unsigned = Rng.Mulberry32(uint.MaxValue);
            for (var i = 0; i < 10; i += 1)
            {
                Assert.AreEqual(unsigned(), signed());
            }
        }

        [Test]
        public void ShuffleDealsTheSameDeckFromTheSameSeed()
        {
            const string expected =
                "13h,7c,12h,12s,6c,7d,13c,4c,8s,5d,11d,11h,3c,10d,13s,5s,12c,9d,10s,3h,6s,3d,8c,14d,6d,9s,"
                + "10h,5h,14s,3s,12d,13d,14h,10c,4s,6h,2c,11s,4h,7s,9h,8h,5c,7h,14c,4d,2h,9c,11c,2d,2s,8d";
            var deck = Rng.Shuffle(Cards.CreateDeck(), Rng.Mulberry32(1));
            Assert.AreEqual(expected, string.Join(",", deck.Select(card => card.Id)));
        }

        [Test]
        public void ShuffleLeavesItsInputUntouched()
        {
            var original = Cards.CreateDeck();
            var before = string.Join(",", original.Select(card => card.Id));
            Rng.Shuffle(original, Rng.Mulberry32(9));
            Assert.AreEqual(before, string.Join(",", original.Select(card => card.Id)));
        }

        [Test]
        public void ThreatScoresMatchToTheLastBit()
        {
            var deck = Rng.Shuffle(Cards.CreateDeck(), Rng.Mulberry32(1));
            var expected = new[]
            {
                0.23214285714285715, 0.19285714285714287, 0.1773809523809524, 0.4589285714285714, 0.3342857142857143,
            };
            for (var count = 1; count <= 5; count += 1)
            {
                Assert.AreEqual(expected[count - 1], Threat.PartialThreat(deck.Take(count).ToList()));
            }

            Assert.AreEqual(0.037142857142857144, Threat.HandStrengthScore(HandRanks.EvaluateHand(deck.Skip(5).Take(5).ToList())));
        }

        private static string Describe(ShowdownMove move) =>
            move.Kind.ToString().ToLowerInvariant() + "/" + (move.Pressure ? 1 : 0) + "/"
            + (move.ForgeDiscardIndex.HasValue ? move.ForgeDiscardIndex.Value.ToString() : "-");

        [TestCase(1, "hold/0/- hold/0/- hold/0/- hold/0/- hold/0/- hold/0/- hold/1/- forge/0/2")]
        [TestCase(2, "hold/0/- hold/0/- hold/0/- bail/0/- hold/0/- hold/0/- hold/1/- hold/0/-")]
        public void BotDecisionsMatchTheOriginalOnTheSameSeed(int seed, string expected)
        {
            var deck = Rng.Shuffle(Cards.CreateDeck(), Rng.Mulberry32(seed));
            var hand = deck.Skip(10).Take(5).ToList();
            var seat = new ShowdownBotSeat
            {
                Cards = hand,
                KickerIndex = Bots.ChooseBotKicker(hand),
                ForgesUsed = seed % 3,
                Pressured = seed % 5 == 0,
                Stack = (seed * 13) % 60,
            };

            var moves = new List<string>();
            foreach (var persona in Bots.BotPersonas)
            {
                for (var round = 1; round <= 4; round += 1)
                {
                    var move = Bots.DecideShowdownMove(
                        seat, deck.Skip(20).Take(round).ToList(), round, 10, persona, Rng.Mulberry32(seed * 7 + round));
                    moves.Add(Describe(move));
                }
            }

            Assert.AreEqual(expected, string.Join(" ", moves));
        }

        private static string Summary(ShowdownRoundState settled) =>
            "dealer=" + string.Join(",", settled.DealerCards.Select(card => card.Id))
            + " stacks=" + string.Join(",", settled.Seats.Select(seat => seat.Stack))
            + " house=" + settled.HouseNet
            + " nets=" + string.Join(",", settled.Seats.Select(seat => seat.Settlement!.Net));

        [TestCase(2, "dealer=11d,3d,11s,6d,12s stacks=480,490,540 house=-10 nets=-20,-10,40", true)]
        [TestCase(2, "dealer=11d,8h,9s,12s,4d stacks=530,490,660 house=-180 nets=30,-10,160", false)]
        [TestCase(7, "dealer=6c,13s,11h,12d,14d stacks=500,485,610 house=-95 nets=0,-15,110", true)]
        [TestCase(7, "dealer=6c,12c,14s,14d,9c stacks=450,485,580 house=-15 nets=-50,-15,80", false)]
        public void WholeHandsSettleExactlyAsTheOriginalDid(int seed, string expected, bool alwaysHold)
        {
            var settled = RoundHarness.PlayHand(seed, alwaysHold ? RoundHarness.AlwaysHold : RoundHarness.Adventurous);
            Assert.AreEqual(expected, Summary(settled));
        }
    }
}
