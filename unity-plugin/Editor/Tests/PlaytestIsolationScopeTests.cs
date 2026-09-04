// TDD: B07 — an Edit-mode playtest must refuse to run against a dirty scene, since it
// mutates persisted scene state directly (no Play-mode reload isolation to fall back on).
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestIsolationScopeTests : SceneTestBase
    {
        [Test]
        public void RefuseIfDirty_CleanScene_ReturnsNull()
        {
            Assert.IsFalse(SceneManager.GetActiveScene().isDirty,
                "Precondition: fixture-owned scene starts clean");
            Assert.IsNull(PlaytestIsolationScope.RefuseIfDirty());
        }

        [Test]
        public void RefuseIfDirty_DirtyScene_ReturnsError()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            var result = PlaytestIsolationScope.RefuseIfDirty();

            StringAssert.Contains("dirty", result);
            CleanDirtyScene(); // restore clean state before the fixture's own teardown runs
        }
    }
}
