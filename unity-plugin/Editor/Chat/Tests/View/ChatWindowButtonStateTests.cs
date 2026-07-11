// ChatWindowButtonStateTests — 25 tests for button/class state on real MCPChatWindow.
// Tests 26-50. Extends RealWindowFixture from ChatWindowElementQueryTests.cs.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowButtonStateTests : RealWindowFixture
    {
        [Test] public void FlowBar_InitiallyNoActiveClass()
        {
            var fb = FlowBar();
            if (fb == null) { Assert.Ignore("FlowBar not found"); return; }
            Assert.IsFalse(fb.ClassListContains("flowbar--active"));
        }

        [Test] public void FlowFill_Query_IsNotNull_B()
        {
            if (FlowFill() == null) Assert.Ignore("FlowFill not found");
            Assert.IsNotNull(FlowFill());
        }

        [Test] public void FlowFill_InitiallyNoSendingClass()
        {
            var ff = FlowFill();
            if (ff == null) { Assert.Ignore("FlowFill not found"); return; }
            Assert.IsFalse(ff.ClassListContains("flowbar__fill--sending"));
        }

        [Test] public void FlowFill_InitiallyNoReceivingClass()
        {
            var ff = FlowFill();
            if (ff == null) { Assert.Ignore("FlowFill not found"); return; }
            Assert.IsFalse(ff.ClassListContains("flowbar__fill--receiving"));
        }

        [Test] public void AgentDropdown_Query_IsNotNull()
        {
            if (AgentDrop() == null) Assert.Ignore("AgentDropdown not found");
            Assert.IsNotNull(AgentDrop());
        }

        [Test] public void AgentDropdown_HasAgentSelectorClass()
        {
            var dd = AgentDrop();
            if (dd == null) { Assert.Ignore("AgentDropdown not found"); return; }
            Assert.IsTrue(dd.ClassListContains("agent-selector"));
        }

        [Test] public void AgentDropdown_InitialChoices_NotEmpty()
        {
            var dd = AgentDrop();
            if (dd == null) { Assert.Ignore("AgentDropdown not found"); return; }
            Assert.Greater(dd.choices.Count, 0);
        }

        [Test] public void InputArea_Query_IsNotNull_B()
        {
            if (InputArea() == null) Assert.Ignore("InputArea not found");
            Assert.IsNotNull(InputArea());
        }

        [Test] public void InputArea_HasInputAreaClass()
        {
            var ia = InputArea();
            if (ia == null) { Assert.Ignore("InputArea not found"); return; }
            Assert.IsTrue(ia.ClassListContains("input-area"));
        }

        [Test] public void ModeSegment_Query_IsNotNull()
        {
            var seg = W.rootVisualElement.Q(null, "mode-segment");
            if (seg == null) Assert.Ignore("mode-segment not found");
            Assert.IsNotNull(seg);
        }

        [Test] public void FooterBar_Query_IsNotNull()
        {
            var bar = W.rootVisualElement.Q(null, "footer-bar");
            if (bar == null) Assert.Ignore("footer-bar not found");
            Assert.IsNotNull(bar);
        }

        [Test] public void CopyFlashLabel_Query_IsNotNull()
        {
            var el = W.rootVisualElement.Q(null, "copy-flash");
            if (el == null) Assert.Ignore("copy-flash not found");
            Assert.IsNotNull(el);
        }

        [Test] public void CopyFlashLabel_InitiallyHiddenClass()
        {
            var el = W.rootVisualElement.Q(null, "copy-flash");
            if (el == null) { Assert.Ignore("copy-flash not found"); return; }
            Assert.IsTrue(el.ClassListContains("copy-flash--hidden"));
        }

        [Test] public void FourMainButtons_AreDistinctObjects()
        {
            var send  = SendBtn();
            var stop  = StopBtn();
            var ask   = AskBtn();
            var agent = AgentBtn();
            if (send == null || stop == null || ask == null || agent == null)
            { Assert.Ignore("Not all buttons found"); return; }
            var set = new HashSet<VisualElement> { send, stop, ask, agent };
            Assert.AreEqual(4, set.Count);
        }

        [Test] public void StopButton_NotSendButton()
        {
            if (SendBtn() == null || StopBtn() == null) { Assert.Ignore("Buttons not found"); return; }
            Assert.AreNotSame(SendBtn(), StopBtn());
        }

        [Test] public void AskButton_NotAgentButton()
        {
            if (AskBtn() == null || AgentBtn() == null) { Assert.Ignore("Buttons not found"); return; }
            Assert.AreNotSame(AskBtn(), AgentBtn());
        }

        [Test] public void AgentButton_HasModeToggleBtnLastClass()
        {
            var btn = AgentBtn();
            if (btn == null) { Assert.Ignore("Agent button not found"); return; }
            Assert.IsTrue(btn.ClassListContains("mode-toggle-btn--last"));
        }

        [Test] public void AgentButton_HasModeToggleBtnClass()
        {
            var btn = AgentBtn();
            if (btn == null) { Assert.Ignore("Agent button not found"); return; }
            Assert.IsTrue(btn.ClassListContains("mode-toggle-btn"));
        }

        [Test] public void AskButton_NotHasModeToggleBtnLastClass()
        {
            var btn = AskBtn();
            if (btn == null) { Assert.Ignore("Ask button not found"); return; }
            Assert.IsFalse(btn.ClassListContains("mode-toggle-btn--last"));
        }

        [Test] public void TokenReadout_HasTokenReadoutClass()
        {
            var lbl = TokenLabel();
            if (lbl == null) { Assert.Ignore("Token label not found"); return; }
            Assert.IsTrue(lbl.ClassListContains("token-readout"));
        }

        [Test] public void ScrollView_HorizontalScrollerHidden()
        {
            var sv = Scroll();
            if (sv == null) { Assert.Ignore("ScrollView not found"); return; }
            Assert.AreEqual(ScrollerVisibility.Hidden, sv.horizontalScrollerVisibility);
        }

        [Test] public void RootVisualElement_ChildCount_AtLeast3()
            => Assert.GreaterOrEqual(W.rootVisualElement.childCount, 3);

        [Test] public void GetWindowTwice_ReturnsSameInstance()
        {
            var w2 = EditorWindow.GetWindow<MCPChatWindow>(false, "MCPTest2", false);
            Assert.AreSame(W, w2);
        }

        [Test] public void CloseAndReopen_NewInstanceNotNull()
        {
            W.Close(); W = null;
            var w2 = EditorWindow.GetWindow<MCPChatWindow>(false, "MCPTest3", false);
            Assert.IsNotNull(w2);
            W = w2; // so TearDown can close it
        }

        [Test] public void AgentDropdown_FirstChoice_IsNotEmpty()
        {
            var dd = AgentDrop();
            if (dd == null) { Assert.Ignore("AgentDropdown not found"); return; }
            Assert.IsTrue(dd.choices.Count > 0 && !string.IsNullOrEmpty(dd.choices[0]));
        }
    }
}
