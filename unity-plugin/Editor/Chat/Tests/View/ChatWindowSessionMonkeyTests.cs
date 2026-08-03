// Monkey tests: session state — backend lifecycle, token counters, resumeRetryCount,
// SessionAllowlist stress, activity chains, SentTextCache.
// Does NOT duplicate: TokenResetTests, WindowStateMonkeyTests, HandleEventMonkeyTests,
//                     SessionAllowlistMonkeyTests (first 12 in ChatUIMonkeyTests).
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowSessionMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly FieldInfo s_inp   = typeof(MCPChatWindow).GetField("_inputTokens",    BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_out   = typeof(MCPChatWindow).GetField("_outputTokens",   BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_act   = typeof(MCPChatWindow).GetField("_activity",       BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_list  = typeof(MCPChatWindow).GetField("_sessionAllowlist", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_fire = typeof(MCPChatWindow).GetMethod("HandleEvent",    BindingFlags.NonPublic | BindingFlags.Instance);

        private static void Fire(MCPChatWindow w, ChatEvent ev) => s_fire.Invoke(w, new object[] { ev });

        // ── Token counter state ───────────────────────────────────────────────

        [Test]
        public void ResetTokenCounters_10xRapid_NoException()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { s_inp.SetValue(w, 999); Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) w.ResetTokenCounters(); }); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_3Turns_LastTokensWin()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                Fire(w, ChatEvent.TurnDone("s1", 0.001f, 100, 50));
                Fire(w, ChatEvent.TurnDone("s1", 0.002f, 300, 150));
                Fire(w, ChatEvent.TurnDone("s1", 0.003f, 200, 80));
                Assert.AreEqual(200, (int)s_inp.GetValue(w));
                Assert.AreEqual(80,  (int)s_out.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_LargeTokenValues_NoThrow()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.TurnDone("s1", 999f, int.MaxValue, int.MaxValue))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_Heartbeat_10x_NoException()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) Fire(w, ChatEvent.Heartbeat()); }); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── ResumeRetryCount ──────────────────────────────────────────────────

        [Test] public void ResumeRetryCount_InitiallyZero()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.AreEqual(0, w._resumeRetryCount); } finally { Object.DestroyImmediate(w); }
        }

        [Test] public void MaxResumeRetries_IsPositive()
            => Assert.Greater(MCPChatWindow.MaxResumeRetries, 0);

        [Test]
        public void ResumeRetryCount_AboveMax_NoGuard()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { w._resumeRetryCount = MCPChatWindow.MaxResumeRetries + 100; Assert.AreEqual(MCPChatWindow.MaxResumeRetries + 100, w._resumeRetryCount); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── SessionAllowlist stress ───────────────────────────────────────────

        [Test]
        public void SessionAllowlist_100Tools_AllAutoApproved()
        {
            var list = new SessionAllowlist();
            var tools = new string[100];
            for (int i = 0; i < 100; i++) tools[i] = $"MCPMonkeyS100_{i}";
            try
            {
                foreach (var t in tools) list.AddSession(t);
                foreach (var t in tools) Assert.IsTrue(list.IsAutoApproved(t));
            }
            finally { list.ClearSession(); }
        }

        [Test]
        public void SessionAllowlist_ClearSession_5x_Idempotent()
        {
            var list = new SessionAllowlist();
            list.AddSession("MCPMonkeyS5x_X");
            Assert.DoesNotThrow(() => { for (int i = 0; i < 5; i++) list.ClearSession(); });
            Assert.IsFalse(list.IsAutoApproved("MCPMonkeyS5x_X"));
        }

        [Test]
        public void SessionAllowlist_InjectedWindow_NotNull()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.IsNotNull(s_list.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SessionAllowlist_InjectedWindow_InitiallyEmpty()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.IsFalse(((SessionAllowlist)s_list.GetValue(w)).IsAutoApproved("AnyTool")); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Activity state ────────────────────────────────────────────────────

        [Test]
        public void Activity_SendFirstTokenTurnDone_BackToIdle()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var a = (ChatActivityState)s_act.GetValue(w);
                a.Send(); a.FirstToken();
                Fire(w, ChatEvent.TurnDone("s1", 0.001f, 100, 50));
                Assert.AreEqual(ActivityPhase.Idle, a.Phase);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── SentTextCache ─────────────────────────────────────────────────────

        [Test] public void SentTextCache_Default_IsEmpty()
            => Assert.AreEqual("", new SentTextCache().Get());

        [Test] public void SentTextCache_SetAndGet_Roundtrip()
            { var c = new SentTextCache(); c.Set("hello"); Assert.AreEqual("hello", c.Get()); }

        [Test] public void SentTextCache_SetNull_StoresEmpty()
            { var c = new SentTextCache(); c.Set(null); Assert.AreEqual("", c.Get()); }

        [Test] public void SentTextCache_Overwrite_ReturnsLast()
            { var c = new SentTextCache(); c.Set("first"); c.Set("second"); Assert.AreEqual("second", c.Get()); }
    }
}
