using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    public abstract class SceneTestBase
    {
        [TearDown]
        public void CleanDirtyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll(); // reset dirty flag — Undo stack persists across NewScene in Unity 6
        }
    }
}
