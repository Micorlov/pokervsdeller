using System;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The settled hand, told line by line on a dark sheet rising from the bottom of the table: an italic headline
    /// and the net in mono, the matchup, a ledger whose rows add up to that net, a gold card for the payout worth
    /// celebrating, what the hand did to the other seats, and one gold button. Mint is money won, rose is money
    /// lost — never alarm red. Build a new one per settled hand: the rise plays on construction.
    /// </summary>
    public sealed class ResultSheet : VisualElement
    {
        private const float SheetRadius = 20f;
        private const float RiseFrom = 40f;
        private const float LedgerSize = 16f;
        private static readonly Color LedgerInk = Theme.Hex("#E6EFE9");
        private static readonly Color Rule = Theme.Rgba(255, 255, 255, 0.12f);

        /// <param name="busted">True when the bankroll can no longer cover a hand here: the button re-ups instead.</param>
        public ResultSheet(HandReport report, bool busted, int reUpAmount, Action onNext, Action onReUp)
        {
            style.flexShrink = 1;
            style.minHeight = 0;
            this.Pad(18, 18, 16, 18).Radius(SheetRadius).Bg(Theme.Colors.Sheet).Border(2, LineFor(report.Tone));
            Add(SoftShadow.Fit(SheetRadius, -10f, 40f, 0f, Theme.Rgba(0, 0, 0, 0.6f)));

            Add(Header(report));
            Add(Matchup(report.Matchup));
            // The headline and the button always show; on a short phone the rows between them scroll.
            var body = Ui.Scroll();
            body.style.flexGrow = 0;
            body.Add(Ledger(report));
            if (report.Award != null) body.Add(Award(report.Award));
            if (report.Others.Count > 0) body.Add(Others(report));
            if (busted) body.Add(Note("Not enough for a full hand at this table"));
            Add(body);

            var button = busted
                ? new PubButton("Re-up +" + reUpAmount, onReUp, PubButtonVariant.Primary, cue: null)
                : new PubButton("Next hand", onNext, PubButtonVariant.Primary, cue: null);
            button.style.marginTop = 14;
            Add(button);

            Anim.Entrance(this, 0f, RiseFrom, 1f, Ease.Panel);
        }

        private static Color ToneInk(ReportTone tone) =>
            tone == ReportTone.Win ? Theme.Colors.Mint : tone == ReportTone.Lose ? Theme.Colors.Rose : Theme.Colors.Cream;

        private static Color LineFor(ReportTone tone) =>
            tone == ReportTone.Win ? Theme.Colors.MintLine : tone == ReportTone.Lose ? Theme.Colors.RoseLine : Theme.Colors.BorderStrong;

        private static Color AmountInk(int amount) =>
            amount > 0 ? Theme.Colors.Mint : amount < 0 ? Theme.Colors.Rose : LedgerInk;

        private static VisualElement Header(HandReport report)
        {
            var row = Ui.View().Row().AlignItems(Align.Center).Justify(Justify.SpaceBetween);
            var title = Ui.Serif(report.Title, 29, ToneInk(report.Tone), TextAnchor.MiddleLeft);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.flexShrink = 1;
            row.Add(title);
            var net = Ui.Mono(TableReport.Signed(report.Net), 34, ToneInk(report.Tone), TextAnchor.MiddleRight);
            net.style.marginLeft = Theme.Spacing.Sm;
            row.Add(net);
            return row;
        }

        private static VisualElement Matchup(string text)
        {
            var label = Ui.Mono(text, 14, Theme.Colors.CreamMuted, TextAnchor.MiddleLeft, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 6;
            label.style.paddingBottom = 10;
            label.BorderBottom(1, Rule);
            return label;
        }

        private static VisualElement Ledger(HandReport report)
        {
            var ledger = Ui.View().MarginTop(10);
            foreach (var line in report.Lines)
            {
                ledger.Add(LedgerRow(line.Label, TableReport.Signed(line.Amount), AmountInk(line.Amount), 5f));
            }
            var total = LedgerRow("Bankroll", report.Bankroll.ToString(), LedgerInk, 7f);
            total.style.paddingTop = 6;
            total.BorderTop(1, Rule);
            ledger.Add(total);
            return ledger;
        }

        private static VisualElement LedgerRow(string label, string amount, Color amountInk, float gap)
        {
            var row = Ui.View().Row().Justify(Justify.SpaceBetween).MarginTop(gap);
            row.Add(Ui.Mono(label, LedgerSize, LedgerInk));
            row.Add(Ui.Mono(amount, LedgerSize, amountInk, TextAnchor.MiddleRight));
            return row;
        }

        private static VisualElement Award(ReportAward award)
        {
            var card = Ui.View().Row().AlignItems(Align.Center).MarginTop(12).Pad(10, 14).Radius(14)
                .Border(1.5f, Theme.Colors.Gold).BgGradient(Theme.Rgba(90, 64, 22, 0.7f), Theme.Rgba(48, 32, 10, 0.7f));
            var medal = Ui.View().Size(38, 38).Radius(19).Center().BgGradient(Theme.Hex("#F6DC9B"), Theme.Hex("#C99A43"));
            medal.Add(Ui.GlyphView(Glyph.Star, 20, Theme.Colors.OrangeInk));
            card.Add(medal);

            var words = Ui.View().Flex().MarginLeft(12);
            words.Add(Ui.Text(award.Title, 18, Theme.Colors.Gold, true, TextAnchor.MiddleLeft, 0f, false));
            words.Add(Ui.Mono(award.Caption, 13, Theme.Colors.CreamMuted, TextAnchor.MiddleLeft, 1f));
            card.Add(words);
            card.Add(Ui.Mono(TableReport.Signed(award.Amount), 22, Theme.Colors.Gold, TextAnchor.MiddleRight));
            return card;
        }

        private static VisualElement Others(HandReport report)
        {
            var row = Ui.View().Row().MarginTop(10);
            for (int i = 0; i < report.Others.Count; i++)
            {
                var other = report.Others[i];
                var chip = Ui.View().Flex().Row().Center().Pad(5, 6).Radius(8).Bg(Theme.Rgba(255, 255, 255, 0.05f));
                if (i > 0) chip.style.marginLeft = Theme.Spacing.Sm;
                chip.Add(Ui.Mono(other.Name + " " + other.Verb, 14, Theme.Colors.CreamMuted));
                chip.Add(Ui.Mono(TableReport.Signed(other.Net), 14, AmountInk(other.Net)).MarginLeft(8));
                row.Add(chip);
            }
            return row;
        }

        private static VisualElement Note(string text)
        {
            var note = Ui.View().MarginTop(10).Pad(6, 10).Radius(8).Bg(Theme.Rgba(255, 255, 255, 0.05f));
            var label = Ui.Mono(text, 14, Theme.Colors.CreamMuted, TextAnchor.MiddleCenter);
            label.style.whiteSpace = WhiteSpace.Normal;
            note.Add(label);
            return note;
        }
    }
}
