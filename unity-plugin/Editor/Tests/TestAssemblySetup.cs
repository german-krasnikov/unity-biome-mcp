using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;

// [SetUpFixture] without namespace = applies to entire assembly
[SetUpFixture]
public class TestAssemblySetup
{
    private string _preTestScenePath;

    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        _preTestScenePath = SceneManager.GetActiveScene().path;
        CommandRegistry.InitDefaults();
        UnityMCP.Editor.Tests.TestPaths.DeleteRoot();
    }

    [OneTimeTearDown]
    public void GlobalTearDown()
    {
        UnityMCP.Editor.Tests.TestPaths.DeleteRoot();
        Undo.ClearAll();
        UnityMCP.Editor.Tests.SceneDirtiedGuard.ClearAllScenesDirty();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (!string.IsNullOrEmpty(_preTestScenePath))
            EditorSceneManager.OpenScene(_preTestScenePath);
    }
}
