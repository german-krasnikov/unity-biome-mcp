using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Chat.Tests
{
    /// <summary>
    /// Verifies domain-reload snap recovery via SessionState + FormatPayload quality.
    /// Bug: SetRegion snap lost when file write fails silently, domain reload clears _cache.
    /// Fix: SessionStateHelper shadows every snap in SessionState; Load() recovers from it.
    /// </summary>
    [TestFixture]
    public class RegionSnapReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        string _tmpFile;
        RegionChipProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _tmpFile = Path.Combine(Path.GetTempPath(),
                $"unity-mcp-region-reload-{Guid.NewGuid():N}.json");
            RegisterCleanup(SceneRegionState.IsolateForTests(_tmpFile, 20).Dispose);
            RegisterCleanup(ChipKindRegistry.PreserveStateForTests().Dispose);
            ChipKindRegistry.ResetToBuiltIns();
            ChipKindRegistry.Register(new RegionChipProvider());
            _provider = ChipKindRegistry.ForKey("region") as RegionChipProvider;
        }

        // ── Group A — Domain reload survival ─────────────────────────────────

        [Test]
        public void SetRegion_ThenReload_SnapRecoveredFromFile()
        {
            SceneRegionState.SetRegion(MakeSnap("r1"));
            SceneRegionState.SimulateDomainReload();
            Assert.IsNotNull(SceneRegionState.GetById("r1"),
                "Happy path: snap must survive domain reload via file");
        }

        [Test]
        public void SetRegion_FileLost_SnapRecoveredViaSessionState()
        {
            var snap = MakeSnap("r1"); snap.Area = 99f;
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, ""); // corrupt file → simulate failed write
            SceneRegionState.SimulateDomainReload();
            var recovered = SceneRegionState.GetById("r1");
            Assert.IsNotNull(recovered, "SessionState must recover snap when file is lost");
            Assert.AreEqual(99f, recovered.Area);
        }

        [Test]
        public void TwoSnaps_BothLost_BothRecoveredViaSessionState()
        {
            SceneRegionState.SetRegion(MakeSnap("r1"));
            SceneRegionState.SetRegion(MakeSnap("r2"));
            File.WriteAllText(_tmpFile, "");
            SceneRegionState.SimulateDomainReload();
            Assert.IsNotNull(SceneRegionState.GetById("r1"), "r1 must recover via SessionState");
            Assert.IsNotNull(SceneRegionState.GetById("r2"), "r2 must recover via SessionState");
        }

        [Test]
        public void SetRegion_ThenReload_AnnotationSnap_Point_Recovered()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(3f, 7f), Array.Empty<string>(), "TestScene");
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, "");
            SceneRegionState.SimulateDomainReload();
            var r = SceneRegionState.GetById("p1");
            Assert.IsNotNull(r, "Point snap must recover via SessionState");
            Assert.AreEqual("point", r.AnnotationType);
            Assert.AreEqual(3f, r.CenterX, 0.001f);
        }

        [Test]
        public void SetRegion_ThenReload_AnnotationSnap_Polyline_Recovered()
        {
            var pts  = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "TestScene");
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, "");
            SceneRegionState.SimulateDomainReload();
            var r = SceneRegionState.GetById("poly1");
            Assert.IsNotNull(r, "Polyline snap must recover via SessionState");
            Assert.AreEqual("polyline", r.AnnotationType);
            Assert.AreEqual(10f, r.LengthOrDistance, 0.01f);
        }

        [Test]
        public void SetRegion_ThenReload_AnnotationSnap_Measurement_Recovered()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", Vector2.zero, new Vector2(5f, 0f), "TestScene");
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, "");
            SceneRegionState.SimulateDomainReload();
            var r = SceneRegionState.GetById("m1");
            Assert.IsNotNull(r, "Measurement snap must recover via SessionState");
            Assert.AreEqual("measurement", r.AnnotationType);
            Assert.AreEqual(5f, r.LengthOrDistance, 0.01f);
        }

        [Test]
        public void ExpiredSnap_NotRecoveredFromSessionState()
        {
            var snap = MakeSnap("old");
            snap.CreatedTicks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86401; // > 24h
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, ""); // ensure file won't recover it either
            SceneRegionState.SimulateDomainReload();
            Assert.IsNull(SceneRegionState.GetById("old"),
                "Snap older than 24h must NOT be recovered from SessionState");
        }

        // ── Group B — FormatPayload content ───────────────────────────────────

        [Test]
        public void FormatPayload_RegionSnap_ContainsArea_Bounds_Objects()
        {
            var snap = MakeSnap("r1", area: 142.5f, objCount: 3);
            SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip("r1"), Ctx("summary"));
            StringAssert.Contains("area=142.5", result);
            StringAssert.Contains("bounds=", result);
            StringAssert.Contains("objects=3", result);
            StringAssert.Contains("/Object_0", result);
        }

        [Test]
        public void FormatPayload_PointSnap_ContainsPos_Scene()
        {
            var snap = RegionSnapshot.CreatePoint("p1", new Vector2(2.5f, 4.1f), Array.Empty<string>(), "Level1");
            SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip("p1"), Ctx("summary"));
            StringAssert.Contains("pos=", result);
            StringAssert.Contains("scene=Level1", result);
        }

        [Test]
        public void FormatPayload_PolylineSnap_ContainsLen_Pts_Dir()
        {
            var pts  = new[] { new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(5f, 5f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip("poly1"), Ctx("summary"));
            StringAssert.Contains("len=", result);
            StringAssert.Contains("pts=3", result);
            StringAssert.Contains("dir=", result);
        }

        [Test]
        public void FormatPayload_MeasurementSnap_ContainsDist_FromTo()
        {
            var snap = RegionSnapshot.CreateMeasurement("m1", new Vector2(0f, 0f), new Vector2(12.5f, 0f), "Level1");
            SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip("m1"), Ctx("summary"));
            StringAssert.Contains("dist=12.5", result);
            StringAssert.Contains("from=", result);
            StringAssert.Contains("to=", result);
        }

        [Test]
        public void FormatPayload_AfterReload_RegionSnap_StillContainsArea()
        {
            var snap = MakeSnap("r1", area: 77f, objCount: 2);
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, "");
            SceneRegionState.SimulateDomainReload();
            var result = _provider.FormatPayload(MakeChip("r1"), Ctx("summary"));
            StringAssert.Contains("area=77.0", result,
                "After reload via SessionState, FormatPayload must still have area");
        }

        [Test]
        public void FormatPayload_AfterReload_PolylineSnap_StillContainsLen()
        {
            var pts  = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "Level1");
            SceneRegionState.SetRegion(snap);
            File.WriteAllText(_tmpFile, "");
            SceneRegionState.SimulateDomainReload();
            var result = _provider.FormatPayload(MakeChip("poly1"), Ctx("summary"));
            StringAssert.Contains("len=10.0", result,
                "After reload via SessionState, FormatPayload must still have len");
        }

        // ── Group C — Null/expired snap fallback ─────────────────────────────

        [Test]
        public void FormatPayload_NullSnap_FallbackContainsDisplayName()
        {
            var chip   = new ChipData("region", "ghost_id", "5obj 100m²", 0);
            var result = _provider.FormatPayload(chip, Ctx("summary"));
            StringAssert.Contains("5obj 100m²", result, "DisplayName must appear in expired fallback");
            StringAssert.Contains("[region:ghost_id]", result);
        }

        [Test]
        public void FormatPayload_NullSnap_EmptyDisplayName_ReturnsExpired()
        {
            var chip   = new ChipData("region", "ghost_id", "", 0);
            var result = _provider.FormatPayload(chip, Ctx("summary"));
            StringAssert.Contains("(expired)", result);
            StringAssert.DoesNotContain("(expired: )", result);
        }

        // ── Group D — Type labels ─────────────────────────────────────────────

        [Test]
        public void RegionLabel_ContainsSceneAreaSelection()
        {
            SceneRegionState.SetRegion(MakeSnap("r1"));
            var result = _provider.FormatPayload(MakeChip("r1"), Ctx("summary"));
            StringAssert.Contains("(scene area selection)", result);
        }

        [Test]
        public void PointLabel_ContainsScenePointMarker()
        {
            var snap = RegionSnapshot.CreatePoint("p1", Vector2.zero, Array.Empty<string>(), "S");
            SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip("p1"), Ctx("summary"));
            StringAssert.Contains("(scene point marker)", result);
        }

        [Test]
        public void PolylineLabel_ContainsScenePath()
        {
            var pts  = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f) };
            var snap = RegionSnapshot.CreatePolyline("poly1", pts, Array.Empty<string>(), "S");
            SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip("poly1"), Ctx("summary"));
            StringAssert.Contains("(scene path)", result);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static ChipData MakeChip(string id) => new ChipData("region", id, id, 0);
        static ChipPayloadContext Ctx(string depth) => new ChipPayloadContext(depth, "");

        static RegionSnapshot MakeSnap(string id, float area = 100f, int objCount = 3)
        {
            var paths = new string[objCount];
            for (int i = 0; i < objCount; i++) paths[i] = $"/Object_{i}";
            return new RegionSnapshot
            {
                Id           = id,
                SchemaVersion = 2,
                AnnotationType = "region",
                VerticesFlat = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f },
                Area         = area,
                CenterX      = 5f, CenterZ = 5f,
                MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 10f,
                ObjectPaths  = paths,
                ObjectIds    = new string[objCount],
                TotalCount   = objCount,
                CreatedTicks = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }
    }
}
