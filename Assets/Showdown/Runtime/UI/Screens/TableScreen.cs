using System;
using System.Collections.Generic;
using Showdown.Domain;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The table, walnut and felt (design/walnut-felt): a walnut header naming the table and whose turn it is, the
    /// house's climb across the far side of the felt, the two regulars on the arc, your hand nearest the rail, and
    /// your bankroll and moves on the floor below it. A settled hand rises as a result sheet over the near half.
    ///
    /// The widgets are built once and updated in place on every <see cref="ITableController.Changed"/>, so a card
    /// already on the table never replays its deal entrance and a count-up is not restarted by an unrelated change.
    /// </summary>
    public sealed class TableScreen : VisualElement
    {
        /// <summary>The player's own hand is the one they hold — dealt largest of all.</summary>
        private const float PlayerCardScale = 1.06f;
        /// <summary>Each covered card keeps a full touch target's width showing: it is a kicker or forge tap.</summary>
        private const float PlayerStrip = Theme.MinTouchTarget;
        private const float PlayerFanDegrees = 4f;
        private const float ArcSag = 11f;
        private const float StatusDot = 9f;

        private readonly ITableController table;
        private readonly VisualElement column;
        private readonly TopBar topBar;
        private readonly ScrollView scroll;
        private readonly VisualElement placard;
        private readonly DealerClimb dealerClimb;
        private readonly PotPill potPill;
        private readonly VisualElement feltLettering;
        private readonly List<PlayerSeat> seats = new List<PlayerSeat>();
        private readonly CardFan fan;
        private readonly HandRankBadge rankBadge;

        private readonly Label bankrollLabel;
        private readonly VisualElement kickerPanel;
        private readonly PubButton lockKicker;
        private readonly ActionBar actionBar;
        private readonly VisualElement statusLine;
        private readonly Label statusText;

        private readonly VisualElement sheetLayer;
        private readonly VisualElement sheetColumn;
        private readonly VisualElement achievementSlot;
        private AchievementToast achievementToast;
        private ResultSheet sheet;
        private string sheetSignature;
        private int shownBankroll = -1;
        private int targetBankroll = -1;
        private TweenHandle bankrollTween;

        public TableScreen(ITableController table, Action onHome, Action onRules, Action onMenu)
        {
            this.table = table;
            style.flexGrow = 1;
            style.alignItems = Align.Center;

            column = Ui.View().WPct(100).MaxW(Theme.ContentMaxWidth).Clip();
            column.style.flexGrow = 1;
            column.style.flexShrink = 1;
            Add(column);

            topBar = new TopBar(onHome, onRules, onMenu);
            column.Add(topBar);

            placard = TableDecor.Placard();
            dealerClimb = new DealerClimb();
            potPill = new PotPill();
            feltLettering = BuildLettering();
            fan = new CardFan(PlayerCardScale, PlayerStrip, PlayerFanDegrees);
            rankBadge = new HandRankBadge();
            scroll = BuildScroller();
            column.Add(scroll);

            bankrollLabel = Ui.Mono(string.Empty, 20, Theme.Colors.Text);
            lockKicker = new PubButton("Lock kicker", table.LockKicker, PubButtonVariant.Primary, cue: null);
            kickerPanel = BuildKickerPanel(lockKicker);
            actionBar = new ActionBar(table.SetPressureArmed, table.Hold, table.BeginForge, table.CancelForge, table.Bail);
            statusText = Ui.Text(string.Empty, 24, Theme.Colors.Cream, false, TextAnchor.MiddleCenter, 0f, false);
            statusLine = BuildStatusLine(statusText);
            column.Add(BuildDock());

            achievementSlot = Ui.View();
            // A short phone cannot fit the sheet and a badge toast: the column is capped and the sheet's middle scrolls.
            sheetColumn = Ui.View().Abs(null, 12, 14, 12);
            sheetColumn.style.maxHeight = Length.Percent(96);
            sheetColumn.Add(achievementSlot);
            sheetLayer = BuildSheetLayer(sheetColumn);
            column.Add(sheetLayer);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                table.Changed += Refresh;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => table.Changed -= Refresh);
        }

        // ---- structure -----------------------------------------------------------------------------

        private ScrollView BuildScroller()
        {
            var scroller = Ui.Scroll().Flex();
            scroller.contentContainer.style.flexGrow = 1;
            scroller.contentContainer.style.flexShrink = 0;

            // flexGrow lets the felt reach the dock when its content is short, so the rail always sits on the
            // floor rather than floating with a gap under it.
            var tableArea = Ui.View().Grow(1);
            tableArea.Add(new TableFelt());
            tableArea.Add(BuildProps());

            // Your hand is named between the two seats, then held nearest the rail.
            var content = Ui.View().AlignItems(Align.Center).Pad(48, 0, 8, 0);
            content.Add(dealerClimb);
            potPill.style.marginTop = 8;
            content.Add(potPill);
            content.Add(feltLettering);
            content.Add(BuildSeats());
            // The badge rises into the gap between the two seats' fans; your fan rises under it.
            rankBadge.style.marginTop = -10;
            content.Add(rankBadge);
            fan.style.marginTop = 6;
            content.Add(fan);
            tableArea.Add(content);

            scroller.Add(tableArea);
            return scroller;
        }

        /// <summary>The placard, the dealer's chip tray and the shoe along the far edge of the felt.</summary>
        private VisualElement BuildProps()
        {
            var props = Ui.View().NoPick().Abs(6, 0, null, 0).Row().Justify(Justify.SpaceBetween).AlignItems(Align.FlexStart)
                .Pad(0, 16, 0, 14);
            props.Add(placard);
            props.Add(TableDecor.ChipTray().MarginTop(2));
            props.Add(TableDecor.Shoe());
            return props;
        }

        private static VisualElement BuildLettering()
        {
            var lettering = Ui.View().AlignSelf(Align.Stretch).MarginTop(4).Pad(0, 40);
            lettering.Add(new ArcText("Sweep pays 5 antes", Ui.CapsFont, 18, Theme.Colors.FeltInk, 3f, ArcSag));
            var second = new ArcText("Match the last card — kicker pays double", Ui.SerifFont, 16,
                Theme.Rgba(244, 230, 200, 0.66f), 0f, ArcSag * 0.7f);
            second.style.marginTop = -8;
            lettering.Add(second);
            return lettering;
        }

        private VisualElement BuildSeats()
        {
            var row = Ui.View().Row().Justify(Justify.SpaceBetween).AlignSelf(Align.Stretch).Pad(0, 10).MarginTop(2);
            for (int i = 0; i < ShowdownRound.BotCount; i++)
            {
                var seat = new PlayerSeat();
                seats.Add(seat);
                row.Add(seat);
            }
            return row;
        }

        private static VisualElement BuildKickerPanel(PubButton lockButton)
        {
            var panel = Ui.View();
            var hint = Ui.Text("Tap the card you want as your kicker.\nMatch the house's last card to double a win.",
                17, Theme.Colors.CreamMuted, false, TextAnchor.MiddleCenter);
            hint.style.marginBottom = 10;
            panel.Add(hint);
            panel.Add(lockButton);
            return panel;
        }

        private static VisualElement BuildStatusLine(Label text)
        {
            var line = Ui.View().Row().Center().MinH(60);
            line.Add(Ui.View().Size(StatusDot, StatusDot).Radius(StatusDot / 2f).Bg(Theme.Colors.GoldDeep).MarginRight(12));
            line.Add(text);
            return line;
        }

        /// <summary>The floor in front of the rail: your chips and bankroll, then whatever you can do right now.</summary>
        private VisualElement BuildDock()
        {
            var dock = Ui.View().Pad(10, 16, 16, 16);
            var bank = Ui.View().Row().Center();
            bank.Add(TableDecor.ChipStacks());
            bank.Add(Ui.Text("Bankroll", 16, Theme.Colors.CreamMuted, false, TextAnchor.MiddleLeft, 0f, false).MarginLeft(10));
            bank.Add(bankrollLabel.MarginLeft(10));
            dock.Add(bank);

            var moves = Ui.View().MarginTop(12);
            moves.Add(kickerPanel);
            moves.Add(actionBar);
            moves.Add(statusLine);
            dock.Add(moves);
            return dock;
        }

        /// <summary>The scrim and result sheet over the near half of the table, hidden until a hand settles.</summary>
        private static VisualElement BuildSheetLayer(VisualElement sheetColumn)
        {
            var layer = Ui.View().AbsFill().NoPick();
            var scrim = Ui.View().AbsFill().NoPick()
                .BgGradient(Theme.Rgba(0, 0, 0, 0f), Theme.Rgba(0, 0, 0, 0.6f));
            // BgGradient backs the texture with its bottom colour; the top of this scrim has to stay clear.
            scrim.style.backgroundColor = Theme.Colors.Transparent;
            layer.Add(scrim);
            layer.Add(sheetColumn);
            layer.Show(false);
            return layer;
        }

        // ---- state → widgets -----------------------------------------------------------------------

        private void Refresh()
        {
            var round = table.Round;
            column.Show(round != null);
            if (round == null) return;

            var human = ShowdownRound.HumanSeat(round);
            bool settled = round.Phase == ShowdownPhase.Settled;
            bool showdown = settled || round.Phase == ShowdownPhase.Reveal;
            bool humanBailed = human.Status == SeatStatus.Bailed;
            bool humanPending = round.Phase == ShowdownPhase.Actions && !humanBailed && human.LastMove == null;
            bool pickingKicker = round.Phase == ShowdownPhase.Kicker && !table.KickerLocked;

            string status = TableReport.StatusChip(round, table.KickerLocked, humanPending, table.ThinkingSeat);
            topBar.Update(TableReport.TableName(round.Ante), $"Ante {round.Ante}", status);
            TableDecor.SetPlacard(placard, $"Ante {round.Ante}", $"Sweep ×{ShowdownPayouts.SweepBonusUnits}");

            UpdateFelt(round, human, settled, showdown);
            UpdateHand(round, human, humanBailed, humanPending, pickingKicker);
            UpdateDock(round, human, settled, humanPending, pickingKicker, status);
            UpdateSheet(round, human, settled);
        }

        private void UpdateFelt(ShowdownRoundState round, ShowdownSeatState human, bool settled, bool showdown)
        {
            var dealerRank = round.DealerCards.Count == HandRanks.HandSize ? ShowdownRound.DealerRankOf(round) : null;
            dealerClimb.Update(round.DealerCards, dealerRank, !showdown);

            // Once the hand is paid the sheet takes the near half: the felt drops its lettering and pots so the
            // hands still show above it.
            potPill.Show(!settled);
            feltLettering.Show(!settled);
            potPill.Update(round.TablePot, human.DealerStake, human.Pressured, round.Ante);

            int bot = 0;
            for (int i = 0; i < round.Seats.Count && bot < seats.Count; i++)
            {
                if (round.Seats[i].IsHuman) continue;
                // ThinkingSeat indexes Round.Seats, where the human is seat 0 and the bots follow.
                seats[bot].Update(round.Seats[i], settled, showdown, table.ThinkingSeat == i);
                bot++;
            }
        }

        private void UpdateHand(ShowdownRoundState round, ShowdownSeatState human, bool humanBailed, bool humanPending,
            bool pickingKicker)
        {
            var mode = pickingKicker ? FanSelectMode.Kicker
                : humanPending && table.ForgeArming ? FanSelectMode.Discard
                : FanSelectMode.None;
            Action<int> onSelect = pickingKicker ? (Action<int>)table.SelectKicker : table.Forge;
            fan.Update(human.Cards, true, humanBailed, round.Phase == ShowdownPhase.Dealing, human.KickerIndex,
                table.FreshCardIndex, mode, onSelect);

            bool hasHand = human.Cards.Count == HandRanks.HandSize;
            rankBadge.Show(hasHand && round.Phase != ShowdownPhase.Settled);
            if (!hasHand) return;
            if (humanBailed)
            {
                rankBadge.Set(null, "You bailed", null, false);
                return;
            }

            var made = HandRanks.EvaluateHand(human.Cards);
            string kicker = human.KickerIndex.HasValue ? Cards.RankLabel(human.Cards[human.KickerIndex.Value].Rank) : null;
            string detail = round.Phase == ShowdownPhase.Reveal ? null
                : human.LastMove?.Kind == ShowdownMoveKind.Hold && !humanPending ? "holding"
                : null;
            rankBadge.Set(kicker, HandRanks.HandName(made), detail,
                HandRanks.CategoryOrder(made.Category) >= HandRanks.CategoryOrder(HandCategory.Pair));
        }

        private void UpdateDock(ShowdownRoundState round, ShowdownSeatState human, bool settled, bool humanPending,
            bool pickingKicker, string status)
        {
            SetBankroll(human.Stack);

            kickerPanel.Show(pickingKicker);
            lockKicker.Disabled = human.KickerIndex == null;

            actionBar.Show(humanPending);
            if (humanPending) UpdateMoves(round, human);

            bool waiting = !pickingKicker && !humanPending && !settled;
            statusLine.Show(waiting);
            statusText.SetText(status);
        }

        private void UpdateMoves(ShowdownRoundState round, ShowdownSeatState human)
        {
            int forgesLeft = ShowdownPayouts.ForgeLimit - human.ForgesUsed;
            bool canForge = forgesLeft > 0 && human.Stack >= round.Ante;
            bool pressureAvailable = round.DealerCards.Count == ShowdownPayouts.PressureRound
                && !human.Pressured && human.Stack >= round.Ante;
            int refund = ShowdownPayouts.BailRefund(human.TableContrib + human.DealerStake);
            actionBar.Update(round.Ante, forgesLeft, canForge, refund, pressureAvailable, table.PressureArmed, table.ForgeArming);
        }

        /// <summary>The bankroll counts to its new figure instead of swapping digits.</summary>
        private void SetBankroll(int bankroll)
        {
            if (bankroll == targetBankroll) return;
            targetBankroll = bankroll;
            bankrollTween?.Cancel();
            if (shownBankroll < 0 || Motion.Reduced)
            {
                shownBankroll = bankroll;
                bankrollLabel.SetText(bankroll.ToString());
                return;
            }
            // Counts on from whatever figure is on screen, so a second change mid-count never jumps.
            bankrollTween = Anim.CountUp(bankrollLabel, shownBankroll, bankroll, value =>
            {
                shownBankroll = value;
                return value.ToString();
            });
            if (bankrollTween == null) shownBankroll = bankroll;
        }

        // ---- result sheet --------------------------------------------------------------------------

        /// <summary>One sheet per settled hand: it is rebuilt only when the hand or what it says changes.</summary>
        private void UpdateSheet(ShowdownRoundState round, ShowdownSeatState human, bool settled)
        {
            var report = settled ? TableReport.Settled(round, table.StreakBonus) : null;
            sheetLayer.Show(report != null);
            UpdateAchievements();
            if (report == null)
            {
                sheetSignature = null;
                sheet?.RemoveFromHierarchy();
                sheet = null;
                return;
            }

            bool busted = !Domain.Bankroll.CanPlay(human.Stack, round.Ante);
            string signature = $"{round.HandNumber}|{report.Net}|{report.Bankroll}|{busted}";
            if (signature == sheetSignature) return;

            sheetSignature = signature;
            sheet?.RemoveFromHierarchy();
            sheet = new ResultSheet(report, busted, Domain.Bankroll.ReUpAmount, table.NextHand, () => table.ReUpNow(round.Ante));
            sheet.pickingMode = PickingMode.Position;
            sheetColumn.Add(sheet);
            scroll.scrollOffset = Vector2.zero;
        }

        private void UpdateAchievements()
        {
            var achievements = table.NewAchievements;
            if (achievements.Count > 0)
            {
                if (achievementToast == null)
                {
                    achievementToast = new AchievementToast(table.DismissAchievements);
                    achievementToast.style.marginBottom = Theme.Spacing.Sm;
                    achievementSlot.Add(achievementToast);
                }
                achievementToast.Update(achievements);
            }
            else if (achievementToast != null)
            {
                achievementToast.RemoveFromHierarchy();
                achievementToast = null;
            }
            achievementSlot.Show(achievementToast != null);
        }
    }
}
