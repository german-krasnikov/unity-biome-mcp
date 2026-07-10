// TDD: SearchHelper ParseQuery filter tokens — tag=X, layer=N, active=true/false.
// EditMode tests; require Unity scene context (same pattern as SearchHelperScopedTests).
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SearchHelperFilterTests : SceneTestBase
    {
        private GameObject _tagged;
        private GameObject _layered;
        private GameObject _inactive;
        private GameObject _plain;

        [SetUp]
        public void SetUp()
        {
            _tagged = new GameObject("Filter_Tagged");
            _tagged.tag = "Respawn"; // built-in Unity tag, always available

            _layered = new GameObject("Filter_Layered");
            _layered.layer = 3; // "Ignore Raycast" — built-in layer 3

            _inactive = new GameObject("Filter_Inactive");
            _inactive.SetActive(false);

            _plain = new GameObject("Filter_Plain");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tagged);
            Object.DestroyImmediate(_layered);
            Object.DestroyImmediate(_inactive);
            Object.DestroyImmediate(_plain);
        }

        // ── tag= filter ──────────────────────────────────────────────────────────

        [Test]
        public void ParseQuery_TagFilter_MatchesObjectWithTag()
        {
            var result = SearchHelper.Search("Filter_ tag=Respawn");
            StringAssert.Contains("Filter_Tagged", result);
        }

        [Test]
        public void ParseQuery_TagFilter_ExcludesObjectWithDifferentTag()
        {
            var result = SearchHelper.Search("Filter_ tag=Respawn");
            StringAssert.DoesNotContain("Filter_Plain", result);
        }

        // ── layer= filter ────────────────────────────────────────────────────────

        [Test]
        public void ParseQuery_LayerFilter_MatchesObjectOnLayer()
        {
            var result = SearchHelper.Search("Filter_ layer=3");
            StringAssert.Contains("Filter_Layered", result);
        }

        [Test]
        public void ParseQuery_LayerFilter_ExcludesObjectOnOtherLayer()
        {
            var result = SearchHelper.Search("Filter_ layer=3");
            StringAssert.DoesNotContain("Filter_Plain", result);
        }

        [Test]
        public void ParseQuery_LayerFilter_NonNumeric_IsIgnored()
        {
            // non-numeric layer token falls through to name filter
            // should not throw, should just find by name
            Assert.DoesNotThrow(() => SearchHelper.Search("Filter_ layer=notanumber"));
        }

        // ── active= filter ───────────────────────────────────────────────────────

        [Test]
        public void ParseQuery_ActiveTrue_ReturnsOnlyActiveObjects()
        {
            var result = SearchHelper.Search("Filter_ active=true");
            StringAssert.DoesNotContain("Filter_Inactive", result);
        }

        [Test]
        public void ParseQuery_ActiveTrue_IncludesActiveObjects()
        {
            var result = SearchHelper.Search("Filter_ active=true");
            StringAssert.Contains("Filter_Plain", result);
        }

        [Test]
        public void ParseQuery_ActiveFalse_ReturnsOnlyInactiveObjects()
        {
            var result = SearchHelper.Search("Filter_ active=false");
            StringAssert.Contains("Filter_Inactive", result);
        }

        [Test]
        public void ParseQuery_ActiveFalse_ExcludesActiveObjects()
        {
            var result = SearchHelper.Search("Filter_ active=false");
            StringAssert.DoesNotContain("Filter_Plain", result);
        }

        // ── combined filters ─────────────────────────────────────────────────────

        [Test]
        public void ParseQuery_TagAndActive_CombinedFilter()
        {
            // tagged object is active — active=true + tag=Respawn should match it
            var result = SearchHelper.Search("Filter_ tag=Respawn active=true");
            StringAssert.Contains("Filter_Tagged", result);
        }

        [Test]
        public void ParseQuery_LayerAndActive_CombinedFilter_NoMatch()
        {
            // No object is both on layer=3 AND inactive
            var result = SearchHelper.Search("Filter_ layer=3 active=false");
            StringAssert.Contains("no matches", result);
        }
    }
}
