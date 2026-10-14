using System;
using Showdown.Domain;
using Showdown.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>Sound, haptics, reduced motion, and the one destructive button (SettingsScreen.tsx).</summary>
    public sealed class SettingsScreen : VisualElement
    {
        /// <summary>Long enough for the sound bank to finish loading after the switch flips.</summary>
        private const long SoundDemoDelayMs = 400;
        private const float RowMinHeight = Theme.MinTouchTarget + 10;
        private const float HintSize = 12.5f;
        private const float HintLineHeight = 18;

        private readonly ISettingsStore settings;
        private readonly Action onResetBankroll;
        private readonly ToggleSwitch soundSwitch;
        private readonly ToggleSwitch hapticsSwitch;
        private readonly ToggleSwitch motionSwitch;

        private bool wasSoundOn;
        private IVisualElementScheduledItem soundDemo;

        public SettingsScreen(ISettingsStore settings, Action onResetBankroll, Action onBack)
        {
            this.settings = settings;
            this.onResetBankroll = onResetBankroll;
            wasSoundOn = settings.Current.SoundOn;

            style.flexGrow = 1;
            style.flexShrink = 1;

            var scroll = ScreenLayout.Page(out var column);
            Add(scroll);

            var current = settings.Current;
            soundSwitch = new ToggleSwitch(current.SoundOn, on => Toggle(s => s with { SoundOn = on }));
            hapticsSwitch = new ToggleSwitch(current.HapticsOn, on => Toggle(s => s with { HapticsOn = on }));
            motionSwitch = new ToggleSwitch(current.ReducedMotion, on => Toggle(s => s with { ReducedMotion = on }));

            var title = ScreenLayout.Title("Settings");
            title.style.marginBottom = Theme.Spacing.Xs;

            var panel = Panels.Glass(0).Pad(0, Theme.Spacing.Md);
            panel.Add(BuildRow("Sound", soundSwitch, isLast: false));
            panel.Add(BuildRow("Haptics", hapticsSwitch, isLast: false));
            panel.Add(BuildRow("Reduced motion", motionSwitch, isLast: true));

            var hint = Ui.Text("Reduced motion keeps every sound and buzz, but stops cards travelling and flipping — they cross-fade into place instead.",
                HintSize, Theme.Colors.TextMuted).SetLineHeight(HintSize, HintLineHeight);

            var actions = Ui.AddAll(Ui.View(), Theme.Spacing.Sm,
                new PubButton("Reset bankroll", ConfirmReset, PubButtonVariant.Danger),
                new PubButton("Back", onBack, PubButtonVariant.Secondary));

            ScreenLayout.Stack(column, title, panel, hint, actions);
            actions.style.marginTop = Theme.Spacing.Md + Theme.Spacing.Xs;
            Anim.Reveal(title, 0);
            Anim.Reveal(panel, 1);
            Anim.Reveal(hint, 2);
            Anim.Reveal(actions, 3);

            ScreenLayout.WhileAttached(this, () => settings.Changed += OnSettingsChanged, Detach);
        }

        private static VisualElement BuildRow(string label, ToggleSwitch toggle, bool isLast)
        {
            var row = Ui.View().Row().Justify(Justify.SpaceBetween).AlignItems(Align.Center).MinH(RowMinHeight);
            if (!isLast) row.BorderBottom(1, Theme.Colors.Border);
            row.Add(Ui.Text(label, Theme.Type.Body, Theme.Colors.Text));
            row.Add(toggle);
            return row;
        }

        private void Toggle(Func<AppSettings, AppSettings> patch)
        {
            AppServices.Play(Cue.Tap);
            settings.Update(patch);
        }

        /// <summary>
        /// Turning sound on plays a cue back, because a silent switch is no way to find out whether sound now
        /// works. The wait covers the players being built.
        /// </summary>
        private void OnSettingsChanged()
        {
            var current = settings.Current;
            soundSwitch.SetValue(current.SoundOn);
            hapticsSwitch.SetValue(current.HapticsOn);
            motionSwitch.SetValue(current.ReducedMotion);

            bool justEnabled = current.SoundOn && !wasSoundOn;
            wasSoundOn = current.SoundOn;
            if (!justEnabled) return;
            soundDemo?.Pause();
            soundDemo = schedule.Execute(() => AppServices.Play(Cue.Chip));
            soundDemo.StartingIn(SoundDemoDelayMs);
        }

        private void Detach()
        {
            settings.Changed -= OnSettingsChanged;
            soundDemo?.Pause();
        }

        private void ConfirmReset()
        {
            ConfirmDialog.Show(
                "Reset bankroll",
                "This puts you back to " + Bankroll.StartingBankroll + " chips and leaves the table. It cannot be undone.",
                "Cancel", "Reset", onResetBankroll);
        }
    }
}
