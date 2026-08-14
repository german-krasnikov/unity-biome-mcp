// TDD RED: PlanStepCard visual element tests.
// These fail to compile until PlanStepCard.cs is created.
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PlanStepCardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_ShowsStepDescription()
        {
            var card = new PlanStepCard("plan_step_started", "Install packages");
            var labels = card.Query<Label>().ToList();
            Assert.IsTrue(labels.Any(l => l.text == "Install packages"),
                "Card must show the step description text");
        }

        [Test]
        public void Build_ShowsStatusBadge_Running()
        {
            var card = new PlanStepCard("plan_step_started", "Do something");
            var statusLabel = card.Q<Label>(null, "plan-step-card__status");
            Assert.IsNotNull(statusLabel, "Card must have a .plan-step-card__status label");
            Assert.IsTrue(statusLabel.ClassListContains("plan-step--running"),
                "plan_step_started must have 'plan-step--running' badge class");
        }

        [Test]
        public void ApproveButton_CallsOnDecision_WithTrue()
        {
            bool? received = null;
            var card = new PlanStepCard("plan_step_started", "Do X");
            card.OnDecision = b => received = b;

            var buttons = card.Query<Button>().ToList();
            var approveBtn = buttons.FirstOrDefault(b => b.text == "Approve");
            Assert.IsNotNull(approveBtn, "Approve button must exist for plan_step_started");
            ((Action)approveBtn.userData)?.Invoke();

            Assert.AreEqual(true, received, "Approve must fire OnDecision(true)");
        }

        [Test]
        public void RejectButton_CallsOnDecision_WithFalse()
        {
            bool? received = null;
            var card = new PlanStepCard("plan_step_started", "Do X");
            card.OnDecision = b => received = b;

            var buttons = card.Query<Button>().ToList();
            var rejectBtn = buttons.FirstOrDefault(b => b.text == "Reject");
            Assert.IsNotNull(rejectBtn, "Reject button must exist for plan_step_started");
            ((Action)rejectBtn.userData)?.Invoke();

            Assert.AreEqual(false, received, "Reject must fire OnDecision(false)");
        }
    }
}
