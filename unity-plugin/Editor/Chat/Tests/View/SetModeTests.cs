// TDD — SetMode must be a pure UI state flip: no process kill/restart.
// Agent mode = _agentMode bool; PermissionPrompt events are auto-approved in EventHandlers.
// Backend instance must remain unchanged across Ask ↔ Agent switches.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SetModeTests
    {
        // Minimal stub implementing IChatBackend for mode-switch tests.
        private sealed class FakeBackend : IChatBackend
        {
            public bool   IsRunning  => false;
            public string SessionId  { get; }
            public FakeBackend(string sessionId = null) { SessionId = sessionId; }
            public void Start()  { }
            public void Stop()   { }
            public void SendTurn(string j)           { }
            public void SendControlResponse(string j){ }
            public void DrainEvents(List<ChatEvent> o, List<ToolCallRecord> t = null) { }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void InjectBackend(MCPChatWindow w, IChatBackend backend)
        {
            typeof(MCPChatWindow)
                .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(w, backend);
        }

        private static IChatBackend GetBackend(MCPChatWindow w) =>
            (IChatBackend)typeof(MCPChatWindow)
                .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        private static void CallSetMode(MCPChatWindow w, bool agentMode)
        {
            typeof(MCPChatWindow)
                .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(w, new object[] { agentMode });
        }

        private static bool GetAgentMode(MCPChatWindow w) =>
            (bool)typeof(MCPChatWindow)
                .GetField("_agentMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        // ── Test 1: SetMode_SameMode_NoOp ────────────────────────────────────

        [Test]
        public void SetMode_SameMode_IsNoOp()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                bool before = GetAgentMode(w);
                var backend = GetBackend(w);

                CallSetMode(w, before); // same mode → early return

                Assert.AreEqual(before,  GetAgentMode(w));
                Assert.AreSame(backend, GetBackend(w), "backend must not change on same-mode call");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Test 2: SetMode_DoesNotReplaceBackend ────────────────────────────

        [Test]
        public void SetMode_DoesNotReplaceBackend_OnModeSwitch()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Arrange: inject a fake backend with a known SessionId
                var fakeBackend = new FakeBackend("sess-abc123");
                InjectBackend(w, fakeBackend);

                bool initialMode = GetAgentMode(w); // false (Ask)

                // Act: switch to opposite mode
                CallSetMode(w, !initialMode);

                // Assert: same backend instance — no process kill/restart on mode switch
                var currentBackend = GetBackend(w);
                Assert.AreSame(fakeBackend, currentBackend,
                    "SetMode must NOT replace backend — mode switch is a pure UI state flip");
                Assert.AreEqual("sess-abc123", currentBackend.SessionId,
                    "session ID unchanged because same backend instance is reused");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Test 3: SetMode_NullSessionId_DoesNotCrash ───────────────────────

        [Test]
        public void SetMode_NullSessionId_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Inject backend with null SessionId (no session yet)
                var fakeBackend = new FakeBackend(); // SessionId defaults to null
                InjectBackend(w, fakeBackend);

                bool initialMode = GetAgentMode(w);
                Assert.DoesNotThrow(() => CallSetMode(w, !initialMode));

                // Backend is unchanged (mode switch is a no-op for the process)
                Assert.AreSame(fakeBackend, GetBackend(w), "backend must not change");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Test 4: SetMode_FlipsAgentMode ───────────────────────────────────

        [Test]
        public void SetMode_FlipsAgentMode_Flag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectBackend(w, new FakeBackend());
                bool before = GetAgentMode(w);
                CallSetMode(w, !before);
                Assert.AreEqual(!before, GetAgentMode(w), "_agentMode must be flipped");
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
