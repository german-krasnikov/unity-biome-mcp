// TDD — RED first. EditMode tests for SearchHelper scoped search (root + limit). F13.
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SearchHelperScopedTests : SceneTestBase
    {
        private GameObject _root;
        private GameObject _child1;
        private GameObject _child2;
        private GameObject _sibling;

        [SetUp]
        public void SetUp()
        {
            // Hierarchy: Root → Child1, Child2 (with Rigidbody); Sibling (outside Root)
            _root = new GameObject("SearchScope_Root");
            _child1 = new GameObject("SearchScope_Child1");
            _child1.transform.SetParent(_root.transform);
            _child2 = new GameObject("SearchScope_Child2");
            _child2.AddComponent<Rigidbody>();
            _child2.transform.SetParent(_root.transform);
            _sibling = new GameObject("SearchScope_Sibling");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_sibling);
        }

        // Scenario 11: scoped search returns only children of root, not sibling
        [Test]
        public void Search_WithRoot_ReturnsOnlyScopedResults()
        {
            var result = SearchHelper.Search("SearchScope_", root: "/" + _root.name);
            // Children match; sibling must NOT appear
            StringAssert.Contains("SearchScope_Child", result);
            StringAssert.DoesNotContain("SearchScope_Sibling", result);
        }

        // Scenario 12: invalid root returns hint (not exception)
        [Test]
        public void Search_InvalidRoot_ReturnsHint()
        {
            var result = SearchHelper.Search("anything", root: "/NonExistentRoot_XYZ");
            StringAssert.Contains("no matches", result);
        }

        // Scenario 13: limit cap stops at exactly limit results
        [Test]
        public void Search_WithLimit_CapsResults()
        {
            // Create 5 more objects inside root to guarantee >1 match
            var extras = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                extras[i] = new GameObject($"SearchScope_Extra{i}");
                extras[i].transform.SetParent(_root.transform);
            }

            var result = SearchHelper.Search("SearchScope_", root: "/" + _root.name, limit: 2);
            // Exactly 2 result lines + 1 overflow line
            var lines = result.Split('\n');
            int resultLines = 0;
            foreach (var l in lines)
                if (l.Contains("SearchScope_") && !l.Contains("...+")) resultLines++;
            Assert.AreEqual(2, resultLines);

            foreach (var e in extras) Object.DestroyImmediate(e);
        }

        // Scenario 14: exact match count at limit — no overflow marker
        [Test]
        public void Search_ExactCount_NoOverflowMarker()
        {
            // Root has exactly 2 children: Child1, Child2
            var result = SearchHelper.Search("SearchScope_Child", root: "/" + _root.name, limit: 2);
            StringAssert.DoesNotContain("...+", result);
        }

        // Scenario 15: limit=0 returns all matches (unlimited)
        [Test]
        public void Search_LimitZero_ReturnsAllMatches()
        {
            var result = SearchHelper.Search("SearchScope_Child", root: "/" + _root.name, limit: 0);
            StringAssert.Contains("SearchScope_Child1", result);
            StringAssert.Contains("SearchScope_Child2", result);
            StringAssert.DoesNotContain("...+", result);
        }

        // Scenario 16: overflow marker exact format "...+{N} more (limit={L})"
        [Test]
        public void Search_OverflowMarker_ExactFormat()
        {
            var extras = new GameObject[7];
            for (int i = 0; i < 7; i++)
            {
                extras[i] = new GameObject($"SearchScope_OFChild{i}");
                extras[i].transform.SetParent(_root.transform);
            }

            // We have 2 original children + 7 extras = 9 matching; limit=2
            var result = SearchHelper.Search("SearchScope_", root: "/" + _root.name, limit: 2);
            // Overflow must be ≥ 7 (sibling is outside scope)
            StringAssert.Contains("(limit=2)", result);
            StringAssert.Contains("...+", result);

            foreach (var e in extras) Object.DestroyImmediate(e);
        }

        // Scenario 17: root + limit combined — scoped AND capped
        [Test]
        public void Search_RootAndLimit_Combined()
        {
            var result = SearchHelper.Search("SearchScope_Child", root: "/" + _root.name, limit: 1);
            var lines = result.Split('\n');
            int matchLines = 0;
            foreach (var l in lines)
                if (l.Contains("SearchScope_Child") && !l.Contains("...+")) matchLines++;
            Assert.AreEqual(1, matchLines);
        }

        // Scenario 18: root + filter (component) combined
        [Test]
        public void Search_RootAndFilter_ReturnsFilteredChildren()
        {
            var result = SearchHelper.Search("t:Rigidbody", root: "/" + _root.name);
            StringAssert.Contains("SearchScope_Child2", result);
            StringAssert.DoesNotContain("SearchScope_Child1", result);
        }

        // Scenario 19: empty scope (no matching children) returns hint
        [Test]
        public void Search_EmptyScope_ReturnsHint()
        {
            var emptyRoot = new GameObject("SearchScope_EmptyRoot");
            var result = SearchHelper.Search("t:Camera", root: "/" + emptyRoot.name);
            StringAssert.Contains("no matches", result);
            Object.DestroyImmediate(emptyRoot);
        }

        // Scenario 20: scoped empty hint names the root, not the scene
        [Test]
        public void Search_ScopedEmptyHint_MentionsRootNotScene()
        {
            // _root has children Child1, Child2 — but no Camera
            var result = SearchHelper.Search("t:Camera", root: "/" + _root.name);
            StringAssert.Contains(_root.name, result);
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(sceneName))
                StringAssert.DoesNotContain(sceneName, result);
        }

        // Scenario 21: overflow value is exact total-minus-limit (single-pass correctness).
        // Root(1) + Child1 + Child2 + Ov0..Ov4 = 8 total matches; limit=3 → overflow=5.
        [Test]
        public void Search_OverflowCount_IsExactTotalMinusLimit()
        {
            var extras = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                extras[i] = new GameObject($"SearchScope_Ov{i}");
                extras[i].transform.SetParent(_root.transform);
            }

            var result = SearchHelper.Search("SearchScope_", root: "/" + _root.name, limit: 3);
            // 8 total matches (root itself + 2 original children + 5 extras), limit=3, overflow=5
            StringAssert.Contains("...+5 more (limit=3)", result);

            foreach (var e in extras) Object.DestroyImmediate(e);
        }
    }
}
