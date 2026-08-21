// TDD: ParticleSerializer detail tests — Tasks 5 (module variants), 6 (Curve format),
// 7 (Gradient format). Reflection used for private static helpers.
// Requires ParticleSystem scene object; uses SceneTestBase cleanup.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ParticleSerializerDetailTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("PSDetailTest"));
            _go.AddComponent<ParticleSystem>();
        }

        private string Path => "/" + _go.name;

        // Helper: invoke private static Curve method via reflection
        private static string InvokeCurve(ParticleSystem.MinMaxCurve c)
        {
            var mi = typeof(ParticleSerializer).GetMethod(
                "Curve",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "private static Curve method must exist");
            return (string)mi.Invoke(null, new object[] { c });
        }

        // Helper: invoke private static Gradient method via reflection
        private static string InvokeGradient(ParticleSystem.MinMaxGradient g)
        {
            var mi = typeof(ParticleSerializer).GetMethod(
                "Gradient",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "private static Gradient method must exist");
            return (string)mi.Invoke(null, new object[] { g });
        }

        // ── Task 5: SerializeModule variants ─────────────────────────────────

        [Test]
        public void Serialize_UnknownModuleVariant_ThrowsInvalidOperation()
        {
            // "trails" module exists; "invalid_xyz" does not
            Assert.Throws<InvalidOperationException>(
                () => ParticleSerializer.Serialize(Path, "invalid_xyz_99"));
        }

        [Test]
        public void Serialize_TrailsModule_ContainsEnabledAndRatio()
        {
            var result = ParticleSerializer.Serialize(Path, "trails");
            StringAssert.Contains("trails:", result);
            StringAssert.Contains("enabled:", result);
            StringAssert.Contains("ratio:", result);
        }

        [Test]
        public void Serialize_VelocityOverLifetime_ContainsXYZAndSpace()
        {
            var result = ParticleSerializer.Serialize(Path, "velocityOverLifetime");
            StringAssert.Contains("velocityOverLifetime:", result);
            StringAssert.Contains("x:", result);
            StringAssert.Contains("y:", result);
            StringAssert.Contains("z:", result);
            StringAssert.Contains("space:", result);
        }

        // ── Task 6: Curve formatter (private via reflection) ──────────────────

        [Test]
        public void Curve_Constant_OutputsSingleNumericValue()
        {
            var c = new ParticleSystem.MinMaxCurve(2.5f); // Constant mode
            var result = InvokeCurve(c);
            // G4 format: "2.5"
            StringAssert.Contains("2.5", result);
            StringAssert.DoesNotContain("..", result);
            StringAssert.DoesNotContain("curve", result);
        }

        [Test]
        public void Curve_TwoConstants_OutputsRangeWithDotDot()
        {
            // Two-constant mode: min..max
            var c = new ParticleSystem.MinMaxCurve(1f, 3f); // constantMin=1, constantMax=3
            var result = InvokeCurve(c);
            StringAssert.Contains("..", result);
        }

        [Test]
        public void Curve_CurveMode_OutputsCurveWithKeyCount()
        {
            // AnimationCurve with 2 keyframes → "curve(2 keys)"
            var anim = AnimationCurve.Linear(0f, 0f, 1f, 1f); // 2 keys
            var c = new ParticleSystem.MinMaxCurve(1f, anim);  // curve mode
            var result = InvokeCurve(c);
            StringAssert.Contains("curve(", result);
            StringAssert.Contains("keys)", result);
        }

        [Test]
        public void Curve_ZeroConstant_OutputsZero()
        {
            var c = new ParticleSystem.MinMaxCurve(0f);
            var result = InvokeCurve(c);
            Assert.AreEqual("0", result);
        }

        // ── Task 7: Gradient formatter (private via reflection) ────────────────

        [Test]
        public void Gradient_SingleColor_OutputsHexHash()
        {
            var g = new ParticleSystem.MinMaxGradient(Color.white);
            var result = InvokeGradient(g);
            // Color mode: "#RRGGBBAA"
            StringAssert.StartsWith("#", result);
            Assert.AreEqual(9, result.Length, "Expected #RRGGBBAA (9 chars)");
        }

        [Test]
        public void Gradient_TwoColors_OutputsBothHexValues()
        {
            var g = new ParticleSystem.MinMaxGradient(Color.black, Color.white);
            var result = InvokeGradient(g);
            // TwoColors mode: "#RRGGBBAA..#RRGGBBAA"
            StringAssert.Contains("..", result);
            Assert.IsTrue(result.StartsWith("#"), $"Expected two-color format, got: {result}");
        }

        [Test]
        public void Gradient_GradientMode_OutputsGradientWithKeyCount()
        {
            // Gradient object with 2 color keys → "gradient(2 keys)"
            var gradient = new Gradient();
            gradient.colorKeys = new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            gradient.alphaKeys = new[] { new GradientAlphaKey(1f, 0f) };
            var g = new ParticleSystem.MinMaxGradient(gradient);
            var result = InvokeGradient(g);
            StringAssert.Contains("gradient(", result);
            StringAssert.Contains("keys)", result);
        }
    }
}
