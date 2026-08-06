// TDD — FingerprintHelper.Fnv1a determinism tests.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class FingerprintHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static uint InvokeFnv1a(string s)
        {
            var method = typeof(FingerprintHelper).GetMethod(
                "Fnv1a",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (uint)method.Invoke(null, new object[] { s });
        }

        private static string InvokeStablePropertyValueString(SerializedProperty prop)
        {
            var method = typeof(FingerprintHelper).GetMethod(
                "StablePropertyValueString",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "P-108: StablePropertyValueString must exist in FingerprintHelper");
            return (string)method.Invoke(null, new object[] { prop });
        }

        [Test]
        public void Fnv1a_Deterministic()
        {
            var a = InvokeFnv1a("hello world");
            var b = InvokeFnv1a("hello world");
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Fnv1a_DifferentInput_DifferentOutput()
        {
            var a = InvokeFnv1a("hello");
            var b = InvokeFnv1a("world");
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Fnv1a_KnownValue_EmptyString_IsOffsetBasis()
        {
            // FNV-1a of empty string = offset basis = 2166136261 (0x811C9DC5)
            var result = InvokeFnv1a("");
            Assert.AreEqual(2166136261u, result);
        }

        // ── P-108: StablePropertyValueString ─────────────────────────────────

        [Test]
        public void StablePropertyValueString_MethodExists()
        {
            // RED until StablePropertyValueString is added to FingerprintHelper
            var method = typeof(FingerprintHelper).GetMethod(
                "StablePropertyValueString",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "P-108: StablePropertyValueString must exist in FingerprintHelper");
        }

        [Test]
        public void StablePropertyValueString_NullObjectRef_ReturnsNull()
        {
            // m_Father is an ObjectReference property that is null for root GOs
            var go = TrackOwnedObject(new GameObject("FP_NullRef"));
            var so = new SerializedObject(go.GetComponent<Transform>());
            var prop = so.FindProperty("m_Father");
            Assert.IsNotNull(prop, "m_Father property not found on Transform");
            Assert.AreEqual(SerializedPropertyType.ObjectReference, prop.propertyType);
            Assert.IsNull(prop.objectReferenceValue, "m_Father must be null for root GO");

            var result = InvokeStablePropertyValueString(prop);
            Assert.AreEqual("null", result, "P-108: null ObjectReference must return 'null'");
        }

        [Test]
        public void StablePropertyValueString_DoesNotContainInstanceIdPattern()
        {
            // For a null ref the result should never contain #<digits>
            var go = TrackOwnedObject(new GameObject("FP_NoHash"));
            var so = new SerializedObject(go.GetComponent<Transform>());
            var prop = so.FindProperty("m_Father");

            var result = InvokeStablePropertyValueString(prop);
            Assert.IsFalse(Regex.IsMatch(result, @"#\d+"),
                $"P-108: StablePropertyValueString must not return volatile #instanceID; got '{result}'");
        }

        [Test]
        public void Fingerprint_IsDeterministicForSameGameObject()
        {
            // Stability regression: computing twice returns same hash
            var go = TrackOwnedObject(new GameObject("FP_Stable"));
            var fp1 = FingerprintHelper.Fingerprint(go.name, 0);
            var fp2 = FingerprintHelper.Fingerprint(go.name, 0);
            Assert.AreEqual(fp1, fp2, "P-108: Fingerprint must be deterministic");
        }
    }
}
