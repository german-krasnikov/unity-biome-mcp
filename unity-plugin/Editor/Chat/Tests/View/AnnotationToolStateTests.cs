using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationToolStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Default_ToolIsArrow()
        {
            var state = new AnnotationToolState();
            Assert.AreEqual(AnnotationTool.Arrow, state.ActiveTool);
        }

        [Test]
        public void Default_ColorIsRed()
        {
            var state = new AnnotationToolState();
            var red = new Color32(255, 50, 50, 255);
            Assert.AreEqual(red, state.ActiveColor);
        }

        [Test]
        public void Reset_RestoresDefaults()
        {
            var state = new AnnotationToolState();
            state.ActiveTool = AnnotationTool.Pen;
            state.ActiveColor = new Color32(0, 0, 255, 255);
            state.StrokeWidth = 10f;
            state.FillMode = AnnotationFill.Solid;

            state.Reset();

            Assert.AreEqual(AnnotationTool.Arrow, state.ActiveTool);
            Assert.AreEqual(AnnotationToolState.Palette[0], state.ActiveColor);
            Assert.AreEqual(AnnotationToolState.WidthPresets[1], state.StrokeWidth);
            Assert.AreEqual(AnnotationFill.None, state.FillMode);
        }

        [Test]
        public void Palette_Has8Colors()
        {
            Assert.AreEqual(8, AnnotationToolState.Palette.Length);
        }

        [Test]
        public void WidthPresets_Has3Sizes()
        {
            Assert.AreEqual(3, AnnotationToolState.WidthPresets.Length);
        }
    }
}
