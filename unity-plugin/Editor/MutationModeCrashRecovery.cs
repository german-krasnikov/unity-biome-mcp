using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Detects crash/restart where Mutation Mode EditorPref says ON but SessionState was cleared.
    /// Restores a safe state (MM off, kAutoRefresh restored) so the Editor doesn't start broken.
    /// </summary>
    [InitializeOnLoad]
    static class MutationModeCrashRecovery
    {
        static MutationModeCrashRecovery() => RecoverIfNeeded();

        internal static bool RecoverIfNeeded()
        {
            if (!MCPSettings.GetMutationMode() || AutoRefreshGuard.IsApplied)
                return false;
            // HR package manages kAutoRefresh itself — don't interfere
            if (HotReloadDetector.IsPackageInstalled())
                return false;

            // Crash detected: MM EditorPref=ON but SessionState lost (guards not applied)
            MCPSettings.SetMutationMode(false);
            MCPSettings.SetFastPlayMode(false);

            // Restore kAutoRefresh to safe default (original value lost with SessionState)
            EditorPrefs.SetInt("kAutoRefresh", 1);
            EditorPrefs.SetInt("kAutoRefreshMode", 0); // 0 = Enabled

            // Restore EditorSettings (FastPlayMode may have left DisableDomainReload set)
            EditorSettings.enterPlayModeOptionsEnabled = false;

            Debug.Log($"{BiomeLabel.Tag} Mutation Mode disabled after crash recovery — all settings restored");
            return true;
        }
    }
}
