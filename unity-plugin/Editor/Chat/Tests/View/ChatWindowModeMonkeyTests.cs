// Monkey tests: mode switch combos not covered by SetModeTests / WindowStateMonkeyTests.
// Focus: 1000x stress, activity + turn-flags + askPending interaction, session ID.
// Does NOT duplicate: SetModeTests (4), WindowStateMonkeyTests SetMode (5),
//                     TokenResetTests SetMode (2).
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowModeMonkeyTests
    {
        private static readonly FieldInfo  s_agent = typeof(MCPChatWindow).GetField("_agentMode",  BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo  s_back  = typeof(MCPChatWindow).GetField("_backend",    BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo  s_act   = typeof(MCPChatWindow).GetField("_activity",   BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo  s_ask   = typeof(MCPChatWindow).GetField("_askPending", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_set   = typeof(MCPChatWindow).GetMethod("SetMode",    BindingFlags.NonPublic | BindingFlags.Instance);

        private sealed class FakeBackend : IChatBackend
        {
            public bool IsRunning => false;
            public string SessionId { get; }
            public FakeBackend(string sid = null) { SessionId = sid; }
            public void Start()  { }
            public void Stop()   { }
            public void SendTurn(string j) { }
            public void SendControlResponse(string j) { }
            public void DrainEvents(List<ChatEvent> o, List<ToolCallRecord> t = null) { }
        }

        private static void Set(MCPChatWindow w, bool v) => s_set.Invoke(w, new object[] { v });
        private static bool Get(MCPChatWindow w) => (bool)s_agent.GetValue(w);

        // ── 1000x stress ──────────────────────────────────────────────────────

        [Test]
        public void SetMode_1000Alternating_NoException()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => { for (int i = 0; i < 1000; i++) Set(w, i % 2 == 0); }); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_1000Alternating_FinalStateCorrect()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { for (int i = 0; i < 1000; i++) Set(w, i % 2 == 0); Assert.IsFalse(Get(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_1000xTrue_AgentModeTrue()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { for (int i = 0; i < 1000; i++) Set(w, true); Assert.IsTrue(Get(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_1000xFalse_AgentModeFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Set(w, true); for (int i = 0; i < 1000; i++) Set(w, false); Assert.IsFalse(Get(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Mode + backend ─────────────────────────────────────────────────────

        [Test]
        public void SetMode_NullBackend_SetsFlag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Set(w, true); Assert.IsTrue(Get(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_WithFakeBackend_SameInstance()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { var fb = new FakeBackend("s-42"); s_back.SetValue(w, fb); Set(w, true); Assert.AreSame(fb, s_back.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_NullSessionId_NoException()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { s_back.SetValue(w, new FakeBackend(null)); Assert.DoesNotThrow(() => Set(w, true)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_BackendSessionId_NotModified()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var fb = new FakeBackend("sess-xyz");
                s_back.SetValue(w, fb);
                Set(w, true); Set(w, false);
                Assert.AreEqual("sess-xyz", ((IChatBackend)s_back.GetValue(w)).SessionId);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Mode + activity state ──────────────────────────────────────────────

        [Test]
        public void SetMode_WhileIdle_ActivityStaysIdle()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Set(w, true); Assert.AreEqual(ActivityPhase.Idle, ((ChatActivityState)s_act.GetValue(w)).Phase); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_WhileReceiving_ActivityUnchanged()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var a = (ChatActivityState)s_act.GetValue(w);
                a.Send(); a.FirstToken();
                Set(w, true);
                Assert.AreEqual(ActivityPhase.Receiving, a.Phase);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Mode + turn flags ──────────────────────────────────────────────────

        [Test]
        public void SetMode_DoesNotClearTurnEditedCode()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { w._turnEditedCode = true; Set(w, true); Assert.IsTrue(w._turnEditedCode); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_DoesNotClearTurnHasToolCalls()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { w._turnHasToolCalls = true; Set(w, true); Assert.IsTrue(w._turnHasToolCalls); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Mode + askPending ──────────────────────────────────────────────────

        [Test]
        public void SetMode_ToAgent_AskPendingUnchanged()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { s_ask.SetValue(w, true); Set(w, true); Assert.IsTrue((bool)s_ask.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Mode flag sequences ────────────────────────────────────────────────

        [Test]
        public void SetMode_ResumeRetryCountUnchanged()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { w._resumeRetryCount = 7; Set(w, true); Assert.AreEqual(7, w._resumeRetryCount); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_NeedsRefresh_Unaffected()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { w._needsRefresh = true; Set(w, true); Assert.IsTrue(w._needsRefresh); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_Sequence_TFTF_EndsAtFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Set(w, true); Set(w, false); Set(w, true); Set(w, false); Assert.IsFalse(Get(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_Sequence_FTFT_EndsAtTrue()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Set(w, false); Set(w, true); Set(w, false); Set(w, true); Assert.IsTrue(Get(w)); }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
