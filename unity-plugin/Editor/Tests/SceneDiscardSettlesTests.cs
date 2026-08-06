// G21: scene(action="discard") must verify the scene is loaded before returning success.
// The fix: use the return value of EditorSceneManager.OpenScene to check isLoaded.
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    [UnityMCP.Editor.Testing.BiomeWorkerOnly("Discards the active scene; disposable worker required")]
    public class SceneDiscardSettlesTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void DiscardChanges_NoPath_ActiveSceneIsLoadedAndClean()
        {
            // G21: Discard without a scene path → creates a new empty scene.
            // After the fix, we verify scene state is settled before returning.
            var result = SceneHelper.DiscardChanges(null);

            Assert.AreEqual("new scene", result);
            var scene = SceneManager.GetActiveScene();
            Assert.IsTrue(scene.isLoaded, "Scene must be loaded after DiscardChanges");
            Assert.IsFalse(scene.isDirty, "Scene must not be dirty after DiscardChanges");
        }

        [Test]
        public void DiscardChanges_WithSavedScenePath_ReloadedSceneIsLoaded()
        {
            // Save the current scene first to give it a path
            var tmpPath = "Assets/TestsTemp/SceneDiscardSettleTest.unity";
            TrackOwnedAsset(tmpPath);
            TestPaths.EnsureFolder("Assets/TestsTemp");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, tmpPath);

            // Now discard (reload from saved path)
            var result = SceneHelper.DiscardChanges(null);
            Assert.That(result == "reloaded" || result == "new scene",
                $"DiscardChanges should report either 'reloaded' or 'new scene', got: {result}");

            var activeScene = SceneManager.GetActiveScene();
            Assert.IsTrue(activeScene.isLoaded, "Scene must be loaded after discard");
        }
    }
}
