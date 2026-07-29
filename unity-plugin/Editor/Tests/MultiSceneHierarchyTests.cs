using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Consolidated: HierarchyMultiSceneTests + SceneFilterTests + MultiSceneStressTests
    /// </summary>
    [TestFixture]
    public class MultiSceneHierarchyTests : MultiSceneTestBase
    {
        public override void SetUp()
        {
            HierarchySerializer.ResetIncrementalCache();
            base.SetUp();
        }

        public override void TearDown()
        {
            base.TearDown();
            HierarchySerializer.ResetIncrementalCache();
        }

        // ── HierarchyMultiSceneTests ──────────────────────────────────────────

        [Test]
        public void SingleScene_NoSceneHeaders()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var result = HierarchySerializer.Serialize();
            Assert.That(result, Does.Not.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_EmitsHeaders()
        {
            // Need at least one root in each scene so phantom-header stripping keeps the headers.
            CreateIn(_additiveScene, "EmitHeaders_AdditiveObj");
            var go = new GameObject("EmitHeaders_MainObj");
            _toDestroy.Add(go);

            var result = HierarchySerializer.Serialize();
            Assert.That(result, Does.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_ObjectsUnderCorrectScene()
        {
            var go = new GameObject("AdditiveObj");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize();
            int headerIdx = result.IndexOf("[" + _additiveScene.name + "]");
            Assert.That(headerIdx, Is.GreaterThanOrEqualTo(0), "Header for additive scene not found");
            int objIdx = result.IndexOf("AdditiveObj");
            Assert.That(objIdx, Is.GreaterThan(headerIdx), "AdditiveObj should appear after its scene header");
        }

        [Test]
        public void MultiScene_RootParam_NoHeaders()
        {
            var go = new GameObject("RootParamGo");
            _toDestroy.Add(go);
            var result = HierarchySerializer.Serialize(root: "RootParamGo");
            Assert.That(result, Does.Not.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_Summary_EmitsHeaders()
        {
            var result = HierarchySerializer.SerializeSummary();
            Assert.That(result, Does.Contain("["));
        }

        [Test]
        public void Fingerprint_MultiScene_IncludesAllScenes()
        {
            string hash1 = FingerprintHelper.Fingerprint(null, 99);

            var go = new GameObject("FingerprintObj");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);
            string hash2 = FingerprintHelper.Fingerprint(null, 99);

            Assert.That(hash1, Is.Not.EqualTo(hash2), "Fingerprint should differ when additive scene has objects");
        }

        [Test]
        public void MultiScene_Filter_MatchesAcrossScenes()
        {
            var go = new GameObject("UniqueFilterTarget");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize(filter: "UniqueFilterTarget");

            Assert.That(result, Does.Contain("UniqueFilterTarget"));
            Assert.That(result, Does.Contain("[" + _additiveScene.name + "]"));
        }

        [Test]
        public void MultiScene_Filter_NoPhantomHeaderForEmptyScene()
        {
            var result = HierarchySerializer.Serialize(filter: "XYZ_NoSuchObject_Phantom");

            Assert.That(result, Does.Not.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_Components_HeaderBeforeComponentList()
        {
            var go = new GameObject("CompObj");
            go.AddComponent<Camera>();
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize(components: true);

            int headerIdx = result.IndexOf("[" + _additiveScene.name + "]");
            int compIdx = result.IndexOf("CompObj [");
            Assert.That(headerIdx, Is.GreaterThanOrEqualTo(0), "Scene header missing");
            Assert.That(compIdx, Is.GreaterThan(headerIdx), "Component list should appear after scene header");
        }

        [Test]
        public void MultiScene_TwoScenes_BothInSceneRoots()
        {
            var sceneRoots = HierarchySerializer.GetAllLoadedSceneRoots();
            Assert.That(sceneRoots.Count, Is.GreaterThanOrEqualTo(2),
                $"Expected 2+ scenes but got {sceneRoots.Count}: {string.Join(", ", sceneRoots.ConvertAll(s => s.name))}");
        }

        [Test]
        public void MultiScene_EmptyAdditiveScene_NoPhantomHeader_WithFilter()
        {
            var activeGo = new GameObject("PhantomTestOnlyActive");
            _toDestroy.Add(activeGo);

            var result = HierarchySerializer.Serialize(filter: "PhantomTestOnlyActive");

            Assert.That(result, Does.Contain("PhantomTestOnlyActive"));
            var headers = Regex.Matches(result, @"(?m)^\[");
            Assert.That(headers.Count, Is.EqualTo(1),
                $"Expected 1 scene header (only scene with matches), got {headers.Count}.\n{result}");
        }

        [Test]
        public void MultiScene_Incremental_CacheBustedOnSceneChange()
        {
            var result1 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            Assert.That(result1, Is.Not.EqualTo("NO_CHANGE"), "First call should never return NO_CHANGE");

            HierarchySerializer.ResetIncrementalCache();

            var result2 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            Assert.That(result2, Is.Not.EqualTo("NO_CHANGE"), "After cache reset, should return fresh hierarchy");
        }

        [Test]
        public void MultiScene_MAX_NODES_CrossScene_Truncates()
        {
            var go = new GameObject("TruncParent");
            _toDestroy.Add(go);
            var child = new GameObject("TruncChild");
            child.transform.SetParent(go.transform);
            _toDestroy.Add(child);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize(depth: 0);
            Assert.That(result, Does.Contain("TruncParent"));
            Assert.That(result, Does.Contain("+1"));
        }

        [Test]
        public void MultiScene_Summary_WithRoot_NoHeaders()
        {
            var go = new GameObject("SummaryRootObj");
            _toDestroy.Add(go);

            var result = HierarchySerializer.SerializeSummary(root: "SummaryRootObj");

            Assert.That(result, Does.Not.Match(@"(?m)^\["), "SerializeSummary(root:X) in multi-scene must not emit scene headers");
            Assert.That(result, Does.Contain("SummaryRootObj"));
        }

        [Test]
        public void GetAllLoadedSceneRoots_ExcludesDontDestroyOnLoad()
        {
            var sceneRoots = HierarchySerializer.GetAllLoadedSceneRoots();
            Assert.That(sceneRoots, Is.Not.Empty, "Should return at least the active scene");
            foreach (var (name, _) in sceneRoots)
                Assert.That(name, Does.Not.StartWith("DontDestroyOnLoad"));
        }

        // ── SceneFilterTests ──────────────────────────────────────────────────

        [Test]
        public void Hierarchy_SceneFilter_OnlyTargetScene()
        {
            var addGo = new GameObject("AdditiveOnly_SceneFilter");
            _toDestroy.Add(addGo);
            SceneManager.MoveGameObjectToScene(addGo, _additiveScene);

            var activeGo = new GameObject("ActiveOnly_SceneFilter");
            _toDestroy.Add(activeGo);

            var result = HierarchySerializer.Serialize(scene: _additiveScene.name);
            Assert.That(result, Does.Contain("AdditiveOnly_SceneFilter"), "Should contain additive scene object");
            Assert.That(result, Does.Not.Contain("ActiveOnly_SceneFilter"), "Should NOT contain active scene object");
        }

        [Test]
        public void Hierarchy_SceneFilter_NoHeader()
        {
            var result = HierarchySerializer.Serialize(scene: _additiveScene.name);
            Assert.That(result, Does.Not.Match(@"(?m)^\[" + _additiveScene.name + @"\]"),
                "Single filtered scene must not emit scene header");
        }

        [Test]
        public void Hierarchy_NoFilter_AllScenes()
        {
            var go = new GameObject("NoFilterObj");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize();
            Assert.That(result, Does.Match(@"(?m)^\["), "Multi-scene without filter must still emit headers");
        }

        [Test]
        public void Search_SceneFilter_OnlyTargetScene()
        {
            var addGo = new GameObject("SearchAdditiveTarget");
            _toDestroy.Add(addGo);
            SceneManager.MoveGameObjectToScene(addGo, _additiveScene);

            var activeGo = new GameObject("SearchAdditiveTarget");
            _toDestroy.Add(activeGo);

            var result = SearchHelper.Search("SearchAdditiveTarget", scene: _additiveScene.name);
            Assert.That(result, Does.Contain(_additiveScene.name + ":/"),
                "Result should prefix with additive scene name");
        }

        [Test]
        public void Search_NoFilter_AllScenes()
        {
            var go = new GameObject("SearchNoFilterObj");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = SearchHelper.Search("SearchNoFilterObj");
            Assert.That(result, Does.Contain("SearchNoFilterObj"), "Should find object in additive scene");
        }

        // ── MultiSceneStressTests ─────────────────────────────────────────────

        [Test]
        public void ThreeScenes_AllHaveHeaders()
        {
            // Add objects to ALL scenes so phantom-header stripping preserves each scene header.
            CreateIn(_additiveScene, "Obj_Additive");
            var s1 = AddScene(); var s2 = AddScene();
            CreateIn(s1, "Obj_S1"); CreateIn(s2, "Obj_S2");
            var result = HierarchySerializer.Serialize();
            Assert.That(Regex.Matches(result, @"(?m)^\[").Count, Is.GreaterThanOrEqualTo(3), result);
        }

        [Test]
        public void ThreeScenes_Filter_OnlyMatchingSceneHeaders()
        {
            var s1 = AddScene(); var s2 = AddScene();
            CreateIn(s1, "NeedleObj");
            var result = HierarchySerializer.Serialize(filter: "NeedleObj");
            Assert.That(result, Does.Contain("[" + s1.name + "]"));
            Assert.That(result, Does.Not.Contain("[" + s2.name + "]"), "s2 phantom header");
        }

        [Test]
        public void FiveScenes_SearchFindsAllObjects()
        {
            for (int i = 0; i < 5; i++) CreateIn(AddScene(), "MultiFind");
            var result = SearchHelper.Search("MultiFind");
            int count = 0;
            foreach (var line in result.Split('\n')) if (line.Contains("MultiFind")) count++;
            Assert.That(count, Is.EqualTo(5), result);
        }

        [Test]
        public void FiveScenes_10Objects_HierarchyContainsAll()
        {
            for (int i = 0; i < 5; i++) { var s = AddScene(); for (int j = 0; j < 10; j++) CreateIn(s, $"StressObj_S{i}_J{j}"); }
            var result = HierarchySerializer.Serialize();
            for (int i = 0; i < 5; i++) for (int j = 0; j < 10; j++)
                Assert.That(result, Does.Contain($"StressObj_S{i}_J{j}"));
        }

        [Test]
        public void ThreeScenes_FindObject_QualifiedInThird()
        {
            AddScene();
            var s2 = AddScene(); CreateIn(s2, "ThirdObj");
            var found = ComponentSerializer.FindObject(s2.name + ":/ThirdObj");
            Assert.That(found, Is.Not.Null); Assert.That(found.name, Is.EqualTo("ThirdObj"));
        }

        [Test]
        public void ThreeScenes_Ambiguity_AllThreeNamed()
        {
            var s1 = AddScene(); var s2 = AddScene();
            var go0 = new GameObject("TripleAmbig"); _toDestroy.Add(go0);
            CreateIn(s1, "TripleAmbig"); CreateIn(s2, "TripleAmbig");
            var ex = Assert.Throws<System.ArgumentException>(() => ComponentSerializer.FindObject("/TripleAmbig"));
            Assert.That(ex.Message, Does.Contain("3 scenes"));
        }

        [Test]
        public void DuplicateSceneLabels_AllUnique()
        {
            AddScene(); AddScene();
            var roots = HierarchySerializer.GetAllLoadedSceneRoots();
            var names = new HashSet<string>();
            foreach (var (name, _) in roots)
                Assert.That(names.Add(name), Is.True, $"Duplicate label: {name}");
        }

        [Test]
        public void ThreeScenes_GetPath_CorrectPrefix()
        {
            AddScene();
            var s2 = AddScene(); var go = CreateIn(s2, "PathObj");
            var path = ComponentSerializer.GetPath(go);
            Assert.That(path, Does.StartWith(s2.name + ":/"));
            Assert.That(path, Does.EndWith("PathObj"));
        }

        [Test]
        public void ThreeScenes_Summary_AllHeaders()
        {
            var s1 = AddScene(); var s2 = AddScene();
            CreateIn(s1, "SumObj1"); CreateIn(s2, "SumObj2");
            var result = HierarchySerializer.SerializeSummary();
            Assert.That(result, Does.Contain("[" + s1.name + "]"));
            Assert.That(result, Does.Contain("[" + s2.name + "]"));
        }

        [Test]
        public void ThreeScenes_Incremental_DetectsChange()
        {
            AddScene();
            var r1 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            var r2 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            Assert.That(r2, Is.EqualTo("NO_CHANGE"), "Second identical call should be NO_CHANGE");
            AddScene(); HierarchySerializer.ResetIncrementalCache();
            var r3 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            Assert.That(r3, Is.Not.EqualTo("NO_CHANGE"), "After scene added should not be NO_CHANGE");
        }

        [Test]
        public void TenScenes_FindObject_Works()
        {
            for (int i = 0; i < 8; i++) AddScene();
            var last = AddScene(); CreateIn(last, "LastSceneObj");
            var found = ComponentSerializer.FindObject(last.name + ":/LastSceneObj");
            Assert.That(found, Is.Not.Null); Assert.That(found.name, Is.EqualTo("LastSceneObj"));
        }

        [Test]
        public void FiveScenes_EmptyHint_ListsAllSceneNames()
        {
            for (int i = 0; i < 4; i++) AddScene();
            var result = SearchHelper.Search("AbsolutelyNonExistentXYZ_123");
            foreach (var (name, _) in HierarchySerializer.GetAllLoadedSceneRoots())
                Assert.That(result, Does.Contain(name), $"Scene '{name}' missing from hint");
        }

        [Test]
        public void ThreeScenes_Filter_MatchOnlyInActive()
        {
            var s1 = AddScene(); var s2 = AddScene();
            var go = new GameObject("ActiveOnlyObj"); _toDestroy.Add(go);
            var result = HierarchySerializer.Serialize(filter: "ActiveOnlyObj");
            Assert.That(result, Does.Contain("ActiveOnlyObj"));
            Assert.That(result, Does.Not.Contain("[" + s1.name + "]"), "s1 phantom");
            Assert.That(result, Does.Not.Contain("[" + s2.name + "]"), "s2 phantom");
        }

        // ── Phase 4: scene param + root param combined (C3 fix) ──────────────

        [Test]
        public void Serialize_WithRootAndSceneParam_QualifiesRootToNamedScene()
        {
            // Two GOs with same name in different scenes — scene param must disambiguate
            var additiveRoot = CreateIn(_additiveScene, "AmbigRoot_C3");
            var additiveChild = CreateIn(_additiveScene, "AdditiveOnlyChild_C3");
            additiveChild.transform.SetParent(additiveRoot.transform);

            var activeGo = new GameObject("AmbigRoot_C3");
            _toDestroy.Add(activeGo);
            var activeChild = new GameObject("ActiveOnlyChild_C3");
            activeChild.transform.SetParent(activeGo.transform);
            _toDestroy.Add(activeChild);

            var result = HierarchySerializer.Serialize(
                root: "AmbigRoot_C3",
                scene: _additiveScene.name);
            Assert.That(result, Does.Contain("AdditiveOnlyChild_C3"),
                "Must serialize the additive scene version (has AdditiveOnlyChild_C3)");
            Assert.That(result, Does.Not.Contain("ActiveOnlyChild_C3"),
                "Must NOT include the active scene version");
        }
    }
}
