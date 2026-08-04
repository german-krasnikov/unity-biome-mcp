using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConsoleClearBufferTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ConsoleClearBuffer_IsRegistered()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("console_clear_buffer"));
        }

        [Test]
        public void ConsoleClearBuffer_ReturnsOk()
        {
            var result = CommandRegistry.Execute("console_clear_buffer", "");
            Assert.AreEqual("ok", result);
        }

        [Test]
        public void ConsoleClearBuffer_AllowedDuringCompile()
        {
            Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("console_clear_buffer"));
        }
    }
}
