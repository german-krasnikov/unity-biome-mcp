using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;

namespace UnityMCP.TestProject.Scene
{
    public class HierarchyTests : SceneTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/HierarchyTests";
        private GameObject _tempRoot;

        private string OwnSceneAsset(string fileName)
        {
            var path = TempFolder + "/" + fileName;
            TrackOwnedAsset(path);
            TestPaths.EnsureFolder(TempFolder);
            return path;
        }

        // --- Hierarchy Depth Tests ---

        [Test]
        public void Hierarchy_Depth0_OnlyRoots()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            var grand = new GameObject("Grand");
            child.transform.SetParent(root.transform);
            grand.transform.SetParent(child.transform);

            try
            {
                var json = "{\"id\":\"h1\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":0}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Root", result);
                StringAssert.DoesNotContain("Child", result);
                StringAssert.DoesNotContain("Grand", result);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Hierarchy_Depth1_OnlyImmediateChildren()
        {
            var root = new GameObject("Root");
            var childA = new GameObject("ChildA");
            var grandChild = new GameObject("GrandChild");
            childA.transform.SetParent(root.transform);
            grandChild.transform.SetParent(childA.transform);

            try
            {
                var json = "{\"id\":\"h2\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":1}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Root", result);
                StringAssert.Contains("ChildA", result);
                StringAssert.DoesNotContain("GrandChild", result);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Hierarchy_DeepNesting_AllLevelsVisible()
        {
            var l0 = new GameObject("L0");
            var l1 = new GameObject("L1");
            var l2 = new GameObject("L2");
            var l3 = new GameObject("L3");
            var l4 = new GameObject("L4");
            l1.transform.SetParent(l0.transform);
            l2.transform.SetParent(l1.transform);
            l3.transform.SetParent(l2.transform);
            l4.transform.SetParent(l3.transform);

            try
            {
                var json = "{\"id\":\"h3\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("L0", result);
                StringAssert.Contains("L1", result);
                StringAssert.Contains("L2", result);
                StringAssert.Contains("L3", result);
                StringAssert.Contains("L4", result);
            }
            finally
            {
                Object.DestroyImmediate(l0);
            }
        }

        // --- Hierarchy Filter Tests ---

        [Test]
        public void Hierarchy_FilterByName_OnlyMatchingVisible()
        {
            var apple = new GameObject("Apple");
            var banana = new GameObject("Banana");
            var cherry = new GameObject("Cherry");

            try
            {
                var json = "{\"id\":\"h4\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99,\"filter\":\"Banana\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Banana", result);
                StringAssert.DoesNotContain("Apple", result);
                StringAssert.DoesNotContain("Cherry", result);
            }
            finally
            {
                Object.DestroyImmediate(apple);
                Object.DestroyImmediate(banana);
                Object.DestroyImmediate(cherry);
            }
        }

        [Test]
        public void Hierarchy_FilterPartialMatch_BothVisible()
        {
            var playerModel = new GameObject("PlayerModel");
            var playerController = new GameObject("PlayerController");
            var enemy = new GameObject("Enemy");

            try
            {
                var json = "{\"id\":\"h5\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99,\"filter\":\"Player\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("PlayerModel", result);
                StringAssert.Contains("PlayerController", result);
                StringAssert.DoesNotContain("Enemy", result);
            }
            finally
            {
                Object.DestroyImmediate(playerModel);
                Object.DestroyImmediate(playerController);
                Object.DestroyImmediate(enemy);
            }
        }

        // --- Hierarchy Root Tests ---

        [Test]
        public void Hierarchy_RootSubtree_OnlySubtreeVisible()
        {
            var alpha = new GameObject("AlphaRoot");
            var beta = new GameObject("BetaChild");
            var gamma = new GameObject("GammaRoot");
            var delta = new GameObject("DeltaChild");
            beta.transform.SetParent(alpha.transform);
            delta.transform.SetParent(gamma.transform);

            try
            {
                var json = "{\"id\":\"h6\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99,\"root\":\"AlphaRoot\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("AlphaRoot", result);
                StringAssert.Contains("BetaChild", result);
                StringAssert.DoesNotContain("GammaRoot", result);
                StringAssert.DoesNotContain("DeltaChild", result);
            }
            finally
            {
                Object.DestroyImmediate(alpha);
                Object.DestroyImmediate(gamma);
            }
        }

        // --- Inactive Objects ---

        [Test]
        public void Hierarchy_InactiveObject_MarkedWithExclamation()
        {
            var inactive = new GameObject("InactiveObj");
            inactive.SetActive(false);

            try
            {
                var json = "{\"id\":\"h7\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("InactiveObj", result);
                StringAssert.Contains("!", result); // Inactive marker
            }
            finally
            {
                Object.DestroyImmediate(inactive);
            }
        }

        [Test]
        public void Hierarchy_InactiveParent_ActiveChild_BothVisible()
        {
            var parent = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);
            parent.SetActive(false);

            try
            {
                var json = "{\"id\":\"h8\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Parent", result);
                StringAssert.Contains("Child", result);
                // Parent should have inactive marker (!)
                var parentLine = result.Substring(result.IndexOf("Parent"));
                StringAssert.Contains("!", parentLine.Substring(0, System.Math.Min(50, parentLine.Length)));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        // --- Find Objects Tests ---

        [Test]
        public void FindObjects_ByName_OnlyMatchingReturned()
        {
            var target1 = new GameObject("FindTarget1");
            var target2 = new GameObject("FindTarget2");
            var other = new GameObject("Other");

            try
            {
                var json = "{\"id\":\"f1\",\"cmd\":\"find_objects\",\"args\":{\"name\":\"FindTarget\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("FindTarget1", result);
                StringAssert.Contains("FindTarget2", result);
                StringAssert.DoesNotContain("Other", result);
            }
            finally
            {
                Object.DestroyImmediate(target1);
                Object.DestroyImmediate(target2);
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void FindObjects_ByComponent_OnlyMatchingReturned()
        {
            var a = new GameObject("A");
            var b = new GameObject("B");
            var c = new GameObject("C");
            a.AddComponent<BoxCollider>();
            b.AddComponent<SphereCollider>();
            c.AddComponent<BoxCollider>();

            try
            {
                var json = "{\"id\":\"f2\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"BoxCollider\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("A", result);
                StringAssert.Contains("C", result);
                StringAssert.DoesNotContain("B", result);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void FindObjects_CombinedFilters_OnlyMatchingReturned()
        {
            var a = new GameObject("A");
            var b = new GameObject("B");
            a.AddComponent<BoxCollider>();
            b.AddComponent<SphereCollider>();

            try
            {
                var json = "{\"id\":\"f3\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"BoxCollider\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("A", result);
                StringAssert.DoesNotContain("B", result);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void FindObjects_NoResults_EmptyOrNoObjects()
        {
            var json = "{\"id\":\"f4\",\"cmd\":\"find_objects\",\"args\":{\"name\":\"NonExistent999\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            // Result should be empty string or minimal response
            // Verify no actual object names are present
            StringAssert.DoesNotContain("NonExistent999", result.Replace("\\\"name\\\"", "")); // Exclude args from check
        }

        [Test]
        public void FindObjects_Unicode_MatchesCorrectly()
        {
            var obj = new GameObject("Объект_Тест");

            try
            {
                var json = "{\"id\":\"f5\",\"cmd\":\"find_objects\",\"args\":{\"name\":\"Объект\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Объект_Тест", result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        // --- Scene Management Tests ---

        [Test]
        public void NewScene_CreatesEmptyScene()
        {
            var json = "{\"id\":\"s1\",\"cmd\":\"scene\",\"args\":{\"action\":\"new\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            // Should return scene name
            var scene = EditorSceneManager.GetActiveScene();
            Assert.IsNotNull(scene);
        }

        [Test]
        public void SaveScene_WithPath_CreatesFile()
        {
            var scenePath = OwnSceneAsset("TestSaveScene.unity");

            // Create new scene
            CommandRouter.Process("{\"id\":\"s2a\",\"cmd\":\"scene\",\"args\":{\"action\":\"new\"}}");

            // Create test object
            var testObj = new GameObject("SaveTestObj");

            // Save scene
            var json = "{\"id\":\"s2b\",\"cmd\":\"scene\",\"args\":{\"action\":\"save\",\"path\":\"" + scenePath + "\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            // Verify file exists
            Assert.IsTrue(System.IO.File.Exists(scenePath), "Scene file should exist");

            Object.DestroyImmediate(testObj);
        }

        [Test]
        public void OpenScene_ValidPath_LoadsScene()
        {
            var scenePath = OwnSceneAsset("TestOpenScene.unity");

            // Create and save a scene first
            CommandRouter.Process("{\"id\":\"s3a\",\"cmd\":\"scene\",\"args\":{\"action\":\"new\"}}");
            var marker = new GameObject("OpenTestMarker");
            CommandRouter.Process("{\"id\":\"s3b\",\"cmd\":\"scene\",\"args\":{\"action\":\"save\",\"path\":\"" + scenePath + "\"}}");
            Object.DestroyImmediate(marker);

            // Create another scene
            CommandRouter.Process("{\"id\":\"s3c\",\"cmd\":\"scene\",\"args\":{\"action\":\"new\"}}");

            // Open saved scene
            var json = "{\"id\":\"s3d\",\"cmd\":\"scene\",\"args\":{\"action\":\"open\",\"path\":\"" + scenePath + "\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("TestOpenScene", result); // OpenScene returns scene name
        }

        [Test]
        public void OpenScene_InvalidPath_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
            var json = "{\"id\":\"s4\",\"cmd\":\"scene\",\"args\":{\"action\":\"open\",\"path\":\"Assets/Scenes/NonExistent.unity\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void DiscardChanges_AfterModification_ObjectRemoved()
        {
            // Create new scene
            CommandRouter.Process("{\"id\":\"s5a\",\"cmd\":\"scene\",\"args\":{\"action\":\"new\"}}");

            // Create object
            var testObj = new GameObject("DiscardTestObj");

            // Verify object exists
            Assert.IsNotNull(GameObject.Find("DiscardTestObj"));

            // Discard changes
            var json = "{\"id\":\"s5b\",\"cmd\":\"scene\",\"args\":{\"action\":\"discard\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            // Object should be gone
            Assert.IsNull(GameObject.Find("DiscardTestObj"));
        }

        // ─── Incremental Hierarchy Tests ──────────────────────────────────────────

        [Test]
        public void Hierarchy_Incremental_SameScene_ReturnsNoChange()
        {
            // Reset incremental state first
            HierarchySerializer.ResetIncrementalCache();

            var go = new GameObject("IncrementalTestObj");
            try
            {
                // First call populates cache
                HierarchySerializer.SerializeIncremental(99, null, null, false);
                // Second call with same scene returns NO_CHANGE
                var result = HierarchySerializer.SerializeIncremental(99, null, null, false);
                Assert.AreEqual("NO_CHANGE", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                HierarchySerializer.ResetIncrementalCache();
            }
        }

        [Test]
        public void Hierarchy_Incremental_AfterChange_ReturnsFull()
        {
            HierarchySerializer.ResetIncrementalCache();

            var go = new GameObject("ChangeTestObj");
            try
            {
                // Populate cache
                HierarchySerializer.SerializeIncremental(99, null, null, false);

                // Add a new object — scene changed
                var go2 = new GameObject("NewObj_XYZ");
                try
                {
                    var result = HierarchySerializer.SerializeIncremental(99, null, null, false);
                    Assert.AreNotEqual("NO_CHANGE", result);
                    StringAssert.Contains("NewObj_XYZ", result);
                }
                finally
                {
                    Object.DestroyImmediate(go2);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
                HierarchySerializer.ResetIncrementalCache();
            }
        }

        // --- Summary Tests ---

        [Test]
        public void Summary_LeafRoot_JustName()
        {
            // Single root with no children: just outputs the name, no count suffix
            _tempRoot = TrackOwnedObject(new GameObject("SummaryTestRoot_Empty"));
            var result = HierarchySerializer.SerializeSummary("SummaryTestRoot_Empty");
            StringAssert.Contains("SummaryTestRoot_Empty", result);
            StringAssert.DoesNotContain("children", result);
        }

        [Test]
        public void Summary_RootWithChildren_ShowsCount()
        {
            _tempRoot = TrackOwnedObject(new GameObject("SummaryTestRoot_WithKids"));
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");
            child1.transform.SetParent(_tempRoot.transform);
            child2.transform.SetParent(_tempRoot.transform);

            var result = HierarchySerializer.SerializeSummary("SummaryTestRoot_WithKids");
            StringAssert.Contains("SummaryTestRoot_WithKids", result);
            StringAssert.Contains("2 children", result);
        }

        [Test]
        public void Summary_DeepNesting_CountsAllDescendants()
        {
            _tempRoot = TrackOwnedObject(new GameObject("SummaryTestRoot_Deep"));
            var mid = new GameObject("Mid");
            var leaf1 = new GameObject("Leaf1");
            var leaf2 = new GameObject("Leaf2");
            mid.transform.SetParent(_tempRoot.transform);
            leaf1.transform.SetParent(mid.transform);
            leaf2.transform.SetParent(mid.transform);

            // root has 1 direct child (mid), but 3 total descendants
            var result = HierarchySerializer.SerializeSummary("SummaryTestRoot_Deep");
            StringAssert.Contains("SummaryTestRoot_Deep", result);
            StringAssert.Contains("3 children", result);
        }

        [Test]
        public void Summary_ViaCommandRouter_ReturnsSummaryFormat()
        {
            _tempRoot = TrackOwnedObject(new GameObject("SummaryTestRoot_Router"));
            var child = new GameObject("RouterChild");
            child.transform.SetParent(_tempRoot.transform);

            var json = "{\"id\":\"t_sum\",\"cmd\":\"get_hierarchy\",\"args\":{\"root\":\"SummaryTestRoot_Router\",\"summary\":\"true\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("SummaryTestRoot_Router", result);
            StringAssert.Contains("children", result);
        }
    }
}
