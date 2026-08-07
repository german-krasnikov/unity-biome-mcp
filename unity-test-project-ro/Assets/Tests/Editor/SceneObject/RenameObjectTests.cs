using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class RenameObjectTests : SceneTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/RenameObjectTests";

        private static string Process(string cmd, string argsJson) =>
            CommandRouter.Process($"{{\"id\":\"ro\",\"cmd\":\"{cmd}\",\"args\":{argsJson}}}");

        private UnityEngine.SceneManagement.Scene CreateOwnedScene(
            string fileName,
            GameObject root)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = CreateOwnedAdditiveScene();
            try
            {
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
        public void RenameObject_Basic()
        {
            var go = new GameObject("Grunt");
            try
            {
                ObjectManager.RenameObject("/Grunt", "Boss");
                Assert.AreEqual("Boss", go.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RenameObject_ReturnsNewPath()
        {
            var go = new GameObject("Grunt2");
            try
            {
                var result = ObjectManager.RenameObject("/Grunt2", "Boss2");
                Assert.AreEqual("/Boss2", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RenameObject_MarksSceneDirty()
        {
            var go = new GameObject("RenDirty");
            try
            {
                var scene = CreateOwnedScene("rename-dirty.unity", go);
                Assert.IsFalse(scene.isDirty, "Scene must be clean after save");

                ObjectManager.RenameObject("/RenDirty", "RenDirtyRenamed");
                Assert.IsTrue(go.scene.isDirty, "Scene must be dirty after rename");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RenameObject_EmptyName_Throws()
        {
            var go = new GameObject("RenEmpty");
            try
            {
                Assert.Throws<ArgumentException>(() => ObjectManager.RenameObject("/RenEmpty", ""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RenameObject_Undo()
        {
            var go = new GameObject("RenUndo");
            try
            {
                ObjectManager.RenameObject("/RenUndo", "RenUndoNew");
                Assert.AreEqual("RenUndoNew", go.name);

                Undo.PerformUndo();
                Assert.AreEqual("RenUndo", go.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
