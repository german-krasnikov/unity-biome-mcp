using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationRasterizerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Constructor_BufferIsTransparent()
        {
            var r = new AnnotationRasterizer(10, 10);
            Assert.AreEqual(new Color32(0, 0, 0, 0), r.Buffer[0]);
            Assert.AreEqual(100, r.Buffer.Length);
        }

        [Test]
        public void Render_Line_SetsPixels()
        {
            var r = new AnnotationRasterizer(100, 100);
            var cmd = new LineCommand(new Color32(255, 0, 0, 255), 1f,
                new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f));
            r.Render(cmd);
            // Middle pixel on horizontal line at y=0.5 → row 50, col 50
            Assert.AreEqual(255, r.Buffer[50 * 100 + 50].r);
        }

        [Test]
        public void Render_Rect_DrawsOutline()
        {
            var r = new AnnotationRasterizer(100, 100);
            var cmd = new RectCommand(new Color32(0, 255, 0, 255), 1f, AnnotationFill.None,
                new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            r.Render(cmd);
            // Top edge at y=0.2 → row 20; middle of that row at x=0.5 → col 50
            Assert.AreEqual(255, r.Buffer[20 * 100 + 50].g);
        }

        [Test]
        public void RenderAll_MultipleCommands_AllRendered()
        {
            var r = new AnnotationRasterizer(100, 100);
            var cmds = new IAnnotationCommand[]
            {
                new LineCommand(new Color32(255, 0, 0, 255), 1f, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f)),
                new LineCommand(new Color32(0, 0, 255, 255), 1f, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f)),
            };
            r.RenderAll(cmds);
            Assert.AreNotEqual(new Color32(0, 0, 0, 0), r.Buffer[50 * 100 + 50]);
        }

        [Test]
        public void Render_Rect_Fill_PaintsInterior()
        {
            var r = new AnnotationRasterizer(100, 100);
            var cmd = new RectCommand(new Color32(0, 0, 255, 255), 1f, AnnotationFill.Solid,
                new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
            r.Render(cmd);
            // Interior pixel at center
            Assert.AreNotEqual(new Color32(0, 0, 0, 0), r.Buffer[50 * 100 + 50]);
        }

        [Test]
        public void Render_Arrow_SetsPixels()
        {
            var r = new AnnotationRasterizer(100, 100);
            var cmd = new ArrowCommand(new Color32(255, 255, 0, 255), 1f,
                new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f));
            r.Render(cmd);
            Assert.AreNotEqual(new Color32(0, 0, 0, 0), r.Buffer[50 * 100 + 50]);
        }

        [Test]
        public void Render_Ellipse_SetsPixels()
        {
            var r = new AnnotationRasterizer(100, 100);
            // center=(0.5,0.5), radiusPoint=(0.8,0.7) → rx=30, ry=20
            var cmd = new EllipseCommand(new Color32(255, 0, 255, 255), 2f, AnnotationFill.None,
                new Vector2(0.5f, 0.5f), new Vector2(0.8f, 0.7f));
            r.Render(cmd);
            // Any non-transparent pixel in the buffer means ellipse was drawn
            bool anySet = false;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < r.Buffer.Length; i++)
                if (r.Buffer[i].a != 0) { anySet = true; break; }
            Assert.IsTrue(anySet, "Ellipse should have drawn some pixels");
        }
    }
}
