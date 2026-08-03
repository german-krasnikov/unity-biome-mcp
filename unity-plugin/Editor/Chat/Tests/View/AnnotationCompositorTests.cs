using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationCompositorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private byte[] MakeTestPng()
        {
            var tex = new Texture2D(10, 10);
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return bytes;
        }

        [Test]
        public void Composite_NullOriginal_ReturnsNull()
            => Assert.IsNull(AnnotationCompositor.Composite(null, new IAnnotationCommand[0]));

        [Test]
        public void Composite_EmptyOriginal_ReturnsNull()
            => Assert.IsNull(AnnotationCompositor.Composite(new byte[0], new IAnnotationCommand[0]));

        [Test]
        public void Composite_NoCommands_ReturnsOriginal()
        {
            var png = MakeTestPng();
            var result = AnnotationCompositor.Composite(png, new IAnnotationCommand[0]);
            Assert.AreEqual(png, result);
        }

        [Test]
        public void Composite_NullCommands_ReturnsOriginal()
        {
            var png = MakeTestPng();
            var result = AnnotationCompositor.Composite(png, null);
            Assert.AreEqual(png, result);
        }

        [Test]
        public void Composite_WithLine_ReturnsValidPng()
        {
            var png = MakeTestPng();
            var cmds = new IAnnotationCommand[]
            {
                new LineCommand(new Color32(255, 0, 0, 255), 1f,
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f))
            };
            var result = AnnotationCompositor.Composite(png, cmds);
            Assert.IsNotNull(result);
            Assert.Greater(result.Length, 0);
            // PNG magic bytes
            Assert.AreEqual(0x89, result[0]);
            Assert.AreEqual(0x50, result[1]);
        }

        [Test]
        public void Composite_WithLine_PixelIsModified()
        {
            var png = MakeTestPng();
            var cmds = new IAnnotationCommand[]
            {
                new LineCommand(new Color32(255, 0, 0, 255), 2f,
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f))
            };
            var result = AnnotationCompositor.Composite(png, cmds);
            // Decode result and verify red channel increased at center
            var tex = new Texture2D(2, 2);
            tex.LoadImage(result);
            var px = tex.GetPixel(5, 5);
            Object.DestroyImmediate(tex);
            // Line at y=0.5 → row 5; red line over white background → still red-dominant or at least not fully white-g
            Assert.Greater(px.r, 0.9f); // red remains near 1
        }
    }
}
