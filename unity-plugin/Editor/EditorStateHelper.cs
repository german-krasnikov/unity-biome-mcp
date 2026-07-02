using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class EditorStateHelper
    {
        public static string GetState()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"playing:{EditorApplication.isPlaying}");
            sb.AppendLine($"paused:{EditorApplication.isPaused}");
            sb.AppendLine($"compiling:{EditorApplication.isCompiling}");
            var scene = SceneManager.GetActiveScene();
            sb.AppendLine($"scene:{scene.path}");
            sb.AppendLine($"dirty:{scene.isDirty}");
            if (Selection.activeGameObject != null)
            {
                var selPath = ComponentSerializer.GetPath(Selection.activeGameObject);
                if (!string.IsNullOrEmpty(selPath))
                    sb.AppendLine($"selected:{selPath}");
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                sb.AppendLine($"prefab:{stage.assetPath}");
            return sb.ToString().TrimEnd();
        }

        public static string PingObject(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            EditorGUIUtility.PingObject(go);
            Selection.activeGameObject = go;
            return $"pinged:{ComponentSerializer.GetPath(go)}";
        }

        public static string GetSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) return "none";
            var path = ComponentSerializer.GetPath(go);
            var comps = ComponentSerializer.ListComponents(path);
            return string.IsNullOrEmpty(comps) ? $"path:{path}" : $"path:{path}\n{comps}";
        }

        public static string Control(string action, string path)
        {
            switch (action)
            {
                case "play":
                    EditorApplication.isPlaying = true;
                    return "ok";
                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return "ok";
                case "stop":
                    EditorApplication.isPlaying = false;
                    return "ok";
                case "select":
                    if (string.IsNullOrEmpty(path))
                        throw new System.ArgumentException("path required for select action");
                    var go = ComponentSerializer.FindObject(path);
                    if (go == null)
                        throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
                    Selection.activeGameObject = go;
                    return $"selected:{ComponentSerializer.GetPath(go)}";
                case "project_path":
                    return System.IO.Path.GetDirectoryName(Application.dataPath);
                default:
                    throw new System.ArgumentException(
                        ErrorHelper.InvalidAction(action, new[] { "state", "play", "pause", "stop", "select", "project_path" }));
            }
        }
    }
}
