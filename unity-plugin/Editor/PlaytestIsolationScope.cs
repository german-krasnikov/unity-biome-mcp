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
    }
}
