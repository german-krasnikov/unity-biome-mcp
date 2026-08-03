// TDD RED: AnimatedCounter lerp-to-target tests.
// Tick() called directly (no panel/scheduler needed in EditMode).
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AnimatedCounterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void SetTarget_FarFromCurrent_SetsTargetField()
        {
            var counter = new AnimatedCounter();
            counter.SetTarget(100f);
            Assert.AreEqual(100f, counter._target);
        }

        [Test]
        public void Tick_ApproachesTarget()
        {
            var counter = new AnimatedCounter();
            counter.SetTarget(100f);
            for (int i = 0; i < 10; i++) counter.Tick();
            // After 10 ticks with lerp 0.15: current ≈ 80 (1 - 0.85^10 ≈ 0.80)
            Assert.Greater(counter._current, 50f, "_current should be past halfway after 10 ticks");
            Assert.Less(counter._current, 100f, "_current should not yet be at target");
        }

        [Test]
        public void Tick_SnapsAtThreshold()
        {
            var counter = new AnimatedCounter();
            counter.SetTarget(100f);
            for (int i = 0; i < 100; i++) counter.Tick();
            // After 100 ticks snap condition (|current - target| < 0.01) fires
            Assert.AreEqual(100f, counter._current, 0.001f, "_current must equal _target after convergence");
        }
    }
}
