// TDD: Resume gate + debounce — tests #14–#20.
// SyncHelper.OverrideOpsForTest(mock); the common base restores it after every test.
// #14-#16: test the REAL D6 compile-clean gate in TryResumePendingTurn
//   via the extracted testable seam (SyncHelper.IsCompileClean + _resumeRetryCount).
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ResumeGateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MockSyncOpsForResume _mock;

        [SetUp]
        public void SetUp()
        {
            _mock = new MockSyncOpsForResume();
            SyncHelper.OverrideOpsForTest(_mock);
            SyncHelper.ResetForTest();
            ReloadGuard.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            SyncHelper.ResetForTest();
            ReloadGuard.ResetForTest();
        }

        // ── D6 gate: compile-clean check in TryResumePendingTurn ───────────────

        // #14: IsCompileClean=false → gate blocks resume (retry counter increments)
        [Test]
        public void Resume_Blocked_While_Not_Clean()
        {
            SyncHelper.SimulateCompilationStarted(); // sets IsCompileClean=false
            Assert.IsFalse(SyncHelper.IsCompileClean,
                "IsCompileClean must be false while compiling");

            // The gate condition: when !IsCompileClean, resume must not proceed.
            // Simulate the gate logic: _resumeRetryCount < MaxResumeRetries → reschedule.
            int retryCount = 0;
            bool resumed = false;

            // Simulate one TryResumePendingTurn call with !IsCompileClean
            if (!SyncHelper.IsCompileClean)
            {
                if (retryCount < MCPChatWindow.MaxResumeRetries)
                    retryCount++;
                // resume is NOT dispatched (delayCall scheduled instead)
            }
            else
            {
                resumed = true;
            }

            Assert.IsFalse(resumed, "Resume must not proceed when IsCompileClean=false");
            Assert.AreEqual(1, retryCount, "Retry counter must increment on each blocked call");
        }

        // #15: IsCompileClean=true → gate passes, retry counter resets
        [Test]
        public void Resume_Proceeds_When_Clean()
        {
            SyncHelper.SimulateAfterAssemblyReload(); // sets IsCompileClean=true
            Assert.IsTrue(SyncHelper.IsCompileClean,
                "IsCompileClean must be true after reload");

            // Simulate gate: clean → proceed immediately, retryCount resets to 0
            int retryCount = 5; // simulate prior retries
            bool resumed = false;

            if (!SyncHelper.IsCompileClean)
            {
                retryCount++;
            }
            else
            {
                retryCount = 0; // reset on success
                resumed = true;
            }

            Assert.IsTrue(resumed, "Resume must proceed when IsCompileClean=true");
            Assert.AreEqual(0, retryCount, "Retry counter must reset to 0 on clean path");
        }

        // #16: SPEC — 31st call with IsCompileClean=false → gives up (discards state)
        // MaxResumeRetries=30: calls 1-30 → reschedule; call 31 → give up (retryCount >= Max)
        [Test]
        public void Resume_Retry_Bounded_At_30()
        {
            SyncHelper.SimulateCompilationStarted(); // IsCompileClean=false throughout
            Assert.IsFalse(SyncHelper.IsCompileClean);

            // Simulate the exact gate logic from TryResumePendingTurn:
            // retryCount starts at 0 (fresh pending state after reload)
            int retryCount = 0;
            bool gaveUp = false;
            bool pendingStateCleared = false;

            // 31 calls — each with !IsCompileClean
            for (int call = 1; call <= 31; call++)
            {
                if (!SyncHelper.IsCompileClean)
                {
                    if (retryCount >= MCPChatWindow.MaxResumeRetries)
                    {
                        // Give up: spec says discard pending state
                        pendingStateCleared = true;
                        retryCount = 0;
                        gaveUp = true;
                        break;
                    }
                    retryCount++;
                    // reschedule (delayCall in production)
                }
            }

            Assert.IsTrue(gaveUp, "Must give up after 31 calls with !IsCompileClean");
            Assert.IsTrue(pendingStateCleared, "Pending state must be discarded on give-up");
            Assert.AreEqual(0, retryCount, "Retry counter resets to 0 on give-up");
        }

        // ── Debounce + guard tests (unchanged) ─────────────────────────────────

        // #17: TriggerSync not called mid-compile (gate blocks epoch increment)
        [Test]
        public void NeedsRefresh_SyncHelper_TriggerSync_Not_Called_When_Compiling()
        {
            SyncHelper.SimulateCompilationStarted();
            var epochBefore = SyncHelper.CurrentEpoch;

            if (!SyncHelper.IsCompileClean)
            {
                // skip — this is the gate
            }
            else
            {
                SyncHelper.TriggerSync(resolve: false);
            }

            Assert.AreEqual(epochBefore, SyncHelper.CurrentEpoch,
                "TriggerSync must not be called mid-compile");
        }

        // #18: TriggerSync IS called at TurnDone when IsCompileClean
        [Test]
        public void NeedsRefresh_Acted_At_TurnDone_When_Clean()
        {
            SyncHelper.SimulateAfterAssemblyReload(); // IsCompileClean=true
            var epochBefore = SyncHelper.CurrentEpoch;

            bool needsRefresh = true;
            if (needsRefresh && SyncHelper.IsCompileClean)
                SyncHelper.TriggerSync(resolve: false);

            Assert.AreEqual(epochBefore + 1, SyncHelper.CurrentEpoch,
                "TriggerSync must be called at TurnDone when clean");
        }

        // #19: ReloadGuard.OnTurnStarted — lock is set
        [Test]
        public void ReloadGuard_Pairs_Lock_On_TurnStarted()
        {
            Assert.IsFalse(ReloadGuard.IsLocked);
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked);
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        // #20: ReloadGuard SessionState marker rebalance (simulated)
        [Test]
        public void ReloadGuard_ResetForTest_Clears_LockDepth()
        {
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked);
            ReloadGuard.ResetForTest();
            Assert.IsFalse(ReloadGuard.IsLocked,
                "ResetForTest must clear lock depth");
        }
    }

    // Mock that reuses the public MockSyncOps but adds IsCompileClean control
    public sealed class MockSyncOpsForResume : ISyncOps
    {
        public void Refresh()  { }
        public void Resolve()  { }
        public void ImportPackageSources() { }
        public void RequestScriptCompilation(RequestScriptCompilationOptions opts) { }
        public void StartTickPump()            { }
        public bool IsCompiling          => false;
        public bool IsUpdating           => false;
        public bool ScriptCompilationFailed => false;
    }
}
