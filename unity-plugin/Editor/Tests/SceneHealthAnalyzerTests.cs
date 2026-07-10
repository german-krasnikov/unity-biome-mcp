// TDD: SceneHealthAnalyzer — F4 Scene Health Audit.
// EditMode tests. Scene objects created/destroyed per test.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SceneHealthAnalyzerTests : SceneTestBase
    {
        readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        GameObject MakeGO(string name = "Obj")
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // ── CheckMissingScripts ────────────────────────────────────────────

        [Test]
        public void CheckMissingScripts_NoMissing_ReturnsNull()
        {
            var go = MakeGO("Player");
            Assert.IsNull(SceneHealthAnalyzer.CheckMissingScripts(new[] { go }));
        }

        // ── CheckDeepHierarchy ─────────────────────────────────────────────

        [Test]
        public void CheckDeepHierarchy_Depth6_ReturnsWarning()
        {
            // root(0) → child0(1) → child1(2) → child2(3) → child3(4) → child4(5) → child5(6)
            var root = MakeGO("Root");
            var current = root;
            for (int i = 0; i < 6; i++)
            {
                var child = MakeGO($"Child{i}");
                child.transform.SetParent(current.transform);
                current = child;
            }
            var result = SceneHealthAnalyzer.CheckDeepHierarchy(new[] { current });
            Assert.IsNotNull(result);
            StringAssert.Contains("WARNING", result);
        }

        [Test]
        public void CheckDeepHierarchy_Depth5_ReturnsNull()
        {
            // root(0) → child0(1) → child1(2) → child2(3) → child3(4) → child4(5)
            var root = MakeGO("Root");
            var current = root;
            for (int i = 0; i < 5; i++)
            {
                var child = MakeGO($"Child{i}");
                child.transform.SetParent(current.transform);
                current = child;
            }
            Assert.IsNull(SceneHealthAnalyzer.CheckDeepHierarchy(new[] { current }));
        }

        // ── CheckBadNaming ─────────────────────────────────────────────────

        [Test]
        public void CheckBadNaming_DefaultName_ReturnsWarning()
        {
            var go = MakeGO("New GameObject");
            var result = SceneHealthAnalyzer.CheckBadNaming(new[] { go });
            Assert.IsNotNull(result);
            StringAssert.Contains("WARNING", result);
        }

        [Test]
        public void CheckBadNaming_CustomName_ReturnsNull()
        {
            var go = MakeGO("Player");
            Assert.IsNull(SceneHealthAnalyzer.CheckBadNaming(new[] { go }));
        }

        // ── CheckEmptyObjects ──────────────────────────────────────────────

        [Test]
        public void CheckEmptyObjects_FiveEmpty_ReturnsWarning()
        {
            var gos = new List<GameObject>();
            for (int i = 0; i < 5; i++) gos.Add(MakeGO($"Empty{i}"));
            var result = SceneHealthAnalyzer.CheckEmptyObjects(gos.ToArray());
            Assert.IsNotNull(result);
            StringAssert.Contains("WARNING", result);
        }

        [Test]
        public void CheckEmptyObjects_FourEmpty_ReturnsNull()
        {
            var gos = new List<GameObject>();
            for (int i = 0; i < 4; i++) gos.Add(MakeGO($"Empty{i}"));
            Assert.IsNull(SceneHealthAnalyzer.CheckEmptyObjects(gos.ToArray()));
        }

        // ── CheckDuplicateSiblings ─────────────────────────────────────────

        [Test]
        public void CheckDuplicateSiblings_TwoSameNames_ReturnsWarning()
        {
            var parent = MakeGO("Arena");
            var e1 = MakeGO("Enemy");
            var e2 = MakeGO("Enemy");
            e1.transform.SetParent(parent.transform);
            e2.transform.SetParent(parent.transform);
            var result = SceneHealthAnalyzer.CheckDuplicateSiblings(new[] { parent, e1, e2 });
            Assert.IsNotNull(result);
            StringAssert.Contains("WARNING", result);
        }

        // ── ParseFocus ─────────────────────────────────────────────────────

        [Test]
        public void ParseFocus_Hierarchy_OnlyHierarchyCheck()
        {
            var checks = SceneHealthAnalyzer.ParseFocus("hierarchy");
            Assert.IsTrue(checks.Contains("hierarchy"));
            Assert.IsFalse(checks.Contains("naming"));
            Assert.IsFalse(checks.Contains("missing"));
        }

        [Test]
        public void ParseFocus_Unknown_ReturnsAll()
        {
            var checks = SceneHealthAnalyzer.ParseFocus("xyz");
            Assert.IsTrue(checks.Contains("hierarchy"));
            Assert.IsTrue(checks.Contains("naming"));
            Assert.IsTrue(checks.Contains("missing"));
            Assert.IsTrue(checks.Contains("empty"));
            Assert.IsTrue(checks.Contains("disabled"));
        }

        // ── Analyze integration ────────────────────────────────────────────

        [Test]
        public void Analyze_FocusMissing_DoesNotRunNamingCheck()
        {
            // Create a badly-named object. If naming check ran, result would contain naming warning.
            // With focus=missing, only the missing check runs — no naming warning expected.
            MakeGO("New GameObject");
            var result = SceneHealthAnalyzer.Analyze("missing");
            Assert.IsNotNull(result);
            StringAssert.Contains("SCENE HEALTH", result);
            StringAssert.DoesNotContain("\"New GameObject\" naming", result);
        }
    }
}
