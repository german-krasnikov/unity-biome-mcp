using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class PolygonDetailTests
    {
        const string PrefKey = "MCP_PolygonDetailLevel";
        int _savedPref;

        [SetUp]
        public void SetUp() => _savedPref = EditorPrefs.GetInt(PrefKey, (int)PolygonDetailLevel.Normal);

        [TearDown]
        public void TearDown() => EditorPrefs.SetInt(PrefKey, _savedPref);

        // ── Epsilon range ────────────────────────────────────────────────────────

        [Test]
        public void Epsilon_Minimal_IsLargest()
            => Assert.Greater(PolygonDetailConfig.Epsilon(PolygonDetailLevel.Minimal),
                              PolygonDetailConfig.Epsilon(PolygonDetailLevel.Normal));

        [Test]
        public void Epsilon_Full_IsZero()
            => Assert.AreEqual(0f, PolygonDetailConfig.Epsilon(PolygonDetailLevel.Full));

        [Test]
        public void Epsilon_AllLevels_NonNegative()
        {
            foreach (PolygonDetailLevel level in System.Enum.GetValues(typeof(PolygonDetailLevel)))
                Assert.GreaterOrEqual(PolygonDetailConfig.Epsilon(level), 0f);
        }

        [Test]
        public void Epsilon_DescendingOrder()
        {
            Assert.Greater(PolygonDetailConfig.Epsilon(PolygonDetailLevel.Minimal),
                           PolygonDetailConfig.Epsilon(PolygonDetailLevel.Normal));
            Assert.Greater(PolygonDetailConfig.Epsilon(PolygonDetailLevel.Normal),
                           PolygonDetailConfig.Epsilon(PolygonDetailLevel.Detailed));
            Assert.Greater(PolygonDetailConfig.Epsilon(PolygonDetailLevel.Detailed),
                           PolygonDetailConfig.Epsilon(PolygonDetailLevel.Full));
        }

        // ── Circle vertices ──────────────────────────────────────────────────────

        [Test]
        public void CircleVertices_Minimal_Is8()
            => Assert.AreEqual(8, PolygonDetailConfig.CircleVertices(PolygonDetailLevel.Minimal));

        [Test]
        public void CircleVertices_Normal_Is16()
            => Assert.AreEqual(16, PolygonDetailConfig.CircleVertices(PolygonDetailLevel.Normal));

        [Test]
        public void CircleVertices_Detailed_Is32()
            => Assert.AreEqual(32, PolygonDetailConfig.CircleVertices(PolygonDetailLevel.Detailed));

        [Test]
        public void CircleVertices_Full_Is64()
            => Assert.AreEqual(64, PolygonDetailConfig.CircleVertices(PolygonDetailLevel.Full));

        // ── Constants ────────────────────────────────────────────────────────────

        [Test]
        public void TokensPerVertex_Is4()
            => Assert.AreEqual(4, PolygonDetailConfig.TokensPerVertex);

        [Test]
        public void WarnVertexCount_Is128()
            => Assert.AreEqual(128, PolygonDetailConfig.WarnVertexCount);

        // ── Settings persistence ─────────────────────────────────────────────────

        [Test]
        public void Default_RoundTrip_EditorPrefs()
        {
            PolygonDetailSettings.Default = PolygonDetailLevel.Detailed;
            Assert.AreEqual(PolygonDetailLevel.Detailed, PolygonDetailSettings.Default);
        }

        [Test]
        public void Default_NoPref_ReturnsNormal()
        {
            EditorPrefs.DeleteKey(PrefKey);
            Assert.AreEqual(PolygonDetailLevel.Normal, PolygonDetailSettings.Default);
        }

        // ── ForRegion override ────────────────────────────────────────────────────

        [Test]
        public void ForRegion_NegativeDetailLevel_UsesGlobalDefault()
        {
            PolygonDetailSettings.Default = PolygonDetailLevel.Minimal;
            var snap = new RegionSnapshot { DetailLevel = -1 };
            Assert.AreEqual(PolygonDetailLevel.Minimal, PolygonDetailSettings.ForRegion(snap));
        }

        [Test]
        public void ForRegion_ExplicitOverride_IgnoresDefault()
        {
            PolygonDetailSettings.Default = PolygonDetailLevel.Minimal;
            var snap = new RegionSnapshot { DetailLevel = (int)PolygonDetailLevel.Full };
            Assert.AreEqual(PolygonDetailLevel.Full, PolygonDetailSettings.ForRegion(snap));
        }

        [Test]
        public void ForRegion_NullSnap_UsesDefault()
        {
            PolygonDetailSettings.Default = PolygonDetailLevel.Detailed;
            Assert.AreEqual(PolygonDetailLevel.Detailed, PolygonDetailSettings.ForRegion(null));
        }

    }
}
