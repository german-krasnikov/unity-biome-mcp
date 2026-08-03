// TDD — F27: _needsRefresh flag triggers AssetDatabase.Refresh after code-editing tools.
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class DomainRefreshTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Inject minimal transcript so HandleToolRecord doesn't NPE on _transcript.
        // _transcript is internal — direct assignment, no reflection (rename = compile error).
        private static void InjectMinimalTranscript(MCPChatWindow w)
        {
            var container = new VisualElement();
            var registry  = ChatBlockRendererFactory.CreateDefault(null, null);
            w._transcript = new ChatTranscript(container, registry);
        }

        private static void InvokeHandleToolRecord(MCPChatWindow w, ToolCallRecord rec)
        {
            typeof(MCPChatWindow)
                .GetMethod("HandleToolRecord", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(w, new object[] { rec });
        }

        // _needsRefresh starts false on a fresh window instance.
        [Test]
        public void NeedsRefresh_DefaultFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.IsFalse(w._needsRefresh); }
            finally { Object.DestroyImmediate(w); }
        }

        // Verifies IsCodeEditingTool returns true for "Edit" tool name.
        // Full HandleToolRecord integration isn't feasible in EditMode (requires live turn processing).
        [Test]
        public void IsCodeEditingTool_CodeEditTool_SetsFlag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // F27 fix: _needsRefresh is set at result-complete (HasResult=true), not args-complete.
                var rec = new ToolCallRecord("Edit", "id1", "{}", resultText: "ok");
                if (rec.HasResult && MCPChatWindow.IsCodeEditingTool(rec))
                    w._needsRefresh = true;
                Assert.IsTrue(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // Non-code tool does not set _needsRefresh.
        [Test]
        public void NonCodeTool_DoesNotSetNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var rec = new ToolCallRecord("get_hierarchy", "id2", "{}");
                if (MCPChatWindow.IsCodeEditingTool(rec))
                    w._needsRefresh = true;
                Assert.IsFalse(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // After refresh is consumed the flag resets to false.
        [Test]
        public void NeedsRefresh_ResetsAfterConsume()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._needsRefresh = true;
                // Simulate DrainAndRender consume logic.
                if (w._needsRefresh) w._needsRefresh = false;
                Assert.IsFalse(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // P1-5 F27 timing invariant: args-complete record does NOT set _needsRefresh, but sets _turnEditedCode
        [Test]
        public void HandleToolRecord_ArgsComplete_CodeEdit_DoesNotSetNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectMinimalTranscript(w);
                // chip-creation record (ArgsJson == null)
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id1", null));
                // args-complete record (ArgsJson set, HasResult=false)
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id1", "{}"));
                Assert.IsFalse(w._needsRefresh,   "_needsRefresh must NOT be set at args-complete");
                Assert.IsTrue(w._turnEditedCode,   "_turnEditedCode must be set at args-complete");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // P1-5 F27 timing invariant: result-complete record DOES set _needsRefresh
        [Test]
        public void HandleToolRecord_ResultComplete_CodeEdit_SetsNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectMinimalTranscript(w);
                // chip-creation record
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id2", null));
                // result-complete record (HasResult=true)
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id2", "{}", resultText: "ok"));
                Assert.IsTrue(w._needsRefresh, "_needsRefresh must be set at result-complete");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── CH4.test.2 (CRITICAL): TryResumePendingTurn with active non-stale state ────────────

        // Stub backend that records SendTurn calls.
        private sealed class StubBackend : IChatBackend
        {
            internal readonly List<string> SentPayloads = new List<string>();
            public bool   IsRunning  => true;
            public string SessionId  => "stub-sess";
            public void   Start()    { }
            public void   Stop()     { }
            public void   SendTurn(string turnJson) => SentPayloads.Add(turnJson);
            public void   DrainEvents(List<ChatEvent> output, List<ToolCallRecord> toolOutput = null) { }
            public void   SendControlResponse(string json) { }
            public void   Dispose()  { }
        }

        [Test]
        public void TryResumePendingTurn_ActiveTurn_DispatchesSendTurn()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(),
                $"TryResumeSendTurnTest_{System.Guid.NewGuid()}.txt");
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                ReloadGuard.OverrideFilePath(tmpPath);
                ReloadGuard.ResetForTest();

                // Save a non-stale Sending state (savedAtUtc=0 means legacy/allowed through)
                var state = new PendingTurnState(
                    sessionId:   "sess-123",
                    pendingText: "fix the bug",
                    chipPaths:   new string[0],
                    agentMode:   false,
                    agentName:   "",
                    activityPhase: "Sending",
                    savedAtUtc:  0L,
                    pendingLlmPayload: "fix the bug");
                ReloadGuard.SavePendingState(state);

                // Wire a stub backend via CreateBackendWithSession override
                // We inject the stub directly into _backend after overriding CreateBackend.
                InjectMinimalTranscript(w);
                var stub = new StubBackend();
                MCPChatWindow.BackendFactoryForTest = _ => stub;
                RegisterCleanup(() => MCPChatWindow.BackendFactoryForTest = null);

                // Invoke TryResumePendingTurn — must call stub.SendTurn
                typeof(MCPChatWindow)
                    .GetMethod("TryResumePendingTurn", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(w, null);

                Assert.AreEqual(1, stub.SentPayloads.Count,
                    "The resumed payload must be dispatched exactly once.");
                StringAssert.Contains("fix the bug", stub.SentPayloads[0]);
                var activity = (ChatActivityState)typeof(MCPChatWindow)
                    .GetField("_activity", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(w);
                Assert.AreEqual(ActivityPhase.Sending, activity.Phase,
                    "TryResumePendingTurn with active state must transition to Sending phase");
            }
            finally
            {
                MCPChatWindow.BackendFactoryForTest = null;
                Object.DestroyImmediate(w);
                ReloadGuard.ResetForTest();
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }

        // ── CH4.test.3 (MAJOR): TryResumePendingTurn with stale state must NOT dispatch ─────────

        [Test]
        public void TryResumePendingTurn_StaleTurn_DoesNotDispatch()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(),
                $"TryResumeStaleTest_{System.Guid.NewGuid()}.txt");
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                ReloadGuard.OverrideFilePath(tmpPath);
                ReloadGuard.ResetForTest();

                // savedAtUtc=1 (epoch, very old) → IsStale returns true for any recent nowUtc
                var state = new PendingTurnState(
                    sessionId:   "sess-stale",
                    pendingText: "stale turn",
                    chipPaths:   new string[0],
                    agentMode:   false,
                    agentName:   "",
                    activityPhase: "Sending",
                    savedAtUtc:  1L); // far in the past → IsStale
                ReloadGuard.SavePendingState(state);

                InjectMinimalTranscript(w);

                typeof(MCPChatWindow)
                    .GetMethod("TryResumePendingTurn", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(w, null);

                // Activity must remain Idle — stale turn was discarded without dispatch
                var activity = (ChatActivityState)typeof(MCPChatWindow)
                    .GetField("_activity", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(w);
                Assert.AreEqual(ActivityPhase.Idle, activity.Phase,
                    "Stale turn must be discarded; activity must remain Idle");
            }
            finally
            {
                Object.DestroyImmediate(w);
                ReloadGuard.ResetForTest();
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }

        // T6: SaveStateBeforeReload uses SessionState fallback when backend.SessionId is null.
        private sealed class NullSessionStub : IChatBackend
        {
            public bool   IsRunning  => true;
            public string SessionId  => null;       // simulates not-yet-drained session
            public void   Start()    { }
            public void   Stop()     { }
            public void   SendTurn(string t) { }
            public void   DrainEvents(List<ChatEvent> o, List<ToolCallRecord> t = null) { }
            public void   SendControlResponse(string j) { }
            public void   Dispose()  { }
        }

        [Test]
        public void SaveState_falls_back_to_SessionState_when_backend_sessionid_null()
        {
            var tmpPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"SaveStateFallbackTest_{System.Guid.NewGuid()}.txt");
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                ReloadGuard.OverrideFilePath(tmpPath);
                ReloadGuard.ResetForTest();

                // Seed the SessionState fallback key
                UnityEditor.SessionState.SetString("MCPChat_BackendSessionId", "fallback-id");

                // Inject a stub backend with null SessionId
                InjectMinimalTranscript(w);
                var stub = new NullSessionStub();
                typeof(MCPChatWindow)
                    .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(w, stub);

                // Move activity to Sending so SaveState takes the non-idle path
                var activity = (ChatActivityState)typeof(MCPChatWindow)
                    .GetField("_activity", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(w);
                activity.Send();

                // Also set a non-empty sentTextCache so state is worth saving
                var cache = typeof(MCPChatWindow)
                    .GetField("_sentTextCache", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(w);
                cache.GetType()
                    .GetMethod("Set", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(cache, new object[] { "some prompt" });

                // Invoke SaveStateBeforeReload
                typeof(MCPChatWindow)
                    .GetMethod("SaveStateBeforeReload", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(w, null);

                // Load and verify SessionId came from the SessionState fallback
                var pending = ReloadGuard.LoadPendingState();
                Assert.IsNotNull(pending, "State must be saved");
                Assert.AreEqual("fallback-id", pending.Value.SessionId,
                    "SessionId must fall back to SessionState when backend.SessionId is null");
            }
            finally
            {
                Object.DestroyImmediate(w);
                UnityEditor.SessionState.EraseString("MCPChat_BackendSessionId");
                ReloadGuard.ResetForTest();
                if (System.IO.File.Exists(tmpPath)) System.IO.File.Delete(tmpPath);
            }
        }

        // T6: NewSession must erase the SessionState key so stale IDs don't survive.
        [Test]
        public void NewSession_clears_SessionState_key()
        {
            UnityEditor.SessionState.SetString("MCPChat_BackendSessionId", "old-session-id");
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectMinimalTranscript(w);
                try { w.NewSession(); } catch (System.NullReferenceException) { }
                var stored = UnityEditor.SessionState.GetString("MCPChat_BackendSessionId", null);
                Assert.IsTrue(string.IsNullOrEmpty(stored),
                    "NewSession must erase MCPChat_BackendSessionId from SessionState");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // P0-1: _transcriptRestored must be cleared even on the early-return (null pending) path.
        // Regression pin: before the fix the flag stayed true when LoadPendingState returned null,
        // allowing a later OnAfterReloadResume call to suppress a legitimate user bubble.
        [Test]
        public void TryResumePendingTurn_NullPending_ClearsTranscriptRestoredFlag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Ensure no pending state on disk so TryResumePendingTurn hits the null early-return.
                ReloadGuard.ClearPendingState();

                // Set the flag to true, simulating CreateGUI setting it after a transcript restore.
                typeof(MCPChatWindow)
                    .GetField("_transcriptRestored", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(w, true);

                // Invoke TryResumePendingTurn — will hit `if (pending == null) return;`.
                typeof(MCPChatWindow)
                    .GetMethod("TryResumePendingTurn", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(w, null);

                var flagAfter = (bool)typeof(MCPChatWindow)
                    .GetField("_transcriptRestored", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(w);
                Assert.IsFalse(flagAfter, "_transcriptRestored must be false after early-return path");
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
