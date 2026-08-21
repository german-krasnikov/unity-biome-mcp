// TDD — ParticleHelper parsing helpers: PMM, PB, ParseGradient, ParseCurve.
// EditMode only — no ParticleSystem scene object needed for pure parsing.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    // ── PMM (ParseMinMaxCurve) ────────────────────────────────────────────────

    [TestFixture]
    public class ParticleHelperPMMTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        static ParticleSystem.MinMaxCurve InvokePMM(string v)
        {
            var m = typeof(ParticleHelper).GetMethod(
                "PMM", BindingFlags.NonPublic | BindingFlags.Static);
            return (ParticleSystem.MinMaxCurve)m.Invoke(null, new object[] { v });
        }

        [Test]
        public void PMM_SingleFloat_ReturnsConstantCurve()
        {
            var result = InvokePMM("5.0");
            Assert.AreEqual(ParticleSystemCurveMode.Constant, result.mode);
            Assert.AreEqual(5f, result.constant, 0.0001f);
        }

        [Test]
        public void PMM_TwoFloats_ReturnsTwoConstantsCurve()
        {
            var result = InvokePMM("0.5,2.0");
            Assert.AreEqual(ParticleSystemCurveMode.TwoConstants, result.mode);
            Assert.AreEqual(0.5f, result.constantMin, 0.0001f);
            Assert.AreEqual(2.0f, result.constantMax, 0.0001f);
        }
    }

    // ── PB (ParseBool) ────────────────────────────────────────────────────────

    [TestFixture]
    public class ParticleHelperPBTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        static bool InvokePB(string v)
        {
            var m = typeof(ParticleHelper).GetMethod(
                "PB", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)m.Invoke(null, new object[] { v });
        }

        [Test]
        public void PB_TrueLowercase_ReturnsTrue() => Assert.IsTrue(InvokePB("true"));

        [Test]
        public void PB_One_ReturnsTrue() => Assert.IsTrue(InvokePB("1"));

        [Test]
        public void PB_TrueUppercase_ReturnsTrue() => Assert.IsTrue(InvokePB("TRUE"));

        [Test]
        public void PB_FalseLowercase_ReturnsFalse() => Assert.IsFalse(InvokePB("false"));

        [Test]
        public void PB_Zero_ReturnsFalse() => Assert.IsFalse(InvokePB("0"));
    }

    // ── ParseGradient ─────────────────────────────────────────────────────────

    [TestFixture]
    public class ParticleHelperParseGradientTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseGradient_TwoKeys_ProducesCorrectColorsAndTimes()
        {
            var g = ParticleHelper.ParseGradient("#FF0000@0;#0000FF@1");
            Assert.AreEqual(2, g.colorKeys.Length);
            Assert.AreEqual(0f, g.colorKeys[0].time, 0.001f);
            Assert.AreEqual(1f, g.colorKeys[1].time, 0.001f);
            // Red channel check
            Assert.AreEqual(1f, g.colorKeys[0].color.r, 0.01f);
            Assert.AreEqual(0f, g.colorKeys[0].color.b, 0.01f);
            // Blue channel check
            Assert.AreEqual(0f, g.colorKeys[1].color.r, 0.01f);
            Assert.AreEqual(1f, g.colorKeys[1].color.b, 0.01f);
        }

        [Test]
        public void ParseGradient_TimeOutOfRange_ClampedToZeroOne()
        {
            var g = ParticleHelper.ParseGradient("#FFFFFF@-0.5;#000000@1.5");
            Assert.AreEqual(0f, g.colorKeys[0].time, 0.001f);
            Assert.AreEqual(1f, g.colorKeys[1].time, 0.001f);
        }

        [Test]
        public void ParseGradient_MoreThanEightKeys_ThrowsArgumentException()
        {
            // 9 tokens — exceeds Unity's limit
            var tokens = string.Join(";",
                "#FFFFFF@0", "#000000@0.125", "#FFFFFF@0.25", "#000000@0.375",
                "#FFFFFF@0.5", "#000000@0.625", "#FFFFFF@0.75", "#000000@0.875",
                "#FFFFFF@1");
            Assert.Throws<ArgumentException>(() => ParticleHelper.ParseGradient(tokens));
        }

        [Test]
        public void ParseGradient_InvalidHex_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ParticleHelper.ParseGradient("NOTACOLOR@0"));
        }

        [Test]
        public void ParseGradient_MissingAtSeparator_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ParticleHelper.ParseGradient("#FF0000;#0000FF"));
        }
    }

    // ── ParseCurve ────────────────────────────────────────────────────────────

    [TestFixture]
    public class ParticleHelperParseCurveTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseCurve_ThreeKeyframes_ProducesCorrectCount()
        {
            var curve = ParticleHelper.ParseCurve("0:0;0.5:1;1:0");
            Assert.AreEqual(3, curve.length);
            Assert.AreEqual(0f,   curve.keys[0].time,  0.001f);
            Assert.AreEqual(0.5f, curve.keys[1].time,  0.001f);
            Assert.AreEqual(1f,   curve.keys[2].time,  0.001f);
        }

        [Test]
        public void ParseCurve_SingleKeyframe_ProducesOneKey()
        {
            var curve = ParticleHelper.ParseCurve("0:5");
            Assert.AreEqual(1, curve.length);
            Assert.AreEqual(5f, curve.keys[0].value, 0.001f);
        }

        [Test]
        public void ParseCurve_InvalidToken_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ParticleHelper.ParseCurve("badtoken"));
        }

        [Test]
        public void ParseCurve_MultipleKeyframes_DoesNotThrowOnSmoothTangents()
        {
            Assert.DoesNotThrow(() => ParticleHelper.ParseCurve("0:0;1:1"));
        }
    }
}
