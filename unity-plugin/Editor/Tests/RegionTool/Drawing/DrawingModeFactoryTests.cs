using System;
using NUnit.Framework;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class DrawingModeFactoryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Create_Lasso_ReturnsLassoMode()
        {
            var mode = DrawingModeFactory.Create(DrawingModeId.Lasso);
            Assert.IsInstanceOf<LassoMode>(mode);
        }

        [Test]
        public void Create_Rectangle_ReturnsRectangleMode()
        {
            var mode = DrawingModeFactory.Create(DrawingModeId.Rectangle);
            Assert.IsInstanceOf<RectangleMode>(mode);
        }

        [Test]
        public void Create_Circle_ReturnsCircleMode()
        {
            var mode = DrawingModeFactory.Create(DrawingModeId.Circle);
            Assert.IsInstanceOf<CircleMode>(mode);
        }

        [Test]
        public void Create_PointByPoint_ReturnsPointByPointMode()
        {
            var mode = DrawingModeFactory.Create(DrawingModeId.PointByPoint);
            Assert.IsInstanceOf<PointByPointMode>(mode);
        }

        [Test]
        public void Create_AllModes_HaveCorrectId()
        {
            foreach (DrawingModeId id in Enum.GetValues(typeof(DrawingModeId)))
            {
                var mode = DrawingModeFactory.Create(id);
                Assert.AreEqual(id, mode.Id, $"Expected mode Id={id}");
            }
        }

        [Test]
        public void Create_UnknownId_ThrowsArgumentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DrawingModeFactory.Create((DrawingModeId)999));
        }
    }
}
