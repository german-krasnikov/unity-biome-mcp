using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class SetParentTests : SceneTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/SetParentTests";

        private static string Process(string cmd, string argsJson) =>
            CommandRouter.Process($"{{\"id\":\"sp\",\"cmd\":\"{cmd}\",\"args\":{argsJson}}}");

        private UnityEngine.SceneManagement.Scene CreateOwnedScene(
            string fileName,
            params GameObject[] roots)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = CreateOwnedAdditiveScene();
            try
            {
                foreach (var root in roots)
                    SceneManager.MoveGameObjectToScene(root, scene);
                var scenePath = TempFolder + "/" + fileName;
                TrackOwnedAsset(scenePath);
                TestPaths.EnsureFolder(TempFolder);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new System.IO.IOException($"Could not save owned scene '{scenePath}'.");
                return scene;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void SetParent_Basic()
        {
            var a = new GameObject("SPTestA");
            var b = new GameObject("SPTestB");
            try
            {
                var result = Process("set_parent", "{\"path\":\"/SPTestA\",\"parent\":\"/SPTestB\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(b.transform, a.transform.parent);
                Assert.AreEqual("/SPTestB/SPTestA", ComponentSerializer.GetPath(a));
            }
            finally
            {
                Object.DestroyImmediate(b); // destroys child too
            }
        }

        [Test]
        public void SetParent_ToRoot()
        {
            var parent = new GameObject("SPParent");
            var child = new GameObject("SPChild");
            child.transform.SetParent(parent.transform);
            try
            {
                var result = Process("set_parent", "{\"path\":\"/SPParent/SPChild\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsNull(child.transform.parent);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(child);
            }
        }

        [Test]
        public void SetParent_InvalidPath()
        {
            var b = new GameObject("SPTestB2");
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("not found"));
                var result = Process("set_parent", "{\"path\":\"/NonExistent\",\"parent\":\"/SPTestB2\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SetParent_InvalidParent()
        {
            var a = new GameObject("SPTestA2");
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("not found"));
                var result = Process("set_parent", "{\"path\":\"/SPTestA2\",\"parent\":\"/NonExistentParent\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(a);
            }
        }

        [Test]
        public void SetParent_NoDuplicate()
        {
            // Regression: original /A must NOT exist at root after reparenting to /B
            var a = new GameObject("SPReg_A");
            var b = new GameObject("SPReg_B");
            try
            {
                Process("set_parent", "{\"path\":\"/SPReg_A\",\"parent\":\"/SPReg_B\",\"world_position_stays\":\"true\"}");
                // Original path /SPReg_A should no longer resolve (it's now /SPReg_B/SPReg_A)
                Assert.IsNull(ComponentSerializer.FindObject("/SPReg_A", strict: true),
                    "/SPReg_A must not resolve after reparenting");
                Assert.IsNotNull(ComponentSerializer.FindObject("/SPReg_B/SPReg_A"),
                    "/SPReg_B/SPReg_A must resolve");
            }
            finally
            {
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SetParent_MarksSceneDirty()
        {
            var a = new GameObject("SPDirty_A");
            var b = new GameObject("SPDirty_B");
            try
            {
                var scene = CreateOwnedScene("set-parent-dirty.unity", a, b);
                Assert.IsFalse(scene.isDirty, "Scene must be clean after save");

                Process("set_parent", "{\"path\":\"/SPDirty_A\",\"parent\":\"/SPDirty_B\",\"world_position_stays\":\"true\"}");
                Assert.IsTrue(a.scene.isDirty, "Scene must be dirty after set_parent");
            }
            finally
            {
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SetParent_HierarchyShowsChildAfterReparent()
        {
            var a = new GameObject("SPHier_Child");
            var b = new GameObject("SPHier_Parent");
            try
            {
                Process("set_parent", "{\"path\":\"/SPHier_Child\",\"parent\":\"/SPHier_Parent\",\"world_position_stays\":\"true\"}");
                var hierarchy = HierarchySerializer.SerializeSubtree(b);
                StringAssert.Contains("SPHier_Child", hierarchy);
            }
            finally
            {
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SetParent_PreservesWorldPosition()
        {
            var a = new GameObject("SPWorld_A");
            a.transform.position = new Vector3(5f, 3f, 1f);
            var b = new GameObject("SPWorld_B");
            b.transform.position = new Vector3(10f, 0f, 0f);
            try
            {
                Process("set_parent", "{\"path\":\"/SPWorld_A\",\"parent\":\"/SPWorld_B\",\"world_position_stays\":\"true\"}");
                // world position must be preserved (default worldPositionStays=true)
                Assert.AreEqual(new Vector3(5f, 3f, 1f), a.transform.position,
                    "World position must be preserved");
            }
            finally
            {
                Object.DestroyImmediate(b);
            }
        }

        // --- Strict path lookup (from MCPSetParentStrictTests) ---

        [Test]
        public void FindObject_Strict_ExactMatch_Succeeds()
        {
            var a = new GameObject("StrictA");
            var b = new GameObject("StrictB");
            b.transform.SetParent(a.transform);
            try
            {
                var found = ComponentSerializer.FindObject("/StrictA/StrictB", strict: true);
                Assert.IsNotNull(found);
                Assert.AreEqual(b, found);
            }
            finally
            {
                Object.DestroyImmediate(a);
            }
        }

        [Test]
        public void FindObject_Strict_MissingPath_ReturnsNull()
        {
            var found = ComponentSerializer.FindObject("/NonExistent/B", strict: true);
            Assert.IsNull(found);
        }

        [Test]
        public void SetParent_StaleSourcePath_FailsStrict()
        {
            // Create /SRC/Child, then reparent SRC to /DST → child is now /DST/SRC/Child
            var src = new GameObject("StrictSRC");
            var child = new GameObject("StrictChild");
            child.transform.SetParent(src.transform);
            var dst = new GameObject("StrictDST");
            var other = new GameObject("StrictOther");
            src.transform.SetParent(dst.transform); // src is now /StrictDST/StrictSRC
            try
            {
                // /StrictSRC/StrictChild no longer valid — strict should fail
                LogAssert.Expect(LogType.Warning, new Regex("not found"));
                var result = Process("set_parent", "{\"path\":\"/StrictSRC/StrictChild\",\"parent\":\"/StrictOther\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(dst);
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void SetParent_ChainedReparent_SameSession()
        {
            var a = new GameObject("ChainA");
            var b = new GameObject("ChainB");
            var c = new GameObject("ChainC");
            try
            {
                // /ChainA → /ChainB
                var r1 = Process("set_parent", "{\"path\":\"/ChainA\",\"parent\":\"/ChainB\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":true", r1);
                // /ChainB/ChainA → /ChainC
                var r2 = Process("set_parent", "{\"path\":\"/ChainB/ChainA\",\"parent\":\"/ChainC\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":true", r2);
                Assert.AreEqual("/ChainC/ChainA", ComponentSerializer.GetPath(a));
            }
            finally
            {
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void SetParent_CrossRoot_Succeeds()
        {
            var root1 = new GameObject("CrossRoot1");
            var obj = new GameObject("CrossObj");
            obj.transform.SetParent(root1.transform);
            var root2 = new GameObject("CrossRoot2");
            try
            {
                var result = Process("set_parent", "{\"path\":\"/CrossRoot1/CrossObj\",\"parent\":\"/CrossRoot2\",\"world_position_stays\":\"true\"}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(0, root1.transform.childCount);
                Assert.AreEqual(root2.transform, obj.transform.parent);
            }
            finally
            {
                Object.DestroyImmediate(root1);
                Object.DestroyImmediate(root2);
            }
        }

        [Test]
        public void SetParent_SequentialBatch_NoDestruction()
        {
            var src = new GameObject("SeqSRC");
            var childA = new GameObject("SeqA");
            var childB = new GameObject("SeqB");
            var childC = new GameObject("SeqC");
            childA.transform.SetParent(src.transform);
            childB.transform.SetParent(src.transform);
            childC.transform.SetParent(src.transform);
            var dst = new GameObject("SeqDST");
            try
            {
                Process("set_parent", "{\"path\":\"/SeqSRC/SeqA\",\"parent\":\"/SeqDST\",\"world_position_stays\":\"true\"}");
                Process("set_parent", "{\"path\":\"/SeqSRC/SeqB\",\"parent\":\"/SeqDST\",\"world_position_stays\":\"true\"}");
                Process("set_parent", "{\"path\":\"/SeqSRC/SeqC\",\"parent\":\"/SeqDST\",\"world_position_stays\":\"true\"}");

                Assert.AreEqual(3, dst.transform.childCount, "DST must have 3 children");
                Assert.AreEqual(0, src.transform.childCount, "SRC must have 0 children");
                Assert.IsNotNull(ComponentSerializer.FindObject("/SeqDST/SeqA"));
                Assert.IsNotNull(ComponentSerializer.FindObject("/SeqDST/SeqB"));
                Assert.IsNotNull(ComponentSerializer.FindObject("/SeqDST/SeqC"));
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
            }
        }

        [Test]
        public void DeleteObject_NonEmpty_RefusesWithoutForce()
        {
            var container = new GameObject("DelContainer");
            var c1 = new GameObject("DelChild1");
            var c2 = new GameObject("DelChild2");
            c1.transform.SetParent(container.transform);
            c2.transform.SetParent(container.transform);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("children"));
                var result = Process("delete_object", "{\"path\":\"/DelContainer\"}");
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("children", result);
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void DeleteObject_NonEmpty_SucceedsWithForce()
        {
            var container = new GameObject("DelForceContainer");
            var c1 = new GameObject("DelForceChild1");
            var c2 = new GameObject("DelForceChild2");
            c1.transform.SetParent(container.transform);
            c2.transform.SetParent(container.transform);

            var result = Process("delete_object", "{\"path\":\"/DelForceContainer\",\"force\":\"true\"}");
            StringAssert.Contains("\"ok\":true", result);
            // container is destroyed, no cleanup needed
        }

        [Test]
        public void DeleteObject_Leaf_SucceedsWithoutForce()
        {
            var leaf = new GameObject("DelLeaf");
            var result = Process("delete_object", "{\"path\":\"/DelLeaf\"}");
            StringAssert.Contains("\"ok\":true", result);
            // destroyed by DeleteObject
        }

        [Test]
        public void DeleteObject_StalePathStrict()
        {
            // Create /StaleA/StaleTarget and /StaleB/StaleTarget
            // Reparent StaleA under /StaleC → /StaleA no longer at root
            var a = new GameObject("StaleA");
            var target = new GameObject("StaleTarget");
            target.transform.SetParent(a.transform);
            var c = new GameObject("StaleC");
            a.transform.SetParent(c.transform); // now /StaleC/StaleA/StaleTarget
            try
            {
                // /StaleA/StaleTarget is a stale path — strict lookup must fail
                LogAssert.Expect(LogType.Warning, new Regex("not found"));
                var result = Process("delete_object", "{\"path\":\"/StaleA/StaleTarget\"}");
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(c);
            }
        }
    }
}
