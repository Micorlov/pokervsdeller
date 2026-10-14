#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Showdown.Domain;

namespace Showdown.Tests.Domain
{
    /// <summary>Shared builders and assertions for the domain tests.</summary>
    internal static class TestSupport
    {
        private static Suit SuitOf(char letter)
        {
            switch (letter)
            {
                case 's': return Suit.Spades;
                case 'h': return Suit.Hearts;
                case 'd': return Suit.Diamonds;
                case 'c': return Suit.Clubs;
                default: throw new ArgumentException("Unknown suit letter " + letter);
            }
        }

        private static int RankOf(char symbol)
        {
            switch (symbol)
            {
                case 'T': return 10;
                case 'J': return 11;
                case 'Q': return 12;
                case 'K': return 13;
                case 'A': return 14;
                default: return symbol - '0';
            }
        }

        /// <summary>"As Kd 7h 4c 2s" becomes five cards (any count works).</summary>
        public static List<Card> Hand(string spec) =>
            spec.Split(' ')
                .Select(token =>
                {
                    var rank = RankOf(token[0]);
                    var suit = SuitOf(token[1]);
                    return new Card { Rank = rank, Suit = suit, Id = Cards.CardId(rank, suit) };
                })
                .ToList();

        /// <summary>Runs the action, which must throw an exception whose message matches the regex.</summary>
        public static void AssertThrowsMatching(Action action, string pattern)
        {
            var thrown = Assert.Catch<Exception>(() => action());
            StringAssert.IsMatch(pattern, thrown.Message);
        }

        /// <summary>Structural equality, like Jest's toEqual: records, lists and dictionaries are compared by content.</summary>
        public static void AssertDeepEqual(object? expected, object? actual)
        {
            var difference = FindDifference(expected, actual, "$");
            if (difference != null)
            {
                Assert.Fail(difference);
            }
        }

        private static string? FindDifference(object? expected, object? actual, string path)
        {
            if (expected == null || actual == null)
            {
                return expected == actual ? null : $"{path}: expected {expected ?? "null"} but was {actual ?? "null"}";
            }

            var type = expected.GetType();
            if (expected is string || type.IsPrimitive || type.IsEnum || expected is decimal)
            {
                return Equals(expected, actual) ? null : $"{path}: expected {expected} but was {actual}";
            }

            if (expected is Delegate)
            {
                return ReferenceEquals(expected, actual) ? null : $"{path}: different delegates";
            }

            if (expected is IDictionary expectedMap && actual is IDictionary actualMap)
            {
                if (expectedMap.Count != actualMap.Count)
                {
                    return $"{path}: expected {expectedMap.Count} entries but was {actualMap.Count}";
                }

                foreach (var key in expectedMap.Keys)
                {
                    if (!actualMap.Contains(key))
                    {
                        return $"{path}[{key}]: missing";
                    }

                    var inner = FindDifference(expectedMap[key], actualMap[key], $"{path}[{key}]");
                    if (inner != null) return inner;
                }

                return null;
            }

            if (expected is IEnumerable expectedSeq && actual is IEnumerable actualSeq)
            {
                var left = expectedSeq.Cast<object?>().ToList();
                var right = actualSeq.Cast<object?>().ToList();
                if (left.Count != right.Count)
                {
                    return $"{path}: expected {left.Count} items but was {right.Count}";
                }

                for (var index = 0; index < left.Count; index += 1)
                {
                    var inner = FindDifference(left[index], right[index], $"{path}[{index}]");
                    if (inner != null) return inner;
                }

                return null;
            }

            if (actual.GetType() != type)
            {
                return $"{path}: expected {type.Name} but was {actual.GetType().Name}";
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                var inner = FindDifference(property.GetValue(expected), property.GetValue(actual), path + "." + property.Name);
                if (inner != null) return inner;
            }

            return null;
        }
    }
}
