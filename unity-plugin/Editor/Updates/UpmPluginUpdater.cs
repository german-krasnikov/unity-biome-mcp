using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class UpmPluginUpdater
    {
        const string EditorPkg = "unity-plugin";
        const string ReloadPkg = "unity-plugin-reload";

#if UNITY_INCLUDE_TESTS
        internal static System.Func<double> _timeProvider = () => EditorApplication.timeSinceStartup;
        static double GetTime() => _timeProvider();
#else
        static double GetTime() => EditorApplication.timeSinceStartup;
#endif

        /// <summary>Build UPM git URL for a package path + version tag.</summary>
        internal static string BuildUrl(string packagePath, string version) =>
            UpdateChecker.RepoGitUrl + $"?path={packagePath}#{(version.StartsWith("v") ? version : "v" + version)}";

        /// <summary>Trigger UPM to update both editor + reload packages via git URL.</summary>
        internal static void Update(string version, System.Action<bool> onComplete = null,
            double timeoutSeconds = 120.0)
        {
            if (string.IsNullOrEmpty(version))
            {
                Debug.LogError($"{BiomeLabel.Tag} No version specified.");
                onComplete?.Invoke(false);
                return;
            }

            var url = BuildUrl(EditorPkg, version);
            var req = Client.Add(url);
            var startTime = GetTime();
            EditorApplication.update += Poll;

            void Poll()
            {
                if (GetTime() - startTime > timeoutSeconds)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError($"{BiomeLabel.Tag} UPM update timed out after {timeoutSeconds}s.");
                    onComplete?.Invoke(false);
                    return;
                }
                if (!req.IsCompleted) return;
                EditorApplication.update -= Poll;

                if (req.Status == StatusCode.Failure)
                {
                    Debug.LogError($"{BiomeLabel.Tag} UPM add failed: {req.Error?.message}");
                    onComplete?.Invoke(false);
                    return;
                }

                // Chain: add reload package after editor package resolves
                var reloadUrl = BuildUrl(ReloadPkg, version);
                var reloadReq = Client.Add(reloadUrl);
                var reloadStart = GetTime();
                EditorApplication.update += PollReload;

                void PollReload()
                {
                    if (GetTime() - reloadStart > timeoutSeconds)
                    {
                        EditorApplication.update -= PollReload;
                        Debug.LogError($"{BiomeLabel.Tag} Reload package timed out.");
                        onComplete?.Invoke(false);
                        return;
                    }
                    if (!reloadReq.IsCompleted) return;
                    EditorApplication.update -= PollReload;
                    if (reloadReq.Status == StatusCode.Failure)
                    {
                        Debug.LogError($"{BiomeLabel.Tag} Reload package failed: {reloadReq.Error?.message}");
                        onComplete?.Invoke(false);
                    }
                    else
                    {
                        onComplete?.Invoke(true);
                    }
                }
            }
        }
    }
}
