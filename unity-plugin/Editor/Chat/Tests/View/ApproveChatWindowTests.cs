// TDD: ApproveAndExecute — T9 mode acknowledgement gate.
// Tests cover the null-prompt guard, non-relay fallback, and SetModeAsync integration.
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ApproveChatWindowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private sealed class FakeBackend : IChatBackend
        {
            public bool             IsRunning => false;
            public string           SessionId { get; }
            public List<string>     SentTurns { get; } = new List<string>();

            public FakeBackend(string sessionId = null) { SessionId = sessionId; }

            public void Start()  { }
            public void Stop()   { }
            public void SendTurn(string j)            { lock (SentTurns) SentTurns.Add(j); }
            public void SendControlResponse(string j) { }
            public void DrainEvents(List<ChatEvent> o, List<ToolCallRecord> t = null) { }
        }

        private static void InjectBackend(MCPChatWindow w, IChatBackend backend) =>
            typeof(MCPChatWindow)
                .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(w, backend);

        private static bool GetAgentMode(MCPChatWindow w) =>
            (bool)typeof(MCPChatWindow)
                .GetField("_agentMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        private static void InvokeApproveAndExecute(MCPChatWindow w) =>
            typeof(MCPChatWindow)
                .GetMethod("ApproveAndExecute", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(w, null);

        private static void SetRelaySessionId(RelayBackend rb, string sessionId) =>
            typeof(RelayBackend)
                .GetField("_sessionId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(rb, sessionId);

        private static async Task WaitUntilAsync(System.Func<bool> condition, string message, int timeoutMs = 3000)
        {
            var deadline = System.DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!condition())
            {
                if (System.DateTime.UtcNow > deadline) Assert.Fail(message);
                await Task.Delay(10);
            }
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void ApproveAndExecute_WhenNullSessionId_DoesNotSetAgentMode()
        {
            // Null sessionId → ApproveHelper returns null → early return, _agentMode unchanged
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectBackend(w, new FakeBackend(null));
                InvokeApproveAndExecute(w);
                Assert.IsFalse(GetAgentMode(w), "_agentMode must stay false when prompt is null");
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void ApproveAndExecute_WhenNoRelayBackend_SetsAgentModeSynchronously()
        {
            // Non-RelayBackend (FakeBackend) → fallback path → _agentMode set synchronously
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectBackend(w, new FakeBackend("sess-test"));
                InvokeApproveAndExecute(w);
                Assert.IsTrue(GetAgentMode(w), "fallback path must set _agentMode=true");
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public async Task ApproveAndExecute_WhenSetModeOk_SetsAgentMode()
        {
            // RelayBackend with fake proc returning ok=true → _agentMode flipped in callback
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
            {
                if (json.Contains("set_mode")) return "{\"ok\":true,\"data\":\"spawned\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelaySpawner.EnsureRunningOverride = () => 19600;

            var rb = new RelayBackend("claude", "ask", "", 9500);
            RegisterCleanup(rb.Stop);
            rb.Start();
            SetRelaySessionId(rb, "sess-ok");

            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectBackend(w, rb);
                InvokeApproveAndExecute(w);

                await WaitUntilAsync(() => GetAgentMode(w),
                    "ApproveAndExecute with ok=true did not set _agentMode");
                Assert.IsTrue(GetAgentMode(w));
            }
            finally
            {
                RelayBackend.ProcessFactory        = null;
                RelaySpawner.EnsureRunningOverride  = null;
                RelaySpawner.StopForTests();
                Object.DestroyImmediate(w);
            }
        }

        [Test]
        public async Task ApproveAndExecute_WhenSetModeFails_DoesNotSetAgentMode()
        {
            // RelayBackend with fake proc returning ok=false → _agentMode stays false
            RelayBackend.ProcessFactory = () => new RelayChatProcess(json =>
            {
                if (json.Contains("set_mode")) return "{\"ok\":false,\"err\":\"spawn failed\"}";
                return "{\"ok\":true,\"data\":\"\"}";
            });
            RelaySpawner.EnsureRunningOverride = () => 19600;

            var rb = new RelayBackend("claude", "ask", "", 9500);
            RegisterCleanup(rb.Stop);
            rb.Start();
            SetRelaySessionId(rb, "sess-fail");

            bool callbackFired = false;
            MCPChatWindow.OnModeSetCallback = _ => callbackFired = true;

            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectBackend(w, rb);
                InvokeApproveAndExecute(w);

                await WaitUntilAsync(() => callbackFired,
                    "SetModeAsync onDone was never called");
                Assert.IsFalse(GetAgentMode(w), "_agentMode must stay false when mode switch fails");
            }
            finally
            {
                MCPChatWindow.OnModeSetCallback    = null;
                RelayBackend.ProcessFactory        = null;
                RelaySpawner.EnsureRunningOverride  = null;
                RelaySpawner.StopForTests();
                Object.DestroyImmediate(w);
            }
        }
    }
}
