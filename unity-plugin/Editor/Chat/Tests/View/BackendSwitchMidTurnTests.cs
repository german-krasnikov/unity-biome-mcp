// TDD — T3.1 call-site verification: each of the three backend-switch paths must
// dispatch the exit `abort` of the chat turn (dsl/chat/chatturn.feature). What the exit does is
// chatturn.examples (abort_leaves_the_backend_to_the_caller); these tests are the triggers.
// Removing any one of the three Abort dispatches makes its test red.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Chat.Turn;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendSwitchMidTurnTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly FieldInfo s_backends = typeof(MCPChatWindow)
            .GetField("_backends", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void Invoke(MCPChatWindow w, string method, params object[] args)
        {
            var m = typeof(MCPChatWindow).GetMethod(method,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, $"{method} must exist as a private instance method");
            m.Invoke(w, args);
        }

        [SetUp]
        public void SetUp()
        {
            DeleteEditorPrefString("MCPChat.SelectedBackend");
            DeleteEditorPrefString("MCPChat.SelectedModel.Claude");
            DeleteEditorPrefString("MCPChat.SelectedModel.Claude.custom");
        }

        // ── Path 1: OnAgentDropdownChanged ────────────────────────────────────
        // Proves that switching the agent dropdown mid-turn triggers the abort.
        // Red: remove the Abort dispatch from OnAgentDropdownChanged → phase stays Sending.

        [Test]
        public void OnAgentDropdownChanged_WhenSending_ResetsActivityToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Two enabled, non-agent backends so the switch is treated as valid
                s_backends.SetValue(window, new List<BackendSpec>
                {
                    new BackendSpec("Claude", null, true, BackendKind.Claude),
                    new BackendSpec("Codex",  null, true, BackendKind.Codex),
                });

                window._turn.Dispatch(ChatTurnIntent.Send());
                Assert.AreEqual(ChatTurnPhase.Sending, window._turn.Phase, "precondition");

                // Directly invoke the extracted callback (no UIElements panel required)
                using var evt = ChangeEvent<string>.GetPooled("Claude", "Codex");
                Invoke(window, "OnAgentDropdownChanged", evt);

                Assert.AreEqual(ChatTurnPhase.Idle, window._turn.Phase,
                    "agent-dropdown switch must abort the in-flight turn immediately — " +
                    "Abort dispatch missing from OnAgentDropdownChanged");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // ── Path 2: OnModelDropdownChanged ───────────────────────────────────
        // Proves that switching the model dropdown mid-turn triggers the abort.
        // Red: remove the Abort dispatch from OnModelDropdownChanged → phase stays Sending.

        [Test]
        public void OnModelDropdownChanged_WhenSending_ResetsActivityToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // BuildModelSelector initialises _customModelField (accessed in OnModelDropdownChanged)
                Invoke(window, "BuildModelSelector");

                window._turn.Dispatch(ChatTurnIntent.Send());
                Assert.AreEqual(ChatTurnPhase.Sending, window._turn.Phase, "precondition");

                // Pick any non-default, non-custom preset — guaranteed to take the abort branch
                var targetLabel = MCPChatWindow.ModelPresets[1].label;
                using var evt = ChangeEvent<string>.GetPooled("Default", targetLabel);
                Invoke(window, "OnModelDropdownChanged", evt);

                Assert.AreEqual(ChatTurnPhase.Idle, window._turn.Phase,
                    "model-dropdown switch must abort the in-flight turn immediately — " +
                    "Abort dispatch missing from OnModelDropdownChanged");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // ── Path 3: ApplyCustomModel ─────────────────────────────────────────
        // Proves that applying a custom-model string mid-turn triggers the abort.
        // Red: remove the Abort dispatch from ApplyCustomModel → phase stays Sending.

        [Test]
        public void ApplyCustomModel_WhenSending_ResetsActivityToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                window._turn.Dispatch(ChatTurnIntent.Send());
                Assert.AreEqual(ChatTurnPhase.Sending, window._turn.Phase, "precondition");

                Invoke(window, "ApplyCustomModel");

                Assert.AreEqual(ChatTurnPhase.Idle, window._turn.Phase,
                    "custom-model apply must abort the in-flight turn immediately — " +
                    "Abort dispatch missing from ApplyCustomModel");
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
