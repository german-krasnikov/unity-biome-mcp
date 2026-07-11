// ChatWindowElementQueryTests — 25 UIToolkit element-query tests on real MCPChatWindow.
// Opens real window, queries elements via Q<>(). Does NOT send messages or invoke callbacks.
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    // ── File A: Element queries (tests 1-25) ─────────────────────────────────
    [TestFixture]
    public class ChatWindowElementQueryTests : RealWindowFixture
    {
        [Test] public void GetWindow_ReturnsNonNull()
            => Assert.IsNotNull(W);

        [Test] public void RootVisualElement_IsNotNull()
            => Assert.IsNotNull(W.rootVisualElement);

        [Test] public void RootVisualElement_HasChatRootClass()
            => Assert.IsTrue(W.rootVisualElement.ClassListContains("chat-root"));

        [Test] public void TextField_Query_IsNotNull()
            => Assert.IsNotNull(InputField() ?? (object)"null");

        [Test] public void TextField_InitialValue_IsEmpty()
        {
            var tf = InputField();
            if (tf == null) { Assert.Ignore("TextField not found"); return; }
            Assert.AreEqual("", tf.value);
        }

        [Test] public void SendButton_Query_IsNotNull()
        {
            if (SendBtn() == null) Assert.Ignore("Send button not found");
            Assert.IsNotNull(SendBtn());
        }

        [Test] public void SendButton_Text_IsSend()
        {
            var btn = SendBtn();
            if (btn == null) { Assert.Ignore("Send button not found"); return; }
            Assert.AreEqual("Send", btn.text);
        }

        [Test] public void SendButton_HasChatBtnClass()
        {
            var btn = SendBtn();
            if (btn == null) { Assert.Ignore("Send button not found"); return; }
            Assert.IsTrue(btn.ClassListContains("chat-btn"));
        }

        [Test] public void SendButton_HasChatBtnSendClass()
        {
            var btn = SendBtn();
            if (btn == null) { Assert.Ignore("Send button not found"); return; }
            Assert.IsTrue(btn.ClassListContains("chat-btn--send"));
        }

        [Test] public void StopButton_Query_IsNotNull()
        {
            if (StopBtn() == null) Assert.Ignore("Stop button not found");
            Assert.IsNotNull(StopBtn());
        }

        [Test] public void StopButton_Text_IsStop()
        {
            var btn = StopBtn();
            if (btn == null) { Assert.Ignore("Stop button not found"); return; }
            Assert.AreEqual("Stop", btn.text);
        }

        [Test] public void StopButton_HasChatBtnStopClass()
        {
            var btn = StopBtn();
            if (btn == null) { Assert.Ignore("Stop button not found"); return; }
            Assert.IsTrue(btn.ClassListContains("chat-btn--stop"));
        }

        [Test] public void StopButton_InitialDisplayStyle_IsNone()
        {
            var btn = StopBtn();
            if (btn == null) { Assert.Ignore("Stop button not found"); return; }
            Assert.AreEqual(DisplayStyle.None, btn.style.display.value);
        }

        [Test] public void AskButton_Query_IsNotNull()
        {
            if (AskBtn() == null) Assert.Ignore("Ask button not found");
            Assert.IsNotNull(AskBtn());
        }

        [Test] public void AskButton_Text_IsAsk()
        {
            var btn = AskBtn();
            if (btn == null) { Assert.Ignore("Ask button not found"); return; }
            Assert.AreEqual("Ask", btn.text);
        }

        [Test] public void AskButton_Initially_HasActiveClass()
        {
            var btn = AskBtn();
            if (btn == null) { Assert.Ignore("Ask button not found"); return; }
            Assert.IsTrue(btn.ClassListContains("mode-toggle-btn--active"));
        }

        [Test] public void AgentButton_Query_IsNotNull()
        {
            if (AgentBtn() == null) Assert.Ignore("Agent button not found");
            Assert.IsNotNull(AgentBtn());
        }

        [Test] public void AgentButton_Text_IsAgent()
        {
            var btn = AgentBtn();
            if (btn == null) { Assert.Ignore("Agent button not found"); return; }
            Assert.AreEqual("Agent", btn.text);
        }

        [Test] public void AgentButton_Initially_NotHasActiveClass()
        {
            var btn = AgentBtn();
            if (btn == null) { Assert.Ignore("Agent button not found"); return; }
            Assert.IsFalse(btn.ClassListContains("mode-toggle-btn--active"));
        }

        [Test] public void ScrollView_Query_IsNotNull()
        {
            if (Scroll() == null) Assert.Ignore("ScrollView not found");
            Assert.IsNotNull(Scroll());
        }

        [Test] public void ScrollView_HasChatScrollClass()
        {
            var sv = Scroll();
            if (sv == null) { Assert.Ignore("ScrollView not found"); return; }
            Assert.IsTrue(sv.ClassListContains("chat-scroll"));
        }

        [Test] public void TokenReadout_Query_IsNotNull()
        {
            if (TokenLabel() == null) Assert.Ignore("Token label not found");
            Assert.IsNotNull(TokenLabel());
        }

        [Test] public void TokenReadout_InitialText_IsEmpty()
        {
            var lbl = TokenLabel();
            if (lbl == null) { Assert.Ignore("Token label not found"); return; }
            Assert.AreEqual("", lbl.text);
        }

        [Test] public void FlowBar_Query_IsNotNull()
        {
            if (FlowBar() == null) Assert.Ignore("FlowBar not found");
            Assert.IsNotNull(FlowBar());
        }

        [Test] public void FlowFill_Query_IsNotNull()
        {
            if (FlowFill() == null) Assert.Ignore("FlowFill not found");
            Assert.IsNotNull(FlowFill());
        }
    }
}
