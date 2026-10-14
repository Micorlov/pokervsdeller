using System;
using System.Collections.Generic;
using System.Linq;
using Showdown.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>The scoreboard: lifetime numbers, achievements, the made-hands table, the last 20 hands (StatsScreen.tsx).</summary>
    public sealed class StatsScreen : VisualElement
    {
        /// <summary>
        /// Badges are laid out in explicit rows of three. A 30% width with wrap left a tenth of the panel empty
        /// down the right edge, so the grid sat off-centre.
        /// </summary>
        private const int BadgesPerRow = 3;

        private const float CellValueSize = 24;
        private const float LabelLineHeight = 18;

        public StatsScreen(Statistics stats, Action onBack)
        {
            style.flexGrow = 1;
            style.flexShrink = 1;

            var scroll = ScreenLayout.Page(out var column);
            Add(scroll);

            var blocks = new[]
            {
                BuildTitle(stats),
                BuildNumbers(stats),
                BuildAchievements(stats),
                BuildHandsMade(stats),
                BuildHistory(stats),
                new PubButton("Back", onBack, PubButtonVariant.Secondary),
            };
            ScreenLayout.Stack(column, blocks);
            blocks[blocks.Length - 1].style.marginTop = Theme.Spacing.Md + Theme.Spacing.Xs;
            for (int i = 0; i < blocks.Length; i++) Anim.Reveal(blocks[i], i);
        }

        private static VisualElement BuildTitle(Statistics stats)
        {
            var block = Ui.View();
            block.Add(ScreenLayout.Title("Your record"));
            string subtitle = stats.BestHandCategory.HasValue
                ? "Best hand made: " + StatsFormat.CategoryLabel(stats.BestHandCategory.Value)
                : "Play a hand and the book opens.";
            var line = Ui.BodyMuted(subtitle);
            line.style.marginTop = Theme.Spacing.Xs;
            block.Add(line);
            return block;
        }

        private static VisualElement BuildNumbers(Statistics stats)
        {
            int? winRate = Stats.WinRatePercent(stats);
            var panel = Panels.Glass();
            Ui.AddAll(panel, Theme.Spacing.Sm,
                CellRow(("Hands", stats.HandsPlayed.ToString()), ("Won", stats.HandsWon.ToString()),
                    ("Win rate", winRate.HasValue ? winRate.Value + "%" : "—")),
                CellRow(("Biggest win", stats.BiggestWin.ToString()), ("Streak", stats.CurrentStreak.ToString()),
                    ("Best streak", stats.BestStreak.ToString())),
                CellRow(("Sweeps", stats.Sweeps.ToString()), ("Kicker strikes", stats.KickerStrikes.ToString()),
                    ("Bails", stats.Bails.ToString())));
            return panel;
        }

        private static VisualElement CellRow(params (string label, string value)[] cells)
        {
            var row = Ui.View().Row();
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = Cell(cells[i].label, cells[i].value);
                if (i > 0) cell.style.marginLeft = Theme.Spacing.Sm;
                row.Add(cell);
            }
            return row;
        }

        /// <summary>
        /// Several labels wrap onto a second line, which would drop their number below the rest of the row and
        /// leave the grid reading as a zig-zag. Pinning the label to the top of the cell and the figure to the
        /// bottom lands every figure in a row on one line — which is the whole point of a scoreboard.
        /// </summary>
        private static VisualElement Cell(string label, string value)
        {
            var cell = Ui.View().Flex().Justify(Justify.SpaceBetween);
            cell.Add(Ui.LabelCaps(label).SetLineHeight(Theme.Type.Label, LabelLineHeight, bold: true));
            var number = Ui.Text(value, CellValueSize, Theme.Colors.Text, true, TextAnchor.MiddleLeft, -0.6f);
            number.style.marginTop = 2;
            cell.Add(number);
            return cell;
        }

        private static VisualElement BuildAchievements(Statistics stats)
        {
            var unlocked = new HashSet<string>(Achievements.UnlockedAchievements(stats).Select(a => a.Id));
            var all = Achievements.All;

            var panel = Panels.Glass();
            var header = Ui.View().Row().Justify(Justify.SpaceBetween).AlignItems(Align.Center);
            header.Add(Ui.LabelCaps("Achievements"));
            header.Add(Ui.LabelCaps(unlocked.Count + "/" + all.Count));
            panel.Add(header);

            var grid = Ui.View();
            grid.style.marginTop = Theme.Spacing.Sm;
            for (int start = 0; start < all.Count; start += BadgesPerRow)
            {
                var row = BadgeRow(all.Skip(start).Take(BadgesPerRow).ToList(), unlocked);
                if (start > 0) row.style.marginTop = Theme.Spacing.Sm;
                grid.Add(row);
            }
            panel.Add(grid);
            return panel;
        }

        private static VisualElement BadgeRow(IReadOnlyList<Achievement> achievements, ISet<string> unlocked)
        {
            var row = Ui.View().Row();
            for (int i = 0; i < BadgesPerRow; i++)
            {
                var item = i < achievements.Count
                    ? AchievementBadge.Create(achievements[i], unlocked.Contains(achievements[i].Id))
                    : AchievementBadge.Spacer();
                if (i > 0) item.style.marginLeft = Theme.Spacing.Sm;
                row.Add(item);
            }
            return row;
        }

        private static VisualElement BuildHandsMade(Statistics stats)
        {
            var card = Panels.Cream();
            card.Add(Panels.CreamLabel("Hands made"));

            bool any = false;
            foreach (var category in StatsFormat.CategoriesStrongestFirst)
            {
                if (!stats.CategoryCounts.TryGetValue(category, out int count) || count <= 0) continue;
                any = true;
                var row = Ui.View().Row().Justify(Justify.SpaceBetween).AlignItems(Align.Center);
                row.style.marginTop = Theme.Spacing.Xs;
                row.Add(Ui.Text(StatsFormat.CategoryLabel(category), 15, Theme.Colors.InkText, true));
                row.Add(Ui.Text("×" + count, 15, Theme.Colors.InkTextMuted, true, TextAnchor.MiddleRight));
                card.Add(row);
            }

            if (!any)
            {
                var empty = Ui.Text("Nothing in the book yet.", 14, Theme.Colors.InkTextMuted);
                empty.style.marginTop = Theme.Spacing.Xs;
                card.Add(empty);
            }
            return card;
        }

        private static VisualElement BuildHistory(Statistics stats)
        {
            var panel = Panels.Glass();
            panel.Add(Ui.LabelCaps("Recent hands"));

            if (stats.History.Count == 0)
            {
                var empty = Ui.Caption("The last twenty hands will show up here.");
                empty.style.marginTop = Theme.Spacing.Sm;
                panel.Add(empty);
                return panel;
            }

            for (int i = 0; i < stats.History.Count; i++)
            {
                var row = HistoryRow(stats.History[i], isLast: i == stats.History.Count - 1);
                row.style.marginTop = Theme.Spacing.Sm;
                panel.Add(row);
            }
            return panel;
        }

        private static VisualElement HistoryRow(HandHistoryEntry entry, bool isLast)
        {
            var row = Ui.View().Row().Justify(Justify.SpaceBetween).AlignItems(Align.Center)
                .Pad(Theme.Spacing.Xs + 2, 0);
            if (!isLast) row.BorderBottom(1, Theme.Colors.Border);
            row.Add(Ui.Body(StatsFormat.OutcomeLine(entry)));
            var net = Ui.Text(StatsFormat.Signed(entry.Net), 15, StatsFormat.NetColor(entry.Net), true,
                TextAnchor.MiddleRight, 0f, false);
            net.style.marginLeft = Theme.Spacing.Sm;
            net.style.flexShrink = 0;
            row.Add(net);
            return row;
        }
    }
}
