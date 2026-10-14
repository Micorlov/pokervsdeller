using System;
using System.Collections;
using Showdown.Domain;
using Showdown.Game;
using UnityEngine;

namespace Showdown.Qa
{
    /// <summary>
    /// Scripted app states for visual QA, selected with <c>--scenario NAME</c> together with <c>--capture out.png</c>.
    /// Each scenario drives the real app through <see cref="AppServices"/> (so the table plays by its own rules)
    /// and calls <see cref="QaCapture.Ready"/> once the screen under test is showing. Hands are dealt from a
    /// pinned RNG seed, so every run produces the same cards.
    /// </summary>
    public static class QaScenarios
    {
        /// <summary>Fixed so the same cards come out every run; the hand is reproducible, not special.</summary>
        private const uint Seed = 20260514;
        private const float StepTimeoutSeconds = 40f;

        public static string Requested => QaCapture.ArgumentValue("--scenario");

        public static IEnumerator Run(string name)
        {
            // Let the shell build and the stores load before driving anything.
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Select(name);
        }

        private static IEnumerator Select(string name)
        {
            switch (name)
            {
                case "home": return Navigate(ScreenName.Home);
                case "stats": return Navigate(ScreenName.Stats);
                case "settings": return Navigate(ScreenName.Settings);
                case "howto": return Navigate(ScreenName.HowToPlay);
                case "table-kicker": return TableKicker();
                case "table-kicker-picked": return TableKickerPicked();
                case "table-climb": return TableClimb(1);
                case "table-pressure": return TableClimb(ShowdownPayouts.PressureRound);
                case "table-forge": return TableForge();
                case "table-bailed": return TableBailed();
                case "table-result": return TableResult();
                case "stats-played": return StatsAfterHand();
                default:
                    Debug.LogError("Showdown QA: unknown scenario '" + name + "'.");
                    return Done();
            }
        }

        // ---- plain screens -------------------------------------------------------------------------

        private static IEnumerator Navigate(ScreenName screen)
        {
            AppServices.Navigator.Go(screen);
            QaCapture.Ready();
            yield break;
        }

        private static IEnumerator Done()
        {
            QaCapture.Ready();
            yield break;
        }

        // ---- table states --------------------------------------------------------------------------

        private static IEnumerator Deal()
        {
            var table = AppServices.Table;
            table.PinNextSeed(Seed);
            table.StartHand(AppServices.Settings.Current.Ante);
            AppServices.Navigator.Go(ScreenName.Table);
            yield return WaitFor(() => table.Round != null && table.Round.Phase == ShowdownPhase.Kicker);
        }

        private static IEnumerator TableKicker()
        {
            yield return Deal();
            QaCapture.Ready();
        }

        private static IEnumerator TableKickerPicked()
        {
            yield return Deal();
            AppServices.Table.SelectKicker(2);
            QaCapture.Ready();
        }

        private static IEnumerator LockKicker()
        {
            yield return Deal();
            AppServices.Table.SelectKicker(2);
            yield return new WaitForSecondsRealtime(0.3f);
            AppServices.Table.LockKicker();
        }

        /// <summary>Holds through the early rounds and stops while the human owes a move after <paramref name="dealerCards"/> dealer cards.</summary>
        private static IEnumerator TableClimb(int dealerCards)
        {
            yield return LockKicker();
            while (true)
            {
                yield return WaitFor(() => HumanOwesMove(dealerCards, atLeast: true));
                var round = AppServices.Table.Round;
                if (round.DealerCards.Count >= dealerCards) break;
                AppServices.Table.Hold();
            }
            yield return new WaitForSecondsRealtime(0.4f);
            QaCapture.Ready();
        }

        private static IEnumerator TableForge()
        {
            yield return TableClimb(2);
            AppServices.Table.BeginForge();
            yield return new WaitForSecondsRealtime(0.3f);
            QaCapture.Ready();
        }

        private static IEnumerator TableBailed()
        {
            yield return TableClimb(2);
            AppServices.Table.Bail();
            yield return new WaitForSecondsRealtime(0.8f);
            QaCapture.Ready();
        }

        private static IEnumerator TableResult()
        {
            yield return LockKicker();
            var table = AppServices.Table;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (table.Round != null && table.Round.Phase != ShowdownPhase.Settled && Time.realtimeSinceStartup < deadline)
            {
                if (HumanOwesMove(0, atLeast: true)) table.Hold();
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.2f);
            QaCapture.Ready();
        }

        /// <summary>Plays one hand through to its result, then opens the record so the screen has real numbers.</summary>
        private static IEnumerator StatsAfterHand()
        {
            yield return TableResult();
            AppServices.Table.LeaveTable();
            AppServices.Navigator.Go(ScreenName.Stats);
        }

        // ---- helpers -------------------------------------------------------------------------------

        private static bool HumanOwesMove(int dealerCards, bool atLeast)
        {
            var round = AppServices.Table.Round;
            if (round == null || round.Phase != ShowdownPhase.Actions) return false;
            var human = ShowdownRound.HumanSeat(round);
            if (human.Status != SeatStatus.Active || human.LastMove != null) return false;
            return atLeast ? round.DealerCards.Count >= dealerCards : round.DealerCards.Count == dealerCards;
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!condition()) Debug.LogError("Showdown QA: timed out waiting for a scenario step.");
        }
    }
}
