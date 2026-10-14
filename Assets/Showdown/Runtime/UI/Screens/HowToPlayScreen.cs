using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The rules (HowToPlayScreen.tsx). Every payout and persona is read off the domain rather than restated,
    /// so the table can change size without the copy going stale. The seat count is the one number that needs spelling out.
    /// </summary>
    public sealed class HowToPlayScreen : VisualElement
    {
        private static readonly string[] SeatWords = { "zero", "one", "two", "three", "four", "five", "six" };

        private const float StepBadgeSize = 28;
        private const float PayRowLabelSize = 14;
        private const float NoteSize = 12.5f;
        private const float NoteLineHeight = 18;
        private const float StepBodyLineHeight = 19;
        private static readonly Color PayRowDivider = Theme.Rgba(36, 52, 71, 0.1f);

        private readonly struct Step
        {
            public readonly string Title;
            public readonly string Body;

            public Step(string title, string body)
            {
                Title = title;
                Body = body;
            }
        }

        private readonly struct PayLine
        {
            public readonly string Name;
            public readonly string Pays;

            public PayLine(string name, string pays)
            {
                Name = name;
                Pays = pays;
            }
        }

        public HowToPlayScreen(Action onBack)
        {
            style.flexGrow = 1;
            style.flexShrink = 1;

            var scroll = ScreenLayout.Page(out var column);
            Add(scroll);

            var blocks = new List<VisualElement> { BuildTitle() };
            var steps = Steps();
            for (int i = 0; i < steps.Count; i++) blocks.Add(BuildStep(i + 1, steps[i]));
            var payouts = BuildPayouts();
            var regulars = BuildRegulars();
            blocks.Add(payouts);
            blocks.Add(regulars);
            blocks.Add(new PubButton("Back", onBack, PubButtonVariant.Secondary));

            ScreenLayout.Stack(column, blocks.ToArray());
            // The rules cards sit a little further from what is above them than the 16px rhythm.
            payouts.style.marginTop = Theme.Spacing.Md + Theme.Spacing.Xs;
            regulars.style.marginTop = Theme.Spacing.Md + Theme.Spacing.Xs;
            for (int i = 0; i < blocks.Count; i++) Anim.Reveal(blocks[i], i);
        }

        private static string SeatWord()
        {
            int seats = ShowdownRound.SeatCount;
            return seats >= 0 && seats < SeatWords.Length ? SeatWords[seats] : seats.ToString(CultureInfo.InvariantCulture);
        }

        private static VisualElement BuildTitle()
        {
            var block = Ui.View();
            block.Add(ScreenLayout.Title("How to play"));
            var subtitle = Ui.Text("Showdown — " + SeatWord() + " seats against the house's climb.", 13.5f, Theme.Colors.TextMuted);
            subtitle.style.marginTop = 4;
            block.Add(subtitle);
            return block;
        }

        private static IReadOnlyList<Step> Steps()
        {
            return new[]
            {
                new Step("Two antes, two battles",
                    "Every seat puts one ante into the Table Pot and stakes another against the house. You are fighting the dealer for your stake and the whole table for the pot — and the pot swallows the chips of everyone who loses along the way."),
                new Step("Pick your kicker",
                    "You get five cards; the dealer starts with none. Before the climb, secretly mark one card as your kicker. If its rank matches the dealer's final card and you beat the dealer, your winnings double — a Kicker Strike."),
                new Step("The house's climb",
                    "The dealer builds a hand one face-up card at a time. After each of the first four cards you choose: HOLD and stay in, FORGE a card away for a fresh one from the deck (one ante into the pot, "
                    + ShowdownPayouts.ForgeLimit + " per hand, never the kicker), or BAIL and take half of everything you have put in straight back — the stake you abandon falls into the Table Pot."),
                new Step("The pressure round",
                    "After the dealer's third card only, you may double your stake against the house. Beat the dealer from there and the doubled stake pays "
                    + ShowdownPayouts.PressureOdds + " to 1 instead of even money."),
                new Step("Showdown",
                    "The fifth card ends the climb. Beat the dealer's five and your stake pays out; every beaten stake joins the pot, and the best hand left at the table takes all of it — antes, forge fees, and everything the losers left behind. Do both outright and that is a Sweep — "
                    + ShowdownPayouts.SweepBonusUnits + " antes extra, on the house."),
            };
        }

        private static VisualElement BuildStep(int number, Step step)
        {
            var row = Ui.View().Row().AlignItems(Align.FlexStart);

            var badge = Ui.View().Size(StepBadgeSize, StepBadgeSize).Radius(StepBadgeSize / 2f).Bg(Theme.Colors.GreenDim).Center();
            badge.Add(Ui.Text(number.ToString(CultureInfo.InvariantCulture), 13, Theme.Colors.GreenLight, true,
                TextAnchor.MiddleCenter, 0f, false));
            row.Add(badge);

            var text = Ui.View().Flex();
            text.style.marginLeft = Theme.Spacing.Md;
            text.Add(Ui.Text(step.Title, Theme.Type.Body, Theme.Colors.Text, true));
            var body = Ui.Caption(step.Body).SetLineHeight(Theme.Type.Caption, StepBodyLineHeight);
            body.style.marginTop = 3;
            text.Add(body);
            row.Add(text);
            return row;
        }

        /// <summary>Book order: the streak counts that pay, lowest first.</summary>
        private static IReadOnlyList<PayLine> PayLines()
        {
            var thresholds = StreakBonus.StreakBonusUnitsTable.Keys.OrderBy(streak => streak).ToList();
            string streaks = string.Join("/", thresholds);
            string units = string.Join("/", thresholds.Select(streak => StreakBonus.StreakBonusUnitsTable[streak]));
            return new[]
            {
                new PayLine("Beat the dealer", "1 to 1"),
                new PayLine("Beat the dealer, pressured", ShowdownPayouts.PressureOdds + " to 1"),
                new PayLine("Kicker Strike", "×2 winnings"),
                new PayLine("Best hand at the table", "the Table Pot"),
                new PayLine("Sweep (both, outright)", "+" + ShowdownPayouts.SweepBonusUnits + " antes"),
                new PayLine("Win streak (" + streaks + ")", "+" + units + " antes"),
                new PayLine("Bail, any time you act", "half back"),
            };
        }

        private static VisualElement BuildPayouts()
        {
            var card = Panels.Cream();
            card.Add(Panels.CreamLabel("The payouts").MarginBottom(Theme.Spacing.Xs));
            foreach (var line in PayLines()) card.Add(BuildPayRow(line));
            card.Add(CreamNote("Pressure and a Kicker Strike stack: a pressured strike pays six times the stake. A tie with the dealer pushes your stake back untouched."));
            return card;
        }

        private static VisualElement BuildPayRow(PayLine line)
        {
            var row = Ui.View().Row().Justify(Justify.SpaceBetween).AlignItems(Align.Center)
                .Pad(5, 0).BorderBottom(1, PayRowDivider);
            row.style.marginTop = Theme.Spacing.Xs;
            row.Add(Ui.Text(line.Name, PayRowLabelSize, Theme.Colors.InkText, true));
            var odds = Ui.Text(line.Pays, PayRowLabelSize, Theme.Colors.InkText, true, TextAnchor.MiddleRight);
            odds.style.marginLeft = Theme.Spacing.Sm;
            row.Add(odds);
            return row;
        }

        private static VisualElement BuildRegulars()
        {
            var card = Panels.Cream();
            card.Add(Panels.CreamLabel("The regulars").MarginBottom(Theme.Spacing.Xs));
            foreach (var persona in Bots.BotPersonas)
            {
                var row = Ui.View().Pad(5, 0);
                row.style.marginTop = Theme.Spacing.Xs;
                row.Add(Ui.Text(persona.Name, PayRowLabelSize, Theme.Colors.InkText, true));
                row.Add(Ui.Text(persona.Style, NoteSize, Theme.Colors.InkTextMuted));
                card.Add(row);
            }
            card.Add(CreamNote("They play their own money and quietly buy back in when they bust — by design, their stacks reset each time you open the app. Only your bankroll is saved between sessions, and it can always be topped up by "
                + Bankroll.ReUpAmount + " on the house."));
            return card;
        }

        private static Label CreamNote(string text)
        {
            var note = Ui.Text(text, NoteSize, Theme.Colors.InkTextMuted).SetLineHeight(NoteSize, NoteLineHeight);
            note.style.marginTop = Theme.Spacing.Sm + Theme.Spacing.Xs;
            return note;
        }
    }
}
