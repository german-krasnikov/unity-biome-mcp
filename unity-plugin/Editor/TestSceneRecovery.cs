using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.TestRuns
{
    public sealed class TestSceneRecoveryPlan
    {
        public readonly List<string> TemporaryPaths = new List<string>();
        public readonly List<string> RecoveryPaths = new List<string>();
    }

    /// <summary>
    /// Persists scenes that cannot be discarded without risking user work. Recovery
    /// assets are deliberately outside the automatic test ownership root.
    /// </summary>
    public static class TestSceneRecovery
    {
        public const string RecoveryRoot = "Assets/UnityMCPRecovery/TestRuns";

        public static TestSceneRecoveryPlan PrepareForDiscard(
            IEnumerable<Scene> scenes,
            string runId)
        {
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            var safeRunId = SafeFileName(runId, "run");
            var plan = new TestSceneRecoveryPlan();

            foreach (var scene in scenes)
            {
                if (!scene.IsValid() || !scene.isLoaded)
                    throw new InvalidOperationException(
                        "An invalid or unloaded scene cannot be prepared for recovery.");

                if (string.IsNullOrEmpty(scene.path))
                {
                    var wasDirty = scene.isDirty;
                    var root = wasDirty
                        ? RecoveryRoot + "/" + safeRunId
                        : TestRunAssetOwnership.Root;
                    EnsureAssetDirectory(root);
                    var path = root + "/" +
                        (wasDirty ? "recovered-untitled-" : "temporary-untitled-") +
                        Guid.NewGuid().ToString("N") + ".unity";
                    if (!EditorSceneManager.SaveScene(scene, path))
                        throw new InvalidOperationException(
                            "Could not persist an untitled scene before explicit close.");
                    if (wasDirty)
                        plan.RecoveryPaths.Add(path);
                    else
                        plan.TemporaryPaths.Add(path);
                    continue;
                }

                if (!scene.isDirty || IsTestOwned(scene.path)) continue;
                var recoveryDir = RecoveryRoot + "/" + safeRunId;
                EnsureAssetDirectory(recoveryDir);
                var recoveryPath = recoveryDir + "/recovered-" +
                    SafeFileName(Path.GetFileNameWithoutExtension(scene.path), "scene") + "-" +
                    Guid.NewGuid().ToString("N") + ".unity";
                // Save a copy without retargeting or cleaning the user's open
                // scene. Explicit CloseScene below discards only the test delta.
                if (!EditorSceneManager.SaveScene(scene, recoveryPath, true))
                    throw new InvalidOperationException(
                        "Could not create a recovery copy for dirty scene: " + scene.path);
                plan.RecoveryPaths.Add(recoveryPath);
            }

            return plan;
        }

        private static bool IsTestOwned(string path) =>
            path.Equals(TestRunAssetOwnership.Root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(TestRunAssetOwnership.Root + "/",
                StringComparison.OrdinalIgnoreCase);

        private static void EnsureAssetDirectory(string assetDirectory)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.Combine(projectRoot, assetDirectory));
        }

        private static string SafeFileName(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (invalid.Contains(chars[i]) || chars[i] == '/' || chars[i] == '\\')
                    chars[i] = '_';
            var result = new string(chars).Trim();
            return string.IsNullOrEmpty(result) ? fallback : result;
        }
    }
}
