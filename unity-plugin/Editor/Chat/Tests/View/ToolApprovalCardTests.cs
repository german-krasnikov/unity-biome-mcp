using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolApprovalCardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ToolApprovalCard Make(
            string toolName = "Bash",
            string toolInput = "{\"command\":\"ls\"}",
            RiskLevel risk = RiskLevel.High,
            Action<ApprovalDecision> cb = null)
            => new ToolApprovalCard("req-1", toolName, toolInput, risk, cb ?? (_ => { }));

        private static void Click(Button b) => ((Action)b.userData)?.Invoke();

        [Test]
        public void Card_HasFourButtons()
        {
            var card = Make();
            Assert.AreEqual(4, card.Query<Button>().ToList().Count,
                "expect Allow / Deny / Session / Always");
        }

        [Test]
        public void Card_ClickAllow_DisablesAllButtons()
        {
            var card = Make();
            var buttons = card.Query<Button>().ToList();
            Click(buttons[0]); // Allow

            foreach (var b in buttons)
                Assert.IsFalse(b.enabledSelf, $"Button '{b.text}' should be disabled after resolve");
        }

        [Test]
        public void Card_ClickAllow_CallsOnDecisionWithAllow()
        {
            ApprovalDecision? received = null;
            var card = Make(cb: d => received = d);
            Click(card.Query<Button>().ToList()[0]); // Allow

            Assert.AreEqual(ApprovalDecision.Allow, received);
        }

        [Test]
        public void Card_ClickDeny_CallsOnDecisionWithDeny()
        {
            ApprovalDecision? received = null;
            var card = Make(cb: d => received = d);
            Click(card.Query<Button>().ToList()[1]); // Deny

            Assert.AreEqual(ApprovalDecision.Deny, received);
        }

        [Test]
        public void Card_DoubleClick_SecondClickIsNoop()
        {
            int callCount = 0;
            var card = Make(cb: _ => callCount++);
            var buttons = card.Query<Button>().ToList();
            Click(buttons[0]); // first click
            Click(buttons[1]); // second click (should be no-op due to _resolved guard)

            Assert.AreEqual(1, callCount, "second click must be ignored after resolve");
        }

        [Test]
        public void Card_ShowsRiskBadge_HighForBash()
        {
            var card = Make(toolName: "Bash", risk: RiskLevel.High);
            bool foundHighRisk = false;
            foreach (var l in card.Query<Label>().ToList())
                if (l.text == "HIGH RISK") { foundHighRisk = true; break; }
            Assert.IsTrue(foundHighRisk, "HIGH RISK badge missing for Bash");
        }

        [Test]
        public void Card_ClickSession_CallsOnDecisionWithAllowSession()
        {
            ApprovalDecision? received = null;
            var card = Make(cb: d => received = d);
            Click(card.Query<Button>().ToList()[2]); // Session
            Assert.AreEqual(ApprovalDecision.AllowSession, received);
        }

        [Test]
        public void Card_ClickAlways_CallsOnDecisionWithAlwaysAllow()
        {
            ApprovalDecision? received = null;
            var card = Make(cb: d => received = d);
            Click(card.Query<Button>().ToList()[3]); // Always
            Assert.AreEqual(ApprovalDecision.AlwaysAllow, received);
        }
    }
}
