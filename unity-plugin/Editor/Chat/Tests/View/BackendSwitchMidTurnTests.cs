// TDD — T3.1 call-site verification: each of the three backend-switch paths must
// invoke AbortCurrentTurnIfActive(), not just that the helper itself works.
// Removing any one of the three AbortCurrentTurnIfActive() calls makes its test
// red; the helper-existence tests in CancelTurnTests stay green.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendSwitchMidTurnTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly FieldInfo s_activity = typeof(MCPChatWindow)
            .GetField("_activity", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_backends = typeof(MCPChatWindow)
            .GetField("_backends", BindingFlags.NonPublic | BindingFlags.Instance);

        private static ChatActivityState GetActivity(MCPChatWindow w)
            => (ChatActivityState)s_activity.GetValue(w);

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
        // Red: remove AbortCurrentTurnIfActive() from OnAgentDropdownChanged → phase stays Sending.

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

                var activity = GetActivity(window);
                activity.Send();
                Assert.AreEqual(ActivityPhase.Sending, activity.Phase, "precondition");

                // Directly invoke the extracted callback (no UIElements panel required)
                using var evt = ChangeEvent<string>.GetPooled("Claude", "Codex");
                Invoke(window, "OnAgentDropdownChanged", evt);

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase,
                    "agent-dropdown switch must abort the in-flight turn immediately — " +
                    "AbortCurrentTurnIfActive() call missing from OnAgentDropdownChanged");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // ── Path 2: OnModelDropdownChanged ───────────────────────────────────
        // Proves that switching the model dropdown mid-turn triggers the abort.
        // Red: remove AbortCurrentTurnIfActive() from OnModelDropdownChanged → phase stays Sending.

        [Test]
        public void OnModelDropdownChanged_WhenSending_ResetsActivityToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // BuildModelSelector initialises _customModelField (accessed in OnModelDropdownChanged)
                Invoke(window, "BuildModelSelector");

                var activity = GetActivity(window);
                activity.Send();
                Assert.AreEqual(ActivityPhase.Sending, activity.Phase, "precondition");

                // Pick any non-default, non-custom preset — guaranteed to take the abort branch
                var targetLabel = MCPChatWindow.ModelPresets[1].label;
                using var evt = ChangeEvent<string>.GetPooled("Default", targetLabel);
                Invoke(window, "OnModelDropdownChanged", evt);

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase,
                    "model-dropdown switch must abort the in-flight turn immediately — " +
                    "AbortCurrentTurnIfActive() call missing from OnModelDropdownChanged");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // ── Path 3: ApplyCustomModel ─────────────────────────────────────────
        // Proves that applying a custom-model string mid-turn triggers the abort.
        // Red: remove AbortCurrentTurnIfActive() from ApplyCustomModel → phase stays Sending.

        [Test]
        public void ApplyCustomModel_WhenSending_ResetsActivityToIdle()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var activity = GetActivity(window);
                activity.Send();
                Assert.AreEqual(ActivityPhase.Sending, activity.Phase, "precondition");

                Invoke(window, "ApplyCustomModel");

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase,
                    "custom-model apply must abort the in-flight turn immediately — " +
                    "AbortCurrentTurnIfActive() call missing from ApplyCustomModel");
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
