using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class RegionSnapshotAnnotationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── CreatePoint ────────────────────────────────────────────────────────

        [Test]
        public void CreatePoint_SetsAnnotationType_Point()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5f, 3f), Array.Empty<string>(), "Level1");
            Assert.AreEqual("point", snap.AnnotationType);
        }

        [Test]
        public void CreatePoint_SingleVertex_AreaZero()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5f, 3f), Array.Empty<string>(), "Level1");
            Assert.AreEqual(0f, snap.Area);
        }

        [Test]
        public void CreatePoint_CenterEqualsPosition()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5.2f, 3.1f), Array.Empty<string>(), "Level1");
            Assert.AreEqual(5.2f, snap.CenterX, 1e-4f);
            Assert.AreEqual(3.1f, snap.CenterZ, 1e-4f);
        }

        [Test]
        public void CreatePoint_WithLabel()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(1f, 2f), Array.Empty<string>(), "S", "SpawnPoint");
            Assert.AreEqual("SpawnPoint", snap.Label);
        }

        [Test]
        public void CreatePoint_WithoutLabel_LabelNullOrEmpty()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(1f, 2f), Array.Empty<string>(), "S");
            Assert.IsTrue(string.IsNullOrEmpty(snap.Label));
        }

        [Test]
        public void CreatePoint_StoresNearestPaths()
        {
            var paths = new[] { "/Player/Spawn", "/Triggers/Zone" };
            var snap = RegionSnapshot.CreatePoint("p1", Vector2.zero, paths, "S");
            Assert.AreEqual(paths, snap.ObjectPaths);
        }

        [Test]
        public void CreatePoint_VerticesFlatHasTwoElements()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(4f, 7f), Array.Empty<string>(), "S");
            Assert.AreEqual(2, snap.VerticesFlat.Length);
            Assert.AreEqual(4f, snap.VerticesFlat[0], 1e-4f);
            Assert.AreEqual(7f, snap.VerticesFlat[1], 1e-4f);
        }

        // ── CreatePolyline ────────────────────────────────────────────────────

        [Test]
        public void CreatePolyline_SetsAnnotationType_Polyline()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(5f, 5f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            Assert.AreEqual("polyline", snap.AnnotationType);
        }

        [Test]
        public void CreatePolyline_CalculatesLength()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S");
            Assert.AreEqual(10f, snap.LengthOrDistance, 0.01f);
        }

        [Test]
        public void CreatePolyline_CalculatesLength_MultiSegment()
        {
            // 3-4-5 right triangle: legs 3 and 4, hypotenuse 5
            var pts = new[] { new Vector2(0f, 0f), new Vector2(3f, 0f), new Vector2(3f, 4f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S");
            Assert.AreEqual(7f, snap.LengthOrDistance, 0.01f);
        }

        [Test]
        public void CreatePolyline_CalculatesDirection_Normalized()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S");
            // Direction should be normalized (0.707, 0.707) = "0.71,0.71"
            Assert.IsNotNull(snap.Direction);
            StringAssert.Contains("0.71", snap.Direction);
        }

        [Test]
        public void CreatePolyline_MinTwoPoints_SetsVerticesFlat()
        {
            var pts = new[] { new Vector2(1f, 2f), new Vector2(4f, 6f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S");
            Assert.AreEqual(4, snap.VerticesFlat.Length); // 2 points * 2 floats
        }

        [Test]
        public void CreatePolyline_StoresNearPaths()
        {
            var pts = new[] { Vector2.zero, new Vector2(5f, 0f) };
            var paths = new[] { "/Enemies/Guard1" };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, paths, "S");
            Assert.AreEqual(paths, snap.ObjectPaths);
        }

        // ── CreateMeasurement ─────────────────────────────────────────────────

        [Test]
        public void CreateMeasurement_SetsAnnotationType_Measurement()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(10f, 0f), "S");
            Assert.AreEqual("measurement", snap.AnnotationType);
        }

        [Test]
        public void CreateMeasurement_CalculatesDistance()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(10f, 0f), "S");
            Assert.AreEqual(10f, snap.LengthOrDistance, 0.01f);
        }

        [Test]
        public void CreateMeasurement_CalculatesDistance_Diagonal()
        {
            // 3-4-5 right triangle
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(3f, 4f), "S");
            Assert.AreEqual(5f, snap.LengthOrDistance, 0.01f);
        }

        [Test]
        public void CreateMeasurement_TwoVertices_InVerticesFlat()
        {
            var a = new Vector2(1f, 2f);
            var b = new Vector2(4f, 6f);
            var snap = RegionSnapshot.CreateMeasurement("m1", a, b, "S");
            Assert.AreEqual(4, snap.VerticesFlat.Length);
            Assert.AreEqual(a.x, snap.VerticesFlat[0], 1e-4f);
            Assert.AreEqual(a.y, snap.VerticesFlat[1], 1e-4f);
            Assert.AreEqual(b.x, snap.VerticesFlat[2], 1e-4f);
            Assert.AreEqual(b.y, snap.VerticesFlat[3], 1e-4f);
        }

        [Test]
        public void CreateMeasurement_WithLabel()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(5f, 0f), "S", "GapWidth");
            Assert.AreEqual("GapWidth", snap.Label);
        }

        // ── BackwardCompat ────────────────────────────────────────────────────

        [Test]
        public void BackwardCompat_NullAnnotationType_ShortLabelUsesRegionDefault()
        {
            // Simulates old RegionSnapshot loaded from JSON (AnnotationType=null)
            var snap = new RegionSnapshot
            {
                Id = "old",
                AnnotationType = null,
                Area = 50f,
                ObjectPaths = new[] { "/Obj/A" },
                TotalCount = 1,
            };
            // ShortLabel should not throw and use region format
            var label = snap.ShortLabel;
            StringAssert.Contains("m²", label);
        }

        // ── ShortLabel dispatch ────────────────────────────────────────────────

        [Test]
        public void ShortLabel_Point_WithLabel()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5.2f, 3.1f), Array.Empty<string>(), "S", "SpawnPoint");
            var label = snap.ShortLabel;
            StringAssert.Contains("SpawnPoint", label);
            StringAssert.Contains("5.2", label);
            StringAssert.Contains("3.1", label);
        }

        [Test]
        public void ShortLabel_Point_WithoutLabel_ShowsPos()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5.2f, 3.1f), Array.Empty<string>(), "S");
            var label = snap.ShortLabel;
            StringAssert.Contains("5.2", label);
            StringAssert.Contains("3.1", label);
        }

        [Test]
        public void ShortLabel_Polyline_WithLabel()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S", "PatrolPath");
            var label = snap.ShortLabel;
            StringAssert.Contains("PatrolPath", label);
            StringAssert.Contains("pts", label);
        }

        [Test]
        public void ShortLabel_Polyline_WithoutLabel_ShowsPtsAndLen()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S");
            var label = snap.ShortLabel;
            StringAssert.Contains("pts", label);
            StringAssert.Contains("m", label);
        }

        [Test]
        public void ShortLabel_Measurement_WithLabel()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(12.4f, 0f), "S", "GapWidth");
            var label = snap.ShortLabel;
            StringAssert.Contains("GapWidth", label);
            StringAssert.Contains("m", label);
        }

        [Test]
        public void ShortLabel_Measurement_WithoutLabel_ShowsDist()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(12.4f, 0f), "S");
            var label = snap.ShortLabel;
            StringAssert.Contains("m", label);
        }

        [Test]
        public void ShortLabel_Region_UsesExistingFormat()
        {
            var snap = new RegionSnapshot
            {
                AnnotationType = "region",
                Area = 100f,
                ObjectPaths = new[] { "/A", "/B" },
                TotalCount = 2,
                Truncated = false,
            };
            var label = snap.ShortLabel;
            StringAssert.Contains("m²", label);
            StringAssert.Contains("2obj", label);
        }
    }
}
