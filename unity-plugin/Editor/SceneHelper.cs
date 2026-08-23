using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor
{
    public static class SceneHelper
    {
        /// <summary>Returns true if any loaded scene has unsaved changes.</summary>
        internal static bool HasDirtyScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return true;
            return false;
        }

        /// <summary>Find loaded scene by path first, then by name.</summary>
        private static Scene FindScene(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new System.ArgumentException("identifier required");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == identifier || s.name == identifier)
                    return s;
            }
            throw new System.ArgumentException($"Scene not found or not loaded: {identifier}");
        }

        public static string OpenAdditive(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new System.ArgumentException("path required");
            if (!System.IO.File.Exists(path))
                throw new System.ArgumentException($"Scene file not found: {path}");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return scene.name;
        }

        public static string CloseScene(string identifier)
        {
            if (SceneManager.sceneCount <= 1)
                throw new System.InvalidOperationException("Cannot close the only loaded scene");
            var scene = FindScene(identifier);
            var name = scene.name;
            // If closing the active scene, promote another
            if (SceneManager.GetActiveScene() == scene)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var other = SceneManager.GetSceneAt(i);
                    if (other != scene) { SceneManager.SetActiveScene(other); break; }
                }
            }
            EditorSceneManager.CloseScene(scene, true);
            return $"Closed: {name}";
        }

        public static string SetActiveScene(string identifier)
        {
            var scene = FindScene(identifier);
            if (!scene.isLoaded)
                throw new System.ArgumentException($"Scene '{identifier}' is not loaded");
            SceneManager.SetActiveScene(scene);
            return scene.name;
        }

        public static string ListScenes()
        {
            var sb = new StringBuilder();
            var active = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                var prefix = s == active ? "* " : "  ";
                var objCount = s.rootCount;
                var dirty = s.isDirty ? " [dirty]" : "";
                var path = string.IsNullOrEmpty(s.path) ? "(unsaved)" : s.path;
                sb.AppendLine($"{prefix}{s.name}  {path}  {objCount} objs{dirty}");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Creates a new empty scene, discarding current changes without save dialog.
        /// </summary>
        public static string NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            return scene.name;
        }

        /// <summary>
        /// Saves the specified scene (or active scene if identifier is null).
        /// If path is null, saves to the scene's current path.
        /// For untitled scenes, path is required.
        /// </summary>
        public static string SaveScene(string path, string identifier = null)
        {
            var scene = string.IsNullOrEmpty(identifier)
                ? SceneManager.GetActiveScene()
                : FindScene(identifier);
            if (!string.IsNullOrEmpty(path))
            {
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new System.IO.IOException($"Unity failed to save scene to '{path}'.");
                return path;
            }
            if (string.IsNullOrEmpty(scene.path))
                throw new System.ArgumentException("untitled scene, path required");
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.IO.IOException($"Unity failed to save scene to '{scene.path}'.");
            return scene.path;
        }

        /// <summary>
        /// Saves a copy of the scene to destinationPath without changing the active scene
        /// reference or dirty flag. identifier selects a scene in multi-scene setups.
        /// </summary>
        public static string SaveCopy(string destinationPath, string identifier = null)
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new System.ArgumentException("destination_path required");
            if (!destinationPath.StartsWith("Assets/", System.StringComparison.Ordinal))
                throw new System.ArgumentException("destination_path must start with Assets/");
            if (!destinationPath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                throw new System.ArgumentException("destination_path must end with .unity");

            var scene = string.IsNullOrEmpty(identifier)
                ? SceneManager.GetActiveScene()
                : FindScene(identifier);

            if (!string.IsNullOrEmpty(scene.path) &&
                string.Equals(scene.path, destinationPath, System.StringComparison.OrdinalIgnoreCase))
                throw new System.ArgumentException("destination_path must not be the active scene path");

            var projectRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, ".."));
            var absPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(projectRoot, destinationPath));

            if (!absPath.StartsWith(projectRoot + System.IO.Path.DirectorySeparatorChar,
                    System.StringComparison.Ordinal))
                throw new System.ArgumentException("destination_path must be inside project");

            var dir = System.IO.Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);

            // saveAsCopy=true: writes copy, does NOT change scene.path or dirty flag
            if (!EditorSceneManager.SaveScene(scene, destinationPath, saveAsCopy: true))
                throw new System.IO.IOException($"Unity failed to write copy to '{destinationPath}'.");

            long sizeBytes = System.IO.File.Exists(absPath)
                ? new System.IO.FileInfo(absPath).Length : 0;

            return $"ok path={destinationPath} size={sizeBytes} scene={scene.name}";
        }

        /// <summary>
        /// Opens a scene by path, discarding dirty state first to prevent save dialog.
        /// </summary>
        public static string OpenScene(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new System.ArgumentException("path required");

            if (!System.IO.File.Exists(path))
                throw new System.ArgumentException($"Scene not found: {path}");

            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Cannot open scenes during Play Mode");

            var current = SceneManager.GetActiveScene();
            bool fileMissing = !string.IsNullOrEmpty(current.path)
                && AssetDatabase.AssetPathToGUID(current.path) == "";

            if (current.isDirty || fileMissing)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            current = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(current.path))
            {
                TestRunAssetOwnership.EnsureRoot();
                EditorSceneManager.SaveScene(current, TestRunner.TempScenePath, false);
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return scene.name;
        }

        /// <summary>
        /// Discards changes. When identifier is provided, reloads only that scene (additive).
        /// Without identifier: single-scene discard (legacy behavior).
        /// </summary>
        public static string DiscardChanges(string identifier = null)
        {
            if (!string.IsNullOrEmpty(identifier))
            {
                var target = FindScene(identifier);
                var path = target.path;
                if (string.IsNullOrEmpty(path))
                    throw new System.ArgumentException($"Scene '{identifier}' has no path, cannot discard");
                EditorSceneManager.CloseScene(target, true);
                var reloaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                // G21: verify the scene settled (isLoaded) before reporting success
                if (!reloaded.isLoaded)
                    throw new System.InvalidOperationException($"Scene '{path}' failed to load after discard");
                return "reloaded";
            }

            var scene = SceneManager.GetActiveScene();
            var scenePath = scene.path;

            // NewScene silently discards dirty state — no save dialog
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (!string.IsNullOrEmpty(scenePath))
            {
                var reloaded = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                // G21: verify the scene settled before reporting success
                if (!reloaded.isLoaded)
                    throw new System.InvalidOperationException($"Scene '{scenePath}' failed to load after discard");
                return "reloaded";
            }
            return "new scene";
        }
    }
}
