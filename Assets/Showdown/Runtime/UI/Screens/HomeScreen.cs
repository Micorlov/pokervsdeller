using System;
using System.Collections.Generic;
using Showdown.Domain;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>What the front door can ask the shell to do.</summary>
    public sealed class HomeActions
    {
        public Action Play;
        public Action ReUp;
        public Action HowToPlay;
        public Action Settings;
        public Action Stats;
        public Action ClaimDailyBonus;
    }

    /// <summary>
    /// The front door, walnut and felt: the title in italic serif over a corner of the table where the dealer
    /// waits between fanned cards, then the bankroll in mono gold, the three tables named by their stake, and one
    /// gold way onto the felt (HomeScreen.tsx). It redraws from the stores whenever they change, and lets go of
    /// them when it leaves the panel.
    /// </summary>
    public sealed class HomeScreen : VisualElement
    {
        private const float BankrollSize = 40;
        private const float StakeChipSize = 34;
        private const float HeroHeight = 330;
        private const float PortraitHeight = 214;
        private const float HeroCardScale = 0.9f;
        private const float TierRadius = 14f;

        private readonly ITableController table;
        private readonly ISettingsStore settings;
        private readonly IDailyBonusStore dailyBonus;

        private readonly Label bankrollLabel;
        private readonly Label bustNote;
        private readonly VisualElement stakeChip;
        private readonly VisualElement bonusButton;
        private readonly Label bonusNote;
        private readonly List<(int ante, VisualElement card, Label name)> tiers = new List<(int, VisualElement, Label)>();
        private readonly VisualElement takeSeat;
        private readonly VisualElement reUp;

        private int shownAnte;
        private TweenHandle countUp;

        public HomeScreen(ITableController table, ISettingsStore settings, IDailyBonusStore dailyBonus, HomeActions actions)
        {
            this.table = table;
            this.settings = settings;
            this.dailyBonus = dailyBonus;

            style.flexGrow = 1;
            style.flexShrink = 1;

            var scroll = Ui.Scroll();
            var content = scroll.contentContainer;
            content.style.flexGrow = 1;
            content.style.alignItems = Align.Center;
            Add(scroll);

            var page = Ui.View().WPct(100).MaxW(Theme.ContentMaxWidth);
            content.Add(page);

            bankrollLabel = Ui.Mono(table.Bankroll.ToString(), BankrollSize, Theme.Colors.Gold);
            bustNote = Ui.Caption("Not enough to cover a full hand — re-up on the house.");
            bonusNote = Ui.Text("Daily bonus claimed — come back tomorrow.", Theme.Type.Caption, Theme.Colors.TextFaint);
            bonusButton = new PubButton("Daily bonus", actions.ClaimDailyBonus, PubButtonVariant.Bonus,
                "+" + Domain.DailyBonus.DailyBonusAmount, cue: null);
            stakeChip = Ui.View();
            takeSeat = new PubButton("Take a seat", actions.Play, cue: null);
            reUp = new PubButton("Re-up +" + Bankroll.ReUpAmount, actions.ReUp, cue: Cue.Chip);

            var hero = BuildHero();
            var body = Ui.View().Pad(Theme.Spacing.Md, Theme.Spacing.Md, Theme.Spacing.Lg, Theme.Spacing.Md);
            var bankroll = BuildBankroll();
            var tables = BuildTables();
            var buttons = BuildButtons(actions);
            Ui.AddAll(body, Theme.Spacing.Lg, bankroll, tables, buttons);
            page.Add(hero);
            page.Add(body);

            Anim.Reveal(hero, 0);
            Anim.Reveal(bankroll, 1);
            Anim.Reveal(tables, 2);
            Anim.Reveal(buttons, 3);

            shownAnte = -1;
            Refresh(animate: false);
            ScreenLayout.WhileAttached(this, Subscribe, Unsubscribe);
        }

        // ---- structure -----------------------------------------------------------------------------

        /// <summary>The walnut title band over a corner of the table: the dealer between two fanned pairs of cards.</summary>
        private static VisualElement BuildHero()
        {
            var hero = Ui.View();

            var band = Ui.View().AlignItems(Align.Center).Pad(18, 0, 16, 0).BgGradient(Theme.Colors.WalnutTop, Theme.Colors.WalnutBottom).Clip();
            band.Add(TableDecor.Bokeh(300, 26, 40));
            band.Add(TableDecor.Bokeh(70, 62, 24));
            band.Add(TableDecor.Bokeh(345, 74, 18));
            band.Add(Ui.Serif("Showdown", 46, Theme.Colors.Cream));
            band.Add(Ui.Mono("DEALER VS THREE", 14, Theme.Colors.CreamMuted, TextAnchor.MiddleCenter, 1.5f).MarginTop(2));
            band.Add(Ui.View().NoPick().Abs(null, 0, 0, 0).H(7).BgGradient(Theme.Colors.WalnutEdge, Theme.Hex("#3C2010")));
            hero.Add(band);

            var felt = Ui.View().H(HeroHeight).AlignItems(Align.Center);
            felt.Add(new TableFelt());
            felt.Add(HeroCard(Cards.Ace, Suit.Hearts, -12f, 0f).Abs(116, null, null, 30));
            felt.Add(HeroCard(Cards.King, Suit.Spades, -4f, 0f).Abs(126, null, null, 76));
            felt.Add(HeroCard(8, Suit.Diamonds, 4f, 0f).Abs(126, 76, null, null));
            felt.Add(HeroCard(Cards.Ace, Suit.Clubs, 12f, 0f).Abs(116, 30, null, null));
            felt.Add(DealerPortrait.Create(PortraitHeight).MarginTop(10));
            var tagline = new ArcText("Beat the house's climb, and the whole table", Ui.SerifFont, 20,
                Theme.Rgba(244, 230, 200, 0.82f), 0f, 14f);
            tagline.style.marginTop = 4;
            tagline.style.paddingLeft = 24;
            tagline.style.paddingRight = 24;
            felt.Add(tagline);
            hero.Add(felt);
            return hero;
        }

        private static VisualElement HeroCard(int rank, Suit suit, float tilt, float delay)
        {
            var card = new Card { Rank = rank, Suit = suit, Id = Cards.CardId(rank, suit) };
            var view = new PlayingCard(card, true, false, HeroCardScale, true, delay);
            view.OriginBottomCenter().Rotate(tilt);
            return view;
        }

        private VisualElement BuildBankroll()
        {
            var row = Ui.View().Row().AlignItems(Align.Center).Justify(Justify.SpaceBetween);

            var left = Ui.View();
            left.Add(Ui.Caps("Bankroll", 17, Theme.Colors.CreamMuted, TextAnchor.MiddleLeft, 2.5f));
            var figure = Ui.View().Row().AlignItems(Align.Center).MarginTop(2);
            figure.Add(stakeChip);
            figure.Add(bankrollLabel.MarginLeft(10));
            left.Add(figure);
            row.Add(left);

            bonusButton.style.paddingLeft = 18;
            bonusButton.style.paddingRight = 18;
            row.Add(bonusButton);

            var block = Ui.View();
            block.Add(row);
            bustNote.style.marginTop = Theme.Spacing.Xs;
            block.Add(bustNote);
            bonusNote.style.marginTop = Theme.Spacing.Xs;
            block.Add(bonusNote);
            return block;
        }

        /// <summary>The three stakes as tables you choose between, each named for its metal.</summary>
        private VisualElement BuildTables()
        {
            var block = Ui.View();
            block.Add(Ui.Caps("Choose your table", 17, Theme.Colors.CreamMuted, TextAnchor.MiddleLeft, 2.5f));
            var row = Ui.View().Row().MarginTop(Theme.Spacing.Sm);
            foreach (int ante in Bankroll.AnteOptions)
            {
                string metal = TableReport.TableName(ante).Replace(" Table", string.Empty);
                var card = Ui.View().Flex().AlignItems(Align.Center).Pad(10, 6).MinH(Theme.MinTouchTarget + 20).Radius(TierRadius);
                if (tiers.Count > 0) card.style.marginLeft = Theme.Spacing.Sm;
                var name = Ui.Serif(metal, 23, Theme.Colors.Cream);
                card.Add(name);
                card.Add(Ui.Mono("ANTE " + ante, 14, Theme.Colors.CreamMuted, TextAnchor.MiddleCenter).MarginTop(2));
                int chosen = ante;
                card.OnTap(() =>
                {
                    AppServices.Play(Cue.Tap);
                    SelectAnte(chosen);
                });
                tiers.Add((ante, card, name));
                row.Add(card);
            }
            block.Add(row);
            return block;
        }

        private VisualElement BuildButtons(HomeActions actions)
        {
            // Only one of the two shares the primary slot, so the gap below it never doubles.
            var primarySlot = Ui.View();
            primarySlot.Add(takeSeat);
            primarySlot.Add(reUp);

            var secondaryRow = Ui.View().Row();
            secondaryRow.Add(new PubButton("How to Play", actions.HowToPlay, PubButtonVariant.Secondary).Flex());
            var settingsButton = new PubButton("Settings", actions.Settings, PubButtonVariant.Secondary).Flex();
            settingsButton.style.marginLeft = Theme.Spacing.Sm;
            secondaryRow.Add(settingsButton);

            return Ui.AddAll(Ui.View(), Theme.Spacing.Sm + 2, primarySlot,
                new PubButton("Your Record", actions.Stats, PubButtonVariant.Secondary), secondaryRow);
        }

        private void SelectAnte(int ante)
        {
            settings.Update(current => current with { Ante = ante });
        }

        // ---- stores → widgets ----------------------------------------------------------------------

        private void Subscribe()
        {
            table.Changed += OnStateChanged;
            settings.Changed += OnStateChanged;
            dailyBonus.Changed += OnStateChanged;
            Refresh(animate: false);
        }

        private void Unsubscribe()
        {
            table.Changed -= OnStateChanged;
            settings.Changed -= OnStateChanged;
            dailyBonus.Changed -= OnStateChanged;
            countUp?.Cancel();
        }

        private void OnStateChanged() => Refresh(animate: true);

        private void Refresh(bool animate)
        {
            int bankroll = table.Bankroll;
            int ante = settings.Current.Ante;
            bool busted = !Bankroll.CanPlay(bankroll, ante);

            ShowBankroll(bankroll, animate);
            ShowStake(ante);

            bustNote.Show(busted);
            takeSeat.Show(!busted);
            reUp.Show(busted);
            bonusButton.Show(dailyBonus.CanClaim);
            bonusNote.Show(!dailyBonus.CanClaim);
        }

        private void ShowBankroll(int bankroll, bool animate)
        {
            countUp?.Cancel();
            countUp = null;
            if (!animate || !int.TryParse(bankrollLabel.text, out int shown))
            {
                bankrollLabel.text = bankroll.ToString();
                return;
            }
            countUp = Anim.CountUp(bankrollLabel, shown, bankroll);
        }

        private void ShowStake(int ante)
        {
            foreach (var (tierAnte, card, name) in tiers)
            {
                bool selected = tierAnte == ante;
                card.Bg(selected ? Theme.Rgba(70, 48, 16, 0.7f) : Theme.Rgba(0, 0, 0, 0.22f))
                    .Border(selected ? 2f : 1f, selected ? Theme.Colors.Gold : Theme.Rgba(255, 255, 255, 0.14f));
                name.style.color = selected ? Theme.Colors.Gold : Theme.Colors.Cream;
            }
            if (ante == shownAnte) return;
            shownAnte = ante;
            stakeChip.Clear();
            stakeChip.Add(PokerChip.Create(ante, StakeChipSize));
        }
    }
}
