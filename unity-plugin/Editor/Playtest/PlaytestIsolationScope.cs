using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Edit-mode playtest pre-flight (B07): an Edit-mode script mutates persisted scene
    /// state directly (no Play-mode reload isolation), so it must never run against a
    /// dirty scene — that would silently mix its own mutations into unsaved user edits.
    /// </summary>
    internal static class PlaytestIsolationScope
    {
        /// <summary>
        /// Returns null when every loaded scene is clean; otherwise an "err:" message
        /// naming the first dirty scene found. Checks every loaded scene, so multi-scene
        /// setups are covered without special-casing.
        /// </summary>
        internal static string RefuseIfDirty()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                    return $"err: scene '{scene.name}' is dirty. Save or discard changes before running an Edit-mode playtest.";
            }
            return null;
        }

        /// <summary>
        /// Opens a named Undo group for an Edit-mode playtest run. Thin wrapper over
        /// <see cref="UndoGroupHelper.OpenNamedGroup"/> — pair with <see cref="RevertGroup"/>
        /// on abort/timeout so an interrupted run cannot leave partial mutations behind.
        /// </summary>
        internal static int OpenGroup(string name) => UndoGroupHelper.OpenNamedGroup(name);

        /// <summary>
        /// Reverts every Undo-recorded mutation made since <paramref name="groupId"/> was opened.
        /// Safe to call with an unopened group id (guarded by <see cref="UndoGroupHelper.CanRevert"/>) —
        /// callers do not need to track whether a group was actually opened for this run.
        /// INVOKE-driven mutations are not Undo-recorded and are NOT covered by this revert.
        /// </summary>
        internal static void RevertGroup(int groupId)
        {
            if (UndoGroupHelper.CanRevert(groupId))
                UndoGroupHelper.RevertToBeforeGroup(groupId);
        }
    }
}
