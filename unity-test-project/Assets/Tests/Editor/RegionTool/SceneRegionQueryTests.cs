// Tests for SceneRegionQuery: finds GameObjects inside polygon via winding-number PIP.
// EditMode tests with fixture-owned scene objects and persistence state.
using System.IO;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    [UnityMCP.Editor.Testing.SkipOnWindows("Path separator issues in region test isolation setup")]
    public class SceneRegionQueryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Objects placed at known XZ positions for deterministic tests
        private GameObject _inside1;   // XZ=(5,5) — center of 10x10 square
        private GameObject _inside2;   // XZ=(3,7), Y=10 — high Y, but XZ inside
        private GameObject _outside;   // XZ=(50,50) — far outside all test polygons
        private GameObject _withBox;   // XZ=(2,2) — has BoxCollider, for component filter test

        // 10x10 square at origin: covers (0-10, 0-10) in XZ
        private const string SquareCsv = "0,0;10,0;10,10;0,10";
        // Tiny triangle at origin: only covers near (0,0)
        private const string TriangleCsv = "0,0;1,0;0,1";
        // Far-away polygon: nothing inside
        private const string RemoteCsv = "900,900;901,900;901,901;900,901";

        string _tmpRegionFile;

        [SetUp]
        public void SetUp()
        {
            _tmpRegionFile = Path.Combine(
                Path.GetTempPath(), "unity-biome-mcp-region-tests",
                Guid.NewGuid().ToString("N") + ".json");
            RegisterCleanup(SceneRegionState.IsolateForTests(_tmpRegionFile, 20).Dispose);

            _inside1 = TrackOwnedObject(new GameObject("SRQ_Inside1"));
            _inside1.transform.position = new Vector3(5, 0, 5);

            _inside2 = TrackOwnedObject(new GameObject("SRQ_Inside2"));
            _inside2.transform.position = new Vector3(3, 10, 7);  // Y=10, XZ inside

            _outside = TrackOwnedObject(new GameObject("SRQ_Outside"));
            _outside.transform.position = new Vector3(50, 0, 50);

            _withBox = TrackOwnedObject(new GameObject("SRQ_WithBox"));
            _withBox.transform.position = new Vector3(2, 0, 2);
            _withBox.AddComponent<BoxCollider>();
        }

        // ── Object inclusion ──────────────────────────────────────────────────

        [Test]
        public void FindInside_SquareRegion_FindsObjectsInside()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            StringAssert.Contains("SRQ_Inside1", result);
            StringAssert.Contains("SRQ_Inside2", result);
        }

        [Test]
        public void FindInside_SquareRegion_ExcludesObjectsOutside()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            StringAssert.DoesNotContain("SRQ_Outside", result);
        }

        [Test]
        public void FindInside_IgnoresYCoordinate()
        {
            // Inside2 at Y=10 should still be found (XZ projection only)
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            StringAssert.Contains("SRQ_Inside2", result);
        }

        [Test]
        public void FindInside_RemotePolygon_ReturnsZeroObjects()
        {
            var poly = Polygon2D.FromCsv(RemoteCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            StringAssert.Contains("0 objects", result);
        }

        // ── Component filter ──────────────────────────────────────────────────

        [Test]
        public void FindInside_ComponentFilter_OnlyMatchingReturned()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", "BoxCollider");
            StringAssert.Contains("SRQ_WithBox", result);
            StringAssert.DoesNotContain("SRQ_Inside1", result);
            StringAssert.DoesNotContain("SRQ_Inside2", result);
        }

        [Test]
        public void FindInside_ComponentFilter_NoMatch_ReturnsZero()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", "MeshRenderer");
            // None of our test objects have MeshRenderer
            StringAssert.Contains("0 objects", result);
        }

        // ── Output format ─────────────────────────────────────────────────────

        [Test]
        public void FindInside_OutputContainsAreaInHeader()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);  // area = 100m2
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            StringAssert.Contains("area=", result);
        }

        [Test]
        public void FindInside_WithLabel_ShowsLabelInHeader()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "north_forest", null);
            StringAssert.Contains("'north_forest'", result);
        }

        [Test]
        public void FindInside_EmptyLabel_ShowsPolygonInHeader()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "", null);
            StringAssert.Contains("polygon", result);
        }

        [Test]
        public void FindInside_OutputContainsInstanceId()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            // Instance IDs are prefixed with #
            StringAssert.Contains("#", result);
        }

        [Test]
        public void FindInside_OutputContainsXZPosition()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null);
            // XZ position format: (x,z)
            StringAssert.Contains("(5.00,5.00)", result);
        }

        // ── Cap behavior ──────────────────────────────────────────────────────

        [Test]
        public void FindInside_Cap1_ShowsOnlyOne()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            var result = SceneRegionQuery.FindInside(poly, "test", null, cap: 1);
            // Should show "+more" since there are multiple objects inside
            StringAssert.Contains("+", result);
        }

        [Test]
        public void FindInside_Cap200_ShowsAll()
        {
            var poly = Polygon2D.FromCsv(SquareCsv);
            // With large cap, no "+more" when only a few objects
            var result = SceneRegionQuery.FindInside(poly, "test", null, cap: 200);
            // Inside1, Inside2, WithBox are inside (3 objects total in this square)
            StringAssert.DoesNotContain("+more", result);
        }

        // ── Execute dispatch ──────────────────────────────────────────────────

        [Test]
        public void Execute_WithVertices_DispatchesCorrectly()
        {
            var argsJson = "{\"action\":\"objects_in_polygon\",\"vertices\":\"0,0;10,0;10,10;0,10\"}";
            var result = SceneRegionQuery.Execute(argsJson);
            StringAssert.Contains("objects in", result);
        }

        [Test]
        public void Execute_NoVerticesNoRegionId_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SceneRegionQuery.Execute("{\"action\":\"objects_in_polygon\"}"));
        }

        [Test]
        public void Execute_BadCsv_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SceneRegionQuery.Execute("{\"action\":\"objects_in_polygon\",\"vertices\":\"bad\"}"));
        }

        [Test]
        public void Execute_RegionId_LooksUpFromState_ReturnsResults()
        {
            // Arrange: store a 10x10 square region covering _inside1 and _inside2
            var snap = new RegionSnapshot
            {
                Id           = "test01",
                SchemaVersion = 1,
                VerticesFlat = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f },
                Area         = 100f,
                CenterX = 5f, CenterZ = 5f,
                MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 10f,
                ObjectPaths  = System.Array.Empty<string>(),
                ObjectIds    = System.Array.Empty<string>(),
                TotalCount   = 0,
                CreatedTicks = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            SceneRegionState.SetRegion(snap);

            // Act: Execute with only region_id (no vertices)
            var result = SceneRegionQuery.Execute("{\"region_id\":\"test01\"}");

            // Assert: should find objects placed inside the 10x10 square
            StringAssert.Contains("SRQ_Inside1", result);
            StringAssert.Contains("SRQ_Inside2", result);
        }

        [Test]
        public void Execute_RegionId_NotFound_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SceneRegionQuery.Execute("{\"region_id\":\"no_such_region\"}"));
        }

        // ── SpatialHelper dispatch ────────────────────────────────────────────

        [Test]
        public void SpatialHelper_ObjectsInPolygon_RoutesCorrectly()
        {
            // Verify SpatialHelper.Execute correctly routes to SceneRegionQuery
            var args = "{\"action\":\"objects_in_polygon\",\"vertices\":\"0,0;10,0;10,10;0,10\"}";
            var result = SpatialHelper.Execute(args);
            StringAssert.Contains("objects in", result);
        }

        [Test]
        public void SpatialHelper_InvalidAction_IncludesObjectsInPolygon()
        {
            // Error message for unknown action should list objects_in_polygon as valid
            var ex = Assert.Throws<System.ArgumentException>(() =>
                SpatialHelper.Execute("{\"action\":\"bad_action\"}"));
            StringAssert.Contains("objects_in_polygon", ex.Message);
        }
    }
}
