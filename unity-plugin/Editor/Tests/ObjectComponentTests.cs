// TDD — EditMode tests for create_object edge cases.
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ObjectComponentTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "DupObj")
                    Object.DestroyImmediate(go);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ── CreateObject with duplicate name ─────────────────────────────────

        [Test]
        public void CreateObjectWithDuplicateName_ReturnsCreatedResponse()
        {
            // Arrange — create first object so a duplicate exists
            var first = new GameObject("DupObj");

            // Act — create second via CommandRouter (exercises ExecCreateObject path)
            var response = CommandRouter.Process(
                "{\"id\":\"t1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"DupObj\"}}");

            // Assert — response must report success, not an ambiguity error
            StringAssert.Contains("Created", response, "Expected 'Created' in response; got: " + response);
            StringAssert.DoesNotContain("Ambiguous", response);
            StringAssert.DoesNotContain("\"ok\":false", response);

            Object.DestroyImmediate(first);
        }
    }
}
