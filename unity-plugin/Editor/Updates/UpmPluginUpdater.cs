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

        /// <summary>
        /// Actionable text for the most recent failure/timeout/busy-block (ARC-10 T3),
        /// for UI display alongside the existing Console log. Null only before the
        /// first Update() call in this session; every terminal path sets it (cleared
        /// to null on success).
        /// </summary>
        internal static string LastFailureReason;

        /// <summary>Build UPM git URL for a package path + version tag.</summary>
        internal static string BuildUrl(string packagePath, string version) =>
            UpdateChecker.RepoGitUrl + $"?path={packagePath}#{(version.StartsWith("v") ? version : "v" + version)}";

        /// <summary>Composes an actionable failure reason from a raw UPM error message.</summary>
        internal static string BuildFailureReason(string version, string rawMessage) =>
            UpmErrorClassifier.ActionableText(UpmErrorClassifier.Classify(rawMessage), version, rawMessage);

        /// <summary>
        /// Shared terminal-branch bookkeeping for <see cref="Update"/>: releases the
        /// guard claimed by <c>TryBegin</c>, records/logs the failure reason (or clears
        /// it on success), and invokes the caller's callback exactly once. Must only be
        /// called from a branch that successfully claimed the guard — never from the
        /// busy-block early return, which would release another caller's claim.
        /// </summary>
        internal static void FinishUpdate(bool success, string failureReason, System.Action<bool> onComplete)
        {
            if (success)
            {
                LastFailureReason = null;
            }
            else
            {
                LastFailureReason = failureReason;
                Debug.LogError($"{BiomeLabel.Tag} {failureReason}");
            }
            UpmOperationGuard.Complete();
            onComplete?.Invoke(success);
        }

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

            if (!UpmOperationGuard.TryBegin(version))
            {
                // Someone else holds the guard — do NOT call Complete() here, that would
                // release the other caller's claim, not ours (we never took one).
                LastFailureReason = $"Another plugin update (v{UpmOperationGuard.InFlightVersion}) " +
                                     "is already in progress. Wait for it to finish, then try again.";
                Debug.LogError($"{BiomeLabel.Tag} {LastFailureReason}");
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
                    FinishUpdate(false, $"UPM update timed out after {timeoutSeconds}s.", onComplete);
                    return;
                }
                if (!req.IsCompleted) return;
                EditorApplication.update -= Poll;

                if (req.Status == StatusCode.Failure)
                {
                    FinishUpdate(false, BuildFailureReason(version, req.Error?.message), onComplete);
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
                        FinishUpdate(false, $"Reload package update timed out after {timeoutSeconds}s.", onComplete);
                        return;
                    }
                    if (!reloadReq.IsCompleted) return;
                    EditorApplication.update -= PollReload;
                    if (reloadReq.Status == StatusCode.Failure)
                    {
                        FinishUpdate(false, BuildFailureReason(version, reloadReq.Error?.message), onComplete);
                    }
                    else
                    {
                        FinishUpdate(true, null, onComplete);
                    }
                }
            }
        }
    }
}
