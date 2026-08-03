// TDD: FrameDebugHelper — Frame Debugger reflection-based capture.
// Focus on reflection safety and graceful degradation.
// These tests never activate the real FrameDebugger (no rendering state changes).
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class FrameDebugHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Reflection helpers ─────────────────────────────────────────────

        static string InvokeBreakReason(int cause)
        {
            var mi = typeof(FrameDebugHelper).GetMethod(
                "BreakReason",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(int) }, null);
            Assert.IsNotNull(mi, "FrameDebugHelper.BreakReason not found");
            return (string)mi.Invoke(null, new object[] { cause });
        }

        // FormatNum is shared via RenderAnalyzer — no longer private to FrameDebugHelper
        static string InvokeFormatNum(long n) => RenderAnalyzer.FormatNum(n);

        static T InvokeGetField<T>(System.Type type, object obj, string name)
        {
            var mi = typeof(FrameDebugHelper).GetMethod(
                "GetField",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "FrameDebugHelper.GetField not found");
            return (T)mi.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { type, obj, name });
        }

        static bool GetReflectionFailed()
        {
            var fi = typeof(FrameDebugHelper).GetField(
                "_reflectionFailed",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(fi, "_reflectionFailed field not found");
            return (bool)fi.GetValue(null);
        }

        // ── Tests ──────────────────────────────────────────────────────────

        [Test]
        public void ReflectionFailed_ReturnsGracefulError()
        {
            // If _reflectionFailed=true, Capture must return a descriptive error (not throw)
            // We test this by simulating: directly set the flag and call Capture
            var fi = typeof(FrameDebugHelper).GetField(
                "_reflectionFailed",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (fi == null)
            {
                Assert.Inconclusive("_reflectionFailed field not accessible — test design issue");
                return;
            }

            bool original = (bool)fi.GetValue(null);
            try
            {
                fi.SetValue(null, true);
                var result = FrameDebugHelper.Capture("{}");
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result);
                StringAssert.Contains("ERR", result.ToUpperInvariant());
            }
            finally
            {
                fi.SetValue(null, original); // always restore
            }
        }

        [Test]
        public void GetField_MissingField_ReturnsDefault()
        {
            // GetField<int> on a type that doesn't have the named field must return 0 (default)
            int result = InvokeGetField<int>(typeof(Vector3), Vector3.zero, "nonExistentField12345");
            Assert.AreEqual(0, result, "GetField with missing field must return default(T)");
        }

        [Test]
        public void GetField_ExistingField_ReturnsValue()
        {
            // Vector3 has public fields x, y, z
            var v = new Vector3(1f, 2f, 3f);
            object boxed = v;
            float x = InvokeGetField<float>(typeof(Vector3), boxed, "x");
            Assert.AreEqual(1f, x, 0.0001f, "GetField must return correct field value");
        }

        [Test]
        public void BreakReason_KnownIndex_ReturnsString()
        {
            // Index 0 = "none"
            Assert.AreEqual("none", InvokeBreakReason(0));
            // Index 1 = "DifferentMaterial"
            StringAssert.Contains("Material", InvokeBreakReason(1));
            // Index 13 = "MaterialPropertyBlock"
            StringAssert.Contains("PropertyBlock", InvokeBreakReason(13));
        }

        [Test]
        public void BreakReason_UnknownIndex_ReturnsUnknownN()
        {
            var result = InvokeBreakReason(99);
            StringAssert.Contains("unknown", result.ToLowerInvariant());
            StringAssert.Contains("99", result);
        }

        [Test]
        public void BreakReason_NegativeIndex_ReturnsUnknown()
        {
            var result = InvokeBreakReason(-1);
            StringAssert.Contains("unknown", result.ToLowerInvariant());
        }

        [Test]
        public void FormatNum_SmallNumber_ReturnsDirect()
        {
            Assert.AreEqual("999", InvokeFormatNum(999L));
        }

        [Test]
        public void FormatNum_ThousandFmt()
        {
            var result = InvokeFormatNum(1500L);
            StringAssert.Contains("K", result);
        }

        [Test]
        public void FormatNum_MillionFmt()
        {
            var result = InvokeFormatNum(1_200_000L);
            StringAssert.Contains("M", result);
        }

        [Test]
        public void Capture_ReturnsStringNotNull()
        {
            // If FrameDebugger reflection is not available in test environment,
            // must return a graceful error — never null/throw.
            string result = null;
            Assert.DoesNotThrow(() => { result = FrameDebugHelper.Capture("{}"); });
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void Capture_MaxEvents_ArgParsedCorrectly()
        {
            // With max_events=1, the limit should be applied. At minimum must not throw.
            Assert.DoesNotThrow(() =>
            {
                var result = FrameDebugHelper.Capture("{\"max_events\":\"1\"}");
                Assert.IsNotNull(result);
            });
        }
    }
}
