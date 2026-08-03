// TDD tests for image drag/drop extension in ProcessExternalPath + ImageChipProvider.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ImageDragDropTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // ── IsImageExtension ──────────────────────────────────────────────────

        [Test]
        public void IsImageExtension_Png_True()
            => Assert.IsTrue(MCPChatWindow.IsImageExtension(".png"));

        [Test]
        public void IsImageExtension_PngUpperCase_True()
            => Assert.IsTrue(MCPChatWindow.IsImageExtension(".PNG"));

        [Test]
        public void IsImageExtension_Jpg_True()
            => Assert.IsTrue(MCPChatWindow.IsImageExtension(".jpg"));

        [Test]
        public void IsImageExtension_Jpeg_True()
            => Assert.IsTrue(MCPChatWindow.IsImageExtension(".jpeg"));

        [Test]
        public void IsImageExtension_Gif_True()
            => Assert.IsTrue(MCPChatWindow.IsImageExtension(".gif"));

        [Test]
        public void IsImageExtension_Bmp_True()
            => Assert.IsTrue(MCPChatWindow.IsImageExtension(".bmp"));

        [Test]
        public void IsImageExtension_Cs_False()
            => Assert.IsFalse(MCPChatWindow.IsImageExtension(".cs"));

        [Test]
        public void IsImageExtension_Empty_False()
            => Assert.IsFalse(MCPChatWindow.IsImageExtension(""));

        [Test]
        public void IsImageExtension_Null_False()
            => Assert.IsFalse(MCPChatWindow.IsImageExtension(null));

        // ── ProcessExternalPath image branch ─────────────────────────────────

        [Test]
        public void ProcessExternalPath_PngFile_InsertsChipWithStoredPath()
        {
            var calls = new List<(UnityEngine.Object obj, string path, string name)>();
            // Use a temp file so ImportFile succeeds
            var src = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_drag.png");
            System.IO.File.WriteAllBytes(src, new byte[] { 137, 80, 78, 71 });
            try
            {
                MCPChatWindow.ProcessExternalPath(src, (o, p, n) => calls.Add((o, p, n)));
                Assert.AreEqual(1, calls.Count);
                // Path must end with .png — the stored destination, not any fallback.
                Assert.IsTrue(calls[0].path.EndsWith(".png"),
                    $"Expected .png destination path, got: {calls[0].path}");
                Assert.AreEqual("test_drag.png", calls[0].name);
            }
            finally { if (System.IO.File.Exists(src)) System.IO.File.Delete(src); }
        }

        [Test]
        public void ProcessExternalPath_JpgFile_InsertsChip()
        {
            var calls = new List<(UnityEngine.Object obj, string path, string name)>();
            var src = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shot.jpg");
            System.IO.File.WriteAllBytes(src, new byte[] { 0xFF, 0xD8 });
            try
            {
                MCPChatWindow.ProcessExternalPath(src, (o, p, n) => calls.Add((o, p, n)));
                Assert.AreEqual(1, calls.Count);
                Assert.AreEqual("shot.jpg", calls[0].name);
            }
            finally { if (System.IO.File.Exists(src)) System.IO.File.Delete(src); }
        }

        [Test]
        public void ProcessExternalPath_CsFile_InsertsRegularChipWithOriginalPath()
        {
            var calls = new List<(UnityEngine.Object obj, string path, string name)>();
            MCPChatWindow.ProcessExternalPath("/Users/dev/MyScript.cs", (o, p, n) => calls.Add((o, p, n)));
            Assert.AreEqual(1, calls.Count);
            // Non-image: path unchanged
            Assert.AreEqual("/Users/dev/MyScript.cs", calls[0].path);
        }

        // ── ImageChipProvider ─────────────────────────────────────────────────

        [Test]
        public void ImageChipProvider_CanHandle_NullObj_PngPath_True()
        {
            var provider = new ImageChipProvider();
            Assert.IsTrue(provider.CanHandle(null, "/abs/path/img.png"));
        }

        [Test]
        public void ImageChipProvider_CanHandle_NullObj_JpgPath_True()
        {
            var provider = new ImageChipProvider();
            Assert.IsTrue(provider.CanHandle(null, "/abs/path/photo.jpg"));
        }

        [Test]
        public void ImageChipProvider_CanHandle_NullObj_CsPath_False()
        {
            var provider = new ImageChipProvider();
            Assert.IsFalse(provider.CanHandle(null, "/abs/path/file.cs"));
        }

        [Test]
        public void ImageChipProvider_CanHandle_UnityObj_PngPath_False()
        {
            var provider = new ImageChipProvider();
            var tex = new UnityEngine.Texture2D(2, 2);
            try { Assert.IsFalse(provider.CanHandle(tex, "/abs/path/img.png")); }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        [Test]
        public void ImageChipProvider_FormatPayload_ReturnsEmpty()
        {
            var provider = new ImageChipProvider();
            var chip     = new ChipData(ChipKindKeys.Image, "/path/img.png", "img.png", 0);
            var ctx      = new ChipPayloadContext("path", null);
            Assert.AreEqual("", provider.FormatPayload(chip, ctx));
        }

        [Test]
        public void ImageChipProvider_Key_IsImage()
        {
            Assert.AreEqual(ChipKindKeys.Image, new ImageChipProvider().Key);
        }

        [Test]
        public void ChipKindRegistry_ResolvesImageProvider_ForNullObjPngPath()
        {
            ChipKindRegistry.ResetToBuiltIns();
            var provider = ChipKindRegistry.Resolve(null, "/some/path/screenshot.png");
            Assert.IsNotNull(provider);
            Assert.AreEqual(ChipKindKeys.Image, provider.Key);
        }
    }
}
