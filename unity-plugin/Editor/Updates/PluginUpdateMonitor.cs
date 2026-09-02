using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Detects plugin version bump after a domain reload, releases the
    /// <see cref="UpmOperationGuard"/> claim so LevelUpPanel/VersionPickerPage stop
    /// showing an "update in progress" state, and logs a notification. UpmPluginUpdater's
    /// own success path releases the guard from a Poll/PollReload closure on
    /// EditorApplication.update — but installing the plugin's own package triggers the
    /// domain reload that tears those closures down before that runs, so this is the only
    /// release path for a self-update. Python MCP server handles the actual update on next
    /// reconnect via get_version.
    /// </summary>
    [InitializeOnLoad]
    internal static class PluginUpdateMonitor
    {
        internal const string LastVersionKey = "UnityMCP.PluginUpdateMonitor.LastVersion";
        internal const string UpdatedFlagKey = "UnityMCP.PluginUpdateMonitor.UpdatedThisSession";

        /// <summary>Override for tests — bypasses PackageInfo lookup.</summary>
        internal static string _versionOverride;

        static PluginUpdateMonitor()
        {
            EditorApplication.delayCall += CheckVersionChange;
        }

        /// <summary>Compare current vs stored version; log if updated. Internal for tests.</summary>
        internal static void CheckVersionChange()
        {
            var current  = GetCurrentVersion();
            var previous = EditorPrefs.GetString(LastVersionKey, "");

            if (!string.IsNullOrEmpty(previous) && previous != current)
            {
                Debug.Log(
                    $"{BiomeLabel.Tag} Plugin updated {previous} → {current}. " +
                    "Python server will update automatically on next connection.");
                SessionState.SetBool(UpdatedFlagKey, true);
                // Safe when nothing is in flight (UpmOperationGuard.Complete() docstring).
                // Releases a claim left behind when the reload triggered by installing our
                // own package tore down Update()'s Poll/PollReload closures before they
                // could call FinishUpdate() themselves.
                UpmOperationGuard.Complete();
            }

            EditorPrefs.SetString(LastVersionKey, current);
        }

        /// <summary>Returns current plugin version from PackageInfo (or override for tests).</summary>
        internal static string GetCurrentVersion()
        {
            if (_versionOverride != null)
                return _versionOverride;

            try
            {
                var info = UnityEditor.PackageManager.PackageInfo
                    .FindForAssembly(typeof(PluginUpdateMonitor).Assembly);
                return (info?.version ?? "0.0.0").TrimStart('v');
            }
            catch
            {
                return "0.0.0";
            }
        }
    }
}
