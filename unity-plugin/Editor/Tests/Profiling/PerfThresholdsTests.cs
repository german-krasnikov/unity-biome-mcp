// TDD RED: PerfThresholds band classification + color lerp tests.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PerfThresholdsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void FpsBand_Above50_ReturnsGood() =>
            Assert.AreEqual("good", PerfThresholds.FpsBand(60f));

        [Test]
        public void FpsBand_Between30And50_ReturnsWarn() =>
            Assert.AreEqual("warn", PerfThresholds.FpsBand(45f));

        [Test]
        public void FpsBand_Below30_ReturnsCrit() =>
            Assert.AreEqual("crit", PerfThresholds.FpsBand(20f));

        [Test]
        public void FrameTimeBand_Below16_ReturnsGood() =>
            Assert.AreEqual("good", PerfThresholds.FrameTimeBand(10f));

        [Test]
        public void DrawCallBand_Above2000_ReturnsCrit() =>
            Assert.AreEqual("crit", PerfThresholds.DrawCallBand(3000));

        [Test]
        public void ColorForBand_Good_ReturnsGoodColor()
        {
            var c = PerfThresholds.ColorForBand("good");
            Assert.AreEqual((byte)0x3A, c.r, "Good color should have r=0x3A (#3ad29f)");
        }

        [Test]
        public void FpsColor_At45_InterpolatesYellowGreen()
        {
            var c45 = PerfThresholds.FpsColor(45f);
            var c60 = PerfThresholds.FpsColor(60f);
            var c30 = PerfThresholds.FpsColor(30f);
            // 45fps must interpolate — not exactly the good (60fps) or warn (30fps) color
            Assert.AreNotEqual(c45.r, c60.r, "At 45fps must not be pure good color");
            Assert.AreNotEqual(c45.r, c30.r, "At 45fps must not be pure warn color");
        }
    }
}
