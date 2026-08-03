// CH2.test.1: Direct unit tests for JsonArrayScan.ExtractNextObject.
// Pure: zero Unity deps, headless-safe.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class JsonArrayScanTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static string Next(string json, ref int pos)
            => JsonArrayScan.ExtractNextObject(json, ref pos);

        [Test]
        public void ExtractNextObject_SingleObject_ReturnsIt()
        {
            int pos = 0;
            var result = Next("[{\"a\":1}]", ref pos);
            Assert.AreEqual("{\"a\":1}", result);
        }

        [Test]
        public void ExtractNextObject_TwoObjects_ReturnsBoth()
        {
            int pos = 0;
            var arr = "[{\"x\":1},{\"y\":2}]";
            var first  = Next(arr, ref pos);
            var second = Next(arr, ref pos);
            Assert.AreEqual("{\"x\":1}", first);
            Assert.AreEqual("{\"y\":2}", second);
        }

        [Test]
        public void ExtractNextObject_AfterSecond_ReturnsNull()
        {
            int pos = 0;
            var arr = "[{\"a\":1}]";
            Next(arr, ref pos);
            var result = Next(arr, ref pos);
            Assert.IsNull(result, "must return null when no more objects");
        }

        [Test]
        public void ExtractNextObject_Empty_ReturnsNull()
        {
            int pos = 0;
            Assert.IsNull(Next("", ref pos));
            Assert.IsNull(Next(null, ref pos));
            Assert.IsNull(Next("[]", ref pos));
        }

        [Test]
        public void ExtractNextObject_NestedObject_BalancedBraces()
        {
            int pos = 0;
            var result = Next("[{\"a\":{\"b\":2}}]", ref pos);
            Assert.AreEqual("{\"a\":{\"b\":2}}", result);
        }

        [Test]
        public void ExtractNextObject_BraceInStringNotCounted()
        {
            // Brace inside a JSON string must not affect depth counter
            int pos = 0;
            var result = Next("[{\"key\":\"value{notabrace}\"}]", ref pos);
            Assert.AreEqual("{\"key\":\"value{notabrace}\"}", result);
        }

        [Test]
        public void ExtractNextObject_EscapedQuoteInString_Handled()
        {
            int pos = 0;
            var result = Next("[{\"k\":\"say \\\"hi\\\"\"}]", ref pos);
            Assert.IsNotNull(result);
            StringAssert.Contains("say", result);
        }

        [Test]
        public void ExtractNextObject_AdvancesPos_CorrectlyForChainedCalls()
        {
            var arr = "[{\"id\":1},{\"id\":2},{\"id\":3}]";
            int pos = 0;
            int count = 0;
            while (Next(arr, ref pos) != null) count++;
            Assert.AreEqual(3, count, "must iterate all 3 objects");
        }
    }
}
