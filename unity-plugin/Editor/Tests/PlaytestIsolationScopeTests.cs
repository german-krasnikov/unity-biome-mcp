// TDD: B07 — an Edit-mode playtest must refuse to run against a dirty scene, since it
// mutates persisted scene state directly (no Play-mode reload isolation to fall back on).
// B08 — that same Edit-mode run gets an Undo group so abort/timeout/outer-catch can revert
// whatever it mutated instead of leaving the scene in a half-run state.
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        // ── B08: OpenGroup/RevertGroup wrappers ─────────────────────────────────

        [Test]
        public void OpenAndRevert_MutationThenRevert_UndoesChange()
        {
            int groupId = PlaytestIsolationScope.OpenGroup("Test: OpenAndRevert");

            var go = TrackOwnedObject(new GameObject("PIS_RevertTest"));
            Undo.RegisterCreatedObjectUndo(go, "create PIS_RevertTest");

            PlaytestIsolationScope.RevertGroup(groupId);

            Assert.IsTrue(go == null, "Object created inside the group should be gone after real RevertToBeforeGroup");
        }

        // ── B08: PlaytestRunner opens/reverts the group around an Edit-mode run ─

        [Test]
        public async Task Run_EditModeAbortOnFail_RevertsGroup()
        {
            var originalRevert = UndoGroupHelper.RevertToGroupAction;
            int spiedGroupId = -1;
            UndoGroupHelper.RevertToGroupAction = id => spiedGroupId = id;
            try
            {
                var target = TrackOwnedObject(new GameObject("PIS_AbortMutationTarget"));

                // Step 1 is an Edit-safe mutation (SET_ACTIVE — the DSL's generic `MCP` verb
                // arrives in Wave C); step 2 targets a nonexistent object so it fails, and with
                // abort_on_fail the run must abort and revert the group opened for it.
                var script = $"SET_ACTIVE /{target.name} false\nSET_ACTIVE /PIS_DoesNotExist true";
                var tcs = new TaskCompletionSource<string>();
                PlaytestRunner.Run(script, 5f, tcs, abortOnFail: true, requiresPlayMode: false);
                var result = await AwaitBoundedAsync(tcs);

                StringAssert.Contains("[2]", result);
                Assert.GreaterOrEqual(spiedGroupId, 0,
                    "Expected PlaytestRunner to revert the Edit-mode undo group opened for this run");
            }
            finally
            {
                UndoGroupHelper.RevertToGroupAction = originalRevert;
            }
        }

        // Bounded wait shared with PlaytestRunnerEditModeTests.cs — races the TCS against a
        // fixed timeout rather than an unbounded spin (Tick() completion rides EditorApplication.update).
        private static async Task<string> AwaitBoundedAsync(TaskCompletionSource<string> tcs, double timeoutSeconds = 5.0)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }
    }
}
