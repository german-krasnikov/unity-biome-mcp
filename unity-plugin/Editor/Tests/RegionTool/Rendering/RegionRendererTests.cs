using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class RegionRendererTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── RenderStyle ───────────────────────────────────────────────────

        [Test]
        public void FillColor_AnyColor_Returns12PercentAlpha()
        {
            var c = new Color(0.2f, 0.5f, 1.0f, 0.9f);
            var fill = RenderStyle.FillColor(c);
            Assert.AreEqual(c.r, fill.r, 1e-5f);
            Assert.AreEqual(c.g, fill.g, 1e-5f);
            Assert.AreEqual(c.b, fill.b, 1e-5f);
            Assert.AreEqual(0.12f, fill.a, 1e-5f);
        }

        [Test]
        public void FillColor_PreservesAllChannels()
        {
            var c = new Color(0.3f, 0.7f, 0.4f, 1.0f);
            var fill = RenderStyle.FillColor(c);
            Assert.AreEqual(0.3f, fill.r, 1e-5f);
            Assert.AreEqual(0.7f, fill.g, 1e-5f);
            Assert.AreEqual(0.4f, fill.b, 1e-5f);
            Assert.AreEqual(0.12f, fill.a, 1e-5f);
        }

        [Test]
        public void RenderStyle_ContourWidth_Is2_5()
        {
            Assert.AreEqual(2.5f, RenderStyle.ContourWidth, 1e-5f);
        }

        [Test]
        public void RenderStyle_GlowWidthOuter_Is9()
        {
            Assert.AreEqual(9f, RenderStyle.GlowWidthOuter, 1e-5f);
        }

        // ── BuildHandlesBuffer ────────────────────────────────────────────

        [Test]
        public void BuildHandlesBuffer_Closed_HasVertsPlusOne()
        {
            var verts = new List<Vector2>
            {
                new(1f, 2f), new(3f, 4f), new(5f, 6f)
            };
            var buf = RegionRenderer.BuildHandlesBuffer(verts, closed: true);
            Assert.AreEqual(4, buf.Length); // 3 verts + closing repeat
        }

        [Test]
        public void BuildHandlesBuffer_Open_HasExactVertCount()
        {
            var verts = new List<Vector2>
            {
                new(1f, 2f), new(3f, 4f), new(5f, 6f), new(7f, 8f)
            };
            var buf = RegionRenderer.BuildHandlesBuffer(verts, closed: false);
            Assert.AreEqual(4, buf.Length);
        }

        [Test]
        public void BuildHandlesBuffer_Closed_LastElementEqualsFirst()
        {
            var verts = new List<Vector2> { new(1f, 2f), new(3f, 4f), new(5f, 6f) };
            var buf = RegionRenderer.BuildHandlesBuffer(verts, closed: true);
            Assert.AreEqual(buf[0], buf[buf.Length - 1]);
        }

        [Test]
        public void BuildHandlesBuffer_SetsY_To0_01()
        {
            var verts = new List<Vector2> { new(1f, 2f), new(3f, 4f), new(5f, 6f) };
            var buf = RegionRenderer.BuildHandlesBuffer(verts, closed: false);
            foreach (var v in buf)
                Assert.AreEqual(0.01f, v.y, 1e-5f, "y should be 0.01 to avoid z-fighting");
        }

        [Test]
        public void BuildHandlesBuffer_MapsXZ_Correctly()
        {
            var verts = new List<Vector2> { new(10f, 20f), new(30f, 40f), new(50f, 60f) };
            var buf = RegionRenderer.BuildHandlesBuffer(verts, closed: false);
            Assert.AreEqual(10f, buf[0].x, 1e-5f);
            Assert.AreEqual(20f, buf[0].z, 1e-5f);
            Assert.AreEqual(30f, buf[1].x, 1e-5f);
            Assert.AreEqual(40f, buf[1].z, 1e-5f);
        }

        // ── Draw guards ───────────────────────────────────────────────────

        [Test]
        public void Draw_NullVertices_DoesNotThrow()
        {
            var state = new RenderState { Vertices = null };
            Assert.DoesNotThrow(() => RegionRenderer.Draw(state));
        }

        [Test]
        public void Draw_EmptyVertices_DoesNotThrow()
        {
            var state = new RenderState { Vertices = new List<Vector2>() };
            Assert.DoesNotThrow(() => RegionRenderer.Draw(state));
        }

        [Test]
        public void Draw_SingleVertex_DoesNotThrow()
        {
            var state = new RenderState
            {
                Vertices = new List<Vector2> { new(1f, 2f) }
            };
            Assert.DoesNotThrow(() => RegionRenderer.Draw(state));
        }
    }
}
