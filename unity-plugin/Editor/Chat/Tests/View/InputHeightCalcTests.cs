using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InputHeightCalcTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private InputHeightCalc _calc;

        [SetUp] public void SetUp() => _calc = new InputHeightCalc();

        // ── CountLines ────────────────────────────────────────────────────────

        [Test]
        public void CountLines_Empty_Returns1()
        {
            Assert.AreEqual(1, InputHeightCalc.CountLines(null));
            Assert.AreEqual(1, InputHeightCalc.CountLines(""));
        }

        [Test]
        public void CountLines_SingleLine_Returns1()
        {
            Assert.AreEqual(1, InputHeightCalc.CountLines("hello"));
        }

        [Test]
        public void CountLines_MultiLine_Returns3()
        {
            Assert.AreEqual(3, InputHeightCalc.CountLines("a\nb\nc"));
        }

        [Test]
        public void CountLines_TrailingNewline_Returns2()
        {
            Assert.AreEqual(2, InputHeightCalc.CountLines("a\n"));
        }

        // ── Compute ───────────────────────────────────────────────────────────

        [Test]
        public void Compute_OneLine_ReturnsExpected()
        {
            // 1*18 + 14 + 31 = 63 < CompactH=117, clamps to 117
            var h = _calc.Compute(1, 800f, false);
            Assert.AreEqual(117f, h, 0.01f);
        }

        [Test]
        public void Compute_ManyLines_CapsAtMax()
        {
            // 30*18+14+31=585, windowH=600 => maxH=min(240,300)=240
            var h = _calc.Compute(30, 600f, false);
            Assert.AreEqual(240f, h, 0.01f);
        }

        [Test]
        public void Compute_WithChips_AddsChipHeight()
        {
            // 1*18+14+31+24=87 < CompactH=117, clamps to 117
            var h = _calc.Compute(1, 800f, true);
            Assert.AreEqual(117f, h, 0.01f);
        }

        [Test]
        public void Compute_SmallWindow_CapsAt40Percent()
        {
            // 10*18+14+31=225, windowH=200 => maxH=min(80,300)=80
            var h = _calc.Compute(10, 200f, false);
            Assert.AreEqual(80f, h, 0.01f);
        }

        [Test]
        public void Compute_TinyWindow_MaxWinsOverCompactH()
        {
            // windowH=200 => maxH=80, CompactH=117 > maxH → minH=80, areaH=63 < minH → returns 80
            var h = _calc.Compute(1, 200f, false);
            Assert.AreEqual(80f, h, 0.01f);
        }

        // ── ManualOverride ────────────────────────────────────────────────────

        [Test]
        public void ManualOverride_IgnoresLineCount()
        {
            _calc.SetManual(150f);
            var h = _calc.Compute(1, 800f, false);
            Assert.AreEqual(150f, h, 0.01f);
        }

        [Test]
        public void ManualOverride_ClampsToRange()
        {
            _calc.SetManual(9999f);
            Assert.AreEqual(300f, _calc.ManualHeight, 0.01f);

            _calc.SetManual(5f);
            Assert.AreEqual(117f, _calc.ManualHeight, 0.01f);
        }

        [Test]
        public void Reset_ClearsManualOverride()
        {
            _calc.SetManual(150f);
            _calc.Reset();
            Assert.IsFalse(_calc.ManualOverride);
            // Returns auto-computed value, not 150
            var h = _calc.Compute(1, 800f, false);
            Assert.AreNotEqual(150f, h);
        }

        // ── ComputeMax ────────────────────────────────────────────────────────

        [Test]
        public void ComputeMax_CapsAtAbsMax()
        {
            // 600*0.4=240 < AbsMaxH=300 -> 240
            Assert.AreEqual(240f, _calc.ComputeMax(600f), 0.01f);
            // 1000*0.4=400 > AbsMaxH=300 -> 300
            Assert.AreEqual(300f, _calc.ComputeMax(1000f), 0.01f);
        }

        [Test]
        public void ComputeMax_SmallWindow()
        {
            // 200*0.4=80 < AbsMaxH=300 -> 80
            Assert.AreEqual(80f, _calc.ComputeMax(200f), 0.01f);
        }

        [Test]
        public void ComputeMax_ZeroWindow_UsesDefault()
        {
            // windowH<=0 defaults to 600 -> 600*0.4=240
            Assert.AreEqual(240f, _calc.ComputeMax(0f), 0.01f);
        }
    }
}
