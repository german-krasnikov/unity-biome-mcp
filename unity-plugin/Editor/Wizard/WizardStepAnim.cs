using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Slide transitions and progress bar for the Setup Wizard.</summary>
    public static class WizardStepAnim
    {
        /// <summary>Slide current content out to the left.</summary>
        public static void TransitionOut(VisualElement el, bool backwards = false)
        {
            el.RemoveFromClassList("wiz-transition-out");
            el.RemoveFromClassList("wiz-transition-out-back");
            el.AddToClassList(backwards ? "wiz-transition-out-back" : "wiz-transition-out");
        }

        /// <summary>Slide new content in from the right.</summary>
        public static void TransitionIn(VisualElement el, bool backwards = false)
        {
            string startClass = backwards
                ? "wiz-transition-in-start-back"
                : "wiz-transition-in-start";
            el.AddToClassList(startClass);
            el.schedule.Execute(() =>
            {
                el.RemoveFromClassList(startClass);
                el.AddToClassList("wiz-transition-in-end");
            }).StartingIn(16);
        }

        /// <summary>Build a progress bar container with an inner fill element.</summary>
        public static VisualElement BuildProgressBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("wiz-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("wiz-progress-fill");
            bar.Add(fill);
            return bar;
        }

        /// <summary>Set progress bar fill to [0..1] ratio.</summary>
        public static void SetProgress(VisualElement progressBar, float ratio)
        {
            var fill = progressBar.Q(className: "wiz-progress-fill");
            if (fill != null)
                fill.style.width = new Length(
                    Mathf.Clamp01(ratio) * 100f,
                    LengthUnit.Percent);
        }
    }
}
