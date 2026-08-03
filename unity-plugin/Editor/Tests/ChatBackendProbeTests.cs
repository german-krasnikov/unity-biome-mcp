// Smoke test: ChatBackendProbe must never throw and must return false
// when no MCPChatWindow with a live backend exists (normal EditMode context).
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChatBackendProbeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void IsChatBackendRunning_NoWindow_ReturnsFalse()
        {
            // In EditMode tests no MCPChatWindow is open, so the result must be false.
            // Also validates that reflection probe doesn't throw when Chat asm is present.
            Assert.IsFalse(ChatBackendProbe.IsChatBackendRunning());
        }

        [Test]
        public void IsChatBackendRunning_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ChatBackendProbe.IsChatBackendRunning());
        }
    }
}
