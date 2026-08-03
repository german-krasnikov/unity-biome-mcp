using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Consolidated: MultiSceneFinderTests + MultiSceneFinderEdgeTests + MultiSceneSearchTests
    /// </summary>
    [TestFixture]
    public class MultiSceneFinderTests : MultiSceneTestBase
    {
        // ── MultiSceneFinderTests ─────────────────────────────────────────────

        [Test]
        public void FindObject_QualifiedPath_ReturnsCorrectScene()
        {
            var addGo = CreateIn(_additiveScene, "TestObj");
            var mainGo = new GameObject("TestObj");
            TrackOwnedObject(mainGo);

            var found = ComponentSerializer.FindObject(_additiveScene.name + ":/TestObj");

            Assert.That(found, Is.EqualTo(addGo));
        }

        [Test]
        public void FindObject_QualifiedPath_Main()
        {
            var mainGo = new GameObject("TestObj");
            TrackOwnedObject(mainGo);
            CreateIn(_additiveScene, "TestObj");

            var found = ComponentSerializer.FindObject(_savedMainSceneName + ":/TestObj");

            Assert.That(found, Is.EqualTo(mainGo));
        }

        [Test]
        public void FindObject_UnqualifiedAmbiguous_Throws()
        {
            var go = new GameObject("TestObj");
            TrackOwnedObject(go);
            CreateIn(_additiveScene, "TestObj");

            Assert.Throws<System.ArgumentException>(() => ComponentSerializer.FindObject("/TestObj"));
        }

        [Test]
        public void FindObject_UnqualifiedUnique_Succeeds()
        {
            var go = CreateIn(_additiveScene, "UniqueObj_12345");

            var found = ComponentSerializer.FindObject("/UniqueObj_12345");

            Assert.That(found, Is.EqualTo(go));
        }

        [Test]
        public void FindObject_InstanceId_CrossScene()
        {
            var go = CreateIn(_additiveScene, "InstanceObj");

            var found = ComponentSerializer.FindObject("#" + TransientObjectId.GetWireValue(go));

            Assert.That(found, Is.EqualTo(go));
        }

        [Test]
        public void FindObject_SlashInName_ByInstanceId()
        {
            var go = new GameObject("OBJ/WITH/SLASH");
            TrackOwnedObject(go);

            var found = ComponentSerializer.FindObject("#" + TransientObjectId.GetWireValue(go));

            Assert.That(found, Is.EqualTo(go));
        }

        [Test]
        public void FindObject_SlashInName_WholeNameFallback()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var go = new GameObject("OBJ/WITH/SLASH");
            TrackOwnedObject(go);

            var found = ComponentSerializer.FindObject("/OBJ/WITH/SLASH");

            Assert.That(found, Is.EqualTo(go));
        }

        [Test]
        public void GetPath_MultiScene_HasScenePrefix()
        {
            var go = CreateIn(_additiveScene, "PrefixObj");

            var path = ComponentSerializer.GetPath(go);

            Assert.That(path, Does.StartWith(_additiveScene.name + ":/"));
            Assert.That(path, Does.EndWith("PrefixObj"));
        }

        [Test]
        public void GetPath_SingleScene_NoPrefix()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var go = new GameObject("NoPrefixObj");
            TrackOwnedObject(go);

            var path = ComponentSerializer.GetPath(go);

            Assert.That(path, Does.StartWith("/"));
            Assert.That(path, Does.Not.Contain(":/"));
        }

        // ── MultiSceneFinderEdgeTests ─────────────────────────────────────────

        [Test]
        public void DeepQualifiedPath_FourLevels()
        {
            var root = CreateIn(_additiveScene, "Root");
            var c1 = CreateChild(root, "C1");
            var c2 = CreateChild(c1, "C2");
            var c3 = CreateChild(c2, "C3");

            var found = ComponentSerializer.FindObject(_additiveScene.name + ":/Root/C1/C2/C3");

            Assert.That(found, Is.EqualTo(c3));
        }

        [Test]
        public void QualifiedPath_ObjectMissing_ReturnsNull()
        {
            var found = ComponentSerializer.FindObject(_additiveScene.name + ":/Ghost");

            Assert.That(found, Is.Null);
        }

        [Test]
        public void SingleCharRootName()
        {
            var go = CreateIn(_additiveScene, "A");

            var found = ComponentSerializer.FindObject(_additiveScene.name + ":/A");

            Assert.That(found, Is.EqualTo(go));
        }

        [Test]
        public void MultipleSlashesInName_InstanceIdFallback()
        {
            var go = new GameObject("[MECH/ZONE/TEMPLATE]");
            TrackOwnedObject(go);

            var found = ComponentSerializer.FindObject("#" + TransientObjectId.GetWireValue(go));

            Assert.That(found, Is.EqualTo(go));
        }

        [Test]
        public void EmptySceneNameInQualifiedPath_ReturnsNull()
        {
            var found = ComponentSerializer.FindObject(":/SomeObj");

            Assert.That(found, Is.Null);
        }

        [Test]
        public void InstanceId_Zero_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ComponentSerializer.FindObject("#0"));
        }

        [Test]
        public void QualifiedPath_ChildSameNameAsParent()
        {
            var parent = CreateIn(_additiveScene, "Parent");
            var child = CreateChild(parent, "Parent");

            var found = ComponentSerializer.FindObject(_additiveScene.name + ":/Parent/Parent");

            Assert.That(found, Is.EqualTo(child));
        }

        [Test]
        public void FindRoot_ThreeScenes_AmbiguityHasAllThree()
        {
            var s2 = AddScene(); var s3 = AddScene();
            var go1 = new GameObject("AmbigRoot"); TrackOwnedObject(go1);
            CreateIn(s2, "AmbigRoot"); CreateIn(s3, "AmbigRoot");

            var ex = Assert.Throws<System.ArgumentException>(
                () => ComponentSerializer.FindObject("/AmbigRoot"));
            Assert.That(ex.Message, Does.Contain("3 scenes"));
        }

        [Test]
        public void UnqualifiedNoSlash_StillAmbiguous()
        {
            var go = new GameObject("SharedRoot"); TrackOwnedObject(go);
            CreateIn(_additiveScene, "SharedRoot");

            Assert.Throws<System.ArgumentException>(
                () => ComponentSerializer.FindObject("SharedRoot"));
        }

        [Test]
        public void GetPath_DeepChild_FullQualifiedPath()
        {
            var r = CreateIn(_additiveScene, "R");
            var a = CreateChild(r, "A");
            var b = CreateChild(a, "B");

            var path = ComponentSerializer.GetPath(b);

            Assert.That(path, Is.EqualTo(_additiveScene.name + ":/R/A/B"));
        }

        // ── MultiSceneSearchTests ─────────────────────────────────────────────

        [Test]
        public void Search_MultiScene_FindsBothScenes()
        {
            var mainGo = new GameObject("Alice_UniqueSearch");
            TrackOwnedObject(mainGo);
            CreateIn(_additiveScene, "Alice_UniqueSearch");

            var result = SearchHelper.Search("Alice_UniqueSearch");

            var lines = result.Split('\n');
            int count = 0;
            foreach (var line in lines)
                if (line.Contains("Alice_UniqueSearch")) count++;

            Assert.That(count, Is.EqualTo(2), $"Expected 2 results, got {count}:\n{result}");
        }

        [Test]
        public void Search_MultiScene_ResultHasScenePrefix()
        {
            CreateIn(_additiveScene, "Alice_PrefixCheck");

            var result = SearchHelper.Search("Alice_PrefixCheck");

            Assert.That(result, Does.Contain(":/"), $"Expected scene prefix in:\n{result}");
        }

        [Test]
        public void Search_SingleScene_NoPrefix()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var go = new GameObject("Alice_NoPrefix");
            TrackOwnedObject(go);

            var result = SearchHelper.Search("Alice_NoPrefix");

            Assert.That(result, Does.Not.Contain(":/"), $"No scene prefix expected:\n{result}");
        }

        [Test]
        public void BuildEmptyHint_MultiScene_ListsAllScenes()
        {
            var result = SearchHelper.Search("NonExistentXYZ_AbsolutelyUnique");

            Assert.That(result, Does.Contain("+"), $"Expected '+' joining scene names:\n{result}");
        }
    }
}
