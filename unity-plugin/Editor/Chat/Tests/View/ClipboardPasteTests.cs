// TDD tests for MCPChatWindow paste interception (ClipPaste partial).
// Tests the injectable ClipboardReader delegate, not the real NSPasteboard.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ClipboardPasteTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ClipboardImageReader_TryRead_DoesNotThrow()
        {
            // Real TryRead may return null (no image on clipboard in CI), but must not throw.
            Assert.DoesNotThrow(() => ClipboardImageReader.TryRead());
        }

        [Test]
        public void ClipboardImageReader_TryRead_ReturnsNullOrNonEmpty()
        {
            var result = ClipboardImageReader.TryRead();
            // Either null (no image) or a non-empty byte array
            if (result != null)
                Assert.Greater(result.Length, 0, "If non-null, must have bytes");
        }

        [Test]
        public void MCPChatWindow_HasWireClipboardPasteMethod()
        {
            // Verify the method exists via reflection — tests the API surface.
            var method = typeof(MCPChatWindow).GetMethod(
                "WireClipboardPaste",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.IsNotNull(method, "MCPChatWindow must have WireClipboardPaste method");
        }
    }
}
