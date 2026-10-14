#nullable enable
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class PartialThreatTests
    {
        [Test]
        public void ScoresAnEmptyDealerHandAsNoThreatAtAll()
        {
            Assert.AreEqual(0.0, Threat.PartialThreat(new Card[0]));
        }

        [Test]
        public void StaysInsideTheZeroToOneMeterAtEveryHandSize()
        {
            var climbs = new[] { "Ah", "Ah Ad", "Ah Ad As", "Ah Ad As Ac", "Ah Ad As Ac Kh" };
            foreach (var spec in climbs)
            {
                var score = Threat.PartialThreat(TestSupport.Hand(spec));
                Assert.Greater(score, 0.0);
                Assert.LessOrEqual(score, 1.0);
            }
        }

        [Test]
        public void RatesAVisiblePairAboveRags()
        {
            Assert.Greater(Threat.PartialThreat(TestSupport.Hand("Kc Kd")), Threat.PartialThreat(TestSupport.Hand("7h 2s")));
        }

        [Test]
        public void RatesTripsAboveAPairOnTheSameBoardSize()
        {
            Assert.Greater(Threat.PartialThreat(TestSupport.Hand("9c 9d 9h")), Threat.PartialThreat(TestSupport.Hand("9c 9d 4h")));
        }

        [Test]
        public void RatesAHighSuitedCompanionAboveALowOffsuitOne()
        {
            Assert.Greater(Threat.PartialThreat(TestSupport.Hand("9h Kh")), Threat.PartialThreat(TestSupport.Hand("9h 2c")));
        }

        [Test]
        public void GrowsAsAPairingCardLands()
        {
            Assert.Greater(Threat.PartialThreat(TestSupport.Hand("Ah Ad")), Threat.PartialThreat(TestSupport.Hand("Ah")));
        }

        [Test]
        public void HandsACompleteDealerHandToTheRealEvaluator()
        {
            var royal = Threat.PartialThreat(TestSupport.Hand("As Ks Qs Js Ts"));
            var rags = Threat.PartialThreat(TestSupport.Hand("Kc 9d 7h 5s 2c"));
            Assert.Greater(royal, rags);
            Assert.AreEqual(royal, Threat.HandStrengthScore(HandRanks.EvaluateHand(TestSupport.Hand("As Ks Qs Js Ts"))));
        }
    }

    [TestFixture]
    public class HandStrengthScoreTests
    {
        [Test]
        public void OrdersFullHandsTheSameWayTheCategoryTableDoes()
        {
            var ladder = new[]
            {
                "Ac Kd 9h 5s 2c", // highCard
                "Ac Ad 9h 5s 2c", // pair
                "Kc Kd 5h 5s 9c", // twoPair
                "Qc Qd Qh 3s 2c", // trips
                "8c 7d 6h 5s 4c", // straight
                "Ad Jd 8d 5d 2d", // flush
                "4c 4d 4h 9s 9c", // fullHouse
                "7c 7d 7h 7s 2c", // quads
                "9h 8h 7h 6h 5h", // straightFlush
                "As Ks Qs Js Ts", // royalFlush
            };
            var scores = new double[ladder.Length];
            var orders = new int[ladder.Length];
            for (var index = 0; index < ladder.Length; index += 1)
            {
                var rank = HandRanks.EvaluateHand(TestSupport.Hand(ladder[index]));
                scores[index] = Threat.HandStrengthScore(rank);
                orders[index] = HandRanks.CategoryOrder(rank.Category);
            }

            for (var index = 1; index < scores.Length; index += 1)
            {
                Assert.Greater(orders[index], orders[index - 1]);
                Assert.Greater(scores[index], scores[index - 1]);
            }
        }

        [Test]
        public void NeverScoresAFinishedPairBelowTheWeightAVisiblePairCarries()
        {
            var finishedPair = Threat.HandStrengthScore(HandRanks.EvaluateHand(TestSupport.Hand("Kc Kd 9h 5s 2c")));
            Assert.GreaterOrEqual(finishedPair, Threat.MadePair);
        }

        [Test]
        public void SeparatesHandsInTheSameCategoryByTheirLeadTiebreak()
        {
            var aces = Threat.HandStrengthScore(HandRanks.EvaluateHand(TestSupport.Hand("Ac Ad 9h 5s 2c")));
            var kings = Threat.HandStrengthScore(HandRanks.EvaluateHand(TestSupport.Hand("Kc Kd 9h 5s 2c")));
            Assert.Greater(aces, kings);
        }
    }
}
