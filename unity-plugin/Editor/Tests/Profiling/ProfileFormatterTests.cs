// TDD RED: ProfileFormatter text output tests.
using NUnit.Framework;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProfileFormatterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void FormatSummary_ContainsSessionId()
        {
            var stats = new ProfileAnalyzer.Stats
            {
                FrameCount = 300,
                DurationS = 5f,
                FpsAvg = 60f,
                FpsMin = 45f,
                FpsMax = 62f,
                GpuAvg = -1f,
            };
            var result = ProfileFormatter.FormatSummary("p1", stats);
            StringAssert.Contains("session:p1", result);
            StringAssert.Contains("fps avg=60", result);
        }

        [Test]
        public void FormatCompare_RegressedFps_ShowsREGRESSED()
        {
            var a = new ProfileAnalyzer.Stats { FpsAvg = 60f };
            var b = new ProfileAnalyzer.Stats { FpsAvg = 45f };
            var d = new ProfileAnalyzer.DeltaStats { Verdict = "REGRESSED", FpsDeltaPct = -25f };
            var result = ProfileFormatter.FormatCompare("p1", "p2", a, b, d);
            StringAssert.Contains("REGRESSED", result);
        }

        [Test]
        public void FormatSummary_GpuUnavailable_ShowsNA()
        {
            var stats = new ProfileAnalyzer.Stats { GpuAvg = -1f };
            var result = ProfileFormatter.FormatSummary("p1", stats);
            StringAssert.Contains("gpu=N/A", result);
        }
    }
}
