using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Utility to clear scene dirty flags via internal ClearSceneDirtiness API.
    /// Called at test-run boundaries (GlobalTearDown) to prevent "Save Scene?" popups.
    /// Does NOT subscribe to sceneDirtied — that breaks tests verifying isDirty.
    /// </summary>
    internal static class SceneDirtiedGuard
    {
        private static readonly MethodInfo _clearMethod;

        static SceneDirtiedGuard()
        {
            _clearMethod = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Scene) }, null);
            if (_clearMethod == null)
                Debug.LogWarning("[MCP] SceneDirtiedGuard: ClearSceneDirtiness not found");
        }

        internal static void ClearAllScenesDirty()
        {
            if (_clearMethod == null) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty)
                    _clearMethod.Invoke(null, new object[] { s });
            }
        }
    }
}
