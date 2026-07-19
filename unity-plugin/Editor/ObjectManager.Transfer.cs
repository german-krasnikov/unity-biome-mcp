using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        private static Scene FindLoadedScene(string name)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == name || s.path == name)
                    return s;
            }
            throw new ArgumentException($"Scene not found or not loaded: {name}");
        }

        private static void ApplyParent(GameObject go, string newParent, bool worldPositionStays, Scene? expectedScene = null)
        {
            if (string.IsNullOrEmpty(newParent)) return;
            var parentGo = ComponentSerializer.FindObjectOrThrow(newParent);
            if (expectedScene.HasValue && parentGo.scene != expectedScene.Value)
                throw new ArgumentException(
                    $"parent '{newParent}' is in scene '{parentGo.scene.name}', " +
                    $"not target scene '{expectedScene.Value.name}'. " +
                    $"Use a parent path inside '{expectedScene.Value.name}'.");
            Undo.SetTransformParent(go.transform, parentGo.transform, worldPositionStays, $"Set parent {go.name}");
        }

        public static string TransferObject(string sourcePath, string action,
            string targetSceneName, string newParent, bool worldPositionStays)
        {
            var go = ComponentSerializer.FindObjectOrThrow(sourcePath);
            var targetScene = string.IsNullOrEmpty(targetSceneName)
                ? go.scene
                : FindLoadedScene(targetSceneName);

            switch (action)
            {
                case "move":
                    Undo.SetTransformParent(go.transform, null, worldPositionStays, $"Unparent {go.name}");
                    SceneManager.MoveGameObjectToScene(go, targetScene);
                    ApplyParent(go, newParent, worldPositionStays, expectedScene: targetScene);
                    EditorUtility.SetDirty(go);
                    if (!EditorApplication.isPlaying)
                        EditorSceneManager.MarkSceneDirty(targetScene);
                    return $"Moved {sourcePath} → {targetScene.name}";

                case "copy":
                    var previousActive = SceneManager.GetActiveScene();
                    GameObject clone;
                    try
                    {
                        if (targetScene.IsValid())
                            SceneManager.SetActiveScene(targetScene);
                        clone = UnityEngine.Object.Instantiate(go);
                    }
                    finally
                    {
                        if (previousActive.IsValid())
                            SceneManager.SetActiveScene(previousActive);
                    }
                    clone.name = go.name;
                    Undo.RegisterCreatedObjectUndo(clone, $"Copy {go.name}");
                    if (clone.scene != targetScene)
                        SceneManager.MoveGameObjectToScene(clone, targetScene);
                    ApplyParent(clone, newParent, worldPositionStays, expectedScene: targetScene);
                    EditorUtility.SetDirty(clone);
                    if (!EditorApplication.isPlaying)
                        EditorSceneManager.MarkSceneDirty(targetScene);
                    return $"Copied {sourcePath} → {ComponentSerializer.GetPath(clone)}";

                default:
                    throw new ArgumentException($"Invalid action: {action}. Must be move or copy");
            }
        }
    }
}
