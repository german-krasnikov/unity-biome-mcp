// TDD: Verify correct cleanup order (NewScene → Undo.ClearAll) leaves scene not dirty.
// Pins H1 (SceneCleanTestBase) and H2 (UndoGroupHelperTests) fixes.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class CleanupOrderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // H1/H2-a: Correct order (NewScene → ClearAll) must leave scene clean.
        [Test]
        public void CleanupOrder_NewSceneThenClearAll_LeavesSceneClean()
        {
            var go = new GameObject("leak_candidate");
            Undo.RegisterCreatedObjectUndo(go, "create for test");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();

            Assert.IsFalse(SceneManager.GetActiveScene().isDirty,
                "Correct order (NewScene→ClearAll) must leave new scene not dirty. " +
                "Undo.ClearAll after NewScene clears the NewScene undo entry itself.");
        }

        // H1-c: SceneCleanTestBase.AssertNoSceneLeaks must leave scene not dirty after fix.
        // No leaked objects — AssertNoSceneLeaks must pass and leave scene clean.
        [Test]
        public void SceneCleanTestBase_AssertNoSceneLeaks_NewSceneIsNotDirtyAfterward()
        {
            var baseType = typeof(SceneCleanTestBase);
            var snapshotMethod = baseType.GetMethod("SnapshotSceneRoots",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var cleanupMethod = baseType.GetMethod("AssertNoSceneLeaks",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            // Find a concrete subclass at runtime
            var concreteType = System.Array.Find(
                System.Reflection.Assembly.GetExecutingAssembly().GetTypes(),
                t => !t.IsAbstract && t.IsSubclassOf(baseType));

            if (concreteType == null)
                Assert.Inconclusive("No concrete SceneCleanTestBase subclass found in test assembly");

            var instance = System.Activator.CreateInstance(concreteType);
            snapshotMethod?.Invoke(instance, null);
            // No leaked objects — AssertNoSceneLeaks must not throw and must leave scene clean

            Assert.DoesNotThrow(() => cleanupMethod?.Invoke(instance, null),
                "AssertNoSceneLeaks must not throw when there are no leaks");

            Assert.IsFalse(SceneManager.GetActiveScene().isDirty,
                "After AssertNoSceneLeaks (H1 fix: NewScene before ClearAll), scene must not be dirty.");
        }
    }
}
