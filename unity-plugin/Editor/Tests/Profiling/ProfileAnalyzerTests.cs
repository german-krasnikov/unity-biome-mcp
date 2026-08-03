// TDD RED: ProfileAnalyzer stats computation tests.
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProfileAnalyzerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static FrameSample[] MakeSamples(int count, float dt, float cpu, long mono)
        {
            var arr = new FrameSample[count];
            for (int i = 0; i < count; i++)
                arr[i] = new FrameSample { DeltaTime = dt, CpuMs = cpu, MonoUsedBytes = mono };
            return arr;
        }

        [Test]
        public void Compute_AverageFps_CorrectForUniformFrames()
        {
            var frames = MakeSamples(60, 0.016f, 14f, 100_000_000L);
            var stats = ProfileAnalyzer.Compute(frames);
            // 1/0.016 = 62.5 fps
            Assert.That(stats.FpsAvg, Is.EqualTo(62.5f).Within(1f));
        }

        [Test]
        public void Compute_StutterCount_DetectsSpikes()
        {
            var frames = MakeSamples(58, 0.016f, 14f, 0L);
            var spikes = MakeSamples(2, 0.050f, 45f, 0L);
            var all = frames.Concat(spikes).ToArray();
            var stats = ProfileAnalyzer.Compute(all);
            Assert.That(stats.StutterCount, Is.EqualTo(2));
        }

        [Test]
        public void Compare_FasterSession_ReturnsImproved()
        {
            var baseline = ProfileAnalyzer.Compute(MakeSamples(60, 0.020f, 18f, 0L));
            var better = ProfileAnalyzer.Compute(MakeSamples(60, 0.016f, 14f, 0L));
            var delta = ProfileAnalyzer.Compare(baseline, better);
            Assert.AreEqual("IMPROVED", delta.Verdict);
        }

        [Test]
        public void Compute_GcCollections_SumsDeltas()
        {
            var frames = new[]
            {
                new FrameSample { DeltaTime = 0.016f, GcGen0Count = 0 },
                new FrameSample { DeltaTime = 0.016f, GcGen0Count = 1 },
                new FrameSample { DeltaTime = 0.016f, GcGen0Count = 0 },
                new FrameSample { DeltaTime = 0.016f, GcGen0Count = 2 },
            };
            var stats = ProfileAnalyzer.Compute(frames);
            Assert.AreEqual(3, stats.GcCollections);
        }

        [Test]
        public void Compute_EmptyFrames_ReturnsZeroStats()
        {
            var stats = ProfileAnalyzer.Compute(System.Array.Empty<FrameSample>());
            Assert.AreEqual(0, stats.FrameCount);
        }
    }
}
