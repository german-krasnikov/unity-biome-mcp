using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class JsonHelperRequireStringTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void RequireString_KeyMissing_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                JsonHelper.RequireString("{\"other\":\"x\"}", "path"));
            Assert.AreEqual("Required argument 'path' is missing or empty", ex.Message);
        }

        [Test]
        public void RequireString_ValuePresentEmpty_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                JsonHelper.RequireString("{\"path\":\"\"}", "path"));
            Assert.AreEqual("Required argument 'path' is missing or empty", ex.Message);
        }

        [Test]
        public void RequireString_ValuePresent_ReturnsValue()
        {
            var result = JsonHelper.RequireString("{\"path\":\"/Foo\"}", "path");
            Assert.AreEqual("/Foo", result);
        }
    }
}
