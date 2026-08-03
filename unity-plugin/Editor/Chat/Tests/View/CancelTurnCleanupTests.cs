// TDD — F20 gap-fill: cleanup side-effect chain triggered by CancelTurn().
// Verifies ReloadGuard, TurnUndoTracker, and backend recreation.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CancelTurnCleanupTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ReloadGuard.ResetForTest();
        [TearDown] public void TearDown() => ReloadGuard.ResetForTest();

        private static ChatActivityState GetActivity(MCPChatWindow w)
            => (ChatActivityState)typeof(MCPChatWindow)
                .GetField("_activity", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        private static TurnUndoTracker GetUndoTracker(MCPChatWindow w)
            => (TurnUndoTracker)typeof(MCPChatWindow)
                .GetField("_undoTracker", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        private static IChatBackend GetBackend(MCPChatWindow w)
            => (IChatBackend)typeof(MCPChatWindow)
                .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        // 1. cancel unlocks ReloadGuard
        [Test]
        public void CancelTurn_UnlocksReloadGuard()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                ReloadGuard.OnTurnStarted();
                Assert.IsTrue(ReloadGuard.IsLocked, "Guard should be locked before cancel");

                GetActivity(window).Send();
                window.CancelTurn();

                Assert.IsFalse(ReloadGuard.IsLocked, "Guard must be unlocked after cancel");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 2. cancel closes in-flight undo group → InflightGroupId == -1
        [Test]
        public void CancelTurn_ClosesUndoGroup()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var tracker = GetUndoTracker(window);
                tracker.OnTurnStart("test");
                Assert.GreaterOrEqual(tracker.InflightGroupId, 0, "Group should be open");

                GetActivity(window).Send();
                window.CancelTurn();

                Assert.AreEqual(-1, tracker.InflightGroupId,
                    "InflightGroupId must be -1 after cancel");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 3. cancel replaces backend with a fresh instance
        [Test]
        public void CancelTurn_StopsBackendAndRecreates()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var backendBefore = GetBackend(window);

                GetActivity(window).Send();
                window.CancelTurn();

                var backendAfter = GetBackend(window);
                Assert.IsNotNull(backendAfter, "Backend must be recreated after cancel");
                Assert.AreNotSame(backendBefore, backendAfter,
                    "Cancel must recreate backend — not reuse the old reference");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 4. cancel from Receiving resets all three subsystems
        [Test]
        public void CancelTurn_FromReceiving_FullCleanup()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                ReloadGuard.OnTurnStarted();
                var tracker = GetUndoTracker(window);
                tracker.OnTurnStart("full");

                var activity = GetActivity(window);
                activity.Send();
                activity.FirstToken();
                Assert.AreEqual(ActivityPhase.Receiving, activity.Phase);

                window.CancelTurn();

                Assert.AreEqual(ActivityPhase.Idle, activity.Phase,    "activity reset");
                Assert.IsFalse(ReloadGuard.IsLocked,                   "guard unlocked");
                Assert.AreEqual(-1, tracker.InflightGroupId,           "undo group closed");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 5. double cancel does not under-decrement ReloadGuard
        [Test]
        public void CancelTurn_Double_NoReloadGuardUnderflow()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                ReloadGuard.OnTurnStarted();
                GetActivity(window).Send();
                window.CancelTurn();
                Assert.DoesNotThrow(() => window.CancelTurn());
                Assert.IsFalse(ReloadGuard.IsLocked, "Guard must not be locked after double cancel");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 6. double cancel leaves tracker in valid idle state
        [Test]
        public void CancelTurn_Double_NoUndoTrackerDoubleFail()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var tracker = GetUndoTracker(window);
                tracker.OnTurnStart("dbl");
                GetActivity(window).Send();
                window.CancelTurn();
                Assert.DoesNotThrow(() => window.CancelTurn());
                Assert.AreEqual(-1, tracker.InflightGroupId,
                    "InflightGroupId must stay -1 after double cancel");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // P0-2 RED: CancelTurn must reset _turnEditedCode, _turnHasToolCalls, _needsRefresh
        [Test]
        public void CancelTurn_ResetsTurnEditedCode()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                window._turnEditedCode = true;
                GetActivity(window).Send();
                window.CancelTurn();
                Assert.IsFalse(window._turnEditedCode,
                    "_turnEditedCode must be false after CancelTurn");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void CancelTurn_ResetsTurnHasToolCalls()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                window._turnHasToolCalls = true;
                GetActivity(window).Send();
                window.CancelTurn();
                Assert.IsFalse(window._turnHasToolCalls,
                    "_turnHasToolCalls must be false after CancelTurn");
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void CancelTurn_ResetsNeedsRefresh()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                window._needsRefresh = true;
                GetActivity(window).Send();
                window.CancelTurn();
                Assert.IsFalse(window._needsRefresh,
                    "_needsRefresh must be false after CancelTurn");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // P0-2: stale-flag cleared — all three flags false simultaneously after stale turn
        [Test]
        public void CancelTurn_AllTurnFlagsCleared_Simultaneously()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                window._turnEditedCode   = true;
                window._turnHasToolCalls = true;
                window._needsRefresh     = true;
                GetActivity(window).Send();
                window.CancelTurn();
                Assert.IsFalse(window._turnEditedCode,   "_turnEditedCode");
                Assert.IsFalse(window._turnHasToolCalls, "_turnHasToolCalls");
                Assert.IsFalse(window._needsRefresh,     "_needsRefresh");
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
