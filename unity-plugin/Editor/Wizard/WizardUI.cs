using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    internal static class WizardUI
    {
        internal static Button Primary(string text, Action clicked)
        {
            var button = BiomeUI.PrimaryButton(text, clicked);
            button.AddToClassList("wiz-btn-primary");
            button.name = "wizard-primary-action";
            return button;
        }

        internal static Button Secondary(string text, Action clicked)
        {
            var button = BiomeUI.SecondaryButton(text, clicked);
            button.AddToClassList("wiz-btn-secondary");
            return button;
        }

        internal static Button Quiet(string text, Action clicked)
        {
            var button = BiomeUI.QuietButton(text, clicked);
            button.AddToClassList("wiz-btn-skip");
            return button;
        }

        internal static VisualElement Navigation(Button back, params VisualElement[] actions)
        {
            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");

            var left = new VisualElement();
            left.AddToClassList("wiz-nav-group");
            if (back != null)
                left.Add(back);
            nav.Add(left);

            var right = new VisualElement();
            right.AddToClassList("wiz-nav-group");
            foreach (var action in actions)
                if (action != null)
                    right.Add(action);
            nav.Add(right);
            return nav;
        }
    }
}
