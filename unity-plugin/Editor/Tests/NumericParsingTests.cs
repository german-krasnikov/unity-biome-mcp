// TDD for D05 — NumericParsing (Core), the engine-free home for ParseFloats.
// Ported from ValueParserTests.cs:191,202,211,217,223 — same assertions, new call
// target (NumericParsing.ParseFloats directly, not the ValueParser wrapper).
// ValueParserTests.cs is left unchanged: it now covers the delegating wrapper,
// still valid regression coverage.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class NumericParsingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseFloats_FourValues_ParsesCorrectly()
        {
            var f = NumericParsing.ParseFloats("1.5, 2.5, 100, 50", 4);
            Assert.AreEqual(4, f.Length);
            Assert.AreEqual(1.5f, f[0], 0.001f);
            Assert.AreEqual(2.5f, f[1], 0.001f);
            Assert.AreEqual(100f, f[2], 0.001f);
            Assert.AreEqual(50f, f[3], 0.001f);
        }

        [Test]
        public void ParseFloats_SixValues_ParsesCorrectly()
        {
            var f = NumericParsing.ParseFloats("(1,2,3,4,5,6)", 6);
            Assert.AreEqual(6, f.Length);
            Assert.AreEqual(1f, f[0], 0.001f);
            Assert.AreEqual(6f, f[5], 0.001f);
        }

        // Double-red: also fails if the length-mismatch check is ever weakened.
        [Test]
        public void ParseFloats_WrongCount_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => NumericParsing.ParseFloats("1,2,3", 4));
        }

        // Double-red: also fails if a malformed component is ever silently coerced.
        [Test]
        public void ParseFloats_InvalidFloat_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => NumericParsing.ParseFloats("1,2,abc,4", 4));
        }

        [Test]
        public void ParseFloats_WithParens_StripsAndParses()
        {
            var f = NumericParsing.ParseFloats("(10, 20, 30, 40)", 4);
            Assert.AreEqual(10f, f[0], 0.001f);
            Assert.AreEqual(40f, f[3], 0.001f);
        }
    }
}
