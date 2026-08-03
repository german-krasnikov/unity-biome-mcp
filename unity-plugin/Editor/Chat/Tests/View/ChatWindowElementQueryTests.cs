// ChatWindowElementQueryTests — 25 UI Toolkit element-query tests on MCPChatWindow.
// Creates an owned window, queries elements via Q<>(). Does not send messages.
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    // ── File A: Element queries (tests 1-25) ─────────────────────────────────
    [TestFixture]
    public class ChatWindowElementQueryTests : RealWindowFixture
    {
        [Test] public void InitializedWindow_ReturnsNonNull()
            => Assert.IsNotNull(W);

        [Test] public void RootVisualElement_IsNotNull()
            => Assert.IsNotNull(W.rootVisualElement);

        [Test] public void RootVisualElement_HasChatRootClass()
            => Assert.IsTrue(W.rootVisualElement.ClassListContains("chat-root"));

        [Test] public void TextField_Query_IsNotNull()
            => Assert.IsNotNull(InputField());

        [Test] public void TextField_InitialValue_IsEmpty()
        {
            var tf = InputField();
            Assert.AreEqual("", tf.value);
        }

        [Test] public void SendButton_Query_IsNotNull()
            => Assert.IsNotNull(SendBtn());

        [Test] public void SendButton_Text_IsSend()
        {
            var btn = SendBtn();
            Assert.AreEqual("Send", btn.text);
        }

        [Test] public void SendButton_HasChatBtnClass()
        {
            var btn = SendBtn();
            Assert.IsTrue(btn.ClassListContains("chat-btn"));
        }

        [Test] public void SendButton_HasChatBtnSendClass()
        {
            var btn = SendBtn();
            Assert.IsTrue(btn.ClassListContains("chat-btn--send"));
        }

        [Test] public void StopButton_Query_IsNotNull()
            => Assert.IsNotNull(StopBtn());

        [Test] public void StopButton_Text_IsStop()
        {
            var btn = StopBtn();
            Assert.AreEqual("Stop", btn.text);
        }

        [Test] public void StopButton_HasChatBtnStopClass()
        {
            var btn = StopBtn();
            Assert.IsTrue(btn.ClassListContains("chat-btn--stop"));
        }

        [Test] public void StopButton_InitialDisplayStyle_IsNone()
        {
            var btn = StopBtn();
            Assert.AreEqual(DisplayStyle.None, btn.style.display.value);
        }

        [Test] public void AskButton_Query_IsNotNull()
            => Assert.IsNotNull(AskBtn());

        [Test] public void AskButton_Text_IsAsk()
        {
            var btn = AskBtn();
            Assert.AreEqual("Ask", btn.text);
        }

        [Test] public void AskButton_Initially_HasActiveClass()
        {
            var btn = AskBtn();
            Assert.IsTrue(btn.ClassListContains("mode-toggle-btn--active"));
        }

        [Test] public void AgentButton_Query_IsNotNull()
            => Assert.IsNotNull(AgentBtn());

        [Test] public void AgentButton_Text_IsAgent()
        {
            var btn = AgentBtn();
            Assert.AreEqual("Agent", btn.text);
        }

        [Test] public void AgentButton_Initially_NotHasActiveClass()
        {
            var btn = AgentBtn();
            Assert.IsFalse(btn.ClassListContains("mode-toggle-btn--active"));
        }

        [Test] public void ScrollView_Query_IsNotNull()
            => Assert.IsNotNull(Scroll());

        [Test] public void ScrollView_HasChatScrollClass()
        {
            var sv = Scroll();
            Assert.IsTrue(sv.ClassListContains("chat-scroll"));
        }

        [Test] public void TokenReadout_Query_IsNotNull()
            => Assert.IsNotNull(TokenLabel());

        [Test] public void TokenReadout_InitialText_IsEmpty()
        {
            var lbl = TokenLabel();
            Assert.AreEqual("", lbl.text);
        }

        [Test] public void FlowBar_Query_IsNotNull()
            => Assert.IsNotNull(FlowBar());

        [Test] public void FlowFill_Query_IsNotNull()
            => Assert.IsNotNull(FlowFill());

        [Test] public void FlowBar_UsesFixedSevenParticlePool()
        {
            var particles = W.rootVisualElement
                .Query<VisualElement>(className: "flowbar__particle")
                .ToList();
            Assert.AreEqual(7, particles.Count);
        }
    }
}
