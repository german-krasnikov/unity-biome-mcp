using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ContextProgressBar : VisualElement
    {
        // Reserve 20% of context window for model output; bar hits 100% at 80% input fill.
        internal const float OutputReserve = 0.8f;
        private readonly VisualElement _fill;
        private readonly Label         _label;

        internal ContextProgressBar()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems    = Align.Center;
            style.display       = DisplayStyle.None;

            var track = new VisualElement();
            track.style.width               = 60;
            track.style.height              = 4;
            track.style.backgroundColor     = new Color(0.3f, 0.3f, 0.3f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius  =
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 2;
            track.style.overflow = Overflow.Hidden;

            _fill = new VisualElement();
            _fill.style.height = 4;
            _fill.style.width  = 0;
            _fill.style.backgroundColor = new Color(0.3f, 0.7f, 1f);
            track.Add(_fill);

            _label = new Label();
            _label.style.fontSize   = 10;
            _label.style.marginLeft = 4;
            _label.style.color      = new Color(0.6f, 0.6f, 0.6f);

            Add(track);
            Add(_label);
        }

        internal void Update(int inputTokens, int contextWindow)
        {
            if (contextWindow <= 0) { style.display = DisplayStyle.None; return; }

            style.display = DisplayStyle.Flex;
            float pct = Mathf.Clamp01((float)inputTokens / (contextWindow * OutputReserve));
            _fill.style.width = new Length(pct * 100f, LengthUnit.Percent);
            _fill.style.backgroundColor = pct < 0.7f
                ? new Color(0.3f, 0.7f, 1f)
                : pct < 0.9f
                    ? new Color(1f, 0.8f, 0.2f)
                    : new Color(1f, 0.3f, 0.3f);
            _label.text = $"{pct * 100f:0}%";
        }

        internal void Reset()
        {
            style.display       = DisplayStyle.None;
            _fill.style.width   = new Length(0, LengthUnit.Percent);
            _label.text         = "";
        }
    }
}
