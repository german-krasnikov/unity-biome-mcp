// Tests for TextureHandle ownership / disposal.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TextureHandleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void OwnedTexture_Detached_Destroyed()
        {
            var tex = new Texture2D(4, 4);
            using (var handle = new TextureHandle(tex, TextureOwnership.Owned))
            {
                Assert.AreSame(tex, handle.Texture);
            }
            Assert.IsTrue(tex == null, "destroyed UnityEngine.Object must report as null via == overload");
        }

        [Test]
        public void UnitySharedTexture_Detached_NotDestroyed()
        {
            var tex = Texture2D.whiteTexture;
            using (var handle = new TextureHandle(tex, TextureOwnership.UnityShared))
            {
                Assert.AreSame(tex, handle.Texture);
            }
            Assert.IsNotNull(tex);
        }

        [Test]
        public void NullTexture_Detached_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                using var handle = new TextureHandle(null, TextureOwnership.Owned);
                handle.Dispose();
            });
        }
    }
}
