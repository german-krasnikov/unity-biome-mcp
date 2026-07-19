using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Consolidated: TransferObjectTests + CreateObjectSceneTests + ObjectDiffTests +
    ///               SceneManagementTests + MultiSceneBugfixTests
    /// </summary>
    [TestFixture]
    public class MultiSceneOperationsTests : MultiSceneTestBase
    {
        // ── TransferObjectTests ───────────────────────────────────────────────
        // These tests use _go directly and need their own creation/cleanup,
        // so we use _toDestroy tracking from base for cleanup.

        private GameObject _transferGo;

        public override void SetUp()
        {
            base.SetUp();
            _transferGo = new GameObject("TransferObj");
            _toDestroy.Add(_transferGo);
        }

        [Test]
        public void Move_RootObject_ChangesScene()
        {
            var originalScene = _transferGo.scene;

            ObjectManager.TransferObject("/TransferObj", "move", _additiveScene.name, null, true);

            Assert.AreEqual(_additiveScene.name, _transferGo.scene.name,
                "After move, object should be in target scene");
            Assert.AreNotEqual(originalScene.name, _transferGo.scene.name);
        }

        [Test]
        public void Move_ChildObject_AutoUnparents()
        {
            var parent = new GameObject("TransferParent");
            _toDestroy.Add(parent);
            _transferGo.transform.SetParent(parent.transform);

            ObjectManager.TransferObject("/TransferParent/TransferObj", "move", _additiveScene.name, null, true);

            Assert.IsNull(_transferGo.transform.parent, "After move to another scene, object should be unparented");
            Assert.AreEqual(_additiveScene.name, _transferGo.scene.name);
        }

        [Test]
        public void Copy_LeavesOriginalIntact()
        {
            var originalScene = _transferGo.scene;

            ObjectManager.TransferObject("/TransferObj", "copy", _additiveScene.name, null, true);

            Assert.IsNotNull(_transferGo, "Original object should still exist after copy");
            Assert.AreEqual(originalScene.name, _transferGo.scene.name,
                "Original should remain in its scene");
        }

        [Test]
        public void Copy_CloneInTargetScene()
        {
            ObjectManager.TransferObject("/TransferObj", "copy", _additiveScene.name, null, true);

            var roots = _additiveScene.GetRootGameObjects();
            bool found = false;
            foreach (var r in roots)
                if (r.name == "TransferObj") { found = true; break; }

            Assert.IsTrue(found, "Clone should exist in target scene after copy");
        }

        [Test]
        public void Copy_DoesNotDirtySourceScene()
        {
            var sourceScene = _transferGo.scene;
            EditorSceneManager.SaveScene(sourceScene);
            EditorSceneManager.SaveScene(_additiveScene);
            Assert.IsFalse(sourceScene.isDirty, "precondition: source scene should be clean");
            Assert.IsFalse(_additiveScene.isDirty, "precondition: target scene should be clean");

            ObjectManager.TransferObject("/TransferObj", "copy", _additiveScene.name, null, true);

            Assert.IsFalse(sourceScene.isDirty, "Copy should not dirty the source scene");
            Assert.IsTrue(_additiveScene.isDirty, "Copy should dirty only the target scene");
        }

        [Test]
        public void Move_SceneNotLoaded_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.TransferObject("/TransferObj", "move", "SceneNotLoaded_XYZ", null, true));
        }

        [Test]
        public void Copy_ParentInWrongScene_Throws()
        {
            // parentGo lives in the main (active) scene; clone would land in additive — mismatch
            var parentGo = new GameObject("WrongSceneParent");
            _toDestroy.Add(parentGo);

            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.TransferObject("/TransferObj", "copy", _additiveScene.name,
                    "/WrongSceneParent", true));
        }

        [Test]
        public void Copy_Response_NoPathDuplication()
        {
            var result = ObjectManager.TransferObject("/TransferObj", "copy", _additiveScene.name, null, true);

            // Before fix: "Copied … → SceneName/SceneName:/TransferObj"
            StringAssert.DoesNotContain($"{_additiveScene.name}/{_additiveScene.name}", result);
        }

        [Test]
        public void InvalidAction_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.TransferObject("/TransferObj", "teleport", _additiveScene.name, null, true));
        }

        // ── CreateObjectSceneTests ────────────────────────────────────────────

        private const string ObjName = "CreateInScene_TestObj";

        public override void TearDown()
        {
            // Clean up CreateObject stray GOs not in _toDestroy
            var stray = GameObject.Find(ObjName);
            if (stray != null) Object.DestroyImmediate(stray);

            if (_additiveScene.IsValid())
            {
                foreach (var root in _additiveScene.GetRootGameObjects())
                    if (root != null && root.name == ObjName) Object.DestroyImmediate(root);
            }

            base.TearDown();
        }

        [Test]
        public void CreateObject_WithScene_CreatesInIt()
        {
            ObjectManager.CreateObject(ObjName, null, null, scene: _additiveScene.name);

            var roots = _additiveScene.GetRootGameObjects();
            bool found = false;
            foreach (var r in roots)
                if (r.name == ObjName) { found = true; break; }

            Assert.IsTrue(found, $"Object should be in additive scene '{_additiveScene.name}'");
        }

        [Test]
        public void CreateObject_InNamedScene_MarksTargetSceneDirty()
        {
            ObjectManager.CreateObject(ObjName, null, null, scene: _additiveScene.name);

            Assert.IsTrue(_additiveScene.isDirty, "CreateObject with scene= must mark target scene dirty");
        }

        [Test]
        public void CreateObject_NoScene_CreatesInActive()
        {
            var activeBefore = SceneManager.GetActiveScene().name;
            ObjectManager.CreateObject(ObjName, null, null);

            var go = GameObject.Find(ObjName);
            Assert.IsNotNull(go, "Object should be created");
            Assert.AreEqual(activeBefore, go.scene.name,
                "Without scene param, object should be in active scene");
        }

        [Test]
        public void CreateObject_SceneNotFound_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.CreateObject(ObjName, null, null, scene: "NoSuchScene_XYZ"));
        }

        // ── ObjectDiffTests ───────────────────────────────────────────────────

        [Test]
        public void Diff_IdenticalObjects_ReturnsIdentical()
        {
            var a = new GameObject("Twin"); _toDestroy.Add(a);
            var b = new GameObject("Twin2"); _toDestroy.Add(b);
            var result = ObjectDiffHelper.Diff(ComponentSerializer.GetPath(a), ComponentSerializer.GetPath(b));
            Assert.That(result, Does.Contain("identical"));
        }

        [Test]
        public void Diff_DifferentComponents_Listed()
        {
            var a = new GameObject("ObjA"); _toDestroy.Add(a);
            var b = new GameObject("ObjB"); _toDestroy.Add(b);
            a.AddComponent<BoxCollider>();
            b.AddComponent<SphereCollider>();

            var result = ObjectDiffHelper.Diff(ComponentSerializer.GetPath(a), ComponentSerializer.GetPath(b));
            Assert.That(result, Does.Contain("BoxCollider").Or.Contain("SphereCollider"));
        }

        [Test]
        public void Diff_DifferentProperties_Shows()
        {
            var a = new GameObject("PropA"); _toDestroy.Add(a);
            var b = new GameObject("PropB"); _toDestroy.Add(b);
            a.transform.position = new Vector3(1, 0, 0);
            b.transform.position = new Vector3(5, 0, 0);

            var result = ObjectDiffHelper.Diff(ComponentSerializer.GetPath(a), ComponentSerializer.GetPath(b));
            Assert.That(result, Does.Contain("Properties").Or.Contain("m_LocalPosition").Or.Contain("→"));
        }

        [Test]
        public void Diff_DifferentChildren_Listed()
        {
            var a = new GameObject("ParentA"); _toDestroy.Add(a);
            var b = new GameObject("ParentB"); _toDestroy.Add(b);
            var child = new GameObject("ChildOnly"); _toDestroy.Add(child);
            child.transform.SetParent(a.transform);

            var result = ObjectDiffHelper.Diff(ComponentSerializer.GetPath(a), ComponentSerializer.GetPath(b));
            Assert.That(result, Does.Contain("ChildOnly").Or.Contain("Children"));
        }

        [Test]
        public void Diff_ObjectNotFound_ReturnsError()
        {
            var a = new GameObject("RealObj"); _toDestroy.Add(a);
            var result = ObjectDiffHelper.Diff(ComponentSerializer.GetPath(a), "/NonExistentXYZ999");
            Assert.That(result, Does.Contain("not found").IgnoreCase.Or.Contain("error").IgnoreCase);
        }

        // ── SceneManagementTests ──────────────────────────────────────────────

        [Test]
        public void ListScenes_SingleScene_ActiveMarker()
        {
            // Close the additive scene opened by base.SetUp to get single-scene state
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var result = SceneHelper.ListScenes();

            StringAssert.Contains("*", result);
            StringAssert.Contains("objs", result);
        }

        [Test]
        public void ListScenes_MultiScene_BothListed()
        {
            // _additiveScene already open from base.SetUp
            var result = SceneHelper.ListScenes();

            var lines = result.Trim().Split('\n');
            Assert.GreaterOrEqual(lines.Length, 2, "Should list at least 2 scenes");
            int starCount = 0;
            foreach (var line in lines)
                if (line.TrimStart().StartsWith("*")) starCount++;
            Assert.AreEqual(1, starCount, "Exactly one scene should be active");
        }

        [Test]
        public void ListScenes_MultiScene_AdditiveName_Present()
        {
            var result = SceneHelper.ListScenes();

            StringAssert.Contains(_additiveScene.name, result);
        }

        [Test]
        public void SetActiveScene_ValidScene_ChangesActive()
        {
            var originalActive = SceneManager.GetActiveScene();

            var result = SceneHelper.SetActiveScene(_additiveScene.name);

            Assert.AreEqual(_additiveScene.name, SceneManager.GetActiveScene().name,
                "Active scene should change after SetActiveScene");
            StringAssert.Contains(_additiveScene.name, result);

            SceneManager.SetActiveScene(originalActive);
        }

        [Test]
        public void SetActiveScene_InvalidName_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                SceneHelper.SetActiveScene("SceneThatDoesNotExist_XYZ"));
        }

        [Test]
        public void CloseScene_NonActive_Success()
        {
            var name = _additiveScene.name;

            var result = SceneHelper.CloseScene(name);

            _additiveScene = default; // already closed
            Assert.IsFalse(SceneManager.GetSceneByName(name).IsValid(),
                "Scene should be gone after CloseScene");
            StringAssert.Contains(name, result);
        }

        [Test]
        public void CloseScene_OnlyScene_Throws()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            Assert.Throws<System.InvalidOperationException>(() =>
                SceneHelper.CloseScene(SceneManager.GetActiveScene().name));
        }

        // ── MultiSceneBugfixTests ─────────────────────────────────────────────

        [Test]
        public void FindObjects_MultiScene_FindsInAllScenes()
        {
            CreateIn(_additiveScene, "AdditiveObj_Bug1");

            var result = ObjectManager.FindObjects("AdditiveObj_Bug1", null, null, null);

            Assert.That(result, Does.Contain("AdditiveObj_Bug1"),
                "FindObjects must search all loaded scenes, not just active scene");
        }

        [Test]
        public void FindObjects_SingleScene_UnchangedBehavior()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;
            SceneContext.InvalidateCache();

            var go = new GameObject("SingleSceneObj_Bug1");
            _toDestroy.Add(go);

            var result = ObjectManager.FindObjects("SingleSceneObj_Bug1", null, null, null);

            Assert.That(result, Does.Contain("/SingleSceneObj_Bug1"));
            Assert.That(result, Does.Not.Contain(":/"),
                "Single-scene result must not have SceneName:/ prefix");
        }

        [Test]
        public void FindReferencesTo_MultiScene_ScansAllScenes()
        {
            var target = new GameObject("RefTarget_Bug2");
            _toDestroy.Add(target);

            var referencer = CreateIn(_additiveScene, "Referencer_Bug2");
            var refComp = referencer.AddComponent<ReferencerComponent>();
            refComp.target = target;

            var result = ReferenceHelper.FindReferencesTo(ComponentSerializer.GetPath(target));

            Assert.That(result, Does.Contain("Referencer_Bug2"),
                "FindReferencesTo must scan all loaded scenes, not just active scene");
            Assert.That(result, Does.Contain("found: 1"));
        }

        [Test]
        public void FindReferencesTo_SingleScene_UnchangedBehavior()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var target = new GameObject("RefTarget_Single_Bug2");
            _toDestroy.Add(target);
            var referencer = new GameObject("Referencer_Single_Bug2");
            _toDestroy.Add(referencer);

            var refComp = referencer.AddComponent<ReferencerComponent>();
            refComp.target = target;

            var result = ReferenceHelper.FindReferencesTo("/RefTarget_Single_Bug2");

            Assert.That(result, Does.Contain("Referencer_Single_Bug2"));
            Assert.That(result, Does.Contain("found: 1"));
        }

        // ── Item 33: DiscardChanges with identifier (multi-scene) ────────────

        [Test]
        public void DiscardChanges_WithIdentifier_ReloadsOnlyTargetScene()
        {
            // Add a GO to the additive scene and dirty it
            CreateIn(_additiveScene, "DirtyObj_Discard33");
            EditorSceneManager.MarkSceneDirty(_additiveScene);

            var mainName = SceneManager.GetActiveScene().name;
            var additiveName = _additiveScene.name;

            var result = SceneHelper.DiscardChanges(additiveName);

            // Reassign so TearDown tracks the reloaded scene
            _additiveScene = SceneManager.GetSceneByName(additiveName);

            // Main scene must still be loaded
            Assert.IsTrue(SceneManager.GetSceneByName(mainName).IsValid(),
                "Main scene must survive DiscardChanges on additive scene");
            Assert.AreEqual(2, SceneManager.sceneCount,
                "Scene count must remain 2 after targeted discard");
            StringAssert.Contains("reloaded", result);
        }

        [Test]
        public void DiscardChanges_WithIdentifier_UnknownScene_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => SceneHelper.DiscardChanges("SceneThatDoesNotExist_DiscardTest_XYZ"));
            StringAssert.Contains("not found", ex.Message);
        }

        [Test]
        public void DiscardChanges_NoIdentifier_CreatesNewScene()
        {
            // Without identifier on an unsaved scene: returns "new scene" (legacy behavior)
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;
            // Create a fresh unsaved scene so path is empty — guarantees "new scene" branch
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var result = SceneHelper.DiscardChanges(null);

            Assert.AreEqual("new scene", result);
        }

        // ── Item 34: SaveScene with identifier (multi-scene) ─────────────────

        [Test]
        public void SaveScene_WithIdentifier_SavesTargetScene()
        {
            // _additiveScene was saved to disk — just mark dirty and re-save
            EditorSceneManager.MarkSceneDirty(_additiveScene);

            var result = SceneHelper.SaveScene(null, _additiveScene.name);

            Assert.AreEqual(_additiveTempPath, result);
            Assert.IsFalse(SceneManager.GetSceneByName(_additiveScene.name).isDirty,
                "Scene must not be dirty after save");
        }

        [Test]
        public void SaveScene_WithIdentifier_NoActiveSceneChange()
        {
            var mainName = SceneManager.GetActiveScene().name;

            SceneHelper.SaveScene(null, _additiveScene.name);

            Assert.AreEqual(mainName, SceneManager.GetActiveScene().name,
                "Active scene must not change when saving a non-active scene");
        }

        [Test]
        public void SaveScene_NoIdentifier_KeepsCurrentBehavior()
        {
            // Without identifier: saves active scene (current behavior unchanged)
            var active = SceneManager.GetActiveScene();
            // active scene has a path from base.SetUp
            Assert.IsFalse(string.IsNullOrEmpty(active.path));

            var result = SceneHelper.SaveScene(null, null);

            Assert.AreEqual(active.path, result);
        }

        // Expose _additiveTempPath for tests above
        private string _additiveTempPath =>
            TestPaths.TempFolder + $"/{GetType().Name}_additive_temp.unity";

        // ── Helper component ──────────────────────────────────────────────────

        private class ReferencerComponent : MonoBehaviour
        {
            public GameObject target;
        }
    }
}
