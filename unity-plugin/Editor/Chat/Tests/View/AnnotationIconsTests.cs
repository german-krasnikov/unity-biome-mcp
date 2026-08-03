using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationIconsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Icons_PenNotNull()     => Assert.IsNotNull(AnnotationIcons.Pen);
        [Test] public void Icons_LineNotNull()    => Assert.IsNotNull(AnnotationIcons.Line);
        [Test] public void Icons_ArrowNotNull()   => Assert.IsNotNull(AnnotationIcons.Arrow);
        [Test] public void Icons_RectNotNull()    => Assert.IsNotNull(AnnotationIcons.Rect);
        [Test] public void Icons_EllipseNotNull() => Assert.IsNotNull(AnnotationIcons.Ellipse);
        [Test] public void Icons_TextNotNull()    => Assert.IsNotNull(AnnotationIcons.Text);
        [Test] public void Icons_EraseNotNull()   => Assert.IsNotNull(AnnotationIcons.Erase);
        [Test] public void Icons_UndoNotNull()    => Assert.IsNotNull(AnnotationIcons.Undo);
        [Test] public void Icons_RedoNotNull()    => Assert.IsNotNull(AnnotationIcons.Redo);
        [Test] public void Icons_ClearNotNull()   => Assert.IsNotNull(AnnotationIcons.Clear);
        [Test] public void Icons_Cube3DNotNull()  => Assert.IsNotNull(AnnotationIcons.Cube3D);
        [Test] public void Icons_SendNotNull()    => Assert.IsNotNull(AnnotationIcons.Send);
        [Test] public void Icons_WidthSNotNull()  => Assert.IsNotNull(AnnotationIcons.WidthS);
        [Test] public void Icons_WidthMNotNull()  => Assert.IsNotNull(AnnotationIcons.WidthM);
        [Test] public void Icons_WidthLNotNull()  => Assert.IsNotNull(AnnotationIcons.WidthL);

        [Test]
        public void Icons_AllToolIconsUnique()
        {
            var icons = new[]
            {
                AnnotationIcons.Pen, AnnotationIcons.Line, AnnotationIcons.Arrow,
                AnnotationIcons.Rect, AnnotationIcons.Ellipse, AnnotationIcons.Text,
                AnnotationIcons.Erase
            };
            Assert.AreEqual(7, new HashSet<Texture2D>(icons).Count, "Each tool icon must be a distinct Texture2D");
        }

        [Test]
        public void Icons_AllAre18x18()
        {
            var all = new[]
            {
                AnnotationIcons.Pen, AnnotationIcons.Line, AnnotationIcons.Arrow,
                AnnotationIcons.Rect, AnnotationIcons.Ellipse, AnnotationIcons.Text,
                AnnotationIcons.Erase, AnnotationIcons.Undo, AnnotationIcons.Redo,
                AnnotationIcons.Clear, AnnotationIcons.Cube3D, AnnotationIcons.Send,
                AnnotationIcons.WidthS, AnnotationIcons.WidthM, AnnotationIcons.WidthL
            };
            foreach (var icon in all)
            {
                Assert.AreEqual(18, icon.width,  $"{icon.name} width must be 18");
                Assert.AreEqual(18, icon.height, $"{icon.name} height must be 18");
            }
        }

        [Test]
        public void Icons_LazyInit_ReturnsSameInstance()
        {
            var a = AnnotationIcons.Pen;
            var b = AnnotationIcons.Pen;
            Assert.AreSame(a, b);
        }

        [Test]
        public void Icons_HideFlags_HideAndDontSave()
        {
            Assert.AreEqual(HideFlags.HideAndDontSave, AnnotationIcons.Pen.hideFlags);
        }

        [Test]
        public void Icons_AreNonReadable()
        {
            Assert.Throws<UnityException>(() => AnnotationIcons.Pen.GetPixel(0, 0));
        }

        [Test]
        public void Icons_AllFifteenUnique()
        {
            var all = new[]
            {
                AnnotationIcons.Pen, AnnotationIcons.Line, AnnotationIcons.Arrow,
                AnnotationIcons.Rect, AnnotationIcons.Ellipse, AnnotationIcons.Text,
                AnnotationIcons.Erase, AnnotationIcons.Undo, AnnotationIcons.Redo,
                AnnotationIcons.Clear, AnnotationIcons.Cube3D, AnnotationIcons.Send,
                AnnotationIcons.WidthS, AnnotationIcons.WidthM, AnnotationIcons.WidthL
            };
            Assert.AreEqual(15, new HashSet<Texture2D>(all).Count, "All 15 icons must be distinct instances");
        }

        [Test]
        public void Icons_AllHaveCorrectHideFlags()
        {
            Assert.AreEqual(HideFlags.HideAndDontSave, AnnotationIcons.Line.hideFlags);
            Assert.AreEqual(HideFlags.HideAndDontSave, AnnotationIcons.Cube3D.hideFlags);
            Assert.AreEqual(HideFlags.HideAndDontSave, AnnotationIcons.WidthL.hideFlags);
        }
    }
}
