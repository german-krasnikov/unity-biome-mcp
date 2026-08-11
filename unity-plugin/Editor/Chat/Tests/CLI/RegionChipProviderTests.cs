using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RegionChipProviderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        string _tmpFile;
        RegionChipProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _tmpFile = Path.Combine(Path.GetTempPath(),
                $"unity-mcp-region-provider-{Guid.NewGuid():N}.json");
            RegisterCleanup(SceneRegionState.IsolateForTests(_tmpFile, 20).Dispose);
            RegisterCleanup(ChipKindRegistry.PreserveStateForTests().Dispose);
            ChipKindRegistry.ResetForTests();
            // RegionChipProvider self-registers via [InitializeOnLoad] on domain load.
            // ResetToBuiltIns() clears it, so manually re-register for test isolation.
            ChipKindRegistry.Register(new RegionChipProvider());
            _provider = ChipKindRegistry.ForKey("region") as RegionChipProvider;
            Assert.IsNotNull(_provider, "RegionChipProvider must be registered via [InitializeOnLoad]");
        }

        // ── Registration ──────────────────────────────────────────────────────

        [Test] public void Key_Is_region()
            => Assert.AreEqual("region", _provider.Key);

        [Test] public void Priority_Is_120()
            => Assert.AreEqual(120, _provider.Priority);

        [Test] public void CanHandle_AlwaysFalse()
            => Assert.IsFalse(_provider.CanHandle(null, "anything"));

        [Test] public void BarePathExtensions_IsEmpty()
            => Assert.AreEqual(0, _provider.BarePathExtensions.Length);

        [Test] public void DefaultDepth_IsSummary()
            => Assert.AreEqual("summary", _provider.DefaultDepth);

        // ── Depth = none ──────────────────────────────────────────────────────

        [Test] public void FormatPayload_DepthNone_ReturnsEmpty()
        {
            var chip   = MakeChip("r1");
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("none", ""));
            Assert.AreEqual("", result);
        }

        // ── Depth = path ──────────────────────────────────────────────────────

        [Test] public void FormatPayload_DepthPath_ReturnsBracketOnly()
        {
            var id     = SceneRegionState.SetRegion(MakeSnap("r1"));
            var chip   = MakeChip(id);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.AreEqual("[region:" + id + "]", result);
        }

        [Test] public void FormatPayload_DepthPath_ExpiredSnap_ReturnsBracketOnly()
        {
            var chip   = MakeChip("does_not_exist");
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.AreEqual("[region:does_not_exist]", result,
                "path depth always returns bracket only, even for expired");
        }

        // ── Depth = summary ───────────────────────────────────────────────────

        [Test] public void FormatPayload_DepthSummary_ContainsArea()
        {
            var snap   = MakeSnap("r1"); snap.Area = 142.5f;
            var id     = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("142.5", result);
        }

        [Test] public void FormatPayload_DepthSummary_ContainsCenter()
        {
            var snap   = MakeSnap("r1"); snap.CenterX = 12.0f; snap.CenterZ = 8.5f;
            var id     = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("(12.0,8.5)", result);
        }

        [Test] public void FormatPayload_DepthSummary_8Objects_ListsAll()
        {
            var snap   = MakeSnap("r1", objCount: 8);
            var id     = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            for (int i = 0; i < 8; i++)
                StringAssert.Contains($"/Object_{i}", result);
            StringAssert.DoesNotContain("...+", result);
        }

        [Test] public void FormatPayload_DepthSummary_12Objects_Truncates()
        {
            var snap   = MakeSnap("r1", objCount: 12);
            var id     = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("...+2 more", result);
        }

        [Test] public void FormatPayload_DepthSummary_ExpiredSnap_ReturnsExpiredText()
        {
            var chip   = MakeChip("ghost_id");
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            StringAssert.Contains("(expired:", result);
        }

        [Test] public void FormatPayload_DepthSummary_StaleFlag_Present()
        {
            var id   = SceneRegionState.SetRegion(MakeSnap("r1"));
            // Directly mutate the cached object (class reference) to force stale.
            // Don't call SetRegion again — it would overwrite SnapshotVersion.
            var snap = SceneRegionState.GetById(id);
            snap.SnapshotVersion = -999; // impossible value → always stale
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("summary", ""));
            StringAssert.Contains("STALE", result);
        }

        // ── Depth = full ──────────────────────────────────────────────────────

        [Test] public void FormatPayload_DepthFull_ContainsPolygonCSV()
        {
            var snap   = MakeSnap("r1", objCount: 2);
            var id     = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            StringAssert.Contains("polygon=", result);
            StringAssert.Contains("0.00,0.00", result);
        }

        [Test] public void FormatPayload_DepthFull_AllObjectsListed_NoTruncation()
        {
            var snap   = MakeSnap("r1", objCount: 15);
            var id     = SceneRegionState.SetRegion(snap);
            var result = _provider.FormatPayload(MakeChip(id), new ChipPayloadContext("full", ""));
            for (int i = 0; i < 15; i++)
                StringAssert.Contains($"/Object_{i}", result);
            StringAssert.DoesNotContain("...+", result);
        }

        // ── Navigate / Ping no-throw ──────────────────────────────────────────

        [Test] public void Navigate_UnknownId_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate("no_such_region"));

        [Test] public void Navigate_NullId_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate(null));

        [Test] public void Ping_UnknownId_NoThrow()
            => Assert.DoesNotThrow(() => _provider.Ping("ghost"));

        // ── Helpers ───────────────────────────────────────────────────────────

        static ChipData MakeChip(string id) => new ChipData("region", id, id, 0);

        static RegionSnapshot MakeSnap(string id, int objCount = 3)
        {
            var paths = new string[objCount];
            var ids   = new string[objCount];
            for (int i = 0; i < objCount; i++) paths[i] = $"/Object_{i}";
            return new RegionSnapshot
            {
                Id            = id,
                SchemaVersion = 2,
                VerticesFlat  = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f },
                Area          = 100f,
                CenterX       = 5f, CenterZ = 5f,
                MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 10f,
                ObjectPaths   = paths,
                ObjectIds     = ids,
                TotalCount    = objCount,
                Truncated     = false,
                CreatedTicks  = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }
    }
}
