// Monkey tests: model/kind field state, ModelPresetsPerKind, backend injection.
// Does NOT duplicate: BackendModelMonkeyTests (ApplySelectedModel/CloneWithModel),
//                     SetModeTests, WindowStateMonkeyTests initial-state tests.
#if UNITY_MCP_CHAT
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowModelChaosTests
    {
        private static readonly FieldInfo s_kind  = typeof(MCPChatWindow).GetField("_selectedKind",  BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_model = typeof(MCPChatWindow).GetField("_selectedModel", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_back  = typeof(MCPChatWindow).GetField("_backend",       BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_agent = typeof(MCPChatWindow).GetField("_agentMode",     BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_set  = typeof(MCPChatWindow).GetMethod("SetMode",       BindingFlags.NonPublic | BindingFlags.Instance);

        private sealed class TrackingBackend : IChatBackend
        {
            public bool IsRunning { get; set; }
            public string SessionId => "track";
            public void Start()  { IsRunning = true; }
            public void Stop()   { IsRunning = false; }
            public void SendTurn(string j) { }
            public void SendControlResponse(string j) { }
            public void DrainEvents(List<ChatEvent> o, List<ToolCallRecord> t = null) { }
        }

        // ── ModelPresetsPerKind ───────────────────────────────────────────────

        [Test] public void Presets_Claude_NotEmpty()
            => Assert.Greater(MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude].Length, 0);

        [Test] public void Presets_Codex_NotEmpty()
            => Assert.Greater(MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex].Length, 0);

        [Test] public void Presets_Kimi_NotEmpty()
            => Assert.Greater(MCPChatWindow.ModelPresetsPerKind[BackendKind.Kimi].Length, 0);

        [Test] public void Presets_Antigravity_NotEmpty()
            => Assert.Greater(MCPChatWindow.ModelPresetsPerKind[BackendKind.Antigravity].Length, 0);

        [Test] public void Presets_OpenCode_NotEmpty()
            => Assert.Greater(MCPChatWindow.ModelPresetsPerKind[BackendKind.OpenCode].Length, 0);

        [Test]
        public void Presets_AllEnumValues_HaveEntry()
        {
            var p = MCPChatWindow.ModelPresetsPerKind;
            foreach (BackendKind k in System.Enum.GetValues(typeof(BackendKind)))
                Assert.IsTrue(p.ContainsKey(k), $"Missing: {k}");
        }

        [Test]
        public void Presets_AllLabels_NonEmpty()
        {
            foreach (var kv in MCPChatWindow.ModelPresetsPerKind)
                foreach (var e in kv.Value)
                    Assert.IsFalse(string.IsNullOrEmpty(e.label), $"Empty label in {kv.Key}");
        }

        [Test] public void DefaultAlias_NotEmpty()
            => Assert.Greater(MCPChatWindow.ModelPresets.Length, 0);

        // ── Initial field state ───────────────────────────────────────────────

        [Test]
        public void InitialSelectedKind_IsClaude()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try   { Assert.AreEqual(BackendKind.Claude, (BackendKind)s_kind.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialSelectedModel_IsEmpty()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try   { Assert.AreEqual("", (string)s_model.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialBackend_IsNotNull()
        {
            // OnEnable() eagerly calls CreateBackend() (since v0.67.0 Chat Relay System,
            // commit 8707c2d) so a RelayBackend instance exists immediately on window
            // creation — it is never null after ScriptableObject.CreateInstance triggers
            // OnEnable. The backend only becomes null again in OnDisable().
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try   { Assert.IsNotNull(s_back.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Reflection roundtrip ──────────────────────────────────────────────

        [Test]
        public void SelectedKind_SetAllValues_RoundTrip()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                foreach (BackendKind k in System.Enum.GetValues(typeof(BackendKind)))
                {
                    s_kind.SetValue(w, k);
                    Assert.AreEqual(k, (BackendKind)s_kind.GetValue(w));
                }
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SelectedModel_Unicode_Preserved()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_model.SetValue(w, "モデル-v1");
                Assert.AreEqual("モデル-v1", (string)s_model.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Backend + SetMode ─────────────────────────────────────────────────

        [Test]
        public void SetMode_WithTrackingBackend_SameInstance()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var b = new TrackingBackend();
                s_back.SetValue(w, b);
                s_set.Invoke(w, new object[] { true });
                Assert.AreSame(b, s_back.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_1000Alternating_NoException()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_back.SetValue(w, new TrackingBackend());
                Assert.DoesNotThrow(() => { for (int i = 0; i < 1000; i++) s_set.Invoke(w, new object[] { i % 2 == 0 }); });
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_1000xTrue_AgentModeTrue()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_back.SetValue(w, new TrackingBackend());
                for (int i = 0; i < 1000; i++) s_set.Invoke(w, new object[] { true });
                Assert.IsTrue((bool)s_agent.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_NullBackend_SetsFlag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_set.Invoke(w, new object[] { true });
                Assert.IsTrue((bool)s_agent.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
#endif
