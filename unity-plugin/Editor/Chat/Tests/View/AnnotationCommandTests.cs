using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationCommandTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);

        [Test]
        public void PenCommand_HasCorrectTool()
        {
            var cmd = new PenCommand(Red, 2f, new List<Vector2> { Vector2.zero });
            Assert.AreEqual(AnnotationTool.Pen, cmd.Tool);
        }

        [Test]
        public void LineCommand_HasTwoPoints()
        {
            var cmd = new LineCommand(Red, 2f, Vector2.zero, Vector2.one);
            Assert.AreEqual(2, cmd.Points.Count);
        }

        [Test]
        public void ArrowCommand_HasTwoPoints()
        {
            var cmd = new ArrowCommand(Red, 2f, Vector2.zero, Vector2.one);
            Assert.AreEqual(2, cmd.Points.Count);
            Assert.AreEqual(AnnotationTool.Arrow, cmd.Tool);
        }

        [Test]
        public void RectCommand_HasTwoCorners()
        {
            var cmd = new RectCommand(Red, 2f, AnnotationFill.None, Vector2.zero, Vector2.one);
            Assert.AreEqual(2, cmd.Points.Count);
            Assert.AreEqual(AnnotationTool.Rect, cmd.Tool);
        }

        [Test]
        public void EllipseCommand_HasCenterAndRadius()
        {
            var center = new Vector2(0.5f, 0.5f);
            var radius = new Vector2(0.8f, 0.5f);
            var cmd = new EllipseCommand(Red, 2f, AnnotationFill.None, center, radius);

            Assert.AreEqual(2, cmd.Points.Count);
            Assert.AreEqual(center, cmd.Points[0]);
            Assert.AreEqual(radius, cmd.Points[1]);
            Assert.AreEqual(AnnotationTool.Ellipse, cmd.Tool);
        }

        [Test]
        public void TextCommand_HasPositionAndText()
        {
            var pos = new Vector2(0.3f, 0.7f);
            var cmd = new TextCommand(Red, pos, "Hello");

            Assert.AreEqual(1, cmd.Points.Count);
            Assert.AreEqual(pos, cmd.Points[0]);
            Assert.AreEqual("Hello", cmd.Text);
            Assert.AreEqual(AnnotationTool.Text, cmd.Tool);
        }

        [Test]
        public void TextCommand_NullText_DefaultsToEmpty()
        {
            var cmd = new TextCommand(Red, Vector2.zero, null);
            Assert.AreEqual("", cmd.Text);
        }

        [Test]
        public void PenCommand_DefensiveCopy_PointsNotMutated()
        {
            var source = new List<Vector2> { new Vector2(0.1f, 0.2f), new Vector2(0.3f, 0.4f) };
            var cmd = new PenCommand(Red, 2f, source);

            source.Add(new Vector2(0.9f, 0.9f)); // mutate original

            Assert.AreEqual(2, cmd.Points.Count, "PenCommand must not be affected by source mutation");
        }
    }
}
