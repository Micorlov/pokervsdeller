using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The player's move for one round of the climb (port of ActionBar.tsx): bail and forge side by side, hold
    /// under them, and in the pressure round the pressure toggle takes half of hold's row — the dock keeps the
    /// same height in every round. The bail button states its refund outright —
    /// leaving a hand is a priced decision here, not a defeat.
    ///
    /// Every label names the direction the chips travel. A bare "+25" on bail and "+25" on pressure would
    /// mean opposite things on two buttons a thumb apart.
    /// </summary>
    public sealed class ActionBar : VisualElement
    {
        private const string ForgeHint = "Tap a card to trade it in — the starred kicker stays.";

        private readonly VisualElement forgePicker = Ui.View();
        private readonly VisualElement moves = Ui.View();
        private readonly PubButton pressure;
        private readonly PubButton bail;
        private readonly PubButton forge;
        private readonly PubButton hold;
        private readonly Action<bool> onTogglePressure;
        private bool pressureArmed;

        public ActionBar(Action<bool> onTogglePressure, Action onHold, Action onBeginForge, Action onCancelForge, Action onBail)
        {
            this.onTogglePressure = onTogglePressure;
            style.flexShrink = 0;

            BuildForgePicker(onCancelForge);

            pressure = new PubButton(string.Empty, () => this.onTogglePressure?.Invoke(!pressureArmed), PubButtonVariant.Secondary, cue: null);
            bail = new PubButton("Bail", onBail, PubButtonVariant.Danger, cue: null);
            forge = new PubButton("Forge", onBeginForge, PubButtonVariant.Secondary, cue: null);

            // Two to a row: at this type size three labels never fit across one. Hold is the move most hands end
            // on, so it keeps the bottom row — alone, or beside pressure in round three.
            var row = Ui.View().Row();
            bail.Flex();
            forge.Flex();
            forge.style.marginLeft = Theme.Spacing.Sm + 2;
            row.Add(bail);
            row.Add(forge);

            var holdRow = Ui.View().Row();
            hold = new PubButton("Hold", onHold, PubButtonVariant.Primary, cue: null).Flex();
            pressure.Flex();
            hold.style.marginLeft = Theme.Spacing.Sm + 2;
            holdRow.Add(pressure);
            holdRow.Add(hold);

            moves.Add(row);
            moves.Add(holdRow);

            Add(forgePicker);
            Add(moves);
        }

        /// <param name="forgesLeft">Forges the player may still buy this hand.</param>
        /// <param name="canForge">Whether a forge is affordable and allowed right now.</param>
        /// <param name="bailRefund">What a bail hands back right now — shown plainly on the button.</param>
        /// <param name="pressureAvailable">True only in the pressure round, with the stake still single and affordable.</param>
        /// <param name="forgeArming">True while the player is picking which card a forge trades away.</param>
        public void Update(int ante, int forgesLeft, bool canForge, int bailRefund, bool pressureAvailable,
            bool pressureArmed, bool forgeArming)
        {
            this.pressureArmed = pressureArmed;
            forgePicker.Show(forgeArming);
            moves.Show(!forgeArming);
            if (forgeArming) return;

            pressure.Show(pressureAvailable);
            pressure.Label = pressureArmed ? "Pressure on" : "Pressure";
            pressure.Detail = pressureArmed ? "tap to undo" : $"pay {ante} · 3:1";
            pressure.Variant = PubButtonVariant.Bonus;

            bail.Detail = $"get {bailRefund} back";
            forge.Detail = $"pay {ante} · {forgesLeft} left";
            forge.Disabled = !canForge;
            hold.style.marginLeft = pressureAvailable ? Theme.Spacing.Sm + 2 : 0f;
            TableStyle.ApplyColumnGap(moves, Theme.Spacing.Sm + 2);
        }

        private void BuildForgePicker(Action onCancelForge)
        {
            var hint = TableStyle.Line(ForgeHint, Theme.Type.Caption, Theme.Colors.Gold, false, TextAnchor.MiddleCenter);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.height = StyleKeyword.Auto;
            hint.style.marginBottom = Theme.Spacing.Sm;
            forgePicker.Add(hint);
            forgePicker.Add(new PubButton("Cancel Forge", onCancelForge, PubButtonVariant.Secondary, cue: null));
        }
    }
}
