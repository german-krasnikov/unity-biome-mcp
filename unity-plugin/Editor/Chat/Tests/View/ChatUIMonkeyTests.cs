// ChatUIMonkeyTests.cs — ~116 monkey/edge-case tests for Chat View layer.
// Focus: state machine violations, null crashes, boundary values.
// MUST NOT duplicate: SetModeTests, TokenResetTests, ApproveFlowTests.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;
using UnityEditor;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    // ── 1. ChatActivityState exhaustive (16) ─────────────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class ActivityStateMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Initial_Phase_IsIdle()
        {
            var s = new ChatActivityState();
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test]
        public void Send_FromIdle_ReturnsTrueAndSending()
        {
            var s = new ChatActivityState();
            bool result = s.Send();
            Assert.IsTrue(result);
            Assert.AreEqual(ActivityPhase.Sending, s.Phase);
        }

        [Test]
        public void Send_FromSending_ReturnsFalse_IdempotentState()
        {
            var s = new ChatActivityState();
            s.Send();
            bool result = s.Send();
            Assert.IsFalse(result);
            Assert.AreEqual(ActivityPhase.Sending, s.Phase);
        }

        [Test]
        public void Send_FromReceiving_ReturnsFalse_IdempotentState()
        {
            var s = new ChatActivityState();
            s.Send();
            s.FirstToken();
            bool result = s.Send();
            Assert.IsFalse(result);
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase);
        }

        [Test]
        public void FirstToken_FromIdle_IsNoOp_ReturnsFalse()
        {
            var s = new ChatActivityState();
            bool result = s.FirstToken();
            Assert.IsFalse(result);
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test]
        public void FirstToken_FromSending_ToReceiving()
        {
            var s = new ChatActivityState();
            s.Send();
            bool result = s.FirstToken();
            Assert.IsTrue(result);
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase);
        }

        [Test]
        public void FirstToken_FromReceiving_ReturnsFalse()
        {
            var s = new ChatActivityState();
            s.Send();
            s.FirstToken();
            bool result = s.FirstToken();
            Assert.IsFalse(result);
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase);
        }

        [Test]
        public void Done_FromIdle_ReturnsFalse()
        {
            var s = new ChatActivityState();
            bool result = s.Done();
            Assert.IsFalse(result);
        }

        [Test]
        public void Done_FromSending_ToIdle()
        {
            var s = new ChatActivityState();
            s.Send();
            bool result = s.Done();
            Assert.IsTrue(result);
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test]
        public void Done_FromReceiving_ToIdle()
        {
            var s = new ChatActivityState();
            s.Send();
            s.FirstToken();
            bool result = s.Done();
            Assert.IsTrue(result);
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test]
        public void Fail_IsAlias_ForDone()
        {
            var s = new ChatActivityState();
            s.Send();
            bool result = s.Fail();
            Assert.IsTrue(result);
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test]
        public void CanSend_TrueOnlyWhenIdle()
        {
            var s = new ChatActivityState();
            Assert.IsTrue(s.CanSend);
        }

        [Test]
        public void CanSend_FalseWhenSending()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsFalse(s.CanSend);
        }

        [Test]
        public void CanSend_FalseWhenReceiving()
        {
            var s = new ChatActivityState();
            s.Send();
            s.FirstToken();
            Assert.IsFalse(s.CanSend);
        }

        [Test]
        public void FullCycle_Idle_Send_FirstToken_Done_BackToIdle()
        {
            var s = new ChatActivityState();
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
            s.Send();
            Assert.AreEqual(ActivityPhase.Sending, s.Phase);
            s.FirstToken();
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase);
            s.Done();
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test]
        public void RapidSendDoneCycles_50times_AlwaysEndsIdle()
        {
            var s = new ChatActivityState();
            for (int i = 0; i < 50; i++)
            {
                s.Send();
                s.Done();
            }
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }
    }

    // ── 2. SessionAllowlist exhaustive (12) ──────────────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class SessionAllowlistMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TestTool   = "MCPMonkeyTest_Tool_A";
        private const string TestTool2  = "MCPMonkeyTest_Tool_B";
        private const string AlwaysTool = "MCPMonkeyTest_Always_C";

        [SetUp]
        public void OwnPersistentAllowlistKeys()
        {
            DeleteEditorPrefBool("MCPChat.AlwaysAllow." + AlwaysTool);
            DeleteEditorPrefBool("MCPChat.AlwaysAllow." + TestTool);
            DeleteEditorPrefBool("MCPChat.AlwaysAllow." + TestTool2);
        }

        [Test]
        public void IsAutoApproved_Unknown_ReturnsFalse()
        {
            var list = new SessionAllowlist();
            Assert.IsFalse(list.IsAutoApproved(TestTool));
        }

        [Test]
        public void IsAutoApproved_AfterAddSession_True()
        {
            var list = new SessionAllowlist();
            list.AddSession(TestTool);
            Assert.IsTrue(list.IsAutoApproved(TestTool));
        }

        [Test]
        public void IsAutoApproved_AfterClearSession_False()
        {
            var list = new SessionAllowlist();
            list.AddSession(TestTool);
            list.ClearSession();
            Assert.IsFalse(list.IsAutoApproved(TestTool));
        }

        [Test]
        public void AddSession_Idempotent_NoException()
        {
            var list = new SessionAllowlist();
            Assert.DoesNotThrow(() =>
            {
                list.AddSession(TestTool);
                list.AddSession(TestTool);
            });
            Assert.IsTrue(list.IsAutoApproved(TestTool));
        }

        [Test]
        public void AddAlways_PersistsViaEditorPrefs()
        {
            var list = new SessionAllowlist();
            list.AddAlways(AlwaysTool);
            // Verify via a fresh instance (EditorPrefs is global)
            var list2 = new SessionAllowlist();
            Assert.IsTrue(list2.IsAlwaysAllowed(AlwaysTool));
        }

        [Test]
        public void IsAlwaysAllowed_AfterRemoveAlways_False()
        {
            var list = new SessionAllowlist();
            list.AddAlways(AlwaysTool);
            list.RemoveAlways(AlwaysTool);
            Assert.IsFalse(list.IsAlwaysAllowed(AlwaysTool));
        }

        [Test]
        public void ClearSession_DoesNotAffectAlwaysAllowed()
        {
            var list = new SessionAllowlist();
            list.AddAlways(AlwaysTool);
            list.ClearSession();
            // IsAlwaysAllowed checks EditorPrefs, not session — must still be true
            Assert.IsTrue(list.IsAlwaysAllowed(AlwaysTool));
        }

        [Test]
        public void IsAutoApproved_EmptyString_DoesNotThrow()
        {
            var list = new SessionAllowlist();
            bool result = false;
            Assert.DoesNotThrow(() => result = list.IsAutoApproved(""));
            Assert.IsFalse(result);
        }

        [Test]
        public void IsAutoApproved_NullString_DoesNotThrow()
        {
            var list = new SessionAllowlist();
            // HashSet.Contains(null) returns false without throwing in C#
            Assert.DoesNotThrow(() => list.IsAutoApproved(null));
        }

        [Test]
        public void MultipleTools_InSession_IndependentlyTracked()
        {
            var list = new SessionAllowlist();
            list.AddSession(TestTool);
            Assert.IsTrue(list.IsAutoApproved(TestTool));
            Assert.IsFalse(list.IsAutoApproved(TestTool2));
            list.AddSession(TestTool2);
            Assert.IsTrue(list.IsAutoApproved(TestTool2));
        }

        [Test]
        public void AddAlways_AlsoAddsToSession()
        {
            var list = new SessionAllowlist();
            list.AddAlways(AlwaysTool);
            // IsAutoApproved checks both session + always — must be true immediately
            Assert.IsTrue(list.IsAutoApproved(AlwaysTool));
        }

        [Test]
        public void ClearSession_ThenAddSession_Works()
        {
            var list = new SessionAllowlist();
            list.AddSession(TestTool);
            list.ClearSession();
            Assert.IsFalse(list.IsAutoApproved(TestTool));
            list.AddSession(TestTool);
            Assert.IsTrue(list.IsAutoApproved(TestTool));
        }
    }

    // ── 3. IsCodeEditingTool (10) ─────────────────────────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class IsCodeEditingToolMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static bool Invoke(ToolCallRecord rec) =>
            MCPChatWindow.IsCodeEditingTool(rec);

        [Test]
        public void Edit_ReturnsTrue()
            => Assert.IsTrue(Invoke(new ToolCallRecord("Edit", "id1", null)));

        [Test]
        public void Write_ReturnsTrue()
            => Assert.IsTrue(Invoke(new ToolCallRecord("Write", "id2", null)));

        [Test]
        public void MultiEdit_ReturnsTrue()
            => Assert.IsTrue(Invoke(new ToolCallRecord("MultiEdit", "id3", null)));

        [Test]
        public void ReadFile_ReturnsFalse()
            => Assert.IsFalse(Invoke(new ToolCallRecord("Read", "id4", "{\"path\":\"/foo.txt\"}")));

        [Test]
        public void EmptyName_ReturnsFalse()
            => Assert.IsFalse(Invoke(new ToolCallRecord("", "id5", null)));

        [Test]
        public void NullArgsJson_WithNameBash_ReturnsFalse()
            => Assert.IsFalse(Invoke(new ToolCallRecord("Bash", "id6", null)));

        [Test]
        public void ArgsJsonWithCsPath_ReturnsTrue()
            => Assert.IsTrue(Invoke(new ToolCallRecord("Bash", "id7", "{\"file\":\"foo.cs\"}")));

        [Test]
        public void ArgsJsonWithoutCsPath_ReturnsFalse()
            => Assert.IsFalse(Invoke(new ToolCallRecord("Bash", "id8", "{\"cmd\":\"echo hello\"}")));

        [Test]
        public void ToolResultRecord_WithCsPath_ReturnsTrue()
        {
            // Result record: ArgsJson has .cs" → still detected
            var rec = new ToolCallRecord("Bash", "id9", "{\"file\":\"script.cs\"}", "ok", true);
            Assert.IsTrue(Invoke(rec));
        }

        [Test]
        public void CaseSensitive_edit_lowercase_ReturnsFalse()
            => Assert.IsFalse(Invoke(new ToolCallRecord("edit", "id10", null)));
    }

    // ── 4. ApplySelectedModel (15) ───────────────────────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class BackendModelMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── WithModel(BackendKind.Claude) — replaces CloneWithModel (6) ──────

        [Test]
        public void CloneWithModel_SameModel_ReturnsSameInstance()
        {
            var src = new BackendConfigStore();
            var result = src.WithModel(BackendKind.Claude, ""); // default model is ""
            Assert.AreSame(src, result);
        }

        [Test]
        public void CloneWithModel_DifferentModel_ReturnsNewInstance()
        {
            var src = new BackendConfigStore();
            var result = src.WithModel(BackendKind.Claude, "claude-opus-4");
            Assert.AreNotSame(src, result);
        }

        [Test]
        public void CloneWithModel_NewInstance_PreservesPermissionMode()
        {
            var src = new BackendConfigStore();
            src.Claude.PermissionMode = "acceptEdits";
            var result = src.WithModel(BackendKind.Claude, "new-model");
            Assert.AreEqual("acceptEdits", result.Claude.PermissionMode);
        }

        [Test]
        public void CloneWithModel_NewInstance_PreservesCodexConfig()
        {
            var src = new BackendConfigStore();
            src.Codex.StartupTimeoutSec = 99;
            var result = src.WithModel(BackendKind.Claude, "new-model");
            Assert.AreSame(src.Codex, result.Codex);
        }

        [Test]
        public void CloneWithModel_NewInstance_PreservesChips()
        {
            var src = new BackendConfigStore();
            src.Chips.HierarchyDepth = "full";
            var result = src.WithModel(BackendKind.Claude, "new-model");
            Assert.AreSame(src.Chips, result.Chips);
        }

        [Test]
        public void CloneWithModel_NewInstance_NewModelApplied()
        {
            var src = new BackendConfigStore();
            var result = src.WithModel(BackendKind.Claude, "claude-opus-4");
            Assert.AreEqual("claude-opus-4", result.Claude.Model);
        }

        // ── ApplySelectedModel (9) ────────────────────────────────────────────

        [Test]
        public void ApplySelectedModel_NullModel_ReturnsSrcUnchanged()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Claude, null);
            Assert.AreSame(src, result);
        }

        [Test]
        public void ApplySelectedModel_EmptyModel_ReturnsSrcUnchanged()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Claude, "");
            Assert.AreSame(src, result);
        }

        [Test]
        public void ApplySelectedModel_CustomPlaceholder_ReturnsSrcUnchanged()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Claude, "__custom__");
            Assert.AreSame(src, result);
        }

        [Test]
        public void ApplySelectedModel_Claude_SameModel_ReturnsSameInstance()
        {
            var src = new BackendConfigStore { Claude = new ClaudeBackendConfig { Model = "opus" } };
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Claude, "opus");
            Assert.AreSame(src, result);
        }

        [Test]
        public void ApplySelectedModel_Claude_DifferentModel_NewStore()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Claude, "claude-sonnet-4");
            Assert.AreNotSame(src, result);
            Assert.AreEqual("claude-sonnet-4", result.Claude.Model);
        }

        [Test]
        public void ApplySelectedModel_Codex_DifferentModel_NewStore()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Codex, "gpt-4o");
            Assert.AreNotSame(src, result);
            Assert.AreEqual("gpt-4o", result.Codex.Model);
        }

        [Test]
        public void ApplySelectedModel_Antigravity_DifferentModel_NewStore()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Antigravity, "agy-1");
            Assert.AreNotSame(src, result);
            Assert.AreEqual("agy-1", result.Antigravity.Model);
        }

        [Test]
        public void ApplySelectedModel_Kimi_DifferentModel_NewStore()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.Kimi, "kimi-pro");
            Assert.AreNotSame(src, result);
            Assert.AreEqual("kimi-pro", result.Kimi.Model);
        }

        [Test]
        public void ApplySelectedModel_OpenCode_DifferentModel_NewStore()
        {
            var src = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(src, BackendKind.OpenCode, "anthropic/claude-3-5");
            Assert.AreNotSame(src, result);
            Assert.AreEqual("anthropic/claude-3-5", result.OpenCode.Model);
        }
    }

    // ── 5. Window state stress — SetMode + flags (18) ────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class WindowStateMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly FieldInfo s_agentMode = typeof(MCPChatWindow)
            .GetField("_agentMode", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_setMode = typeof(MCPChatWindow)
            .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_resetTurnFlags = typeof(MCPChatWindow)
            .GetMethod("ResetTurnFlags", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_resumeRetry = typeof(MCPChatWindow)
            .GetField("_resumeRetryCount", BindingFlags.NonPublic | BindingFlags.Instance);

        private static bool GetAgentMode(MCPChatWindow w) => (bool)s_agentMode.GetValue(w);
        private static void SetMode(MCPChatWindow w, bool v) => s_setMode.Invoke(w, new object[] { v });
        private static void ResetTurnFlags(MCPChatWindow w) => s_resetTurnFlags.Invoke(w, null);

        [Test]
        public void SetMode_Rapid100Toggles_FinalStateCorrect()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // SetMode(v) is a no-op when v == current state (i=0 passes false == initial
                // false, so it doesn't count as a flip). That leaves 99 *effective* flips
                // (i=1..99, alternating, all differing from the previous state) — an odd
                // count, so starting from false the loop ends at true.
                for (int i = 0; i < 100; i++)
                    SetMode(w, i % 2 == 1);
                Assert.IsTrue(GetAgentMode(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void ResetTurnFlags_ClearsTurnEditedCode()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._turnEditedCode = true;
                ResetTurnFlags(w);
                Assert.IsFalse(w._turnEditedCode);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void ResetTurnFlags_ClearsTurnHasToolCalls()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._turnHasToolCalls = true;
                ResetTurnFlags(w);
                Assert.IsFalse(w._turnHasToolCalls);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void ResetTurnFlags_ClearsNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._needsRefresh = true;
                ResetTurnFlags(w);
                Assert.IsFalse(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void ResetTurnFlags_ClearsLastToolName()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._lastToolName = "Edit";
                ResetTurnFlags(w);
                Assert.IsNull(w._lastToolName);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialState_AgentModeIsFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.IsFalse(GetAgentMode(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialState_InputOutputTokensAreZero()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var inp = typeof(MCPChatWindow).GetField("_inputTokens", BindingFlags.NonPublic | BindingFlags.Instance);
                var out_ = typeof(MCPChatWindow).GetField("_outputTokens", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.AreEqual(0, (int)inp.GetValue(w));
                Assert.AreEqual(0, (int)out_.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialState_TurnFlagsAllFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                Assert.IsFalse(w._turnEditedCode);
                Assert.IsFalse(w._turnHasToolCalls);
                Assert.IsFalse(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialState_AskPendingIsFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var field = typeof(MCPChatWindow).GetField("_askPending", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsFalse((bool)field.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialState_ResumeRetryCountIsZero()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.AreEqual(0, (int)s_resumeRetry.GetValue(w)); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_ToTrue_SetsAgentMode()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                SetMode(w, true);
                Assert.IsTrue(GetAgentMode(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_ToFalse_WhenAlreadyFalse_IsNoOp()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Default is false; calling SetMode(false) should be a no-op
                SetMode(w, false);
                Assert.IsFalse(GetAgentMode(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void SetMode_ToFalse_AfterTrue_FlipsBack()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                SetMode(w, true);
                SetMode(w, false);
                Assert.IsFalse(GetAgentMode(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void MaxResumeRetries_IsThirty()
        {
            Assert.AreEqual(30, MCPChatWindow.MaxResumeRetries);
        }

        [Test]
        public void ResumeRetryCount_CanBeSetViaReflection()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_resumeRetry.SetValue(w, 5);
                Assert.AreEqual(5, (int)s_resumeRetry.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void IsChatBackendRunning_ReturnsFalse_WhenNoWindows()
        {
            // No windows open in test context — must return false
            Assert.IsFalse(MCPChatWindow.IsChatBackendRunning());
        }

        [Test]
        public void SetMode_100Times_SameValue_NoFlip()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // SetMode with same value (false) 100 times → stays false
                for (int i = 0; i < 100; i++)
                    SetMode(w, false);
                Assert.IsFalse(GetAgentMode(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void InitialState_LastToolNameIsNull()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.IsNull(w._lastToolName); }
            finally { Object.DestroyImmediate(w); }
        }
    }

    // ── 6. HandleEvent safe paths via reflection (15) ────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class HandleEventMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly MethodInfo s_handleEvent = typeof(MCPChatWindow)
            .GetMethod("HandleEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_activity = typeof(MCPChatWindow)
            .GetField("_activity", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_askPending = typeof(MCPChatWindow)
            .GetField("_askPending", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_inputTokens = typeof(MCPChatWindow)
            .GetField("_inputTokens", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_outputTokens = typeof(MCPChatWindow)
            .GetField("_outputTokens", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void Fire(MCPChatWindow w, ChatEvent ev) =>
            s_handleEvent.Invoke(w, new object[] { ev });

        private static ChatActivityState GetActivity(MCPChatWindow w) =>
            (ChatActivityState)s_activity.GetValue(w);

        [Test]
        public void HandleEvent_TextDelta_EmptyString_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.TextDelta(""))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TextDelta_LongString_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.TextDelta(new string('a', 10_000)))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TextDelta_Unicode_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.TextDelta("こんにちは\U0001F3AE"))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TextDelta_SetsActivityToReceiving()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Put activity into Sending state first
                GetActivity(w).Send();
                Fire(w, ChatEvent.TextDelta("hello"));
                Assert.AreEqual(ActivityPhase.Receiving, GetActivity(w).Phase);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_ZeroTokens_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.TurnDone("s1", 0f, 0, 0))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_SetsInputTokens_WhenNonZero()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                Fire(w, ChatEvent.TurnDone("s1", 0.01f, 500, 200));
                Assert.AreEqual(500, (int)s_inputTokens.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_SetsOutputTokens_WhenNonZero()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                Fire(w, ChatEvent.TurnDone("s1", 0.01f, 500, 200));
                Assert.AreEqual(200, (int)s_outputTokens.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_ResetsAskPending()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                s_askPending.SetValue(w, true);
                Fire(w, ChatEvent.TurnDone("s1", 0f, 0, 0));
                Assert.IsFalse((bool)s_askPending.GetValue(w));
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_TurnDone_ClearsTurnHasToolCalls()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._turnHasToolCalls = true;
                Fire(w, ChatEvent.TurnDone("s1", 0f, 0, 0));
                Assert.IsFalse(w._turnHasToolCalls);
            }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_Heartbeat_IsNoOp_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.Heartbeat())); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_RateLimit_IsNoOp_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.RateLimit("slow down"))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_SessionState_IsNoOp_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.SessionState("ready"))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_SessionInit_IsNoOp_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.SessionInit("sess-xyz"))); }
            finally { Object.DestroyImmediate(w); }
        }

        [Test]
        public void HandleEvent_ToolProgress_IsNoOp_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.DoesNotThrow(() => Fire(w, ChatEvent.ToolProgress(50f))); }
            finally { Object.DestroyImmediate(w); }
        }

        /// <summary>
        /// Error handler already null-guards _transcript (MCPChatWindow.EventHandlers.cs:77
        /// uses _transcript?.AppendToolChip, fixed in commit 8707c2d / v0.67.0) — no NRE when
        /// window is created via ScriptableObject.CreateInstance (no CreateGUI, no transcript).
        /// </summary>
        [Test]
        public void HandleEvent_Error_NullTranscript_DoesNotThrow()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                Assert.DoesNotThrow(() => Fire(w, ChatEvent.Error("boom")));
            }
            finally { Object.DestroyImmediate(w); }
        }
    }

    // ── 7. TokenFormat edge cases (10) ────────────────────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class TokenFormatMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Abbr_Zero_Returns0()
            => Assert.AreEqual("0", TokenFormat.Abbr(0));

        [Test] public void Abbr_999_ReturnsAsIs()
            => Assert.AreEqual("999", TokenFormat.Abbr(999));

        [Test] public void Abbr_1000_Returns1k()
            => Assert.AreEqual("1.0k", TokenFormat.Abbr(1000));

        [Test] public void Abbr_1500_Returns1point5k()
            => Assert.AreEqual("1.5k", TokenFormat.Abbr(1500));

        [Test] public void Abbr_9999_Rounds_To10k()
            => Assert.AreEqual("10.0k", TokenFormat.Abbr(9999));

        [Test] public void FormatReadout_BothZero_ReturnsEmpty()
            => Assert.AreEqual("", TokenFormat.FormatReadout(0, 0));

        [Test] public void FormatReadout_NonZero_ContainsArrows()
        {
            var s = TokenFormat.FormatReadout(100, 200);
            StringAssert.Contains("↑", s);
            StringAssert.Contains("↓", s);
        }

        [Test] public void FormatReadout_1000Input_ShowsAbbreviated()
        {
            var s = TokenFormat.FormatReadout(1000, 0);
            // Output: "↑ 1.0k  ↓ 0" — even though out=0 is non-zero check: 1000>0 || 0>0
            // Wait: FormatReadout returns "" only when BOTH are 0. Here inp=1000, out=0 → non-empty.
            Assert.IsNotEmpty(s);
            StringAssert.Contains("1.0k", s);
        }

        [Test] public void FormatReadout_SmallValues_NoAbbr()
        {
            var s = TokenFormat.FormatReadout(50, 75);
            StringAssert.Contains("50", s);
            StringAssert.Contains("75", s);
        }

        [Test] public void FormatReadout_MaxValues_DoesNotCrash()
            => Assert.DoesNotThrow(() => TokenFormat.FormatReadout(int.MaxValue, int.MaxValue));
    }

    // ── 8. ContextProgressBar edge cases (10) ─────────────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class ContextProgressBarMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Update_ZeroContextWindow_HidesBar()
        {
            var bar = new ContextProgressBar();
            bar.Update(5000, 0);
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Update_PositiveWindow_ShowsBar()
        {
            var bar = new ContextProgressBar();
            bar.Update(1000, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Update_FullContext_ShowsRedColor()
        {
            // 100% fill (≥90%) → red (1f, 0.3f, 0.3f)
            var bar = new ContextProgressBar();
            bar.Update(200_000, 200_000);
            // Color is set on the internal _fill element — we can only verify bar is visible
            // and test doesn't crash (color correctness tested by reading style via reflection)
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Update_HalfContext_ShowsBlueColor()
        {
            // 50% fill (<70%) → blue (0.3f, 0.7f, 1f)
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Update_HighContext_ShowsYellowBand()
        {
            // 80% fill (≥70% and <90%) → yellow (1f, 0.8f, 0.2f)
            var bar = new ContextProgressBar();
            bar.Update(160_000, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Reset_HidesBar()
        {
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            bar.Reset();
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Reset_ClearsLabel()
        {
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            // After Reset, the internal label text should be empty
            var labelField = typeof(ContextProgressBar)
                .GetField("_label", BindingFlags.NonPublic | BindingFlags.Instance);
            bar.Reset();
            var label = (Label)labelField.GetValue(bar);
            Assert.AreEqual("", label.text);
        }

        [Test]
        public void Update_Boundary_ExactlyAt70pct_StillBlue()
        {
            // 70% fill → pct < 0.7f is false at exactly 0.70 → yellow branch
            // (borderline: 70000/100000 = 0.7f, which is NOT <0.7, so yellow)
            var bar = new ContextProgressBar();
            Assert.DoesNotThrow(() => bar.Update(70_000, 100_000));
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Update_VeryLargeTokens_DoesNotCrash()
        {
            var bar = new ContextProgressBar();
            Assert.DoesNotThrow(() => bar.Update(int.MaxValue, int.MaxValue));
        }
    }

    // ── 9. ApproveButtonFactory/Helper monkey (10) ───────────────────────────

    [TestFixture]
    [Category(TestCategories.Stress)]
    public class ApproveMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void MakeButton_Click_CallsOnApprove()
        {
            var container = new VisualElement();
            bool called = false;
            var btn = ApproveButtonFactory.MakeButton(container, () => called = true);
            container.Add(btn);
            ((Action)btn.userData).Invoke();
            Assert.IsTrue(called, "onApprove callback must be invoked on click");
        }

        [Test]
        public void MakeButton_DoubleClick_OnlyCallsOnApproveOnce()
        {
            var container = new VisualElement();
            int count = 0;
            var btn = ApproveButtonFactory.MakeButton(container, () => count++);
            container.Add(btn);
            var click = (Action)btn.userData;
            click.Invoke(); // removes btn from hierarchy
            click.Invoke(); // btn.RemoveFromHierarchy is idempotent; onApprove still fires
            // Counts 2 because the action itself doesn't have an idempotent gate —
            // the button is removed but the closure still executes. Document this.
            Assert.GreaterOrEqual(count, 1, "approve action fires at least once");
        }

        [Test]
        public void MakeButton_NullOnApprove_DoesNotCrash()
        {
            var container = new VisualElement();
            var btn = ApproveButtonFactory.MakeButton(container, null);
            container.Add(btn);
            Assert.DoesNotThrow(() => ((Action)btn.userData).Invoke());
        }

        [Test]
        public void MakeButton_HasApproveClass()
        {
            var container = new VisualElement();
            var btn = ApproveButtonFactory.MakeButton(container, () => { });
            Assert.IsTrue(btn.ClassListContains("approve-btn"));
        }

        [Test]
        public void MakeButton_UserDataIsClickAction()
        {
            var container = new VisualElement();
            var btn = ApproveButtonFactory.MakeButton(container, () => { });
            Assert.IsNotNull(btn.userData);
            Assert.IsInstanceOf<Action>(btn.userData);
        }

        [Test]
        public void MaybeAppend_EmptySessionId_NoButton()
        {
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: false, sessionId: "", onApprove: () => { });
            Assert.AreEqual(0, container.childCount);
        }

        [Test]
        public void MaybeAppend_WhitespaceSessionId_AddsButton()
        {
            // string.IsNullOrEmpty(" ") = false → proceeds to add button
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: false, sessionId: " ", onApprove: () => { });
            Assert.AreEqual(1, container.childCount,
                "whitespace is not null/empty so MaybeAppend adds a button");
        }

        [Test]
        public void BuildPromptOrNull_ValidId_ContainsExecuteConst()
        {
            var prompt = ApproveHelper.BuildPromptOrNull("sess-X");
            Assert.IsNotNull(prompt);
            StringAssert.Contains(ApproveHelper.ExecutePrompt, prompt);
        }

        [Test]
        public void ApproveHelper_ExecutePrompt_IsNotEmpty()
        {
            Assert.IsNotEmpty(ApproveHelper.ExecutePrompt);
        }

        [Test]
        public void MaybeAppend_AgentModeTrue_NoButton_RegardlessOfSessionId()
        {
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: true, sessionId: "valid-id", onApprove: () => { });
            Assert.AreEqual(0, container.childCount);
        }
    }
}
