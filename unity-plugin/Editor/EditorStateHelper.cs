using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class EditorStateHelper
    {
        // Testable seams — tests inject false/no-op to avoid entering real Play Mode.
        internal static System.Func<bool> GetIsPlaying = () => EditorApplication.isPlaying;
        internal static System.Action<bool> SetIsPlaying = v => { EditorApplication.isPlaying = v; };

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

        public static string Control(string action, string path, string argsJson = null)
        {
            switch (action)
            {
                case "play":
                    if (GetIsPlaying())
                        return "already_playing";
                    SetIsPlaying(true);
                    return "requested";
                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return "ok";
                case "stop":
                    EditorApplication.isPlaying = false;
                    return "ok";
                case "select":
                    var paths = argsJson != null ? JsonHelper.ExtractString(argsJson, "paths") : null;
                    if (!string.IsNullOrEmpty(paths))
                        return SelectMulti(paths);
                    if (string.IsNullOrEmpty(path))
                        throw new System.ArgumentException("path or paths required for select");
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

        private static string SelectMulti(string commaList)
        {
            var parts = commaList.Split(',');
            var objects = new System.Collections.Generic.List<Object>();
            var notFound = new System.Collections.Generic.List<string>();
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var found = ComponentSerializer.FindObject(trimmed);
                if (found == null) notFound.Add(trimmed);
                else objects.Add(found);
            }
            if (objects.Count == 0)
                throw new System.ArgumentException($"No objects found: {commaList}");
            Selection.objects = objects.ToArray();
            var sb = new StringBuilder();
            sb.Append($"ok:selected {objects.Count}");
            foreach (var o in objects) sb.Append('\n').Append(ComponentSerializer.GetPath((GameObject)o));
            if (notFound.Count > 0) sb.Append('\n').Append($"not_found:{string.Join(",", notFound)}");
            return sb.ToString();
        }
    }
}
