using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Chat.Tests
{
    /// <summary>
    /// Tests for RegionChipProvider.FormatPayload dispatch on annotation types:
    /// point, polyline, measurement (Phase 1A).
    /// </summary>
    [TestFixture]
    public class RegionChipProviderAnnotationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        string _tmpFile;
        RegionChipProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _tmpFile = Path.Combine(Path.GetTempPath(),
                $"unity-mcp-region-annotation-{Guid.NewGuid():N}.json");
            RegisterCleanup(SceneRegionState.IsolateForTests(_tmpFile, 20).Dispose);
            RegisterCleanup(ChipKindRegistry.PreserveStateForTests().Dispose);
            ChipKindRegistry.ResetToBuiltIns();
            ChipKindRegistry.Register(new RegionChipProvider());
            _provider = ChipKindRegistry.ForKey("region") as RegionChipProvider;
        }

        // ── Point summary ─────────────────────────────────────────────────────

        [Test]
        public void FormatPayload_Point_Summary_ContainsPos()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5.2f, 3.1f), Array.Empty<string>(), "Level1", "SpawnPoint");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("pos=", result);
            StringAssert.Contains("5.20", result);
            StringAssert.Contains("3.10", result);
        }

        [Test]
        public void FormatPayload_Point_Summary_ContainsLabel()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5f, 3f), Array.Empty<string>(), "Level1", "SpawnPoint");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("label=SpawnPoint", result);
        }

        [Test]
        public void FormatPayload_Point_Summary_ContainsScene()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5f, 3f), Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("scene=Level1", result);
        }

        [Test]
        public void FormatPayload_Point_Summary_NoNearestObjects()
        {
            var snap = RegionSnapshot.CreatePoint("p1", Vector2.zero, Array.Empty<string>(), "S");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.DoesNotContain("nearest:", result);
        }

        // ── Point full ───────────────────────────────────────────────────────

        [Test]
        public void FormatPayload_Point_Full_ContainsNearest()
        {
            var paths = new[] { "/Player/SpawnAnchor", "/Triggers/SpawnTrigger" };
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(5f, 3f), paths, "Level1", "SpawnPoint");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            StringAssert.Contains("nearest:", result);
            StringAssert.Contains("/Player/SpawnAnchor", result);
            StringAssert.Contains("/Triggers/SpawnTrigger", result);
        }

        [Test]
        public void FormatPayload_Point_Full_NoNearestWhenEmpty()
        {
            var snap = RegionSnapshot.CreatePoint("p1", Vector2.zero, Array.Empty<string>(), "S");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            StringAssert.DoesNotContain("nearest:", result);
        }

        // ── Polyline summary ──────────────────────────────────────────────────

        [Test]
        public void FormatPayload_Polyline_Summary_ContainsPts()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(5f, 5f),
                              new Vector2(0f, 5f), new Vector2(0f, 2f), new Vector2(3f, 2f), new Vector2(3f, 4f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1", "PatrolPath");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("pts=7", result);
        }

        [Test]
        public void FormatPayload_Polyline_Summary_ContainsLen()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("len=10", result);
            StringAssert.Contains("m", result);
        }

        [Test]
        public void FormatPayload_Polyline_Summary_ContainsDir()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("dir=", result);
        }

        [Test]
        public void FormatPayload_Polyline_Summary_ContainsLabel()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1", "PatrolPath");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("label=PatrolPath", result);
        }

        // ── Polyline full ─────────────────────────────────────────────────────

        [Test]
        public void FormatPayload_Polyline_Full_ContainsPoints()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5.1f, 0.3f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            StringAssert.Contains("points:", result);
            StringAssert.Contains("x=0.00", result);
        }

        // ── Polyline enriched format (Phase v0.64) ────────────────────────────

        [Test]
        public void Polyline_FormatPayload_ContainsTypeField()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("type=polyline", result);
        }

        [Test]
        public void Polyline_FormatPayload_ContainsStartEnd()
        {
            var pts = new[] { new Vector2(1f, 2f), new Vector2(3f, 4f), new Vector2(5f, 6f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("start=", result);
            StringAssert.Contains("end=", result);
        }

        [Test]
        public void Polyline_FormatPayload_PointsYamlStyle()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 3f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            StringAssert.Contains("- x=", result);
            StringAssert.Contains(" z=", result);
        }

        [Test]
        public void FormatPayload_Polyline_Summary_NoPoints()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 3f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.DoesNotContain("- x=", result);
        }

        [Test]
        public void FormatPayload_Polyline_Full_ContainsNearObjects()
        {
            var pts = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f) };
            var paths = new[] { "/Enemies/Guard1", "/Waypoints/WP_01" };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, paths, "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            StringAssert.Contains("near", result);
            StringAssert.Contains("/Enemies/Guard1", result);
        }

        // ── Measurement summary ───────────────────────────────────────────────

        [Test]
        public void FormatPayload_Measurement_Summary_ContainsDist()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(12.4f, 0f), "Level1", "GapWidth");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("dist=12.4", result);
            StringAssert.Contains("m", result);
        }

        [Test]
        public void FormatPayload_Measurement_Summary_ContainsFromTo()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", new Vector2(0f, 0f), new Vector2(8.7f, 9.1f), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("from=", result);
            StringAssert.Contains("to=", result);
        }

        [Test]
        public void FormatPayload_Measurement_Summary_ContainsLabel()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(5f, 0f), "Level1", "GapWidth");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("label=GapWidth", result);
        }

        [Test]
        public void FormatPayload_Measurement_Summary_ContainsScene()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(5f, 0f), "Level1");
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("scene=Level1", result);
        }

        // ── Region backward compat ────────────────────────────────────────────

        [Test]
        public void FormatPayload_Region_Unchanged_ContainsArea()
        {
            var snap = MakeRegionSnap("r1");
            snap.Area = 142.5f;
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("area=142.5", result);
        }

        [Test]
        public void FormatPayload_NullAnnotationType_FallsBackToRegion()
        {
            var snap = MakeRegionSnap("r1");
            snap.AnnotationType = null; // simulate old persisted snapshot
            snap.Area = 77f;
            var id = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("area=77.0", result);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static ChipData MakeChip(string id) => new ChipData("region", id, id, 0);

        static RegionSnapshot MakeRegionSnap(string id, int objCount = 2)
        {
            var paths = new string[objCount];
            for (int i = 0; i < objCount; i++) paths[i] = $"/Object_{i}";
            return new RegionSnapshot
            {
                Id            = id,
                SchemaVersion = 1,
                VerticesFlat  = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f },
                Area          = 100f,
                CenterX       = 5f, CenterZ = 5f,
                MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 10f,
                ObjectPaths   = paths,
                TotalCount    = objCount,
                CreatedTicks  = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }
    }
}
