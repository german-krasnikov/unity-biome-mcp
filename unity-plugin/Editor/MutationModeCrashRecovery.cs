using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Detects crash/restart where Mutation Mode EditorPref says ON but SessionState was cleared.
    /// Restores a safe state (MM off) so the Editor doesn't start with a stale kAutoRefresh=0.
    /// </summary>
    [InitializeOnLoad]
    static class MutationModeCrashRecovery
    {
        static MutationModeCrashRecovery()
        {
            // MM says ON but SessionState lost (crash/force-quit) → restore safe state
            if (MCPSettings.GetMutationMode() && !AutoRefreshGuard.IsApplied)
            {
                MCPSettings.SetMutationMode(false);
                Debug.Log("[MCP] Mutation Mode disabled after crash recovery (SessionState lost)");
            }
        }
    }
}
