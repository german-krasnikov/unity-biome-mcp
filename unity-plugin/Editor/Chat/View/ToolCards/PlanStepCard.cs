using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class PlanStepCard : VisualElement
    {
        internal Action<bool> OnDecision;

        private readonly Label         _statusLabel;
        private readonly VisualElement _buttons;

        internal PlanStepCard(string stepKind, string description)
        {
            AddToClassList("plan-step-card");

            Add(new Label(description));

            _statusLabel = new Label(stepKind == "plan_step_completed" ? "Done" : "Running");
            _statusLabel.AddToClassList("plan-step-card__status");
            _statusLabel.AddToClassList(
                stepKind == "plan_step_completed" ? "plan-step--done" : "plan-step--running");
            Add(_statusLabel);

            if (stepKind != "plan_step_completed")
            {
                _buttons = new VisualElement();
                _buttons.AddToClassList("plan-step-card__buttons");
                _buttons.Add(MakeBtn("Approve", () => OnDecision?.Invoke(true)));
                _buttons.Add(MakeBtn("Reject",  () => OnDecision?.Invoke(false)));
                Add(_buttons);
            }
        }

        /// <summary>Mark this step done — replaces status badge and removes action buttons.</summary>
        internal void MarkCompleted()
        {
            _statusLabel.text = "Done";
            _statusLabel.RemoveFromClassList("plan-step--running");
            _statusLabel.AddToClassList("plan-step--done");
            _buttons?.RemoveFromHierarchy();
        }

        private static Button MakeBtn(string label, Action onClick)
        {
            var b = new Button { text = label };
            b.userData = onClick; // expose for tests
            b.clicked += onClick;
            return b;
        }
    }
}
