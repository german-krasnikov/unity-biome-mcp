using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneRegionStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        string _tmpFile;

        [SetUp]
        public void SetUp()
        {
            _tmpFile = Path.Combine(Path.GetTempPath(),
                $"unity-mcp-regions-{Guid.NewGuid():N}.json");
            RegisterCleanup(SceneRegionState.IsolateForTests(_tmpFile, 5).Dispose);
        }

        static RegionSnapshot Snap(string id, int objCount = 2, long created = 0) =>
            new RegionSnapshot
            {
                Id            = id,
                SchemaVersion = 2,
                VerticesFlat  = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f },
                Area          = 100f,
                CenterX       = 5f, CenterZ = 5f,
                MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 10f,
                ObjectPaths   = new string[objCount],
                ObjectIds     = new string[objCount],
                TotalCount    = objCount,
                CreatedTicks  = created == 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : created,
            };

        // ── CRUD ──────────────────────────────────────────────────────────────

        [Test] public void SetRegion_Get_RoundTrip()
        {
            var id = SceneRegionState.SetRegion(Snap("abc"));
            var got = SceneRegionState.GetById(id);
            Assert.IsNotNull(got);
            Assert.AreEqual("abc", got.Id);
        }

        [Test] public void GetById_UnknownId_ReturnsNull()
            => Assert.IsNull(SceneRegionState.GetById("does_not_exist"));

        [Test] public void GetById_Null_ReturnsNull()
            => Assert.IsNull(SceneRegionState.GetById(null));

        [Test] public void Remove_ExistingId_ReturnsTrue_AndGone()
        {
            SceneRegionState.SetRegion(Snap("r1"));
            Assert.IsTrue(SceneRegionState.Remove("r1"));
            Assert.IsNull(SceneRegionState.GetById("r1"));
        }

        [Test] public void Remove_UnknownId_ReturnsFalse()
            => Assert.IsFalse(SceneRegionState.Remove("not_here"));

        [Test] public void SetRegion_Replace_UpdatesValue()
        {
            SceneRegionState.SetRegion(Snap("r1", objCount: 2));
            SceneRegionState.SetRegion(Snap("r1", objCount: 7));
            Assert.AreEqual(7, SceneRegionState.GetById("r1").TotalCount);
        }

        // ── LRU eviction ──────────────────────────────────────────────────────

        [Test] public void MaxRegions_Evicts_OldestOnExceed()
        {
            for (int i = 0; i < 6; i++)
                SceneRegionState.SetRegion(Snap($"r{i}"));
            Assert.AreEqual(5, SceneRegionState.All.Count());
        }

        // ── File persistence ──────────────────────────────────────────────────

        [Test] public void Save_Load_PreservesSnapshots()
        {
            SceneRegionState.SetRegion(Snap("persist1"));
            SceneRegionState.SetRegion(Snap("persist2"));
            SceneRegionState.Load();
            Assert.IsNotNull(SceneRegionState.GetById("persist1"));
            Assert.IsNotNull(SceneRegionState.GetById("persist2"));
        }

        [Test] public void Load_MissingFile_NoThrow()
        {
            File.Delete(_tmpFile);
            Assert.DoesNotThrow(() => SceneRegionState.Load());
        }

        [Test] public void Load_CorruptFile_NoThrow_EmptyCache()
        {
            File.WriteAllText(_tmpFile, "NOT_VALID_JSON{{{");
            Assert.DoesNotThrow(() => SceneRegionState.Load());
            Assert.AreEqual(0, SceneRegionState.All.Count());
        }

        [Test] public void Load_PrunesExpired_24h()
        {
            var old = Snap("old_region");
            old.CreatedTicks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86401;
            SceneRegionState.SetRegion(old);
            SceneRegionState.Load();
            Assert.IsNull(SceneRegionState.GetById("old_region"),
                "24h+ old region should be pruned on Load");
        }

        [Test] public void Load_KeepsFresh_JustUnder24h()
        {
            var fresh = Snap("fresh_region");
            fresh.CreatedTicks = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86399;
            SceneRegionState.SetRegion(fresh);
            SceneRegionState.Load();
            Assert.IsNotNull(SceneRegionState.GetById("fresh_region"));
        }

        // ── Staleness ─────────────────────────────────────────────────────────

        [Test] public void IsStale_FreshRegion_ReturnsFalse()
        {
            var id = SceneRegionState.SetRegion(Snap("r1"));
            Assert.IsFalse(SceneRegionState.IsStale(id));
        }

        [Test] public void IsStale_UnknownId_ReturnsFalse()
            => Assert.IsFalse(SceneRegionState.IsStale("nope"));

        // ── FrameRegion no-throw ──────────────────────────────────────────────

        [Test] public void FrameRegion_NullId_NoThrow()
            => Assert.DoesNotThrow(() => SceneRegionState.FrameRegion(null));

        [Test] public void FrameRegion_UnknownId_NoThrow()
            => Assert.DoesNotThrow(() => SceneRegionState.FrameRegion("ghost"));

        // ── Clear ─────────────────────────────────────────────────────────────

        [Test] public void Clear_EmptiesCache()
        {
            SceneRegionState.SetRegion(Snap("r1"));
            SceneRegionState.Clear();
            Assert.AreEqual(0, SceneRegionState.All.Count());
        }

        [Test]
        public void IsolateForTests_RestoresPathFileCacheVersionAndSessionShadow()
        {
            var baseline = Snap("baseline");
            SceneRegionState.SetRegion(baseline);
            var baselineBytes = File.ReadAllBytes(_tmpFile);
            var baselineVersion = SceneRegionState.CurrentVersion;
            var innerPath = Path.Combine(Path.GetTempPath(),
                $"unity-mcp-regions-inner-{Guid.NewGuid():N}.json");

            using (SceneRegionState.IsolateForTests(innerPath, 1))
            {
                Assert.AreEqual(innerPath, SceneRegionState.PersistPath);
                Assert.AreEqual(1, SceneRegionState.MaxRegions);
                Assert.AreEqual(0, SceneRegionState.All.Count());
                SceneRegionState.SetRegion(Snap("inner"));
            }

            Assert.AreEqual(_tmpFile, SceneRegionState.PersistPath);
            Assert.AreEqual(5, SceneRegionState.MaxRegions);
            Assert.AreEqual(baselineVersion, SceneRegionState.CurrentVersion);
            Assert.AreSame(baseline, SceneRegionState.GetById("baseline"));
            Assert.IsFalse(File.Exists(innerPath));
            CollectionAssert.AreEqual(baselineBytes, File.ReadAllBytes(_tmpFile));

            SceneRegionState.SimulateDomainReload();
            Assert.IsNotNull(SceneRegionState.GetById("baseline"),
                "The original file and SessionState shadow must both be restored.");
            Assert.IsNull(SceneRegionState.GetById("inner"));
        }

        // ── RegionSnapshot invariants ─────────────────────────────────────────

        [Test] public void RegionSnapshot_ShortLabel_IncludesCountAndArea()
        {
            var snap = Snap("r1", objCount: 5);
            snap.Area         = 142f;
            snap.ObjectPaths  = new string[5];
            Assert.IsTrue(snap.ShortLabel.Contains("5obj"));
            Assert.IsTrue(snap.ShortLabel.Contains("142"));
        }

        [Test] public void RegionSnapshot_ToPolygon2D_ReturnsValidPolygon()
        {
            var snap = Snap("r1");
            var poly = snap.ToPolygon2D();
            Assert.AreEqual(4, poly.Vertices.Length);
        }

        [Test] public void RegionSnapshot_ToPolygon2D_EmptyFlat_ReturnsDefault()
        {
            var snap = new RegionSnapshot { VerticesFlat = Array.Empty<float>() };
            var poly = snap.ToPolygon2D();
            Assert.IsNull(poly.Vertices); // default(Polygon2D)
        }
    }
}
