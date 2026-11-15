using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Roslyn;

namespace UnityMCP.Editor
{
    // Wires RoslynHost's delegate seam from Core at domain-load, mirroring
    // ChatBackendProbe/ChatSettingsHook (PR-05). Keeps UnityMCP.Editor.Roslyn
    // free of any Core reference.
    [InitializeOnLoad]
    internal static class RoslynHostBootstrap
    {
        static RoslynHostBootstrap()
        {
            RoslynHost.SecurityLevelProvider = MCPSettings.GetSecurityLevel;
            RoslynHost.LogWarning = msg => Debug.LogWarning($"{BiomeLabel.Tag} {msg}");
            RoslynHost.LogError   = msg => Debug.LogError($"{BiomeLabel.Tag} {msg}");
        }
    }
}
