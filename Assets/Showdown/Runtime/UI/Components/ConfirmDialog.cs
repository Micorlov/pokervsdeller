using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The two-button question that stood in for RN's <c>Alert.alert</c> ("Leave this hand?", "Reset bankroll"):
    /// a scrim over the whole screen, system bars included, and a solid panel so the dimmed table reads as depth.
    /// Only one is ever open. Tapping the scrim, the Android back key or the cancel button all answer "no".
    /// </summary>
    public sealed class ConfirmDialog : VisualElement
    {
        private const float PanelMaxWidth = 340;

        /// <summary>The dialog currently on screen, or null.</summary>
        public static ConfirmDialog Open { get; private set; }

        private readonly Action onCancel;

        /// <summary>Opens a dialog; <paramref name="onConfirm"/> runs after it has closed. The confirm button is the destructive one.</summary>
        public static ConfirmDialog Show(string title, string body, string cancelLabel, string confirmLabel,
            Action onConfirm, Action onCancel = null)
        {
            Open?.Close();
            var dialog = new ConfirmDialog(title, body, cancelLabel, confirmLabel, onConfirm, onCancel);
            UiHost.Instance.Ground.Add(dialog);
            Open = dialog;
            return dialog;
        }

        /// <summary>Answers "no" to the open dialog, if any. Returns whether there was one — the back key uses it to know the press was spent.</summary>
        public static bool DismissOpen()
        {
            if (Open == null) return false;
            var dialog = Open;
            dialog.Close();
            dialog.onCancel?.Invoke();
            return true;
        }

        public void Close()
        {
            if (Open == this) Open = null;
            RemoveFromHierarchy();
        }

        private ConfirmDialog(string title, string body, string cancelLabel, string confirmLabel, Action onConfirm, Action onCancel)
        {
            this.onCancel = onCancel;
            this.AbsFill().Bg(Theme.Colors.Scrim).Center().Pad(Theme.Spacing.Lg);

            var panel = Ui.View().WPct(100).MaxW(PanelMaxWidth).Bg(Theme.Colors.PanelSolid)
                .Radius(Theme.Radii.Lg).Border(1, Theme.Colors.BorderStrong).Pad(Theme.Spacing.Lg);
            // A tap inside the panel must not reach the scrim underneath it.
            panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            panel.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());

            panel.Add(Ui.Text(title, Theme.Type.Heading, Theme.Colors.Text, true, TextAnchor.MiddleLeft, -0.7f));
            var message = Ui.Text(body, Theme.Type.Body, Theme.Colors.TextMuted);
            message.style.marginTop = Theme.Spacing.Sm;
            panel.Add(message);

            var buttons = Ui.View().Row().MarginTop(Theme.Spacing.Lg);
            buttons.Add(new PubButton(cancelLabel, () => Dismiss(onCancel), PubButtonVariant.Secondary).Flex());
            var confirm = new PubButton(confirmLabel, () => Dismiss(onConfirm), PubButtonVariant.Danger).Flex();
            confirm.style.marginLeft = Theme.Spacing.Sm;
            buttons.Add(confirm);
            panel.Add(buttons);
            Add(panel);

            this.OnTap(() => Dismiss(onCancel));
            Anim.Fade(this, 0f, 1f, Motion.Base);
            Anim.Entrance(panel, 0f, 0f, 0.94f);
        }

        private void Dismiss(Action then)
        {
            Close();
            then?.Invoke();
        }
    }
}
