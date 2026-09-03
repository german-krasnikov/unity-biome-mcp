// TDD (DEV-66): TestRunService/TestRunObserver post-reload recovery scheduling must
// never depend on EditorApplication.delayCall — a backgrounded Editor (no focus/render
// frames) does not reliably drain it (RELAY-FIX, commit 1bcc90b7). Mirrors the existing
// R3-01 source-guard pattern (ProjectConfigWriterTests/PluginUpdateMonitorTests).
using NUnit.Framework;
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
    }
}
