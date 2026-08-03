// TDD RED: ProfileRecorder session lifecycle tests.
using NUnit.Framework;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProfileRecorderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private float _fakeTime = 0f;

        [SetUp]
        public void SetUp()
        {
            ProfileRecorder.Reset();
            ProfileRecorder._frameProvider = () => new FrameSample { DeltaTime = 0.016f, CpuMs = 14f };
            ProfileRecorder._realtime = () => _fakeTime;
        }

        [TearDown]
        public void TearDown()
        {
            ProfileRecorder.Reset();
            ProfileRecorder._frameProvider = ProfilerBridge.CollectFrame;
            ProfileRecorder._realtime = () => UnityEngine.Time.realtimeSinceStartup;
        }

        [Test]
        public void Reset_WhileRecording_StopsAndCanStartAgain()
        {
            ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");

            ProfileRecorder.Reset();

            StringAssert.Contains("idle", ProfileRecorder.Dispatch("status", "{}"));
            StringAssert.Contains("started",
                ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}"));
        }

        [Test]
        public void Dispatch_StartBurst_RecordingStateSet()
        {
            var result = ProfileRecorder.Dispatch("start", "{\"mode\":\"burst\",\"duration\":\"5\"}");
            StringAssert.Contains("started", result);
            var status = ProfileRecorder.Dispatch("status", "{}");
            StringAssert.Contains("recording", status);
        }

        [Test]
        public void Dispatch_StopAfterFrames_ReturnsSummary()
        {
            ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
            for (int i = 0; i < 10; i++) ProfileRecorder.SimulateTick();
            var result = ProfileRecorder.Dispatch("stop", "{}");
            StringAssert.Contains("fps", result);
            StringAssert.Contains("10frames", result);
        }

        [Test]
        public void Dispatch_StartWhileRecording_ReturnsError()
        {
            ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
            var result = ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
            StringAssert.Contains("already recording", result.ToLower());
        }

        [Test]
        public void Dispatch_BurstAutoStops_AfterDuration()
        {
            _fakeTime = 0f;
            ProfileRecorder.Dispatch("start", "{\"mode\":\"burst\",\"duration\":\"5\"}");
            // 320 ticks * 0.016s = 5.12s > 5s → auto-stop triggered
            for (int i = 0; i < 320; i++) { _fakeTime += 0.016f; ProfileRecorder.SimulateTick(); }
            var status = ProfileRecorder.Dispatch("status", "{}");
            StringAssert.Contains("idle", status);
        }

        [Test]
        public void Dispatch_ListSessions_ShowsFinalized()
        {
            ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
            ProfileRecorder.SimulateTick();
            ProfileRecorder.Dispatch("stop", "{}");
            var list = ProfileRecorder.Dispatch("list_sessions", "{}");
            StringAssert.Contains("p1", list);
        }

        [Test]
        public void Dispatch_Compare_RequiresTwoSessions()
        {
            var result = ProfileRecorder.Dispatch("compare", "{\"session\":\"p1\",\"compare_with\":\"p99\"}");
            StringAssert.Contains("not found", result.ToLower());
        }
    }
}
