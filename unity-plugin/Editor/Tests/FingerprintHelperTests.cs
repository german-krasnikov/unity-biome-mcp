// TDD — FingerprintHelper.Fnv1a determinism tests.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using System.Reflection;
using NUnit.Framework;

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
    }
}
