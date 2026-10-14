#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class CueAndHapticTests
    {
        [Test]
        public void Every_cue_has_a_spec()
        {
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
            {
                Assert.IsNotNull(CueSpecs.For(cue), cue.ToString());
            }

            Assert.AreEqual(Enum.GetValues(typeof(Cue)).Length, CueSpecs.All.Count());
        }

        [TestCase(Cue.Deal, "deal", 0.5f, HapticKind.Light)]
        [TestCase(Cue.Flip, "flip", 0.4f, HapticKind.Selection)]
        [TestCase(Cue.Chip, "chip", 0.45f, HapticKind.Light)]
        [TestCase(Cue.Raise, "raise", 0.6f, HapticKind.Medium)]
        [TestCase(Cue.Fold, "fold", 0.5f, HapticKind.Medium)]
        [TestCase(Cue.Reveal, "reveal", 0.6f, HapticKind.Medium)]
        [TestCase(Cue.Win, "win", 0.65f, HapticKind.Success)]
        [TestCase(Cue.WinBig, "winBig", 0.7f, HapticKind.Success)]
        [TestCase(Cue.Lose, "lose", 0.6f, HapticKind.Warning)]
        [TestCase(Cue.Push, "push", 0.4f, HapticKind.Light)]
        [TestCase(Cue.Bust, "bust", 0.7f, HapticKind.Error)]
        [TestCase(Cue.Tap, "tap", 0.28f, HapticKind.Light)]
        public void Specs_match_the_typescript_cue_table(Cue cue, string resource, float volume, HapticKind haptic)
        {
            var spec = CueSpecs.For(cue);

            Assert.AreEqual(resource, spec.ResourceName);
            Assert.AreEqual(volume, spec.Volume, 1e-6f);
            Assert.AreEqual(haptic, spec.Haptic);
        }

        [Test]
        public void Tap_is_the_quietest_cue()
        {
            var quietest = CueSpecs.All.OrderBy(cue => CueSpecs.For(cue).Volume).First();

            Assert.AreEqual(Cue.Tap, quietest);
        }

        [Test]
        public void Every_haptic_kind_has_a_well_formed_pattern()
        {
            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                var pattern = HapticPatterns.For(kind);

                Assert.AreEqual(pattern.TimingsMs.Count, pattern.Amplitudes.Count, kind.ToString());
                Assert.AreEqual(0, pattern.TimingsMs.Count % 2, "off/on pairs: " + kind);
                Assert.IsTrue(pattern.TimingsMs.Any(ms => ms > 0), kind.ToString());
                Assert.IsTrue(pattern.Amplitudes.All(a => a >= 0 && a <= 255), kind.ToString());
                for (var i = 0; i < pattern.TimingsMs.Count; i += 2)
                {
                    Assert.AreEqual(0, pattern.Amplitudes[i], "off periods are silent: " + kind);
                }
            }
        }

        [Test]
        public void Stronger_haptics_are_never_softer_than_lighter_ones()
        {
            long Peak(HapticKind kind) => HapticPatterns.For(kind).Amplitudes.Max();

            Assert.Less(Peak(HapticKind.Selection), Peak(HapticKind.Light));
            Assert.Less(Peak(HapticKind.Light), Peak(HapticKind.Medium));
            Assert.Less(Peak(HapticKind.Medium), Peak(HapticKind.Heavy));
        }
    }
}
