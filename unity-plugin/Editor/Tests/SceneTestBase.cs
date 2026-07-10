using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    public abstract class SceneTestBase
    {
        [TearDown]
        public void CleanDirtyScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
