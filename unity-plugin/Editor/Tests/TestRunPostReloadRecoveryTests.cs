// TDD (DEV-66): TestRunService/TestRunObserver post-reload recovery scheduling must
// never depend on EditorApplication.delayCall — a backgrounded Editor (no focus/render
// frames) does not reliably drain it (RELAY-FIX, commit 1bcc90b7). Mirrors the existing
// R3-01 source-guard pattern (ProjectConfigWriterTests/PluginUpdateMonitorTests).
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class TestRunPostReloadRecoveryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void TestRunService_SchedulesFinalizationViaMainThreadDispatcher_NotDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(TestRunService), "Editor/TestRuns/TestRunService.cs");
            Assert.That(src, Does.Contain("MainThreadDispatcher.Enqueue"),
                "TestRunService must schedule finalization via MainThreadDispatcher (EditorApplication.update-driven) — delayCall does not drain in a backgrounded Editor (see RELAY-FIX, commit 1bcc90b7)");
            Assert.That(src, Does.Not.Contain("delayCall"),
                "TestRunService must not depend on delayCall anywhere — it does not drain in a backgrounded Editor");
        }

        [Test]
        public void TestRunObserver_SchedulesPostReloadRecoveryViaEditorApplicationUpdate_NotDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(TestRunObserverRegistration), "Editor/TestRuns/TestRunObserver.cs");
            Assert.That(src, Does.Contain("EditorApplication.update"),
                "TestRunObserver must schedule post-reload recovery via EditorApplication.update — delayCall does not drain in a backgrounded Editor (see RELAY-FIX, commit 1bcc90b7)");
            Assert.That(src, Does.Not.Contain("delayCall"),
                "TestRunObserver must not depend on delayCall anywhere — it does not drain in a backgrounded Editor");
        }

        // ── MAJOR (C1 r4 #2): the Play Mode re-arm guard had zero behavioral coverage ──

        private static void UnsubscribeWaitForPlayModeExit()
        {
            while ((EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                   .Any(d => d.Method.Name == nameof(TestRunObserverRegistration.WaitForPlayModeExit)))
                EditorApplication.update -= TestRunObserverRegistration.WaitForPlayModeExit;
        }

        [Test]
        public void RecoverTerminalEnvironments_WhilePlayModeTransitioning_ArmsWaitForPlayModeExit()
        {
            TestRunObserverRegistration.IsPlayingOrWillChangePlaymode = () => true;
            RegisterCleanup(TestRunObserverRegistration.ResetPlayModeSeamForTest);
            RegisterCleanup(UnsubscribeWaitForPlayModeExit);

            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            TestRunObserverRegistration.RecoverTerminalEnvironments();
            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();

            Assert.AreEqual(1, added.Length,
                "must arm exactly one poll while Play Mode is transitioning — the recovery body must not run yet");
            Assert.AreEqual(nameof(TestRunObserverRegistration.WaitForPlayModeExit), added[0].Method.Name,
                "the armed delegate must be WaitForPlayModeExit");
        }

        [Test]
        public void WaitForPlayModeExit_WhileStillTransitioning_StaysSubscribed()
        {
            TestRunObserverRegistration.IsPlayingOrWillChangePlaymode = () => true;
            RegisterCleanup(TestRunObserverRegistration.ResetPlayModeSeamForTest);
            RegisterCleanup(UnsubscribeWaitForPlayModeExit);

            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            TestRunObserverRegistration.RecoverTerminalEnvironments();
            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();
            Assert.AreEqual(1, added.Length, "precondition: exactly one poll must be armed while transitioning");

            foreach (var d in added) d.DynamicInvoke(); // simulate a tick — still transitioning

            var stillSubscribed = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Intersect(added).ToArray();
            Assert.AreEqual(added.Length, stillSubscribed.Length,
                "WaitForPlayModeExit must remain subscribed while Play Mode transition has not finished");
        }

        [Test]
        public void WaitForPlayModeExit_OnceExited_UnsubscribesAndRunsRecovery()
        {
            TestRunObserverRegistration.IsPlayingOrWillChangePlaymode = () => true;
            RegisterCleanup(TestRunObserverRegistration.ResetPlayModeSeamForTest);
            RegisterCleanup(UnsubscribeWaitForPlayModeExit);

            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            TestRunObserverRegistration.RecoverTerminalEnvironments();
            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();
            Assert.AreEqual(1, added.Length, "precondition: exactly one poll must be armed while transitioning");

            TestRunObserverRegistration.IsPlayingOrWillChangePlaymode = () => false;

            Assert.DoesNotThrow(() =>
            {
                foreach (var d in added) d.DynamicInvoke();
            }, "recovery must run without throwing once Play Mode has exited");

            var stillSubscribed = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Intersect(added).ToArray();
            Assert.IsEmpty(stillSubscribed,
                "WaitForPlayModeExit must unsubscribe itself once Play Mode has actually exited");
        }

        [Test]
        public void RecoverTerminalEnvironments_CalledTwiceDuringPlayMode_SubscribesOnce()
        {
            TestRunObserverRegistration.IsPlayingOrWillChangePlaymode = () => true;
            RegisterCleanup(TestRunObserverRegistration.ResetPlayModeSeamForTest);
            RegisterCleanup(UnsubscribeWaitForPlayModeExit);

            var beforeCount = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>()).Length;
            TestRunObserverRegistration.RecoverTerminalEnvironments();
            TestRunObserverRegistration.RecoverTerminalEnvironments();
            var afterCount = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>()).Length;

            // Raw length delta, not a deduped Except/Distinct — two equal static-method
            // delegates would otherwise collapse to one and hide a real double-subscribe.
            Assert.AreEqual(1, afterCount - beforeCount,
                "two RecoverTerminalEnvironments calls during Play Mode must not accumulate a second WaitForPlayModeExit subscriber");
        }
    }
}
