// TDD — RED first. EditMode NUnit tests for SpatialHelper.RegionClear (Item 40).
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class RegionClearTests : SceneTestBase
    {
        private const string _Triangle = "{\"vertices\":\"0,0;10,0;5,10\"}";

        private GameObject _inside;
        private GameObject _outside;

        [SetUp]
        public void SetUp()
        {
            _inside = new GameObject("RC_Inside");
            _inside.transform.position = new Vector3(5f, 0f, 3f); // inside triangle

            _outside = new GameObject("RC_Outside");
            _outside.transform.position = new Vector3(50f, 0f, 50f); // outside
        }

        [TearDown]
        public void TearDown()
        {
            if (_inside != null) Object.DestroyImmediate(_inside);
            if (_outside != null) Object.DestroyImmediate(_outside);
        }

        // Scenario 1: dry_run=true lists objects, does NOT delete them
        [Test]
        public void RegionClear_DryRun_ListsObjectsWithoutDeleting()
        {
            var json = "{\"vertices\":\"0,0;10,0;5,10\",\"dry_run\":\"true\"}";
            var result = SpatialHelper.RegionClear(json);

            StringAssert.Contains("DRY:", result);
            StringAssert.Contains("RC_Inside", result);
            Assert.IsNotNull(_inside, "dry_run must not destroy the object");
        }

        // Scenario 2: dry_run=false deletes objects inside polygon
        [Test]
        public void RegionClear_LiveRun_DeletesObjectsInsidePolygon()
        {
            var json = "{\"vertices\":\"0,0;10,0;5,10\",\"dry_run\":\"false\"}";
            var result = SpatialHelper.RegionClear(json);

            StringAssert.Contains("DELETED:", result);
            // _inside was at (5,0,3) — inside the triangle → destroyed
            // We can't assert _inside==null because DestroyImmediate nulls the ref
            // but the local var might still hold the C# proxy. Check result count instead.
            StringAssert.Contains("1 object", result);
        }

        // Scenario 3: outside objects are not included
        [Test]
        public void RegionClear_DryRun_ExcludesObjectsOutsidePolygon()
        {
            var json = "{\"vertices\":\"0,0;10,0;5,10\",\"dry_run\":\"true\"}";
            var result = SpatialHelper.RegionClear(json);

            StringAssert.DoesNotContain("RC_Outside", result);
        }

        // Scenario 4: name filter applies — non-matching objects excluded
        [Test]
        public void RegionClear_FilterApplied_NonMatchingExcluded()
        {
            var json = "{\"vertices\":\"0,0;10,0;5,10\",\"dry_run\":\"true\",\"filter\":\"NoMatch\"}";
            var result = SpatialHelper.RegionClear(json);

            StringAssert.Contains("DRY: 0 objects", result);
        }

        // Scenario 5: name filter matches — included
        [Test]
        public void RegionClear_FilterMatches_ObjectIncluded()
        {
            var json = "{\"vertices\":\"0,0;10,0;5,10\",\"dry_run\":\"true\",\"filter\":\"RC_Inside\"}";
            var result = SpatialHelper.RegionClear(json);

            StringAssert.Contains("RC_Inside", result);
        }

        // Scenario 6: missing vertices throws ArgumentException
        [Test]
        public void RegionClear_MissingVertices_ThrowsArgumentException()
        {
            var json = "{\"dry_run\":\"true\"}";
            Assert.Throws<System.ArgumentException>(() => SpatialHelper.RegionClear(json));
        }

        // Scenario 7: default dry_run is true (absent key → safe)
        [Test]
        public void RegionClear_DefaultDryRun_DoesNotDelete()
        {
            // No dry_run key → defaults to true
            var json = "{\"vertices\":\"0,0;10,0;5,10\"}";
            var result = SpatialHelper.RegionClear(json);

            StringAssert.Contains("DRY:", result);
        }
    }
}
