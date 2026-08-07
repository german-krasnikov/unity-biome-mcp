using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// NUnit tests for P-109 (Camera MissingRef after fresh reload) and
    /// P-291 (infinite MissingReferenceException log storm from monitors).
    /// </summary>
    [TestFixture]
    public class PlaytestRunnerFreshModeTests : SceneTestBase
    {
        [TearDown]
        public void TearDown()
        {
            PlaytestMonitorRegistry.Reset();
            PlaytestRunner.SetFreshTestState(false, false, false);
        }

        // ── P-109 / P-291: PrepareForFreshLoad stops all monitors ─────────────

        [Test]
        public void FreshModeStopsMonitorsBeforeLoad_SingleMonitor()
        {
            // Arrange: inject a stub monitor (simulates active monitor before reload)
            PlaytestMonitorRegistry.InjectForTest(new StubMonitor());
            Assert.AreEqual(1, PlaytestMonitorRegistry.ActiveCount, "precondition: 1 active monitor");

            // Act: call the seam that the fresh block invokes before LoadScene
            PlaytestRunner.PrepareForFreshLoad();

            // Assert: no monitors remain — no stale callbacks after scene destroy
            Assert.AreEqual(0, PlaytestMonitorRegistry.ActiveCount,
                "P-291: PrepareForFreshLoad must stop all monitors before scene reload");
        }

        [Test]
        public void FreshModeStopsMonitorsBeforeLoad_MultipleMonitors()
        {
            // Arrange: multiple active monitors
            PlaytestMonitorRegistry.InjectForTest(new StubMonitor());
            PlaytestMonitorRegistry.InjectForTest(new StubMonitor());
            PlaytestMonitorRegistry.InjectForTest(new StubMonitor());
            Assert.AreEqual(3, PlaytestMonitorRegistry.ActiveCount, "precondition");

            PlaytestRunner.PrepareForFreshLoad();

            Assert.AreEqual(0, PlaytestMonitorRegistry.ActiveCount,
                "P-291: all monitors must be cleared before reload, not just first");
        }

        [Test]
        public void FreshModeStopsMonitorsBeforeLoad_NoMonitors_DoesNotThrow()
        {
            // Empty registry — PrepareForFreshLoad must be idempotent
            Assert.AreEqual(0, PlaytestMonitorRegistry.ActiveCount, "precondition: empty");

            Assert.DoesNotThrow(() => PlaytestRunner.PrepareForFreshLoad(),
                "PrepareForFreshLoad must not throw when no monitors are registered");
        }

        // ── Change 3: Camera.main validity guard in FindCamera ─────────────────

        [Test]
        public void FindCamera_NoCamera_ThrowsArgumentException()
        {
            // With no camera in the scene, FindCamera must throw ArgumentException.
            // The defensive mainCam.gameObject check at the Camera.main path must
            // not prevent the correct exception from propagating when there really is no camera.
            // (Camera.main == null → fall through all paths → throw)
            Assert.Throws<System.ArgumentException>(() => ScreenshotCapture.FindCamera(null),
                "P-109: FindCamera must throw when no camera exists");
        }

        // ── Helper: stub monitor for test injection ────────────────────────────

        private sealed class StubMonitor : IPlaytestMonitor
        {
            public string Name => "StubMonitorForTest";
            public void Start() { }
            public void Stop() { }
            public string Report() => "stub";
        }
    }
}
