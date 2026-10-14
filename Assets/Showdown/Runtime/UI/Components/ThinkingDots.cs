using UnityEngine;
using UnityEngine.UIElements;

namespace Showdown.UI
{
    /// <summary>
    /// The "… is thinking" line (port of ThinkingDots.tsx). The AI takes a deliberate beat before playing so
    /// its move is readable; three dots travelling left to right make that pause look like consideration
    /// rather than the app having stalled. Reduced motion gets the same text, held still.
    /// </summary>
    public sealed class ThinkingDots : VisualElement
    {
        private const int DotCount = 3;
        private const float StepSeconds = 0.16f;
        private const float RiseSeconds = 0.38f;
        private const float DotSize = 3.5f;
        private const float DotRise = 3f;
        private const float RestOpacity = 0.28f;

        private readonly VisualElement[] dots = new VisualElement[DotCount];

        public ThinkingDots(string label)
        {
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            Add(TableStyle.Line(label, Theme.Type.Caption, Theme.Colors.TextMuted));

            var dotRow = Ui.View().Row().AlignItems(Align.Center).MarginLeft(7).MarginTop(2);
            for (int i = 0; i < DotCount; i++)
            {
                var dot = Ui.View().Size(DotSize, DotSize).Radius(DotSize / 2f).Bg(Theme.Colors.TextMuted);
                if (i > 0) dot.style.marginLeft = Theme.Spacing.Xs;
                dots[i] = dot;
                dotRow.Add(dot);
            }
            Add(dotRow);

            Apply(0f);
            RegisterCallback<AttachToPanelEvent>(_ => Start());
        }

        private void Start()
        {
            if (Motion.Reduced) return;
            float cycle = DotCount * StepSeconds + 2f * RiseSeconds - StepSeconds;
            Anim.Loop(cycle, t => Apply(t * cycle), this);
        }

        /// <summary>Each dot waits its turn, rises, falls, then waits out the rest of the cycle.</summary>
        private void Apply(float seconds)
        {
            for (int i = 0; i < DotCount; i++)
            {
                float local = seconds - i * StepSeconds;
                float lift = 0f;
                if (local >= 0f && local < RiseSeconds) lift = Ease.InOutQuad(local / RiseSeconds);
                else if (local >= RiseSeconds && local < 2f * RiseSeconds) lift = Ease.InOutQuad(1f - (local - RiseSeconds) / RiseSeconds);

                dots[i].style.opacity = Mathf.Lerp(RestOpacity, 1f, lift);
                dots[i].Translate(0f, -DotRise * lift);
            }
        }
    }
}
