// TDD: ProfileContextSerializer — TCP text formatting for profiler sessions.
using NUnit.Framework;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProfileContextSerializerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
#if UNITY_INCLUDE_TESTS
            ProfileContextSerializer.GetOverride = null;
#endif
        }

        [Test]
        public void GetLatest_NoSessions_ReturnsNoSessionsString()
        {
            // Reset() already called in SetUp
            Assert.AreEqual("no sessions", ProfileContextSerializer.GetLatest());
        }

        [Test]
        public void GetLatest_OneSession_ReturnsFormattedStats()
        {
            ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
            for (int i = 0; i < 10; i++) ProfileRecorder.SimulateTick();
            ProfileRecorder.Dispatch("stop", "{}");

            string result = ProfileContextSerializer.GetLatest();

            StringAssert.Contains("fps avg=", result);
            StringAssert.Contains("session:", result);
        }

        [Test]
        public void Get_UnknownSid_ReturnsErrorString()
        {
            string result = ProfileContextSerializer.Get("x999");
            StringAssert.StartsWith("error:", result);
        }

        [Test]
        public void Get_KnownSid_ReturnsFormattedStats()
        {
            ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
            for (int i = 0; i < 5; i++) ProfileRecorder.SimulateTick();
            ProfileRecorder.Dispatch("stop", "{}");
            // After stop, session "p1" exists
            string result = ProfileContextSerializer.Get("p1");
            StringAssert.Contains("fps avg=", result);
            StringAssert.Contains("session:p1", result);
        }
    }
}
