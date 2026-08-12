using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ContextProgressBar : VisualElement
    {
        // Reserve 20 % of context window for model output; bar hits 100 % at 80 % input fill.
        internal const float OutputReserve = 0.8f;

        private readonly VisualElement _fill;
        private readonly Label         _label;

        internal ContextProgressBar()
        {
            AddToClassList("context-bar");
            style.flexDirection = FlexDirection.Row;
            style.alignItems    = Align.Center;
            style.display       = DisplayStyle.None;

            var track = new VisualElement();
            track.AddToClassList("context-bar__track");
            track.style.width               = 60;
            track.style.height              = 4;
            track.style.backgroundColor     = new Color(0.3f, 0.3f, 0.3f); // structural, not state
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius  =
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 2;
            track.style.overflow = Overflow.Hidden;

            _fill = new VisualElement();
            _fill.AddToClassList("context-bar__fill");
            _fill.style.height = 4;
            _fill.style.width  = 0;
            track.Add(_fill);

            _label = new Label();
            _label.AddToClassList("context-bar__label");
            _label.style.fontSize   = 10;
            _label.style.marginLeft = 4;
            _label.style.color      = new Color(0.6f, 0.6f, 0.6f); // structural, not state

            Add(track);
            Add(_label);
        }

        // inputTokens < 0 means "usage unknown" — hide rather than show an empty bar.
        internal void Update(int inputTokens, int contextWindow)
        {
            if (contextWindow <= 0 || inputTokens < 0)
            {
                style.display = DisplayStyle.None;
                SetState(null);
                return;
            }

            style.display = DisplayStyle.Flex;

            float rawPct = (float)inputTokens / (contextWindow * OutputReserve);
            var state = rawPct >= 1.0f ? BarState.Overflow
                      : rawPct >= 0.9f ? BarState.Danger
                      : rawPct >= 0.7f ? BarState.Warn
                      : BarState.Normal;

            SetState(state);

            // Fill width capped at 100 % visually; label shows the actual ratio.
            _fill.style.width = new Length(Mathf.Clamp01(rawPct) * 100f, LengthUnit.Percent);
            _label.text = $"{rawPct * 100f:0}%";
        }

        internal void Reset()
        {
            style.display       = DisplayStyle.None;
            _fill.style.width   = new Length(0, LengthUnit.Percent);
            _label.text         = "";
            SetState(null);
        }

        // ── State management ─────────────────────────────────────────────────

        internal enum BarState { Normal, Warn, Danger, Overflow }

        private void SetState(BarState? state)
        {
            EnableInClassList("context-bar--normal",   state == BarState.Normal);
            EnableInClassList("context-bar--warn",     state == BarState.Warn);
            EnableInClassList("context-bar--danger",   state == BarState.Danger);
            EnableInClassList("context-bar--overflow", state == BarState.Overflow);
        }
    }
}
