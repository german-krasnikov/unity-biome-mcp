using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ValueParserQuaternionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseQuaternion_ThreeFloats_EulerConversion()
        {
            var result = ValueParser.ParseQuaternion("(45, 90, 0)");
            var expected = Quaternion.Euler(45, 90, 0);
            Assert.AreEqual(expected.x, result.x, 0.001f);
            Assert.AreEqual(expected.y, result.y, 0.001f);
            Assert.AreEqual(expected.z, result.z, 0.001f);
            Assert.AreEqual(expected.w, result.w, 0.001f);
        }

        [Test]
        public void ParseQuaternion_FourFloats_RawXYZW()
        {
            var result = ValueParser.ParseQuaternion("(0, 0.7071, 0, 0.7071)");
            Assert.AreEqual(0f,      result.x, 0.001f);
            Assert.AreEqual(0.7071f, result.y, 0.001f);
            Assert.AreEqual(0f,      result.z, 0.001f);
            Assert.AreEqual(0.7071f, result.w, 0.001f);
        }

        [Test]
        public void ParseQuaternion_RoundTrip_EulerAnglesMatch()
        {
            // Reproduce exact serializer output: G4 format for each euler component
            var source = Quaternion.Euler(30, 60, 0);
            var euler = source.eulerAngles;
            var serialized = $"({euler.x:G4}, {euler.y:G4}, {euler.z:G4})";
            var parsed = ValueParser.ParseQuaternion(serialized);
            Assert.AreEqual(euler.x, parsed.eulerAngles.x, 0.1f);
            Assert.AreEqual(euler.y, parsed.eulerAngles.y, 0.1f);
            Assert.AreEqual(euler.z, parsed.eulerAngles.z, 0.1f);
        }

        [Test]
        public void ParseQuaternion_TwoFloats_ThrowsArgumentException()
            => Assert.Throws<System.ArgumentException>(
                () => ValueParser.ParseQuaternion("(1, 2)"));
    }
}
