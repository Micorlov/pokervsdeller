#nullable enable
using System;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    [TestFixture]
    public class EvaluateHandTests
    {
        [TestCase("As Ks Qs Js Ts", HandCategory.RoyalFlush)]
        [TestCase("9h 8h 7h 6h 5h", HandCategory.StraightFlush)]
        [TestCase("7c 7d 7h 7s 2c", HandCategory.Quads)]
        [TestCase("4c 4d 4h 9s 9c", HandCategory.FullHouse)]
        [TestCase("Ad Jd 8d 5d 2d", HandCategory.Flush)]
        [TestCase("8c 7d 6h 5s 4c", HandCategory.Straight)]
        [TestCase("Qc Qd Qh 3s 2c", HandCategory.Trips)]
        [TestCase("Kc Kd 5h 5s 9c", HandCategory.TwoPair)]
        [TestCase("Ac Ad 9h 5s 2c", HandCategory.Pair)]
        [TestCase("Ac Kd 9h 5s 2c", HandCategory.HighCard)]
        public void ReadsHandAsCategory(string spec, HandCategory category)
        {
            Assert.AreEqual(category, HandRanks.EvaluateHand(TestSupport.Hand(spec)).Category);
        }

        [Test]
        public void NamesEveryCategoryForTheBadge()
        {
            Assert.AreEqual("Full House", HandRanks.HandName(HandRanks.EvaluateHand(TestSupport.Hand("4c 4d 4h 9s 9c"))));
            Assert.AreEqual("High Card", HandRanks.HandName(HandRanks.EvaluateHand(TestSupport.Hand("Ac Kd 9h 5s 2c"))));
        }

        [Test]
        public void RejectsAHandThatIsNotFiveCards()
        {
            TestSupport.AssertThrowsMatching(() => HandRanks.EvaluateHand(TestSupport.Hand("Ac Kd 9h 5s")), "5 cards");
        }

        [Test]
        public void KeepsTheOriginalDisplayNamesAndIdsForEveryCategory()
        {
            var names = new[]
            {
                "High Card", "Pair", "Two Pair", "Three of a Kind", "Straight",
                "Flush", "Full House", "Four of a Kind", "Straight Flush", "Royal Flush",
            };
            var ids = new[]
            {
                "highCard", "pair", "twoPair", "trips", "straight",
                "flush", "fullHouse", "quads", "straightFlush", "royalFlush",
            };
            for (var index = 0; index < names.Length; index += 1)
            {
                var rank = new HandRank { Category = (HandCategory)index };
                Assert.AreEqual(names[index], HandRanks.HandName(rank));
                Assert.AreEqual(ids[index], HandRanks.CategoryId((HandCategory)index));
                Assert.IsTrue(HandRanks.TryParseCategoryId(ids[index], out var parsed));
                Assert.AreEqual((HandCategory)index, parsed);
            }

            Assert.IsFalse(HandRanks.TryParseCategoryId("notAHand", out _));
        }
    }

    [TestFixture]
    public class TheWheelTests
    {
        [Test]
        public void ReadsAceToFiveAsAFiveHighStraight()
        {
            var wheel = HandRanks.EvaluateHand(TestSupport.Hand("Ac 2d 3h 4s 5c"));
            Assert.AreEqual(HandCategory.Straight, wheel.Category);
            CollectionAssert.AreEqual(new[] { 5 }, wheel.Tiebreak);
        }

        [Test]
        public void ReadsASuitedWheelAsAStraightFlushNotARoyal()
        {
            var steel = HandRanks.EvaluateHand(TestSupport.Hand("Ah 2h 3h 4h 5h"));
            Assert.AreEqual(HandCategory.StraightFlush, steel.Category);
            CollectionAssert.AreEqual(new[] { 5 }, steel.Tiebreak);
        }

        [Test]
        public void LosesToASixHighStraightBecauseTheAcePlaysLow()
        {
            var wheel = HandRanks.EvaluateHand(TestSupport.Hand("Ac 2d 3h 4s 5c"));
            var sixHigh = HandRanks.EvaluateHand(TestSupport.Hand("6c 5d 4h 3s 2c"));
            Assert.Greater(HandRanks.CompareHands(sixHigh, wheel), 0);
        }

        [Test]
        public void IsBeatenByAWheelThatIsAlsoAFlush()
        {
            Assert.Greater(
                HandRanks.CompareHands(
                    HandRanks.EvaluateHand(TestSupport.Hand("Ah 2h 3h 4h 5h")),
                    HandRanks.EvaluateHand(TestSupport.Hand("Ac 2d 3h 4s 5c"))),
                0);
        }
    }

    [TestFixture]
    public class TiebreakTests
    {
        [Test]
        public void OrdersEqualPairsByTheirKickersInTurn()
        {
            var better = HandRanks.EvaluateHand(TestSupport.Hand("Ac Ad Kh 5s 2c"));
            var worse = HandRanks.EvaluateHand(TestSupport.Hand("Ac Ad Qh 5s 2c"));
            Assert.Greater(HandRanks.CompareHands(better, worse), 0);
        }

        [Test]
        public void ReadsTwoPairAsHighPairLowPairKicker()
        {
            CollectionAssert.AreEqual(
                new[] { 13, 5, 9 },
                HandRanks.EvaluateHand(TestSupport.Hand("Kc Kd 5h 5s 9c")).Tiebreak);
            var better = HandRanks.EvaluateHand(TestSupport.Hand("Kc Kd 5h 5s 9c"));
            var worse = HandRanks.EvaluateHand(TestSupport.Hand("Kc Kd 5h 5s 8c"));
            Assert.Greater(HandRanks.CompareHands(better, worse), 0);
        }

        [Test]
        public void ReadsAFullHouseAsTripsOverPair()
        {
            CollectionAssert.AreEqual(
                new[] { 4, 9 },
                HandRanks.EvaluateHand(TestSupport.Hand("4c 4d 4h 9s 9c")).Tiebreak);
            Assert.Greater(
                HandRanks.CompareHands(
                    HandRanks.EvaluateHand(TestSupport.Hand("9c 9d 9h 4s 4c")),
                    HandRanks.EvaluateHand(TestSupport.Hand("4c 4d 4h 9s 9c"))),
                0);
        }

        [Test]
        public void RanksAFlushByAllFiveCardsHighToLow()
        {
            CollectionAssert.AreEqual(
                new[] { 14, 11, 8, 5, 2 },
                HandRanks.EvaluateHand(TestSupport.Hand("Ad Jd 8d 5d 2d")).Tiebreak);
        }

        [Test]
        public void CallsAGenuineTieATie()
        {
            Assert.AreEqual(
                0,
                HandRanks.CompareHands(
                    HandRanks.EvaluateHand(TestSupport.Hand("Ac Kd 9h 5s 2c")),
                    HandRanks.EvaluateHand(TestSupport.Hand("Ah Ks 9c 5d 2s"))));
        }
    }

    [TestFixture]
    public class CompareHandsTests
    {
        private static readonly string[] Specs =
        {
            "As Ks Qs Js Ts",
            "9h 8h 7h 6h 5h",
            "7c 7d 7h 7s 2c",
            "4c 4d 4h 9s 9c",
            "Ad Jd 8d 5d 2d",
            "8c 7d 6h 5s 4c",
            "Qc Qd Qh 3s 2c",
            "Kc Kd 5h 5s 9c",
            "Ac Ad 9h 5s 2c",
            "Ac Kd 9h 5s 2c",
        };

        [Test]
        public void IsAntisymmetricAcrossEveryPairOfHands()
        {
            foreach (var left in Specs)
            {
                foreach (var right in Specs)
                {
                    var a = HandRanks.EvaluateHand(TestSupport.Hand(left));
                    var b = HandRanks.EvaluateHand(TestSupport.Hand(right));
                    Assert.AreEqual(0, Math.Sign(HandRanks.CompareHands(a, b)) + Math.Sign(HandRanks.CompareHands(b, a)));
                }
            }
        }

        [Test]
        public void RanksTheTenCategoriesInThePublishedOrder()
        {
            var ordered = new HandRank[Specs.Length];
            for (var index = 0; index < Specs.Length; index += 1)
            {
                ordered[index] = HandRanks.EvaluateHand(TestSupport.Hand(Specs[index]));
            }

            for (var index = 1; index < ordered.Length; index += 1)
            {
                Assert.Greater(
                    HandRanks.CategoryOrder(ordered[index - 1].Category),
                    HandRanks.CategoryOrder(ordered[index].Category));
                Assert.Greater(HandRanks.CompareHands(ordered[index - 1], ordered[index]), 0);
            }
        }
    }
}
