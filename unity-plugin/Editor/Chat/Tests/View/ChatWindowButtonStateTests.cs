// ChatWindowButtonStateTests — 25 tests for button/class state on real MCPChatWindow.
// Tests 26-50. Uses the owned-window RealWindowFixture.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowButtonStateTests : RealWindowFixture
    {
        [Test] public void FlowBar_InitiallyNoActiveClass()
        {
            var fb = FlowBar();
            Assert.IsFalse(fb.ClassListContains("flowbar--active"));
        }

        [Test] public void FlowFill_Query_IsNotNull_B()
            => Assert.IsNotNull(FlowFill());

        [Test] public void FlowFill_InitiallyNoSendingClass()
        {
            var ff = FlowFill();
            Assert.IsFalse(ff.ClassListContains("flowbar__fill--sending"));
        }

        [Test] public void FlowFill_InitiallyNoReceivingClass()
        {
            var ff = FlowFill();
            Assert.IsFalse(ff.ClassListContains("flowbar__fill--receiving"));
        }

        [Test] public void AgentDropdown_Query_IsNotNull()
            => Assert.IsNotNull(AgentDrop());

        [Test] public void AgentDropdown_HasAgentSelectorClass()
        {
            var dd = AgentDrop();
            Assert.IsTrue(dd.ClassListContains("agent-selector"));
        }

        [Test] public void AgentDropdown_InitialChoices_NotEmpty()
        {
            var dd = AgentDrop();
            Assert.Greater(dd.choices.Count, 0);
        }

        [Test] public void InputArea_Query_IsNotNull_B()
            => Assert.IsNotNull(InputArea());

        [Test] public void InputArea_HasInputAreaClass()
        {
            var ia = InputArea();
            Assert.IsTrue(ia.ClassListContains("input-area"));
        }

        [Test] public void ModeSegment_Query_IsNotNull()
        {
            var seg = W.rootVisualElement.Q(null, "mode-segment");
            Assert.IsNotNull(seg);
        }

        [Test] public void FooterBar_Query_IsNotNull()
        {
            var bar = W.rootVisualElement.Q(null, "footer-bar");
            Assert.IsNotNull(bar);
        }

        [Test] public void CopyFlashLabel_Query_IsNotNull()
        {
            var el = W.rootVisualElement.Q(null, "copy-flash");
            Assert.IsNotNull(el);
        }

        [Test] public void CopyFlashLabel_InitiallyHiddenClass()
        {
            var el = W.rootVisualElement.Q(null, "copy-flash");
            Assert.IsNotNull(el);
            Assert.IsTrue(el.ClassListContains("copy-flash--hidden"));
        }

        [Test] public void FourMainButtons_AreDistinctObjects()
        {
            var send  = SendBtn();
            var stop  = StopBtn();
            var ask   = AskBtn();
            var agent = AgentBtn();
            var set = new HashSet<VisualElement> { send, stop, ask, agent };
            Assert.AreEqual(4, set.Count);
        }

        [Test] public void StopButton_NotSendButton()
            => Assert.AreNotSame(SendBtn(), StopBtn());

        [Test] public void AskButton_NotAgentButton()
            => Assert.AreNotSame(AskBtn(), AgentBtn());

        [Test] public void AgentButton_HasModeToggleBtnLastClass()
        {
            var btn = AgentBtn();
            Assert.IsTrue(btn.ClassListContains("mode-toggle-btn--last"));
        }

        [Test] public void AgentButton_HasModeToggleBtnClass()
        {
            var btn = AgentBtn();
            Assert.IsTrue(btn.ClassListContains("mode-toggle-btn"));
        }

        [Test] public void AskButton_NotHasModeToggleBtnLastClass()
        {
            var btn = AskBtn();
            Assert.IsFalse(btn.ClassListContains("mode-toggle-btn--last"));
        }

        [Test] public void TokenReadout_HasTokenReadoutClass()
        {
            var lbl = TokenLabel();
            Assert.IsTrue(lbl.ClassListContains("token-readout"));
        }

        [Test] public void ScrollView_HorizontalScrollerHidden()
        {
            var sv = Scroll();
            Assert.AreEqual(ScrollerVisibility.Hidden, sv.horizontalScrollerVisibility);
        }

        [Test] public void RootVisualElement_ChildCount_AtLeast3()
            => Assert.GreaterOrEqual(W.rootVisualElement.childCount, 3);

        [Test] public void CreateInitializedWindow_Twice_ReturnsDistinctOwnedInstances()
        {
            var w2 = CreateInitializedWindow("MCPTest2");
            Assert.AreNotSame(W, w2);
            Assert.IsTrue(w2.rootVisualElement.ClassListContains("chat-root"));
        }

        [Test] public void DestroyAndRecreate_NewInstanceIsInitialized()
        {
            var destroyed = W;
            UnityEngine.Object.DestroyImmediate(W);
            W = CreateInitializedWindow("MCPTest3");
            Assert.AreNotSame(destroyed, W);
            Assert.IsNotNull(W);
            Assert.IsTrue(W.rootVisualElement.ClassListContains("chat-root"));
        }

        [Test] public void AgentDropdown_FirstChoice_IsNotEmpty()
        {
            var dd = AgentDrop();
            Assert.IsTrue(dd.choices.Count > 0 && !string.IsNullOrEmpty(dd.choices[0]));
        }
    }
}
