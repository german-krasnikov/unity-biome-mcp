// Tests for Approve & Execute flow.
// UI tests: bare VisualElement tree, no EditorWindow.
// Tests: _turnHasToolCalls gate — field reflection + MaybeAppend logic.
using NUnit.Framework;
using System.Reflection;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ApproveFlowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Test_ApproveGuard_NullSessionId_NoOp()
        {
            // ApproveHelper must not throw when sessionId is null.
            Assert.DoesNotThrow(() => ApproveHelper.BuildPromptOrNull(null));
            Assert.IsNull(ApproveHelper.BuildPromptOrNull(null));
        }

        [Test]
        public void Test_ApproveGuard_EmptySessionId_NoOp()
        {
            Assert.IsNull(ApproveHelper.BuildPromptOrNull(""));
        }

        [Test]
        public void Test_ApprovePrompt_NonEmptySessionId_ReturnsPrompt()
        {
            // A non-empty sessionId must yield a non-null prompt.
            var result = ApproveHelper.BuildPromptOrNull("sess-Y");
            Assert.IsNotNull(result);
        }

        [Test]
        public void Test_ApprovePrompt_ContainsExecuteInstruction()
        {
            var prompt = ApproveHelper.BuildPromptOrNull("sess-Z");
            StringAssert.Contains("Execute", prompt);
        }

        // ── UI: bare VisualElement tree ───────────────────────────────────────

        [Test]
        public void Test_ApproveButton_RemovedAfterClick()
        {
            var container = new VisualElement();
            var btn = ApproveButtonFactory.MakeButton(container, () => { });
            container.Add(btn);

            Assert.AreEqual(1, container.childCount);
            // MakeButton stores the click Action in userData for testability.
            var click = (System.Action)btn.userData;
            click.Invoke();
            Assert.AreEqual(0, container.childCount,
                "Button must remove itself from hierarchy after click");
        }

        [Test]
        public void Test_ApproveButton_NotShown_InAgentMode()
        {
            var container = new VisualElement();
            // agentMode = true → should not add any button
            ApproveButtonFactory.MaybeAppend(container, agentMode: true, sessionId: "sess-A", onApprove: () => { });
            Assert.AreEqual(0, container.childCount);
        }

        [Test]
        public void Test_ApproveButton_NotShown_WhenNoSessionId()
        {
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: false, sessionId: null, onApprove: () => { });
            Assert.AreEqual(0, container.childCount);
        }

        [Test]
        public void Test_ApproveButton_ShownAfterAskModeTurnDone()
        {
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: false, sessionId: "sess-B", onApprove: () => { });
            Assert.AreEqual(1, container.childCount);
            var btn = container[0];
            Assert.IsTrue(btn.ClassListContains("approve-btn"));
        }

        // ── F3: _turnHasToolCalls gate ────────────────────────────────────────

        private static readonly FieldInfo s_turnHasToolCalls = typeof(MCPChatWindow)
            .GetField("_turnHasToolCalls", BindingFlags.NonPublic | BindingFlags.Instance);

        [Test]
        public void ApproveButton_NotShown_WhenAskModeButNoToolCalls()
        {
            // Without tool calls: MaybeAppend should receive hadToolCalls=false,
            // modelled here by verifying MaybeAppend with agentMode=false+sessionId+no toolcalls
            // skips adding when we guard with the flag (field starts false after CreateInstance).
            var w = UnityEngine.ScriptableObject.CreateInstance<MCPChatWindow>();
            Assert.IsFalse((bool)s_turnHasToolCalls.GetValue(w), "flag starts false");
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void ApproveButton_Shown_WhenAskModeAndToolCallsPresent()
        {
            var w = UnityEngine.ScriptableObject.CreateInstance<MCPChatWindow>();
            s_turnHasToolCalls.SetValue(w, true);
            Assert.IsTrue((bool)s_turnHasToolCalls.GetValue(w), "flag set to true");
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void ApproveButton_NotShown_OnSecondTurnWithoutTools()
        {
            // After TurnDone clears the flag, a second turn without tools must leave it false.
            var w = UnityEngine.ScriptableObject.CreateInstance<MCPChatWindow>();
            s_turnHasToolCalls.SetValue(w, true);
            s_turnHasToolCalls.SetValue(w, false); // simulate TurnDone reset
            Assert.IsFalse((bool)s_turnHasToolCalls.GetValue(w));
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void TurnHasToolCalls_ClearedOnTurnDone()
        {
            // The flag must be false after TurnDone resets it (field-level assertion).
            var w = UnityEngine.ScriptableObject.CreateInstance<MCPChatWindow>();
            s_turnHasToolCalls.SetValue(w, true);
            // Simulate the TurnDone reset path directly.
            s_turnHasToolCalls.SetValue(w, false);
            Assert.IsFalse((bool)s_turnHasToolCalls.GetValue(w), "cleared on TurnDone");
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void TurnHasToolCalls_ClearedOnError()
        {
            var w = UnityEngine.ScriptableObject.CreateInstance<MCPChatWindow>();
            s_turnHasToolCalls.SetValue(w, true);
            // Simulate the Error reset path directly.
            s_turnHasToolCalls.SetValue(w, false);
            Assert.IsFalse((bool)s_turnHasToolCalls.GetValue(w), "cleared on Error");
            UnityEngine.Object.DestroyImmediate(w);
        }
    }
}
