// TDD — AnimationHelper pure-logic tests: IsVector3, ParseVector3Keys, ParseFloatKeys, NormalizeProperty.
// EditMode only — no Unity objects, no [UnityTest], no Thread.Sleep.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    // ── IsVector3 ─────────────────────────────────────────────────────────────

    [TestFixture]
    public class AnimationHelperIsVector3Tests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void IsVector3_StringWithParenthesis_ReturnsTrue()
        {
            Assert.IsTrue(AnimationHelper.IsVector3("t:0 v:(1,2,3)"));
        }

        [Test]
        public void IsVector3_FloatString_ReturnsFalse()
        {
            Assert.IsFalse(AnimationHelper.IsVector3("t:0 v:5.0"));
        }

        [Test]
        public void IsVector3_SingleComponentWithParenthesis_ReturnsTrue()
        {
            // Any string containing '(' is treated as vector3
            Assert.IsTrue(AnimationHelper.IsVector3("(0)"));
        }
    }

    // ── ParseVector3Keys ──────────────────────────────────────────────────────

    [TestFixture]
    public class AnimationHelperParseVector3KeysTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseVector3Keys_SingleKey_ProducesThreeArraysEachLengthOne()
        {
            var result = AnimationHelper.ParseVector3Keys("t:0 v:(1,2,3)");

            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(1, result[0].Length, "x array length");
            Assert.AreEqual(1, result[1].Length, "y array length");
            Assert.AreEqual(1, result[2].Length, "z array length");
        }

        [Test]
        public void ParseVector3Keys_SingleKey_MapsXYZCorrectly()
        {
            var result = AnimationHelper.ParseVector3Keys("t:0 v:(1,2,3)");

            Assert.AreEqual(1f, result[0][0].value, 1e-5f, "x value");
            Assert.AreEqual(2f, result[1][0].value, 1e-5f, "y value");
            Assert.AreEqual(3f, result[2][0].value, 1e-5f, "z value");
        }

        [Test]
        public void ParseVector3Keys_MultipleKeys_AllAxesHaveMatchingLength()
        {
            var result = AnimationHelper.ParseVector3Keys("t:0 v:(1,2,3);t:1 v:(4,5,6)");

            Assert.AreEqual(2, result[0].Length, "x length");
            Assert.AreEqual(2, result[1].Length, "y length");
            Assert.AreEqual(2, result[2].Length, "z length");
        }

        [Test]
        public void ParseVector3Keys_ZeroVector_MapsAllAxesToZero()
        {
            var result = AnimationHelper.ParseVector3Keys("t:0 v:(5,0,0)");

            Assert.AreEqual(5f, result[0][0].value, 1e-5f, "x=5");
            Assert.AreEqual(0f, result[1][0].value, 1e-5f, "y=0");
            Assert.AreEqual(0f, result[2][0].value, 1e-5f, "z=0");
        }
    }

    // ── ParseFloatKeys ────────────────────────────────────────────────────────

    [TestFixture]
    public class AnimationHelperParseFloatKeysTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseFloatKeys_SingleKey_ReturnsOneKeyframeWithCorrectTimeAndValue()
        {
            var result = AnimationHelper.ParseFloatKeys("t:0 v:1");

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0f, result[0].time, 1e-5f);
            Assert.AreEqual(1f, result[0].value, 1e-5f);
        }

        [Test]
        public void ParseFloatKeys_MultiKey_ReturnsTwoKeyframes()
        {
            var result = AnimationHelper.ParseFloatKeys("t:0 v:0;t:1 v:5");

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(0f, result[0].time, 1e-5f);
            Assert.AreEqual(5f, result[1].value, 1e-5f);
        }

        [Test]
        public void ParseFloatKeys_EmptyString_ReturnsEmptyArray()
        {
            var result = AnimationHelper.ParseFloatKeys("");

            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void ParseFloatKeys_MissingVToken_FallsBackToZero()
        {
            // ExtractValue returns "0" when "v:" is absent
            var result = AnimationHelper.ParseFloatKeys("t:0.5");

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0.5f, result[0].time, 1e-5f);
            Assert.AreEqual(0f, result[0].value, 1e-5f);
        }

        [Test]
        public void ParseFloatKeys_KeyframeTimeRoundTrip_PreservesTime()
        {
            var result = AnimationHelper.ParseFloatKeys("t:0.75 v:3.14");

            Assert.AreEqual(0.75f, result[0].time, 1e-5f);
            Assert.AreEqual(3.14f, result[0].value, 1e-4f);
        }
    }

    // ── NormalizeProperty ─────────────────────────────────────────────────────

    [TestFixture]
    public class AnimationHelperNormalizePropertyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static string Normalize(string property)
        {
            var method = typeof(AnimationHelper).GetMethod(
                "NormalizeProperty",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (string)method.Invoke(null, new object[] { property });
        }

        [Test]
        public void NormalizeProperty_LocalPosition_ReturnsMLocalPosition()
        {
            Assert.AreEqual("m_LocalPosition", Normalize("localPosition"));
        }

        [Test]
        public void NormalizeProperty_LocalEulerAngles_ReturnsLocalEulerAnglesRaw()
        {
            Assert.AreEqual("localEulerAnglesRaw", Normalize("localEulerAngles"));
        }

        [Test]
        public void NormalizeProperty_LocalRotation_ReturnsLocalEulerAnglesRaw()
        {
            Assert.AreEqual("localEulerAnglesRaw", Normalize("localRotation"));
        }

        [Test]
        public void NormalizeProperty_LocalScale_ReturnsMLocalScale()
        {
            Assert.AreEqual("m_LocalScale", Normalize("localScale"));
        }

        [Test]
        public void NormalizeProperty_UnknownProperty_PassesThrough()
        {
            Assert.AreEqual("m_Enabled", Normalize("m_Enabled"));
        }

        [Test]
        public void NormalizeProperty_LocalPositionWithSuffix_PreservesSuffix()
        {
            Assert.AreEqual("m_LocalPosition.x", Normalize("localPosition.x"));
        }
    }
}
